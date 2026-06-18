using Microsoft.AspNetCore.Mvc;
using Net.payOS;
using Net.payOS.Types;
using PizzaApp.Core.Interfaces;
using MongoDB.Driver;
using PizzaApp.Infrastructure.Data;
using PizzaApp.Core.Entities;

namespace PizzaApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly PayOS _payOS;
    private readonly IOrderService _orderService;
    private readonly IMongoCollection<Payment> _payments;

    public PaymentController(PayOS payOS, IOrderService orderService, MongoDbService mongoDb)
    {
        _payOS = payOS;
        _orderService = orderService;
        _payments = mongoDb.GetCollection<Payment>("Payments");
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandlePayOSWebhook([FromBody] WebhookType webhookBody)
    {
        try
        {
            // 1. Xác thực chữ ký để đảm bảo request này thực sự đến từ PayOS
            WebhookData data = _payOS.verifyPaymentWebhookData(webhookBody);

            // 2. Kiểm tra xem thanh toán có thành công không
            // PayOS Webhook trả về mã đơn hàng là data.orderCode
            if (webhookBody.success)
            {
                // Tìm Payment trong DB dựa trên mã đơn hàng của PayOS
                var payment = await _payments.Find(p => p.PayOSOrderCode == data.orderCode).FirstOrDefaultAsync();

                if (payment != null)
                {
                    // Tự động xác nhận thanh toán cho Order
                    await _orderService.ConfirmPaymentAsync(payment.OrderId);
                    Console.WriteLine($"[PAYOS] Đơn hàng {payment.OrderId} đã được tự động thanh toán qua Webhook.");
                }
            }

            return Ok(new { message = "Webhook received successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PAYOS ERROR] Webhook error: {ex.Message}");
            return Ok(); // Vẫn trả về Ok để PayOS không gửi lại liên tục
        }
    }
}
