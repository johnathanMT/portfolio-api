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

            // At design time (dotnet ef) we never connect — the version is pinned and
            // migrations only need the model. If appsettings.json holds placeholders
            // (e.g. "YOUR_PORT") the real connection string isn't available here, so
            // fall back to a syntactically valid dummy just so the string parses.
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_"))
                connectionString = "Server=localhost;Port=3306;Database=design_time;User=root;Password=root;";

            // Pin the MySQL version so the tooling does NOT open a live DB connection.
            optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35)));

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}