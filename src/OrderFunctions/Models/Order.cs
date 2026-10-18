namespace OrderFunctions.Models;

/// <summary>Payload accepted by POST /api/orders.</summary>
public record OrderRequest(string CustomerName, decimal Total);

/// <summary>Order as stored and returned by the API.</summary>
public record Order(string Id, string CustomerName, decimal Total, string Status, DateTimeOffset CreatedAt);
