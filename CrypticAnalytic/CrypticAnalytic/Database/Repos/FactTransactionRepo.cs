using System.Linq.Expressions;
using System.Text;
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using Cryptic_Domain.Helpers;
using CrypticAnalytic.Database.Tables;
using Npgsql;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Repos;

public class FactTransactionRepo : BaseDbRepo<FactTransactionTable>
{
    public FactTransactionRepo(
        IDatabaseConnectionService connectionService,
        IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }
    
    public async Task CreateBatchAsync(IEnumerable<FactTransactionTable> entities)
    {
        var list = entities as IList<FactTransactionTable> ?? new List<FactTransactionTable>(entities);
        if (list.Count == 0) return;
        
        var columns = new[]
        {
            "wallet_id",
            "token_id",
            "transaction_hash",
            "from_address",
            "to_address",
            "amount",
            "ts",
            "transaction_type",
            "chain"
        };
        
        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {FullTablePath} ({string.Join(", ", columns)}) VALUES ");
        
        for (int i = 0; i < list.Count; i++)
        {
            var prefix = $"@p{i}_";
            sb.Append("(");
            sb.Append($"{prefix}WalletId, ");
            sb.Append($"{prefix}TokenId, ");
            sb.Append($"{prefix}TxHash, ");
            sb.Append($"{prefix}FromAddress, ");
            sb.Append($"{prefix}ToAddress, ");
            sb.Append($"{prefix}Amount, ");
            sb.Append($"{prefix}Ts, ");
            sb.Append($"{prefix}TxType, ");
            sb.Append($"{prefix}Chain");
            sb.Append(")");

            if (i < list.Count - 1)
                sb.Append(", ");
        }
        
        sb.Append(" ON CONFLICT (transaction_hash) DO NOTHING;");

        using var cmd = new NpgsqlCommand(sb.ToString(), Connection);

        for (int i = 0; i < list.Count; i++)
        {
            var entity = list[i];
            var prefix = $"@p{i}_";

            cmd.Parameters.AddWithValue(prefix + "WalletId", NpgsqlDbType.Integer, entity.WalletId);
            cmd.Parameters.AddWithValue(prefix + "TokenId", NpgsqlDbType.Integer, entity.TokenId);
            cmd.Parameters.AddWithValue(prefix + "TxHash", NpgsqlDbType.Varchar, entity.TransactionHash);
            cmd.Parameters.AddWithValue(prefix + "FromAddress", NpgsqlDbType.Varchar, entity.FromAddress);
            cmd.Parameters.AddWithValue(prefix + "ToAddress", NpgsqlDbType.Varchar, entity.ToAddress);
            cmd.Parameters.AddWithValue(prefix + "Amount", NpgsqlDbType.Numeric, entity.Amount);
            cmd.Parameters.AddWithValue(prefix + "Ts", NpgsqlDbType.Bigint, entity.Ts);
            cmd.Parameters.AddWithValue(prefix + "TxType", NpgsqlDbType.Integer, entity.TransactionType);
            cmd.Parameters.AddWithValue(prefix + "Chain", NpgsqlDbType.Varchar, entity.Chain);
        }
        
        var affectedRows = await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Створює один запис у fact_transaction.
    /// </summary>
    public async Task<FactTransactionTable> CreateAsync(FactTransactionTable entity)
    {
        var insertSql = $@"
                INSERT INTO {FullTablePath} 
                    (wallet_id, token_id, transaction_hash, from_address, to_address, amount, ts, transaction_type, chain)
                VALUES 
                    (@WalletId, @TokenId, @TransactionHash, @FromAddress, @ToAddress, @Amount, @Ts, @TransactionType, @Chain)
                RETURNING transaction_id;
            ";

        using var cmd = new NpgsqlCommand(insertSql, Connection);
        cmd.Parameters.AddWithValue("WalletId", NpgsqlDbType.Integer, entity.WalletId);
        cmd.Parameters.AddWithValue("TokenId", NpgsqlDbType.Integer, entity.TokenId);
        cmd.Parameters.AddWithValue("TransactionHash", NpgsqlDbType.Varchar, entity.TransactionHash);
        cmd.Parameters.AddWithValue("FromAddress", NpgsqlDbType.Varchar, entity.FromAddress);
        cmd.Parameters.AddWithValue("ToAddress", NpgsqlDbType.Varchar, entity.ToAddress);
        cmd.Parameters.AddWithValue("Amount", NpgsqlDbType.Numeric, entity.Amount);
        cmd.Parameters.AddWithValue("Ts", NpgsqlDbType.Bigint, entity.Ts);
        cmd.Parameters.AddWithValue("TransactionType", NpgsqlDbType.Integer, entity.TransactionType);
        cmd.Parameters.AddWithValue("Chain", NpgsqlDbType.Varchar, entity.Chain);

        var returned = await cmd.ExecuteScalarAsync();
        if (returned is long newId)
        {
            entity.TransactionId = newId;
            return entity;
        }
        
        return default;
    }

    /// <summary>
    /// Перевіряє, чи існує транзакція з таким hash (щоб уникнути дублів).
    /// </summary>
    public async Task<bool> ExistsByHashAsync(string txHash)
    {
        var sql = $"SELECT 1 FROM {FullTablePath} WHERE transaction_hash = @TxHash LIMIT 1";
        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("TxHash", NpgsqlDbType.Varchar, txHash);

        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    /// <summary>
    /// Отримує всі транзакції для заданого wallet_id.
    /// </summary>
    public async Task<List<FactTransactionTable>> GetByWalletIdAsync(int walletId)
    {
        var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE wallet_id = @WalletId";
        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("WalletId", NpgsqlDbType.Integer, walletId);

        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<FactTransactionTable>();
        while (await reader.ReadAsync())
        {
            list.Add(await reader.MapAsync<FactTransactionTable>());
        }

        return list;
    }

    /// <summary>
    /// Видаляє транзакцію за ID.
    /// </summary>
    public async Task DeleteAsync(long transactionId)
    {
        using var cmd = new NpgsqlCommand($"DELETE FROM {FullTablePath} WHERE transaction_id = @Id", Connection);
        cmd.Parameters.AddWithValue("Id", NpgsqlDbType.Bigint, transactionId);
        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected == 0)
            throw new ArgumentException($"Transaction {transactionId} not found or already deleted.");
    }
    
    public async Task<Dictionary<int, decimal>> GetAllTokenBalancesAtAsync(int walletId, long snapshotTs)
    {
        const string sql = @"
                SELECT
                  ft.token_id,
                  COALESCE(SUM(
                    CASE
                      WHEN ft.transaction_type = 0 THEN ft.amount
                      ELSE -ft.amount
                    END
                  ), 0) AS balance
                FROM " + "{schema}.{table}" + @" AS ft
                WHERE ft.wallet_id = @WalletId
                  AND ft.ts <= @SnapshotTs
                GROUP BY ft.token_id;
            ";

        using var cmd = new NpgsqlCommand(
            sql.Replace("{schema}.{table}", FullTablePath), 
            Connection);
        cmd.Parameters.AddWithValue("WalletId", NpgsqlDbType.Integer, walletId);
        cmd.Parameters.AddWithValue("SnapshotTs", NpgsqlDbType.Bigint, snapshotTs);

        var dict = new Dictionary<int, decimal>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tid = reader.GetInt32(0);
            var bal = reader.GetDecimal(1);
            dict[tid] = bal;
        }

        return dict;
    }
}
    