using CrypticAnalytic.DI;
using CrypticAnalytic.Interfaces;
using CrypticAnalytic.Services;
using CrypticAnalytic.Services.Background;
using CrypticAnalytic.Services.Config;
using CrypticAnalytic.Services.gRpc;
using CrypticAnalytic.Services.Processors;
using CrypticAnalytic.Services.Processors.MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var cfg = new ConfigService();

builder.Services.InjectConfiguration(cfg);
builder.Services.ConfigureMicroservices(cfg);
builder.Services.ConfigureRepositories();
builder.Services.AddMassTransitWithRabbitMQ(cfg);
builder.Services.AddScoped<WalletConnectedProcessor>();
builder.Services.AddScoped<SyncTransactionProcessor>();
builder.Services.AddScoped<PortfolioCorrelationService>();
builder.Services.AddScoped<ITokenPriceProcessor, TokenPriceProcessor>();
builder.Services.AddScoped<ISyncTransactionService, SyncTransactionService>();
/*builder.Services.AddHostedService<TokenPriceBackgroundService>();
builder.Services.AddHostedService<TransactionWorker>();*/


var app = builder.Build();

app.MapGrpcService<PortfolioAnalyticService>();
app.MapGrpcService<TransactionServiceImpl>();

app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Urls.Add("http://+:3000");
app.Urls.Add("https://+:3001");

app.Run();
