using EDIDashboard.Core.Models;
using EDIDashboard.Core.Services;
using EDIDashboard.Infrastructure.Data;
using EDIDashboard.Infrastructure.Repositories;
using EDIDashboard.Infrastructure.Services;
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
var provider = builder.Configuration["Database:Provider"]?.Trim();
provider = string.IsNullOrWhiteSpace(provider) ? "SqlServer" : provider;

switch (provider!.ToLowerInvariant())
{
    case "sqlserver":
    case "mssql":
    {
        var cs = builder.Configuration.GetConnectionString("SqlServerConnection")
                 ?? builder.Configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Missing connection string. Configure ConnectionStrings:SqlServerConnection.");
        builder.Services.AddDbContext<EdiDbContext>(o => o.UseSqlServer(cs));
        break;
    }
    case "postgres":
    case "postgresql":
    case "npgsql":
    {
        var cs = builder.Configuration.GetConnectionString("PostgresConnection")
                 ?? builder.Configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException(
                     "Missing connection string. Configure ConnectionStrings:PostgresConnection.");

        builder.Services.AddDbContext<EdiDbContextPostgres>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<EdiDbContext>(sp => sp.GetRequiredService<EdiDbContextPostgres>());
        break;
    }
    case "sqlite":
    {
        var cs = builder.Configuration.GetConnectionString("SqliteConnection")
                 ?? "Data Source=edi_processor.db";
        builder.Services.AddDbContext<EdiDbContext>(o => o.UseSqlite(cs));
        break;
    }
    default:
        throw new InvalidOperationException(
            $"Unsupported Database:Provider '{provider}'. Supported values: SqlServer, Postgres, Sqlite.");
}

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
