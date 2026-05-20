using EdiProcessor.Core.Models;
using EdiProcessor.Core.Services;
using EdiProcessor.Infrastructure.Data;
using EdiProcessor.Infrastructure.Repositories;
using EdiProcessor.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── MVC + Swagger ──────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EDI Processor API", Version = "v1",
        Description = "EDI 837/TA1/999 ingestion, folder watcher, and metrics API" });
});

// ── Database ───────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=edi_processor.db";

if (connectionString.Contains("Server="))
    builder.Services.AddDbContext<EdiDbContext>(o => o.UseSqlServer(connectionString));
else
    builder.Services.AddDbContext<EdiDbContext>(o => o.UseSqlite(connectionString));

// ── Core application services ──────────────────────────────────────────────
builder.Services.AddScoped<IEdiProcessingService,   EdiProcessingService>();
builder.Services.AddScoped<IMetricsService,          MetricsService>();
builder.Services.AddScoped<ITradingPartnerService,   TradingPartnerService>();

// ── Folder watcher services ────────────────────────────────────────────────
builder.Services.Configure<EdiWatcherOptions>(
    builder.Configuration.GetSection(EdiWatcherOptions.Section));

builder.Services.AddScoped<IFileProcessingLogRepository, FileProcessingLogRepository>();

builder.Services.AddSingleton<EdiFolderWatcherBackgroundService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<EdiFolderWatcherBackgroundService>());

builder.Services.AddScoped<IEdiFolderWatcherService, EdiFolderWatcherService>();

// ── Build app ──────────────────────────────────────────────────────────────
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EdiDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
