using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PizzaApp.Core.DTOs.Order;

namespace PizzaApp.Core.Interfaces;

public interface IOrderService
{
    Task<string> CreateOrderAsync(string userId, CreateOrderDto dto);
    Task<bool> ConfirmPaymentAsync(string orderId);
    Task<List<OrderResultDto>> GetMyOrdersAsync(string userId);
    Task<OrderResultDto?> GetOrderDetailAsync(string orderId, string userId);
    Task<bool> CancelOrderAsync(string orderId, string userId);
    // Admin methods
    Task<List<OrderResultDto>> GetAllOrdersAsync();
    Task<List<OrderResultDto>> GetOrdersByStatusAsync(string status);
    Task<bool> UpdateOrderStatusAsync(string orderId, string status);

    // Shipper methods
    Task<bool> ClaimOrderAsync(string orderId, string shipperId);          // Ready -> Delivering, gán shipper
    Task<List<OrderResultDto>> GetShipperOrdersAsync(string shipperId);    // đơn của shipper này
    Task<bool> UpdateDeliveryStatusAsync(string orderId, string shipperId, string status); // Delivering -> Done/Cancelled
}
