using OrderFunctions.Models;

namespace OrderFunctions.Services;

/// <summary>Abstraction over order storage. The Functions code depends only on
/// this interface, so the backing store can change without touching triggers.</summary>
public interface IOrderStore
{
    Order Create(string customerName, decimal total);
    Order? Get(string id);
    IReadOnlyCollection<Order> GetAll();
    int Count { get; }
}
