using System.Text;
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using CrypticAnalytic.Database.Tables;
using CrypticAnalytic.Models;
using Npgsql;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Repos;

public class FactTokenPriceRepo : BaseDbRepo<FactTokenPriceTable>
{
    public FactTokenPriceRepo(
        IDatabaseConnectionService connectionService,
        IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }

    /// <summary>
    /// Для кожного tokenId з масиву повертає максимальний price_date (або 0, якщо записів не було).
    /// </summary>
    public async Task<Dictionary<int, long>> GetLastPriceDatesAsync(int[] tokenIds)
    {
        if (tokenIds == null || tokenIds.Length == 0)
            return new Dictionary<int, long>();

        // Запит: для кожного token_id бере макс(price_date). Якщо записів немає — цього token_id не буде в результаті.
        var sql = $@"
                SELECT token_id, COALESCE(MAX(price_date), 0) AS last_ts
                FROM {FullTablePath}
                WHERE token_id = ANY(@Ids)
                GROUP BY token_id;
            ";

        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("Ids", NpgsqlDbType.Array | NpgsqlDbType.Integer, tokenIds);

        var result = new Dictionary<int, long>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tId = reader.GetInt32(0);
            var lastTs = reader.GetInt64(1);
            result[tId] = lastTs;
        }
        
        foreach (var id in tokenIds)
        {
            if (!result.ContainsKey(id))
                result[id] = 0L;
        }

        return result;
    }

    public async Task<decimal> GetLastPriceAtAsync(int tokenId, int currency, long snapshotTs)
    {
        const string sql = @"
                SELECT price
                FROM " + "{schema}.{table}" + @"
                WHERE token_id = @TokenId
                  AND currency = @Currency
                  AND price_date <= @SnapshotTs
                ORDER BY price_date DESC
                LIMIT 1;
            ";

        using var cmd = new NpgsqlCommand(
            sql.Replace("{schema}.{table}", FullTablePath), Connection);

        cmd.Parameters.AddWithValue("TokenId", NpgsqlDbType.Integer, tokenId);
        cmd.Parameters.AddWithValue("Currency", NpgsqlDbType.Integer, currency);
        cmd.Parameters.AddWithValue("SnapshotTs", NpgsqlDbType.Bigint, snapshotTs);

        var result = await cmd.ExecuteScalarAsync();
        if (result is decimal dec) return dec;
        if (result is double dbl) return Convert.ToDecimal(dbl);
        return 0m;
    }
    
    public async Task CreateBatchAsync(IEnumerable<FactTokenPriceTable> entities)
    {
        var list = entities as IList<FactTokenPriceTable> ?? new List<FactTokenPriceTable>(entities);
        if (list.Count == 0) return;

        var columns = new[]
        {
            "token_id",
            "price",
            "price_date",
            "currency"
        };

        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {FullTablePath} ({string.Join(", ", columns)}) VALUES ");

        for (int i = 0; i < list.Count; i++)
        {
            var prefix = $"@p{i}_";
            sb.Append("(");
            sb.Append($"{prefix}TokenId, ");
            sb.Append($"{prefix}Price, ");
            sb.Append($"{prefix}PriceDate, ");
            sb.Append($"{prefix}Currency");
            sb.Append(")");

            if (i < list.Count - 1)
                sb.Append(", ");
        }

        sb.Append(" ON CONFLICT (token_id, price_date) DO NOTHING;");

        using var cmd = new NpgsqlCommand(sb.ToString(), Connection);

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            var prefix = $"@p{i}_";

            cmd.Parameters.AddWithValue(prefix + "TokenId", NpgsqlDbType.Integer, e.TokenId);
            cmd.Parameters.AddWithValue(prefix + "Price", NpgsqlDbType.Numeric, e.Price);
            cmd.Parameters.AddWithValue(prefix + "PriceDate", NpgsqlDbType.Bigint, e.PriceDate);
            cmd.Parameters.AddWithValue(prefix + "Currency", NpgsqlDbType.Integer, (int)e.Currency);
        }

        await cmd.ExecuteNonQueryAsync();
    }
}