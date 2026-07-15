using PizzaApp.Payment.Core.DTOs;

namespace PizzaApp.Payment.Core.Interfaces;

public record PaymentCheckoutInfo(string OrderId, decimal Amount, string Status);

public interface IPaymentService
{
    Task<PaymentCreation> CreatePaymentAsync(CreatePaymentDto dto);
    Task<PaymentCheckoutInfo?> GetCheckoutAsync(string paymentCode);
    Task<bool> ConfirmAsync(string paymentCode);              // mock: bấm xác nhận trên trang nội bộ
    Task<bool> ConfirmByProviderCodeAsync(long providerCode); // PayOS webhook
    Task<PaymentView?> GetByOrderAsync(string orderId);
}
