using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderFunctions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // In-memory store keeps the sample runnable without a database.
        // Swap this registration for a Cosmos DB / SQL implementation in production.
        services.AddSingleton<IOrderStore, InMemoryOrderStore>();
    })
    .Build();

host.Run();
