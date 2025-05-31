using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Tables;

[Table("fact_transaction")]
public class FactTransactionTable : IDatabaseTable
{
    [Column("transaction_id", NpgsqlDbType.Bigint)]
    public long TransactionId { get; set; }

    [Column("wallet_id", NpgsqlDbType.Integer)]
    public int WalletId { get; set; }

    [Column("token_id", NpgsqlDbType.Integer)]
    public int TokenId { get; set; }

    [Column("transaction_hash", NpgsqlDbType.Varchar)]
    public string TransactionHash { get; set; } = null!;

    [Column("from_address", NpgsqlDbType.Varchar)]
    public string FromAddress { get; set; } = null!;

    [Column("to_address", NpgsqlDbType.Varchar)]
    public string ToAddress { get; set; } = null!;

    [Column("amount", NpgsqlDbType.Numeric)]
    public decimal Amount { get; set; }
    
    [Column("ts", NpgsqlDbType.Bigint)]
    public long Ts { get; set; }
    
    [Column("transaction_type", NpgsqlDbType.Integer)]
    public int TransactionType { get; set; }
    
    [Column("chain", NpgsqlDbType.Varchar)]
    public string Chain { get; set; } = null!;
}