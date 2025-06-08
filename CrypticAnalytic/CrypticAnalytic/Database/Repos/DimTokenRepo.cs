using System.Text;
using Cryptic_Domain.Database.Config.Interfaces;
using Cryptic_Domain.Database.Interfaces;
using Cryptic_Domain.Database.Repos.Base;
using Cryptic_Domain.Helpers;
using CrypticAnalytic.Database.Tables;
using Npgsql;
using NpgsqlTypes;

public class DimTokenRepo : BaseDbRepo<DimTokenTable>
{
    private static List<DimTokenTable>? _cachedTokenList = null;
    private static readonly object _cacheLock = new object();

    public DimTokenRepo(
        IDatabaseConnectionService connectionService,
        IDatabaseConfiguration configuration)
        : base(connectionService, configuration)
    {
    }
    
    public async Task<List<DimTokenTable>> GetAllAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedTokenList != null)
            {
                return _cachedTokenList.ToList();
            }
        }
        
        var tokenList = new List<DimTokenTable>();
        var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath}";

        using (var cmd = new NpgsqlCommand(sql, Connection))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var token = await reader.MapAsync<DimTokenTable>();
                tokenList.Add(token);
            }
        }
        
        var ordered = tokenList.OrderBy(t => t.TokenId).ToList();
        
        lock (_cacheLock)
        {
            if (_cachedTokenList == null)
            {
                _cachedTokenList = ordered;
            }
        }
        
        return ordered.ToList();
    }
    
    public async Task<DimTokenTable?> GetByContractAddressAsync(string contractAddress)
    {
        var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE contract_address = @ContractAddress";
        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("ContractAddress", NpgsqlDbType.Varchar, contractAddress);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return await reader.MapAsync<DimTokenTable>();
        }

        return null;
    }
    
    public async Task<DimTokenTable?> GetBySymbolAsync(string symbol)
    {
        const string sql = @"
                SELECT " + "{columns}" + @"
                FROM " + "{schema}.{table}" + @"
                WHERE symbol = @Symbol
                LIMIT 1;
            ";

        using var cmd = new NpgsqlCommand(
            sql
                .Replace("{schema}.{table}", FullTablePath)
                .Replace("{columns}", string.Join(", ", Columns)),
            Connection);

        cmd.Parameters.AddWithValue("Symbol", NpgsqlDbType.Varchar, symbol);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return await reader.MapAsync<DimTokenTable>();
        }

        return null;
    }
    
    public async Task<List<DimTokenTable>> GetByContractAddressesAsync(string[] contractAddresses)
    {
        if (contractAddresses == null || contractAddresses.Length == 0)
            return new List<DimTokenTable>();

        var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} " +
                  $"WHERE contract_address = ANY(@Addresses)";
        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("Addresses", NpgsqlDbType.Array | NpgsqlDbType.Varchar, contractAddresses);

        var list = new List<DimTokenTable>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(await reader.MapAsync<DimTokenTable>());
        }

        return list;
    }
    
    public async Task<DimTokenTable> CreateAsync(DimTokenTable token)
    {
        var insertSql = $@"
                INSERT INTO {FullTablePath} 
                    (symbol, name, contract_address, decimals, logo_uri)
                VALUES 
                    (@Symbol, @Name, @ContractAddress, @Decimals, @LogoUri)
                RETURNING token_id;
            ";
        using var cmd = new NpgsqlCommand(insertSql, Connection);

        cmd.Parameters.AddWithValue("Symbol", NpgsqlDbType.Varchar, token.Symbol);
        cmd.Parameters.AddWithValue("ContractAddress", NpgsqlDbType.Varchar, token.ContractAddress);

        if (string.IsNullOrWhiteSpace(token.Name))
            cmd.Parameters.AddWithValue("Name", NpgsqlDbType.Varchar, DBNull.Value);
        else
            cmd.Parameters.AddWithValue("Name", NpgsqlDbType.Varchar, token.Name);

        if (!token.Decimals.HasValue)
            cmd.Parameters.AddWithValue("Decimals", NpgsqlDbType.Integer, DBNull.Value);
        else
            cmd.Parameters.AddWithValue("Decimals", NpgsqlDbType.Integer, token.Decimals.Value);

        if (string.IsNullOrWhiteSpace(token.LogoUri))
            cmd.Parameters.AddWithValue("LogoUri", NpgsqlDbType.Varchar, DBNull.Value);
        else
            cmd.Parameters.AddWithValue("LogoUri", NpgsqlDbType.Varchar, token.LogoUri);

        var returned = await cmd.ExecuteScalarAsync();
        if (returned is int newId)
        {
            token.TokenId = newId;
            lock (_cacheLock)
            {
                _cachedTokenList = null;
            }

            return token;
        }
        else if (returned is long newLongId)
        {
            token.TokenId = (int)newLongId;
            lock (_cacheLock)
            {
                _cachedTokenList = null;
            }

            return token;
        }
        else
        {
            throw new ArgumentException("Failed to insert dim_token record");
        }
    }

    public async Task<List<DimTokenTable>> CreateBatchAsync(IEnumerable<DimTokenTable> tokens)
    {
        var list = tokens as IList<DimTokenTable> ?? new List<DimTokenTable>(tokens);
        if (list.Count == 0)
            return new List<DimTokenTable>();

        var columns = new[] { "symbol", "name", "contract_address", "decimals", "logo_uri" };
        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {FullTablePath} ({string.Join(", ", columns)}) VALUES ");

        for (int i = 0; i < list.Count; i++)
        {
            var prefix = $"@p{i}_";
            sb.Append("(");
            sb.Append($"{prefix}Symbol, ");
            sb.Append($"{prefix}Name, ");
            sb.Append($"{prefix}ContractAddress, ");
            sb.Append($"{prefix}Decimals, ");
            sb.Append($"{prefix}LogoUri");
            sb.Append(")");

            if (i < list.Count - 1)
                sb.Append(", ");
        }

        sb.Append($@" ON CONFLICT (contract_address) DO NOTHING
                          RETURNING token_id, contract_address;");
        using var cmd = new NpgsqlCommand(sb.ToString(), Connection);

        for (int i = 0; i < list.Count; i++)
        {
            var token = list[i];
            var prefix = $"@p{i}_";

            cmd.Parameters.AddWithValue(prefix + "Symbol", NpgsqlDbType.Varchar, token.Symbol);

            if (string.IsNullOrWhiteSpace(token.Name))
                cmd.Parameters.AddWithValue(prefix + "Name", NpgsqlDbType.Varchar, DBNull.Value);
            else
                cmd.Parameters.AddWithValue(prefix + "Name", NpgsqlDbType.Varchar, token.Name);

            cmd.Parameters.AddWithValue(prefix + "ContractAddress", NpgsqlDbType.Varchar, token.ContractAddress);

            if (!token.Decimals.HasValue)
                cmd.Parameters.AddWithValue(prefix + "Decimals", NpgsqlDbType.Integer, DBNull.Value);
            else
                cmd.Parameters.AddWithValue(prefix + "Decimals", NpgsqlDbType.Integer, token.Decimals.Value);

            if (string.IsNullOrWhiteSpace(token.LogoUri))
                cmd.Parameters.AddWithValue(prefix + "LogoUri", NpgsqlDbType.Varchar, DBNull.Value);
            else
                cmd.Parameters.AddWithValue(prefix + "LogoUri", NpgsqlDbType.Varchar, token.LogoUri);
        }

        var inserted = new List<DimTokenTable>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var insertedId = reader.GetInt32(0);
                var insertedAddress = reader.GetString(1);

                var tok = list.FirstOrDefault(x =>
                    x.ContractAddress.Equals(insertedAddress, StringComparison.OrdinalIgnoreCase));
                if (tok != null)
                {
                    tok.TokenId = insertedId;
                    inserted.Add(tok);
                }
            }
        }
        
        lock (_cacheLock)
        {
            _cachedTokenList = null;
        }

        return inserted;
    }
    
    public async Task<List<DimTokenTable>> GetByIdsAsync(int[] ids)
    {
        if (ids == null || ids.Length == 0)
            return new List<DimTokenTable>();

        var sql = $"SELECT {string.Join(", ", Columns)} FROM {FullTablePath} WHERE token_id = ANY(@Ids)";
        using var cmd = new NpgsqlCommand(sql, Connection);
        cmd.Parameters.AddWithValue("Ids", NpgsqlDbType.Array | NpgsqlDbType.Integer, ids);

        var list = new List<DimTokenTable>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(await reader.MapAsync<DimTokenTable>());
        }

        return list;
    }
}