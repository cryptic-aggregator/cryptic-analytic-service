namespace CrypticAnalytic.Models;

public class WalletSeedInfo
{
    public int WalletId { get; set; }
    public string WalletAddress { get; set; } = null!;
    public string Chain { get; set; } = null!;
    public long SinceTs { get; set; }
}