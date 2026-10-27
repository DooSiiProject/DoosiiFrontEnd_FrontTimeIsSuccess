using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Doosii.DAL.Data;

/// <summary>Build migrations without starting the API, its seeders or background workers.</summary>
public sealed class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DOOSII_EF_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=DoosiiPart7SchemaTest;Integrated Security=true;TrustServerCertificate=true";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    }
}
