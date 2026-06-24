using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChronoCode.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> builds the model against the
/// PostgreSQL provider (matching the existing migrations) without running Program.cs
/// startup/DB-init code. EF does not open a connection for <c>migrations add</c>.
/// </summary>
public sealed class ChronoCodeDesignTimeFactory : IDesignTimeDbContextFactory<ChronoDbContext>
{
    public ChronoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseNpgsql("Host=localhost;Database=chronocode-design;Username=postgres;Password=postgres")
            .Options;
        return new ChronoDbContext(options);
    }
}
