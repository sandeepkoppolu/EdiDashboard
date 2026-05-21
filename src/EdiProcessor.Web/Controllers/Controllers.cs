using EdiProcessor.Core.Models;
using EdiProcessor.Core.Services;
using EdiProcessor.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EdiProcessor.Web.Controllers;

// ─── Dashboard (MVC) ──────────────────────────────────────────────────────────

public class DashboardController : Controller
{
    public IActionResult Index() => View();
}

// ─── Metrics API ──────────────────────────────────────────────────────────────

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

// ─── EDI Upload API ───────────────────────────────────────────────────────────

[ApiController]
[Route("api/[controller]")]
public class EdiController : ControllerBase
{
    private readonly IEdiProcessingService _ediService;

    public EdiController(IEdiProcessingService ediService) => _ediService = ediService;

    /// <summary>
    /// Upload a raw EDI file (837P/I/D, TA1, or 999).
    /// </summary>
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

            var singleResult = await _ediService.ProcessEdiFileAsync(singleContent, tradingPartnerId);
            return singleResult.Success ? Ok(singleResult) : BadRequest(singleResult);
        }

        var batchResults = new List<(string FileName, EdiUploadResult Result)>();

        foreach (var uploadFile in uploadFiles)
        {
            using var reader = new StreamReader(uploadFile.OpenReadStream());
            var content = await reader.ReadToEndAsync();

            var result = await _ediService.ProcessEdiFileAsync(content, tradingPartnerId);
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

    /// <summary>
    /// Submit raw EDI content as text (useful for API integrations).
    /// </summary>
    [HttpPost("submit")]
    public async Task<ActionResult<EdiUploadResult>> Submit([FromBody] EdiSubmitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest("EDI content is required.");

        var result = await _ediService.ProcessEdiFileAsync(request.Content, request.TradingPartnerId);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public record EdiSubmitRequest(int TradingPartnerId, string Content);

// ─── Trading Partners API ─────────────────────────────────────────────────────

[ApiController]
[Route("api/[controller]")]
public class TradingPartnersController : ControllerBase
{
    private readonly ITradingPartnerService _svc;
    public TradingPartnersController(ITradingPartnerService svc) => _svc = svc;

    [HttpGet]
    public async Task<List<TradingPartner>> GetAll() => await _svc.GetAllAsync();

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

// ─── Claims API ───────────────────────────────────────────────────────────────

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

// ─── Transactions API ─────────────────────────────────────────────────────────

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
                t.Id, t.TransactionType, t.ControlNumber, t.Status,
                t.ReceivedAt, t.ErrorDescription,
                TradingPartner = t.TradingPartner.Name,
                ClaimCount = t.Claims.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}

// ─── Acknowledgments API ──────────────────────────────────────────────────────

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
                TradingPartner = a.EdiTransaction.TradingPartner.Name
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }
}
