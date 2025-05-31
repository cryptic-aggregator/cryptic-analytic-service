using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using Cryptic_Domain.Helpers;
using CrypticAnalytic.Database.Tables;
using Npgsql;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Repos;

public class WalletSyncInfoRepo : BaseDbRepo<WalletSyncInfoTable>
    {
        public WalletSyncInfoRepo(
            IDatabaseConnectionService connectionService,
            IDatabaseConfiguration configuration)
            : base(connectionService, configuration)
        {
        }

        public async Task<WalletSyncInfoTable?> GetByWalletIdAsync(int walletId)
        {
            var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE wallet_id = @WalletId";
            using var cmd = new NpgsqlCommand(sql, Connection);
            cmd.Parameters.AddWithValue("WalletId", NpgsqlDbType.Integer, walletId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return await reader.MapAsync<WalletSyncInfoTable>();
            }
            return null;
        }
        
        public async Task<WalletSyncInfoTable> CreateAsync(WalletSyncInfoTable entity)
        {
            var insertSql = $@"
                INSERT INTO {FullTablePath} (wallet_id, last_synced_ts, updated_at)
                VALUES (@WalletId, @LastSyncedTs, @UpdatedAt);
            ";
            using var cmd = new NpgsqlCommand(insertSql, Connection);
            cmd.Parameters.AddWithValue("WalletId",      NpgsqlDbType.Integer,  entity.WalletId);
            cmd.Parameters.AddWithValue("LastSyncedTs",  NpgsqlDbType.Bigint,   entity.LastSyncedTs);
            cmd.Parameters.AddWithValue("UpdatedAt",     NpgsqlDbType.TimestampTz, entity.UpdatedAt);

            await cmd.ExecuteNonQueryAsync();
            return entity;
        }
        
        public async Task UpdateLastSyncedTsAsync(int walletId, long newLastSyncedTs)
        {
            var updateSql = $@"
                UPDATE {FullTablePath}
                SET last_synced_ts = @LastSyncedTs,
                    updated_at      = @UpdatedAt
                WHERE wallet_id = @WalletId;
            ";
            using var cmd = new NpgsqlCommand(updateSql, Connection);
            cmd.Parameters.AddWithValue("LastSyncedTs", NpgsqlDbType.Bigint, newLastSyncedTs);
            cmd.Parameters.AddWithValue("UpdatedAt",    NpgsqlDbType.TimestampTz, DateTime.UtcNow);
            cmd.Parameters.AddWithValue("WalletId",     NpgsqlDbType.Integer, walletId);

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                throw new ArgumentException($"WalletSyncInfo for wallet_id={walletId} not found.");
        }
        
        public async Task<WalletSyncInfoTable> UpsertAsync(int walletId, long newLastSyncedTs)
        {
            var updateSql = $@"
                UPDATE {FullTablePath}
                SET last_synced_ts = @LastSyncedTs,
                    updated_at      = @UpdatedAt
                WHERE wallet_id = @WalletId;
            ";
            using var cmdUpdate = new NpgsqlCommand(updateSql, Connection);
            cmdUpdate.Parameters.AddWithValue("LastSyncedTs", NpgsqlDbType.Bigint, newLastSyncedTs);
            cmdUpdate.Parameters.AddWithValue("UpdatedAt",    NpgsqlDbType.TimestampTz, DateTime.UtcNow);
            cmdUpdate.Parameters.AddWithValue("WalletId",     NpgsqlDbType.Integer, walletId);

            var affected = await cmdUpdate.ExecuteNonQueryAsync();
            if (affected > 0)
            {
                return new WalletSyncInfoTable
                {
                    WalletId = walletId,
                    LastSyncedTs = newLastSyncedTs,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            
            var entity = new WalletSyncInfoTable
            {
                WalletId      = walletId,
                LastSyncedTs  = newLastSyncedTs,
                UpdatedAt     = DateTime.UtcNow
            };
            return await CreateAsync(entity);
        }
    }