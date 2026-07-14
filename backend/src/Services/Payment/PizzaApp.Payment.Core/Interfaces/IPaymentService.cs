using PizzaApp.Payment.Core.DTOs;

namespace PizzaApp.Payment.Core.Interfaces;

public record PaymentCheckoutInfo(string OrderId, decimal Amount, string Status);

public interface IPaymentService
{
    Task<PaymentCreation> CreatePaymentAsync(CreatePaymentDto dto);
    Task<PaymentCheckoutInfo?> GetCheckoutAsync(string paymentCode); // thông tin để render trang thanh toán
    Task<bool> ConfirmAsync(string paymentCode); // xác nhận -> mark PAID + publish PaymentSucceeded
    Task<PaymentView?> GetByOrderAsync(string orderId);
}
