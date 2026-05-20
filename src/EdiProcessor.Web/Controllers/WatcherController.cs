using EdiProcessor.Core.Models;
using EdiProcessor.Core.Services;
using EdiProcessor.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EdiProcessor.Web.Controllers;

/// <summary>
/// REST API for the EDI Folder Watcher feature.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WatcherController : ControllerBase
{
    private readonly IEdiFolderWatcherService _watcher;
    private readonly EdiDbContext _db;

    public WatcherController(IEdiFolderWatcherService watcher, EdiDbContext db)
    {
        _watcher = watcher;
        _db      = db;
    }

    /// <summary>Get current watcher status, folder paths, and recent file logs.</summary>
    [HttpGet("status")]
    public async Task<WatcherStatus> GetStatus() => await _watcher.GetStatusAsync();

    /// <summary>Trigger an immediate inbox scan (bypasses the timer).</summary>
    [HttpPost("scan")]
    public async Task<ActionResult<object>> ScanNow()
    {
        var logs = await _watcher.ScanNowAsync();
        return Ok(new
        {
            filesProcessed = logs.Count,
            succeeded      = logs.Count(l => l.Status == "Success"),
            failed         = logs.Count(l => l.Status == "Failed"),
            skipped        = logs.Count(l => l.Status == "Skipped"),
            logs,
        });
    }

    /// <summary>Paginated file processing log.</summary>
    [HttpGet("logs")]
    public async Task<ActionResult<object>> GetLogs(
        [FromQuery] int? tradingPartnerId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var q = _db.FileProcessingLogs.AsQueryable();
        if (tradingPartnerId.HasValue) q = q.Where(l => l.TradingPartnerId == tradingPartnerId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(l => l.Status == status);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(l => l.PickedUpAt)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>List files currently sitting in the inbox folder.</summary>
    [HttpGet("inbox")]
    public async Task<ActionResult<object>> GetInboxFiles()
    {
        var status = await _watcher.GetStatusAsync();
        return Ok(new
        {
            inboxPath    = status.InboxPath,
            totalWaiting = status.FilesInInboxNow,
            folders      = status.PartnerFolders,
        });
    }
}

/// <summary>
/// MVC controller that serves the Watcher dashboard page.
/// </summary>
public class WatcherViewController : Controller
{
    public IActionResult Index() => View();
}
