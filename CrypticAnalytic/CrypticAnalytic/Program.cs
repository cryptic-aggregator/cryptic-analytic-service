using CrypticAnalytic.Services.gRpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<PortfolioAnalyticService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Urls.Add("http://+:3000");
app.Urls.Add("https://+:3001");

app.Run();
