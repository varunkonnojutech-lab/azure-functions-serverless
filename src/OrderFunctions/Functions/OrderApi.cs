using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OrderFunctions.Models;
using OrderFunctions.Services;

namespace OrderFunctions.Functions;

/// <summary>
/// HTTP API for orders. In production, CreateOrder would also publish the order
/// to the "orders" Service Bus queue so <see cref="OrderProcessor"/> can run the
/// fulfillment steps asynchronously.
/// </summary>
public class OrderApi
{
    private readonly ILogger<OrderApi> _logger;
    private readonly IOrderStore _store;

    public OrderApi(ILogger<OrderApi> logger, IOrderStore store)
    {
        _logger = logger;
        _store = store;
    }

    [Function("CreateOrder")]
    public async Task<HttpResponseData> CreateOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req,
        FunctionContext context)
    {
        var input = await req.ReadFromJsonAsync<OrderRequest>();
        if (input is null || string.IsNullOrWhiteSpace(input.CustomerName) || input.Total <= 0)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "CustomerName is required and Total must be positive." });
            return bad;
        }

        var order = _store.Create(input.CustomerName.Trim(), input.Total);
        _logger.LogInformation("Created order {OrderId} for {Customer}", order.Id, order.CustomerName);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/orders/{order.Id}");
        await response.WriteAsJsonAsync(order);
        return response;
    }

    [Function("GetOrders")]
    public async Task<HttpResponseData> GetOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(_store.GetAll());
        return response;
    }

    [Function("GetOrder")]
    public async Task<HttpResponseData> GetOrder(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequestData req,
        string id)
    {
        var order = _store.Get(id);
        if (order is null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = $"Order '{id}' not found." });
            return notFound;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(order);
        return response;
    }
}
