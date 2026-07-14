namespace PizzaApp.Order.Core.Interfaces;

public record CartLine(string ProductId, int Quantity, string Size);

public interface ICartClient
{
    Task<List<CartLine>> GetCartAsync();
}
