using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using CrypticAnalytic.Enums;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Tables;

[Table("fact_token_price")]
public class FactTokenPriceTable : IDatabaseTable
{
    [Column("price_id", NpgsqlDbType.Bigint)]
    public long PriceId { get; set; }

    [Column("token_id", NpgsqlDbType.Integer)]
    public int TokenId { get; set; }

    [Column("price", NpgsqlDbType.Numeric)]
    public decimal Price { get; set; }

    [Column("price_date", NpgsqlDbType.Bigint)]
    public long PriceDate { get; set; }
    
    [Column("currency", NpgsqlDbType.Integer)]
    public CurrencyType Currency { get; set; }
}