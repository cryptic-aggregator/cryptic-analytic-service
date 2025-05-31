namespace CrypticAnalytic.Interfaces;

public interface ITokenPriceProcessor
{
    Task ProcessAllTokensAsync(CancellationToken cancellationToken);
}