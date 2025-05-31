using Cryptic_Domain.Database.Attributes;
using Cryptic_Domain.Interfaces.Database;
using NpgsqlTypes;

namespace CrypticAnalytic.Database.Tables;

[Table("dim_token")]
public class DimTokenTable : IDatabaseTable
{
    [Column("token_id", NpgsqlDbType.Integer)]
    public int TokenId { get; set; }

    [Column("symbol", NpgsqlDbType.Varchar)]
    public string Symbol { get; set; } = null!;

    [Column("name", NpgsqlDbType.Varchar)]
    public string? Name { get; set; }

    [Column("contract_address", NpgsqlDbType.Varchar)]
    public string ContractAddress { get; set; } = null!;

    [Column("decimals", NpgsqlDbType.Integer)]
    public int? Decimals { get; set; }

    [Column("logo_uri", NpgsqlDbType.Varchar)]
    public string? LogoUri { get; set; }
}