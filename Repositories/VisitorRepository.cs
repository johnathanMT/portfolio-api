using System.Data;
using Microsoft.EntityFrameworkCore;
using PortfolioApi.Data;
using PortfolioApi.DTOs;
using PortfolioApi.Interfaces;

namespace PortfolioApi.Repositories;

/// <summary>
/// Raw-SQL implementation of <see cref="IVisitorRepository"/>. Two tables are
/// auto-created on first use (no EF migration needed):
///   • visitor_stats     : single row (id=1) with the global total.
///   • visitor_countries : one row per country (country PK + running count).
/// All writes are atomic/parameterized → concurrency- and injection-safe.
/// </summary>
public class VisitorRepository : IVisitorRepository
{
    private readonly AppDbContext _db;

    // Schema-ensure runs at most once per process.
    private static bool _ensured;
    private static readonly SemaphoreSlim _ensureLock = new(1, 1);

    public VisitorRepository(AppDbContext db) => _db = db;

    private async Task EnsureSchemaAsync()
    {
        if (_ensured) return;
        await _ensureLock.WaitAsync();
        try
        {
            if (_ensured) return;

            await _db.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS visitor_stats (
                      id            INT PRIMARY KEY,
                      total_visits  BIGINT   NOT NULL DEFAULT 0,
                      updated_at    DATETIME NOT NULL
                  );");
            await _db.Database.ExecuteSqlRawAsync(
                "INSERT IGNORE INTO visitor_stats (id, total_visits, updated_at) VALUES (1, 0, UTC_TIMESTAMP());");

            await _db.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS visitor_countries (
                      country     VARCHAR(100) NOT NULL PRIMARY KEY,
                      visits      BIGINT       NOT NULL DEFAULT 0,
                      updated_at  DATETIME     NOT NULL
                  );");

            _ensured = true;
        }
        finally { _ensureLock.Release(); }
    }

    public async Task<long> GetTotalAsync()
    {
        await EnsureSchemaAsync();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT total_visits FROM visitor_stats WHERE id = 1;";
        var r = await cmd.ExecuteScalarAsync();
        return r is null or DBNull ? 0L : Convert.ToInt64(r);
    }

    public async Task IncrementTotalAsync()
    {
        await EnsureSchemaAsync();
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE visitor_stats SET total_visits = total_visits + 1, updated_at = UTC_TIMESTAMP() WHERE id = 1;");
    }

    public async Task IncrementCountryAsync(string country)
    {
        await EnsureSchemaAsync();
        // Parameterized upsert ({0} → bound parameter, injection-safe).
        await _db.Database.ExecuteSqlRawAsync(
            @"INSERT INTO visitor_countries (country, visits, updated_at)
              VALUES ({0}, 1, UTC_TIMESTAMP())
              ON DUPLICATE KEY UPDATE visits = visits + 1, updated_at = UTC_TIMESTAMP();",
            country);
    }

    public async Task<IReadOnlyList<CountryCount>> GetCountriesAsync()
    {
        await EnsureSchemaAsync();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT country, visits FROM visitor_countries ORDER BY visits DESC, country ASC LIMIT 100;";

        var list = new List<CountryCount>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(new CountryCount(Convert.ToString(reader.GetValue(0)) ?? string.Empty, Convert.ToInt64(reader.GetValue(1))));
        return list;
    }
}
