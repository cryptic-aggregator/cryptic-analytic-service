using Cryptic_Domain.Models.MassTransit;
using CrypticAnalytic.Services.Processors.MassTransit;
using MassTransit;

namespace CrypticAnalytic.Controllers;

public class WalletConnectedConsumer : IConsumer<NewWalletConnectedMessage>
{
    private readonly ILogger<WalletConnectedConsumer> _logger;
    private readonly WalletConnectedProcessor _processor;

    public WalletConnectedConsumer(
        ILogger<WalletConnectedConsumer> logger,
        WalletConnectedProcessor processor)
    {
        _logger = logger;
        _processor = processor;
    }

    public async Task Consume(ConsumeContext<NewWalletConnectedMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "WalletConnectedConsumer: отримано WalletConnected: WalletId={WalletId}, Address={Address}",
            message.WalletId, message.WalletAddress);

        try
        {
            await _processor.ProcessAsync(message, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "WalletConnectedConsumer: Помилка під час ProcessAsync для WalletId={WalletId}.",
                message.WalletId);
        }
    }
}