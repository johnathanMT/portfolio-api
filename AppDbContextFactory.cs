using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PortfolioApi.Data;

namespace PortfolioApi.Data
{
    /// <summary>
    /// Design-time factory used ONLY by the EF Core tooling (`dotnet ef ...`).
    /// `dotnet ef` does not run your real app, so it needs a way to build a
    /// fully-configured AppDbContext (provider + connection string) on its own.
    /// EF prefers this factory over spinning up your Program.cs host.
    ///
    /// KEY FIX: read the SAME configuration sources your app uses — appsettings,
    /// the environment-specific file, .NET user-secrets (dev machine), AND
    /// environment variables — so the real Aiven connection string is actually
    /// found here. Previously only appsettings.json was read, so a real secret
    /// supplied via user-secrets/env was silently ignored and the factory fell
    /// back to root@localhost (→ "Access denied").
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
                // Dev-machine secrets (run: `dotnet user-secrets set ...`) — NEVER committed.
                .AddUserSecrets(typeof(AppDbContextFactory).Assembly, optional: true)
                // Honours ConnectionStrings__DefaultConnection (CI / Render / shell export).
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // `dotnet ef migrations add` only needs the MODEL (no DB connection), so a
            // syntactically-valid DUMMY is fine when no real string is supplied. But
            // `dotnet ef database update` DOES connect — for that you MUST provide the
            // real Aiven string via user-secrets or an env var (see the warning below).
            if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_"))
            {
                Console.WriteLine(
                    "[AppDbContextFactory] WARNING: no real 'DefaultConnection' found — using a dummy string.\n" +
                    "  • `migrations add` will work.\n" +
                    "  • `database update` will FAIL until you supply the real Aiven connection string\n" +
                    "    via:  dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<aiven string>\"");
                connectionString = "Server=localhost;Port=3306;Database=design_time;User Id=root;Password=;";
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                // Pinned version → the tooling never makes an AutoDetect DB round-trip.
                .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 35)),
                    my =>
                    {
                        // Aiven's first connection can be slow (cold pooler + TLS handshake),
                        // which EF reports as a "transient failure". Retry instead of throwing,
                        // and give the migration a generous command timeout. Mirrors Program.cs.
                        my.EnableRetryOnFailure(
                            maxRetryCount: 8,
                            maxRetryDelay: TimeSpan.FromSeconds(15),
                            errorNumbersToAdd: null);
                        my.CommandTimeout(120);
                    })
                .Options;

            return new AppDbContext(options);
        }
    }
}