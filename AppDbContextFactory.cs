using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PortfolioApi.Data;
using System.IO;

namespace PortfolioApi.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // appsettings.json ဖိုင်ကို ရှာဖွေခြင်း
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Pin the MySQL version so design-time tooling (dotnet ef) does NOT
            // open a live DB connection. Migrations only need the model, not the server.
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35)));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}