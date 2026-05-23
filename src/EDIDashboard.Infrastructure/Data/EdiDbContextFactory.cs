using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EDIDashboard.Infrastructure.Data;

public class EdiDbContextFactory : IDesignTimeDbContextFactory<EdiDbContext>
{
    public EdiDbContext CreateDbContext(string[] args)
    {
        // Resolve configuration from common execution locations for dotnet ef.
        var basePath = Directory.GetCurrentDirectory();
        var webPathFromInfra = Path.GetFullPath(Path.Combine(basePath, "..", "EDIDashboard.Web"));
        var webPathFromRepoRoot = Path.GetFullPath(Path.Combine(basePath, "src", "EDIDashboard.Web"));

        var configBasePath = File.Exists(Path.Combine(webPathFromInfra, "appsettings.json"))
            ? webPathFromInfra
            : File.Exists(Path.Combine(webPathFromRepoRoot, "appsettings.json"))
                ? webPathFromRepoRoot
                : basePath;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(configBasePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var provider = configuration["Database:Provider"]?.Trim();
        provider = string.IsNullOrWhiteSpace(provider) ? "SqlServer" : provider;

        var optionsBuilder = new DbContextOptionsBuilder<EdiDbContext>();

        switch (provider.ToLowerInvariant())
        {
            case "sqlserver":
            case "mssql":
            {
                var cs = configuration.GetConnectionString("SqlServerConnection")
                         ?? configuration.GetConnectionString("DefaultConnection")
                         ?? throw new InvalidOperationException(
                             "Missing SQL Server connection string. Configure ConnectionStrings:SqlServerConnection or DefaultConnection.");
                optionsBuilder.UseSqlServer(cs);
                break;
            }
            case "sqlite":
            {
                var cs = configuration.GetConnectionString("SqliteConnection")
                         ?? "Data Source=edi_processor.db";
                optionsBuilder.UseSqlite(cs);
                break;
            }
            default:
                throw new InvalidOperationException(
                    $"Unsupported design-time provider '{provider}' for EdiDbContext. Supported values: SqlServer, Sqlite.");
        }

        return new EdiDbContext(optionsBuilder.Options);
    }
}
