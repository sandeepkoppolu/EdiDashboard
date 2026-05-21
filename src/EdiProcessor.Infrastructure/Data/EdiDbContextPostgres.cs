using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EdiProcessor.Infrastructure.Data;

public class EdiDbContextPostgres : EdiDbContext
{
    public EdiDbContextPostgres(DbContextOptions<EdiDbContextPostgres> options)
        : base(options)
    {
    }
}

public class EdiDbContextPostgresFactory : IDesignTimeDbContextFactory<EdiDbContextPostgres>
{
    public EdiDbContextPostgres CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EdiDbContextPostgres>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=EdiProcessor;Username=postgres;Password=postgres");
        return new EdiDbContextPostgres(optionsBuilder.Options);
    }
}
