namespace CrypticAnalytic.Models;

public class TransactionRecord
{
    public long TransactionId { get; set; }
    public int WalletId { get; set; }
    public int TokenId { get; set; }
    public string TransactionHash { get; set; } = null!;
    public string FromAddress { get; set; } = null!;
    public string ToAddress { get; set; } = null!;
    public decimal Amount { get; set; }
    public long Ts { get; set; }
    public int TransactionType { get; set; }
    public string Chain { get; set; } = null!;

    public string Symbol { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string LogoUri { get; set; } = null!;
    
    public decimal LastPrice { get; set; }
}