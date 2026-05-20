using EdiProcessor.Core.Models;
using EdiProcessor.Core.Parsers;
using EdiProcessor.Core.Services;
using EdiProcessor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EdiProcessor.Infrastructure.Repositories;

public class EdiProcessingService : IEdiProcessingService
{
    private readonly EdiDbContext _db;
    private readonly ILogger<EdiProcessingService> _logger;

    public EdiProcessingService(EdiDbContext db, ILogger<EdiProcessingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EdiUploadResult> ProcessEdiFileAsync(string rawContent, int tradingPartnerId)
    {
        var result = new EdiUploadResult();

        try
        {
            // Verify trading partner exists
            var partner = await _db.TradingPartners.FindAsync(tradingPartnerId);
            if (partner == null)
            {
                result.Success = false;
                result.Message = $"Trading partner ID {tradingPartnerId} not found.";
                return result;
            }

            // Detect type
            var ediType = EdiTypeDetector.Detect(rawContent);
            result.TransactionType = ediType;

            _logger.LogInformation("Processing {EdiType} from partner {Partner}", ediType, partner.Name);

            switch (ediType)
            {
                case "837P":
                case "837I":
                case "837D":
                    await Process837Async(rawContent, tradingPartnerId, result);
                    break;

                case "TA1":
                    await ProcessTa1Async(rawContent, tradingPartnerId, result);
                    break;

                case "999":
                    await Process999Async(rawContent, tradingPartnerId, result);
                    break;

                default:
                    result.Success = false;
                    result.Message = $"Unsupported EDI type: {ediType}. Supported: 837P, 837I, 837D, TA1, 999.";
                    return result;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing EDI file");
            result.Success = false;
            result.Message = $"Processing error: {ex.Message}";
            result.Errors.Add(ex.ToString());
        }

        return result;
    }

    private async Task Process837Async(string rawContent, int tradingPartnerId, EdiUploadResult result)
    {
        var parser = new Edi837Parser();
        var (transaction, claims) = parser.Parse(rawContent, tradingPartnerId);

        // Check for duplicate control number
        var exists = await _db.EdiTransactions
            .AnyAsync(t => t.ControlNumber == transaction.ControlNumber &&
                           t.TradingPartnerId == tradingPartnerId &&
                           t.TransactionType == transaction.TransactionType);
        if (exists)
        {
            result.Errors.Add($"Duplicate interchange control number: {transaction.ControlNumber}");
        }

        _db.EdiTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        foreach (var claim in claims)
        {
            claim.EdiTransactionId = transaction.Id;
            _db.Claims.Add(claim);
        }

        await _db.SaveChangesAsync();

        result.ControlNumber = transaction.ControlNumber;
        result.ClaimsProcessed = claims.Count;
        result.Message = $"Successfully processed {claims.Count} claim(s) from {transaction.TransactionType} transaction.";
    }

    private async Task ProcessTa1Async(string rawContent, int tradingPartnerId, EdiUploadResult result)
    {
        var parser = new Ta1Parser();
        var (transaction, ack) = parser.Parse(rawContent, tradingPartnerId);

        _db.EdiTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        ack.EdiTransactionId = transaction.Id;
        _db.Acknowledgments.Add(ack);

        // Update matching 837 transaction status if found
        var original = await _db.EdiTransactions
            .Where(t => t.ControlNumber == ack.ControlNumber &&
                        t.TradingPartnerId == tradingPartnerId &&
                        t.TransactionType.StartsWith("837"))
            .OrderByDescending(t => t.ReceivedAt)
            .FirstOrDefaultAsync();

        if (original != null)
        {
            original.Status = transaction.Status;
            // Cascade status to claims
            var claimsToUpdate = await _db.Claims
                .Where(c => c.EdiTransactionId == original.Id)
                .ToListAsync();
            foreach (var claim in claimsToUpdate)
                claim.Status = transaction.Status;
        }

        await _db.SaveChangesAsync();

        result.ControlNumber = transaction.ControlNumber;
        result.Message = $"TA1 processed. Interchange {ack.ControlNumber}: {ack.Description}";
    }

    private async Task Process999Async(string rawContent, int tradingPartnerId, EdiUploadResult result)
    {
        var parser = new Edi999Parser();
        var (transaction, acks) = parser.Parse(rawContent, tradingPartnerId);

        _db.EdiTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        foreach (var ack in acks)
        {
            ack.EdiTransactionId = transaction.Id;
            _db.Acknowledgments.Add(ack);

            // Match and update the referenced 837 transaction set
            if (!string.IsNullOrEmpty(ack.TransactionSetControlNumber))
            {
                var original = await _db.EdiTransactions
                    .Where(t => t.ControlNumber == ack.TransactionSetControlNumber &&
                                t.TradingPartnerId == tradingPartnerId &&
                                t.TransactionType.StartsWith("837"))
                    .OrderByDescending(t => t.ReceivedAt)
                    .FirstOrDefaultAsync();

                if (original != null)
                {
                    original.Status = ack.AcknowledgmentCode switch
                    {
                        "A" => "Accepted",
                        "E" => "Accepted",
                        "R" => "Rejected",
                        _ => original.Status
                    };
                    original.ErrorDescription = ack.AcknowledgmentCode == "R" ? ack.Description : null;

                    var claimsToUpdate = await _db.Claims
                        .Where(c => c.EdiTransactionId == original.Id)
                        .ToListAsync();
                    foreach (var claim in claimsToUpdate)
                    {
                        claim.Status = original.Status;
                        if (original.Status == "Rejected") claim.RejectionReason = ack.Description;
                    }
                }
            }
        }

        await _db.SaveChangesAsync();

        result.ControlNumber = transaction.ControlNumber;
        result.Message = $"999 processed. {acks.Count} functional group(s) acknowledged. Status: {transaction.Status}";
    }
}

public class MetricsService : IMetricsService
{
    private readonly EdiDbContext _db;

    public MetricsService(EdiDbContext db) => _db = db;

    public async Task<DashboardMetrics> GetDashboardMetricsAsync(
        int? tradingPartnerId = null, DateTime? from = null, DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var claimsQuery = _db.Claims
            .Include(c => c.EdiTransaction)
            .Where(c => c.CreatedAt >= fromDate && c.CreatedAt <= toDate);

        if (tradingPartnerId.HasValue)
            claimsQuery = claimsQuery.Where(c => c.EdiTransaction.TradingPartnerId == tradingPartnerId.Value);

        var txQuery = _db.EdiTransactions
            .Where(t => t.ReceivedAt >= fromDate && t.ReceivedAt <= toDate);
        if (tradingPartnerId.HasValue)
            txQuery = txQuery.Where(t => t.TradingPartnerId == tradingPartnerId.Value);

        var allClaims = await claimsQuery.ToListAsync();
        var allTx = await txQuery.CountAsync();

        // By trading partner
        var byPartner = await claimsQuery
            .GroupBy(c => new { c.EdiTransaction.TradingPartnerId, c.EdiTransaction.TradingPartner.Name })
            .Select(g => new TradingPartnerMetric
            {
                TradingPartnerId = g.Key.TradingPartnerId,
                TradingPartnerName = g.Key.Name,
                TotalClaims = g.Count(),
                Accepted = g.Count(x => x.Status == "Accepted"),
                Rejected = g.Count(x => x.Status == "Rejected"),
                Pending = g.Count(x => x.Status == "Received"),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        // Daily volume (last 30 days)
        var daily = allClaims
            .GroupBy(c => c.CreatedAt.Date)
            .Select(g => new DailyVolume
            {
                Date = g.Key,
                Claims = g.Count(),
                Accepted = g.Count(x => x.Status == "Accepted"),
                Rejected = g.Count(x => x.Status == "Rejected")
            })
            .OrderBy(d => d.Date)
            .ToList();

        // By claim type
        var byType = allClaims
            .GroupBy(c => c.ClaimType)
            .Select(g => new ClaimTypeMetric
            {
                ClaimType = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount)
            })
            .ToList();

        // Recent transactions
        var recent = await _db.EdiTransactions
            .Include(t => t.TradingPartner)
            .Include(t => t.Claims)
            .OrderByDescending(t => t.ReceivedAt)
            .Take(20)
            .Select(t => new RecentTransaction
            {
                Id = t.Id,
                TradingPartner = t.TradingPartner.Name,
                TransactionType = t.TransactionType,
                ControlNumber = t.ControlNumber,
                Status = t.Status,
                ReceivedAt = t.ReceivedAt,
                ClaimCount = t.Claims.Count
            })
            .ToListAsync();

        return new DashboardMetrics
        {
            TotalClaims = allClaims.Count,
            AcceptedClaims = allClaims.Count(c => c.Status == "Accepted"),
            RejectedClaims = allClaims.Count(c => c.Status == "Rejected"),
            PendingClaims = allClaims.Count(c => c.Status == "Received"),
            TotalBilledAmount = allClaims.Sum(c => c.TotalAmount),
            TotalTransactions = allTx,
            ByTradingPartner = byPartner,
            DailyVolumes = daily,
            ByClaimType = byType,
            RecentTransactions = recent
        };
    }
}

public class TradingPartnerService : ITradingPartnerService
{
    private readonly EdiDbContext _db;
    public TradingPartnerService(EdiDbContext db) => _db = db;

    public Task<List<TradingPartner>> GetAllAsync() =>
        _db.TradingPartners.OrderBy(p => p.Name).ToListAsync();

    public Task<TradingPartner?> GetByIdAsync(int id) =>
        _db.TradingPartners.FindAsync(id).AsTask();

    public Task<TradingPartner?> GetByInterchangeIdAsync(string id) =>
        _db.TradingPartners.FirstOrDefaultAsync(p => p.InterchangeId == id);

    public async Task<TradingPartner> CreateAsync(TradingPartner partner)
    {
        _db.TradingPartners.Add(partner);
        await _db.SaveChangesAsync();
        return partner;
    }

    public async Task<TradingPartner> UpdateAsync(TradingPartner partner)
    {
        _db.TradingPartners.Update(partner);
        await _db.SaveChangesAsync();
        return partner;
    }

    public async Task DeleteAsync(int id)
    {
        var p = await _db.TradingPartners.FindAsync(id);
        if (p != null) { _db.TradingPartners.Remove(p); await _db.SaveChangesAsync(); }
    }
}
