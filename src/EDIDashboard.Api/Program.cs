using EDIDashboard.Core.Models;
using EDIDashboard.Core.Services;
using EDIDashboard.Infrastructure.Data;
using EDIDashboard.Infrastructure.Repositories;
using EDIDashboard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var services = builder.Services;
services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "EDI Processor API",
        Version = "v1",
        Description = "EDI 837/TA1/999 ingestion, folder watcher, and metrics API"
    });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFile("logs/api-log.txt");

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("WebApp", policy =>
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}

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

builder.Services.AddScoped<IEdiProcessingService, EdiProcessingService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<ITradingPartnerService, TradingPartnerService>();

builder.Services.AddScoped<IFileProcessingLogRepository, FileProcessingLogRepository>();
builder.Services.AddScoped<IEdiFolderWatcherService, EdiFolderWatcherService>();
builder.Services.Configure<EdiWatcherOptions>(
    builder.Configuration.GetSection(EdiWatcherOptions.Section));

var watcherEnabled = builder.Configuration.GetValue<bool?>("EdiWatcher:Enabled") ?? true;
if (watcherEnabled)
{
    builder.Services.AddSingleton<EdiFolderWatcherBackgroundService>();
    builder.Services.AddHostedService(sp =>
        sp.GetRequiredService<EdiFolderWatcherBackgroundService>());
}

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

if (corsOrigins.Length > 0)
{
    app.UseCors("WebApp");
}

app.UseRouting();
app.MapControllers();

app.Run();
