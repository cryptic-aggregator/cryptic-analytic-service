namespace CrypticAnalytic.Helpers.Chain;

public static class ChainDetector
{
    public static string DetectChain(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "unknown";

        address = address.Trim();
        if (address.StartsWith("0x") && address.Length == 42)
            return "eth";

        if (address.Length == 44 && IsBase58(address))
            return "sol";

        if (address.StartsWith("1") || address.StartsWith("3") || address.StartsWith("bc1"))
            return "btc";

        return "eth";
    }

    private static bool IsBase58(string input)
    {
        const string ALPHABET = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        foreach (var c in input)
        {
            if (!ALPHABET.Contains(c))
                return false;
        }
        return true;
    }
}