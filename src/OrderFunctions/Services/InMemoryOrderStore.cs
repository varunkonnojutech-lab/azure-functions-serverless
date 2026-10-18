using System.Collections.Concurrent;
using OrderFunctions.Models;

namespace OrderFunctions.Services;

/// <summary>Thread-safe in-memory order store for local development and demos.</summary>
public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    public Order Create(string customerName, decimal total)
    {
        var order = new Order(
            Id: Guid.NewGuid().ToString("N"),
            CustomerName: customerName,
            Total: total,
            Status: "Created",
            CreatedAt: DateTimeOffset.UtcNow);

        _orders[order.Id] = order;
        return order;
    }

    public Order? Get(string id) =>
        _orders.TryGetValue(id, out var order) ? order : null;

    public IReadOnlyCollection<Order> GetAll() =>
        _orders.Values.OrderByDescending(o => o.CreatedAt).ToList();

    public int Count => _orders.Count;
}
