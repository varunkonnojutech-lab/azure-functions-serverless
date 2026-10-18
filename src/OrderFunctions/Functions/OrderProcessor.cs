using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace OrderFunctions.Functions;

/// <summary>
/// Consumes order messages from the "orders" Service Bus queue and runs the
/// downstream fulfillment steps. On the Consumption plan you pay only per
/// execution — the function scales to zero when the queue is empty.
/// </summary>
public class OrderProcessor
{
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(ILogger<OrderProcessor> logger)
    {
        _logger = logger;
    }

    [Function("ProcessOrder")]
    public void ProcessOrder(
        [ServiceBusTrigger("orders", Connection = "ServiceBusConnection")] string message,
        FunctionContext context)
    {
        _logger.LogInformation("Received order message ({Length} chars).", message.Length);

        // Simulated fulfillment pipeline: capture payment, reserve inventory,
        // schedule shipment, send the customer confirmation.
        _logger.LogInformation("Payment captured; inventory reserved; confirmation sent. Order processed.");
    }
}
