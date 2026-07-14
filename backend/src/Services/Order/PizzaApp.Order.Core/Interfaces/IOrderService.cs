using PizzaApp.Order.Core.DTOs;

namespace PizzaApp.Order.Core.Interfaces;

public interface IOrderService
{
    Task<OrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto);
    Task<OrderResultDto> CheckoutFromCartAsync(string userId, string deliveryAddress);
    Task<bool> ConfirmPaymentAsync(string orderId);
    Task<List<OrderResultDto>> GetMyOrdersAsync(string userId);
    Task<OrderResultDto?> GetOrderDetailAsync(string orderId, string userId);
    Task<bool> CancelOrderAsync(string orderId, string userId);
    Task<List<OrderResultDto>> GetAllOrdersAsync();
    Task<List<OrderResultDto>> GetOrdersByStatusAsync(string status);
    Task<bool> UpdateOrderStatusAsync(string orderId, string status);
    Task<bool> ClaimOrderAsync(string orderId, string shipperId);
    Task<List<OrderResultDto>> GetShipperOrdersAsync(string shipperId);
    Task<bool> UpdateDeliveryStatusAsync(string orderId, string shipperId, string status);
}
