using EDIDashboard.Core.Models;
using EDIDashboard.Core.Services;
using EDIDashboard.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EDIDashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metrics;

    public MetricsController(IMetricsService metrics) => _metrics = metrics;

    [HttpGet]
    public async Task<DashboardMetrics> Get(
        [FromQuery] int? tradingPartnerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
        => await _metrics.GetDashboardMetricsAsync(tradingPartnerId, from, to);
}

[ApiController]
[Route("api/[controller]")]
public class EdiController : ControllerBase
{
    private readonly IEdiProcessingService _ediService;
    private readonly IFileProcessingLogRepository _logRepo;
    private readonly EdiDbContext _db;

    public EdiController(
        IEdiProcessingService ediService,
        IFileProcessingLogRepository logRepo,
        EdiDbContext db)
    {
        _ediService = ediService;
        _logRepo = logRepo;
        _db = db;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<object>> Upload(
        [FromForm] int tradingPartnerId,
        [FromForm] List<IFormFile>? files,
        IFormFile? file)
    {
        var uploadFiles = new List<IFormFile>();

        if (files != null)
            uploadFiles.AddRange(files.Where(f => f.Length > 0));
        if (file != null && file.Length > 0)
            uploadFiles.Add(file);

        if (uploadFiles.Count == 0)
            return BadRequest("No files uploaded.");

        if (uploadFiles.Count == 1)
        {
            using var singleReader = new StreamReader(uploadFiles[0].OpenReadStream());
            var singleContent = await singleReader.ReadToEndAsync();

            var singleResult = await _ediService.ProcessEdiFileAsync(singleContent, tradingPartnerId, uploadFiles[0].FileName);
            await CreateUploadLogAsync(uploadFiles[0], singleResult);
            return singleResult.Success ? Ok(singleResult) : BadRequest(singleResult);
        }

        var batchResults = new List<(string FileName, EdiUploadResult Result)>();

        foreach (var uploadFile in uploadFiles)
        {
            using var reader = new StreamReader(uploadFile.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            var result = await _ediService.ProcessEdiFileAsync(content, tradingPartnerId, uploadFile.FileName);
            await CreateUploadLogAsync(uploadFile, result);
            batchResults.Add((uploadFile.FileName, result));
        }

        var successCount = batchResults.Count(r => r.Result.Success);
        var failureCount = batchResults.Count - successCount;

        return Ok(new
        {
            totalFiles = batchResults.Count,
            successCount,
            failureCount,
            results = batchResults.Select(r => new
            {
                fileName = r.FileName,
                success = r.Result.Success,
                message = r.Result.Message,
                transactionType = r.Result.TransactionType,
                controlNumber = r.Result.ControlNumber,
                claimsProcessed = r.Result.ClaimsProcessed,
                errors = r.Result.Errors
            })
        });
    }

    [HttpPost("submit")]
    public async Task<ActionResult<EdiUploadResult>> Submit([FromBody] EdiSubmitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("EDI content is required.");

        var result = await _ediService.ProcessEdiFileAsync(request.Content, request.TradingPartnerId);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private async Task CreateUploadLogAsync(IFormFile uploadFile, EdiUploadResult result)
    {
        var now = DateTime.UtcNow;
        var resolvedPartnerId = await ResolveTradingPartnerIdAsync(uploadFile.FileName, result);

        await _logRepo.CreateAsync(new FileProcessingLog
        {
            FileName = uploadFile.FileName,
            SubmissionDate = ParseSubmissionDate(uploadFile.FileName),
            OriginalPath = uploadFile.FileName,
            FinalPath = uploadFile.FileName,
            FileSizeBytes = uploadFile.Length,
            DetectedType = result.TransactionType,
            TradingPartnerId = resolvedPartnerId,
            Status = result.Success ? "Success" : "Failed",
            ErrorMessage = result.Success ? null : result.Message,
            ControlNumber = result.ControlNumber,
            ClaimsProcessed = result.ClaimsProcessed,
            PickedUpAt = now,
            CompletedAt = now,
            ProcessingDuration = TimeSpan.Zero,
            Source = "Upload"
        });
    }

    private async Task<int?> ResolveTradingPartnerIdAsync(string fileName, EdiUploadResult result)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(result.TransactionType))
            return null;

        var query = _db.EdiTransactions
            .Where(t => t.FileName == fileName && t.TransactionType == result.TransactionType);

        if (!string.IsNullOrWhiteSpace(result.ControlNumber))
            query = query.Where(t => t.ControlNumber == result.ControlNumber);

        return await query
            .OrderByDescending(t => t.ReceivedAt)
            .Select(t => (int?)t.TradingPartnerId)
            .FirstOrDefaultAsync();
    }

    private static DateTime? ParseSubmissionDate(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length < 8)
            return null;

        return DateTime.TryParseExact(
            fileName[..8],
            "MMddyyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? parsed.Date
            : null;
    }
}

public record EdiSubmitRequest(int TradingPartnerId, string Content);

[ApiController]
[Route("api/[controller]")]
public class TradingPartnersController : ControllerBase
{
    private readonly ITradingPartnerService _svc;
    public TradingPartnersController(ITradingPartnerService svc) => _svc = svc;

    [HttpGet]
    public async Task<ActionResult<object>> GetAll()
    {
        var partners = await _svc.GetAllAsync();
        return Ok(partners.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            interchangeId = p.InterchangeId,
            interchangeQualifier = p.InterchangeQualifier,
            isActive = p.IsActive,
            createdAt = p.CreatedAt
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TradingPartner>> Get(int id)
    {
        var tp = await _svc.GetByIdAsync(id);
        return tp == null ? NotFound() : Ok(tp);
    }

    [HttpPost]
    public async Task<ActionResult<TradingPartner>> Create([FromBody] TradingPartner partner)
    {
        var created = await _svc.CreateAsync(partner);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TradingPartner>> Update(int id, [FromBody] TradingPartner partner)
    {
        if (id != partner.Id) return BadRequest();
        return Ok(await _svc.UpdateAsync(partner));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    private readonly EdiDbContext _db;
    public ClaimsController(EdiDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<object>> GetClaims(
        [FromQuery] int? tradingPartnerId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var q = _db.Claims
            .Include(c => c.EdiTransaction).ThenInclude(t => t.TradingPartner)
            .AsQueryable();

        if (tradingPartnerId.HasValue)
            q = q.Where(c => c.EdiTransaction.TradingPartnerId == tradingPartnerId.Value);
        if (!string.IsNullOrEmpty(status))
            q = q.Where(c => c.Status == status);
        if (from.HasValue)
            q = q.Where(c => c.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(c => c.CreatedAt <= to.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id, c.ClaimNumber, c.PatientName, c.ProviderName,
                c.TotalAmount, c.Status, c.ClaimType, c.ServiceDateFrom,
                c.RejectionReason, c.CreatedAt,
                TradingPartner = c.EdiTransaction.TradingPartner.Name,
                TradingPartnerId = c.EdiTransaction.TradingPartnerId
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetClaim(int id)
    {
        var claim = await _db.Claims
            .Include(c => c.EdiTransaction).ThenInclude(t => t.TradingPartner)
            .Include(c => c.ServiceLines)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (claim == null) return NotFound();
        return Ok(claim);
    }
}

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly EdiDbContext _db;
    public TransactionsController(EdiDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<object>> GetTransactions(
        [FromQuery] int? tradingPartnerId,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var q = _db.EdiTransactions
            .Include(t => t.TradingPartner)
            .Include(t => t.Claims)
            .AsQueryable();

        if (tradingPartnerId.HasValue) q = q.Where(t => t.TradingPartnerId == tradingPartnerId.Value);
        if (!string.IsNullOrEmpty(type)) q = q.Where(t => t.TransactionType == type);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(t => t.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id, t.TransactionType, t.ControlNumber, t.FileName, t.Status,
                t.ReceivedAt, t.ErrorDescription,
                TradingPartner = t.TradingPartner.Name,
                ClaimCount = t.Claims.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}

[ApiController]
[Route("api/[controller]")]
public class AcknowledgmentsController : ControllerBase
{
    private readonly EdiDbContext _db;
    public AcknowledgmentsController(EdiDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<object>> GetAcks(
        [FromQuery] int? tradingPartnerId,
        [FromQuery] string? ackType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var q = _db.Acknowledgments
            .Include(a => a.EdiTransaction).ThenInclude(t => t.TradingPartner)
            .AsQueryable();

        if (tradingPartnerId.HasValue)
            q = q.Where(a => a.EdiTransaction.TradingPartnerId == tradingPartnerId.Value);
        if (!string.IsNullOrEmpty(ackType))
            q = q.Where(a => a.AckType == ackType);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(a => a.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.AckType, a.ControlNumber, a.AcknowledgmentCode,
                a.Description, a.ReceivedAt, a.ErrorCode,
                FileName = a.EdiTransaction.FileName,
                TradingPartner = a.EdiTransaction.TradingPartner.Name
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}
