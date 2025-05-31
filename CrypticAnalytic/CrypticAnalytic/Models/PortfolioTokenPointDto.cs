namespace CrypticAnalytic.Models;

public class PortfolioTokenPointDto
{
    public long Ts { get; set; }
    
    public decimal PortfolioValue { get; set; }
    
    public decimal TokenPrice { get; set; }
    
    public decimal PortfolioChangePct { get; set; }

    public decimal TokenChangePct { get; set; }
}