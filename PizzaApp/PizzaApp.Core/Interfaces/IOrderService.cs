using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PizzaApp.Core.DTOs.Order;

namespace PizzaApp.Core.Interfaces;

public interface IOrderService
{
    Task<int> CreateOrderAsync(int userId, CreateOrderDto dto);
    Task<List<OrderResultDto>> GetMyOrdersAsync(int userId);
}
