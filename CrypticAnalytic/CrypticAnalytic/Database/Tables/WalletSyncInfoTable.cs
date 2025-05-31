using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Tables;

[Table("wallet_sync_info")]
public class WalletSyncInfoTable : IDatabaseTable
{
    [Column("wallet_id", NpgsqlDbType.Integer)]
    public int WalletId { get; set; }

    [Column("last_synced_ts", NpgsqlDbType.Bigint)]
    public long LastSyncedTs { get; set; }

    [Column("updated_at", NpgsqlDbType.TimestampTz)]
    public DateTime UpdatedAt { get; set; }
}