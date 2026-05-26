using EDIDashboard.Core.Models;
using EDIDashboard.Core.Parsers;
using EDIDashboard.Core.Services;
using EDIDashboard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EDIDashboard.Infrastructure.Repositories;

public class EdiProcessingService : IEdiProcessingService
{
    private readonly EdiDbContext _db;
    private readonly ILogger<EdiProcessingService> _logger;

    public EdiProcessingService(EdiDbContext db, ILogger<EdiProcessingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<EdiUploadResult> ProcessEdiFileAsync(string rawContent, int tradingPartnerId, string? fileName = null)
    {
        var result = new EdiUploadResult();

        try
        {
            // Detect type
            var ediType = EdiTypeDetector.Detect(rawContent);
            result.TransactionType = ediType;

            // Resolve explicit partner ID, or auto-detect/create from ISA06/ISA08 when absent.
            var partner = await ResolveOrCreateTradingPartnerAsync(rawContent, tradingPartnerId, ediType);
            tradingPartnerId = partner.Id;

            _logger.LogInformation("Processing {EdiType} from partner {Partner}", ediType, partner.Name);

            switch (ediType)
            {
                case "837P":
                case "837I":
                case "837D":
                    await Process837Async(rawContent, tradingPartnerId, result, fileName);
                    break;

                case "TA1":
                    await ProcessTa1Async(rawContent, tradingPartnerId, result, fileName);
                    break;

                case "999":
                    await Process999Async(rawContent, tradingPartnerId, result, fileName);
                    break;

                case "277CA":
                    await Process277CaAsync(rawContent, tradingPartnerId, result, fileName);
                    break;

                default:
                    result.Success = false;
                    result.Message = $"Unsupported EDI type: {ediType}. Supported: 837P, 837I, 837D, TA1, 999, 277CA.";
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

    private async Task<TradingPartner> ResolveOrCreateTradingPartnerAsync(string rawContent, int tradingPartnerId, string ediType)
    {
        var isAcknowledgment = ediType is "TA1" or "999" or "277CA";
        var isClaimSubmission = ediType is "837P" or "837I" or "837D";

        if (tradingPartnerId > 0 && !isAcknowledgment && !isClaimSubmission)
        {
            var explicitPartner = await _db.TradingPartners.FindAsync(tradingPartnerId);
            if (explicitPartner != null)
                return explicitPartner;

            throw new InvalidOperationException($"Trading partner ID {tradingPartnerId} not found.");
        }

        var isa06 = ExtractIsaElement(rawContent, 6); // sender id
        var isa05 = ExtractIsaElement(rawContent, 5); // sender qualifier
        var isa08 = ExtractIsaElement(rawContent, 8); // receiver id
        var isa07 = ExtractIsaElement(rawContent, 7); // receiver qualifier

        var primaryId = isAcknowledgment ? isa08 : isa06;
        var primaryQualifier = isAcknowledgment ? isa07 : isa05;
        var secondaryId = isAcknowledgment ? isa06 : isa08;
        var secondaryQualifier = isAcknowledgment ? isa05 : isa07;
        var primaryRole = isAcknowledgment ? "ISA08 receiver" : "ISA06 sender";
        var canUseSecondary = !isClaimSubmission;

        if (isClaimSubmission && tradingPartnerId > 0)
        {
            var explicitPartner = await _db.TradingPartners.FindAsync(tradingPartnerId);
            if (explicitPartner != null &&
                !string.IsNullOrWhiteSpace(primaryId) &&
                !string.Equals(explicitPartner.InterchangeId, primaryId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Ignoring explicit TradingPartnerId {ExplicitPartnerId} ('{ExplicitInterchangeId}') for {EdiType}; using ISA06 sender '{Isa06}' for 837 resolution.",
                    explicitPartner.Id,
                    explicitPartner.InterchangeId,
                    ediType,
                    primaryId.Trim());
            }
        }

        if (isAcknowledgment && tradingPartnerId > 0)
        {
            var explicitPartner = await _db.TradingPartners.FindAsync(tradingPartnerId);
            if (explicitPartner != null &&
                !string.IsNullOrWhiteSpace(primaryId) &&
                !string.Equals(explicitPartner.InterchangeId, primaryId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Ignoring explicit TradingPartnerId {ExplicitPartnerId} ('{ExplicitInterchangeId}') for {EdiType}; using {PrimaryRole} '{PrimaryId}' for acknowledgment resolution.",
                    explicitPartner.Id,
                    explicitPartner.InterchangeId,
                    ediType,
                    primaryRole,
                    primaryId.Trim());
            }
        }

        TradingPartner? existing = null;
        if (!string.IsNullOrWhiteSpace(primaryId))
            existing = await _db.TradingPartners.FirstOrDefaultAsync(p => p.InterchangeId == primaryId.Trim());
        if (existing != null)
            return existing;

        if (canUseSecondary && !string.IsNullOrWhiteSpace(secondaryId))
        {
            existing = await _db.TradingPartners.FirstOrDefaultAsync(p => p.InterchangeId == secondaryId.Trim());
            if (existing != null)
                return existing;
        }

        var createId = !string.IsNullOrWhiteSpace(primaryId)
            ? primaryId.Trim()
            : (canUseSecondary && !string.IsNullOrWhiteSpace(secondaryId) ? secondaryId.Trim() : string.Empty);
        var createQualifier = !string.IsNullOrWhiteSpace(primaryId)
            ? primaryQualifier
            : (canUseSecondary ? secondaryQualifier : null);

        if (string.IsNullOrWhiteSpace(createId) && tradingPartnerId > 0)
        {
            var explicitPartner = await _db.TradingPartners.FindAsync(tradingPartnerId);
            if (explicitPartner != null)
                return explicitPartner;
        }

        if (string.IsNullOrWhiteSpace(createId) && isAcknowledgment)
            throw new InvalidOperationException("Acknowledgment trading partner could not be resolved because ISA08/ISA06 are missing.");

        if (string.IsNullOrWhiteSpace(createId))
            throw new InvalidOperationException("Trading partner could not be resolved because ISA06 and ISA08 are both missing.");

        var created = new TradingPartner
        {
            Name = $"Auto-{createId}",
            InterchangeId = createId,
            InterchangeQualifier = string.IsNullOrWhiteSpace(createQualifier) ? "ZZ" : createQualifier.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.TradingPartners.Add(created);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Auto-created trading partner {PartnerId} for EDI {EdiType} using interchange ID '{InterchangeId}'.",
            created.Id, ediType, created.InterchangeId);
        return created;
    }

    private static string ExtractIsaElement(string content, int elementIndex)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length < 106)
            return string.Empty;

        var sep = content[3];
        var head = content.Length > 200 ? content[..200] : content;
        var parts = head.Split(sep);
        return parts.Length > elementIndex ? parts[elementIndex].Trim() : string.Empty;
    }

    private async Task Process837Async(string rawContent, int tradingPartnerId, EdiUploadResult result, string? fileName)
    {
        var parser = new Edi837Parser();
        var (transaction, claims) = parser.Parse(rawContent, tradingPartnerId);
        transaction.FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();

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

        // --- NEW: Reconcile with any pre-existing TA1/999 acknowledgments ---
        // 1. TA1: match on control number
        var ta1 = await _db.Acknowledgments
            .Where(a => a.AckType == "TA1" &&
                        a.ControlNumber == transaction.ControlNumber &&
                        a.EdiTransaction.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(a => a.ReceivedAt)
            .FirstOrDefaultAsync();
        if (ta1 != null)
        {
            transaction.Status = ta1.AcknowledgmentCode switch { "A" => "Accepted", "E" => "Accepted", "R" => "Rejected", _ => transaction.Status };
            var claimsToUpdate = await _db.Claims.Where(c => c.EdiTransactionId == transaction.Id).ToListAsync();
            foreach (var claim in claimsToUpdate) claim.Status = transaction.Status;
        }

        // 2. 999: match on transaction set control number
        var nine99 = await _db.Acknowledgments
            .Where(a => a.AckType == "999" &&
                        a.TransactionSetControlNumber == transaction.ControlNumber &&
                        a.EdiTransaction.TradingPartnerId == tradingPartnerId)
            .OrderByDescending(a => a.ReceivedAt)
            .FirstOrDefaultAsync();
        if (nine99 != null)
        {
            transaction.Status = nine99.AcknowledgmentCode switch { "A" => "Accepted", "E" => "Accepted", "R" => "Rejected", _ => transaction.Status };
            transaction.ErrorDescription = nine99.AcknowledgmentCode == "R" ? nine99.Description : null;
            var claimsToUpdate = await _db.Claims.Where(c => c.EdiTransactionId == transaction.Id).ToListAsync();
            foreach (var claim in claimsToUpdate)
            {
                claim.Status = transaction.Status;
                if (transaction.Status == "Rejected") claim.RejectionReason = nine99.Description;
            }
        }

        await _db.SaveChangesAsync();

        result.ControlNumber = transaction.ControlNumber;
        result.ClaimsProcessed = claims.Count;
        result.Message = $"Successfully processed {claims.Count} claim(s) from {transaction.TransactionType} transaction.";
    }

    private async Task ProcessTa1Async(string rawContent, int tradingPartnerId, EdiUploadResult result, string? fileName)
    {
        var parser = new Ta1Parser();
        var (transaction, ack) = parser.Parse(rawContent, tradingPartnerId);
        transaction.FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();

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

    private async Task Process999Async(string rawContent, int tradingPartnerId, EdiUploadResult result, string? fileName)
    {
        var parser = new Edi999Parser();
        var (transaction, acks) = parser.Parse(rawContent, tradingPartnerId);
        transaction.FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();

        if (acks.Count == 0)
        {
            // Keep Acknowledgments page populated even when partner 999 files only provide group-level status.
            var fallbackCode = transaction.Status switch
            {
                "Rejected" => "R",
                "Accepted" => "A",
                _ => ""
            };

            acks.Add(new AcknowledgmentRecord
            {
                AckType = "999",
                ControlNumber = transaction.ControlNumber,
                AcknowledgmentCode = fallbackCode,
                Description = "999 processed using group summary (AK9). Transaction-set detail (IK5/AK5) not present.",
                ReceivedAt = DateTime.UtcNow
            });
        }

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

    private async Task Process277CaAsync(string rawContent, int tradingPartnerId, EdiUploadResult result, string? fileName)
    {
        var parser = new Edi277CaParser();
        var (transaction, claimStatuses) = parser.Parse(rawContent, tradingPartnerId);
        transaction.FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();

        _db.EdiTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        foreach (var cs in claimStatuses)
        {
            cs.EdiTransactionId = transaction.Id;

            // Link to the matching 837 transaction via submitter claim ID (CLM01)
            if (!string.IsNullOrEmpty(cs.SubmitterClaimId))
            {
                var matched837 = await _db.Claims
                    .Where(c => c.ClaimNumber == cs.SubmitterClaimId &&
                                c.EdiTransaction.TradingPartnerId == tradingPartnerId)
                    .Include(c => c.EdiTransaction)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();

                if (matched837 != null)
                {
                    cs.Linked837TransactionId = matched837.EdiTransactionId;

                    // Update the claim status based on 277CA status category
                    var newStatus = MapStcCategoryToClaimStatus(cs.StatusCategoryCode);
                    if (newStatus != null)
                    {
                        matched837.Status = newStatus;
                        if (newStatus == "Rejected") matched837.RejectionReason = cs.StatusDescription;
                    }
                }
            }

            _db.Claims277CA.Add(cs);
        }

        await _db.SaveChangesAsync();

        result.ControlNumber = transaction.ControlNumber;
        result.ClaimsProcessed = claimStatuses.Count;
        result.Message = $"277CA processed. {claimStatuses.Count} claim status(es) received.";
    }

    private static string? MapStcCategoryToClaimStatus(string? categoryCode) =>
        categoryCode switch
        {
            "A1" or "A2" or "A3" or "A8" => "Accepted",
            "A4" or "A7" or "R3" => "Rejected",
            "F1" => "Accepted",
            "F2" => "Rejected",
            "P1" or "P2" or "P3" or "P4" => "Pending",
            _ => null   // Don't overwrite status for unknown categories
        };
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
        var fromSubmissionDate = fromDate.Date;
        var toSubmissionDate = toDate.Date;
        var toSubmissionDateExclusive = toSubmissionDate.AddDays(1);

        var txWindowQuery = _db.EdiTransactions.AsQueryable();

        if (tradingPartnerId.HasValue)
            txWindowQuery = txWindowQuery.Where(t => t.TradingPartnerId == tradingPartnerId.Value);

        txWindowQuery = txWindowQuery.Where(t => _db.FileProcessingLogs.Any(l =>
            (
                (l.SubmissionDate.HasValue &&
                 l.SubmissionDate.Value >= fromSubmissionDate &&
                 l.SubmissionDate.Value <= toSubmissionDate) ||
                (!l.SubmissionDate.HasValue &&
                 l.PickedUpAt >= fromSubmissionDate &&
                 l.PickedUpAt < toSubmissionDateExclusive)
            ) &&
            (!tradingPartnerId.HasValue || l.TradingPartnerId == tradingPartnerId.Value) &&
            (
                (!string.IsNullOrEmpty(l.FileName) && l.FileName == t.FileName) ||
                (!string.IsNullOrEmpty(l.ControlNumber) && l.ControlNumber == t.ControlNumber &&
                    (string.IsNullOrEmpty(l.DetectedType) || l.DetectedType == t.TransactionType))
            )
        ));

        var txIdsInWindow = txWindowQuery.Select(t => t.Id);

        var claimsQuery = _db.Claims
            .Include(c => c.EdiTransaction)
            .Where(c => txIdsInWindow.Contains(c.EdiTransactionId));

        var allClaims = await claimsQuery.ToListAsync();
        var allTx = await txWindowQuery.CountAsync();

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

        // Daily volume based on effective date from FileProcessingLogs:
        // SubmissionDate when present, otherwise PickedUpAt date.
        var logQuery = _db.FileProcessingLogs.AsQueryable();

        if (tradingPartnerId.HasValue)
            logQuery = logQuery.Where(l => l.TradingPartnerId == tradingPartnerId.Value);

        var logRows = await logQuery
            .Select(l => new { l.SubmissionDate, l.PickedUpAt, l.ClaimsProcessed, l.Status })
            .ToListAsync();

        var daily = logRows
            .Select(l => new
            {
                EffectiveDate = (l.SubmissionDate ?? l.PickedUpAt.Date),
                l.ClaimsProcessed,
                l.Status
            })
            .Where(l => l.EffectiveDate >= fromDate.Date && l.EffectiveDate <= toDate.Date)
            .GroupBy(l => l.EffectiveDate)
            .Select(g => new DailyVolume
            {
                Date = g.Key,
                Claims = g.Sum(x => x.ClaimsProcessed),
                Accepted = g.Where(x => x.Status == "Success").Sum(x => x.ClaimsProcessed),
                Rejected = g.Where(x => x.Status == "Failed").Sum(x => x.ClaimsProcessed)
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
        var recentQuery = _db.EdiTransactions
            .Include(t => t.TradingPartner)
            .Include(t => t.Claims)
            .Where(t => txIdsInWindow.Contains(t.Id));

        if (tradingPartnerId.HasValue)
            recentQuery = recentQuery.Where(t => t.TradingPartnerId == tradingPartnerId.Value);

        var recent = await recentQuery
            .OrderByDescending(t => t.ReceivedAt)
            .Take(20)
            .Select(t => new RecentTransaction
            {
                Id = t.Id,
                TradingPartner = t.TradingPartner.Name,
                TransactionType = t.TransactionType,
                ControlNumber = t.ControlNumber,
                FileName = t.FileName,
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
