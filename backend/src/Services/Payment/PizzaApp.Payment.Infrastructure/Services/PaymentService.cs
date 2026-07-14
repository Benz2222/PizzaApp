using MongoDB.Driver;
using PizzaApp.Payment.Core;
using PizzaApp.Payment.Core.DTOs;
using PizzaApp.Payment.Core.Interfaces;
using PizzaApp.BuildingBlocks.Messaging;
using PaymentEntity = PizzaApp.Payment.Core.Entities.Payment;

namespace PizzaApp.Payment.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IMongoCollection<PaymentEntity> _payments;
    private readonly IPaymentGateway _gateway;
    private readonly IEventBus _bus;
    private readonly PaymentSettings _settings;

    public PaymentService(PaymentDbContext db, IPaymentGateway gateway, IEventBus bus, PaymentSettings settings)
    {
        _payments = db.Payments;
        _gateway = gateway;
        _bus = bus;
        _settings = settings;
    }

    public async Task<PaymentCreation> CreatePaymentAsync(CreatePaymentDto dto)
    {
        var code = Guid.NewGuid().ToString("N");
        var confirmUrl = $"{_settings.PublicBaseUrl.TrimEnd('/')}/api/payment/confirm/{code}";
        var creation = _gateway.CreateQr(code, dto.Amount, confirmUrl);

        var payment = new PaymentEntity
        {
            OrderId = dto.OrderId,
            PaymentCode = code,
            Amount = dto.Amount,
            Status = "PENDING",
            CheckoutUrl = creation.CheckoutUrl,
            QrCodeDataUri = creation.QrCodeDataUri
        };
        await _payments.InsertOneAsync(payment);
        return creation;
    }

    public async Task<PaymentCheckoutInfo?> GetCheckoutAsync(string paymentCode)
    {
        var p = await _payments.Find(x => x.PaymentCode == paymentCode).FirstOrDefaultAsync();
        return p == null ? null : new PaymentCheckoutInfo(p.OrderId, p.Amount, p.Status);
    }

    public async Task<bool> ConfirmAsync(string paymentCode)
    {
        var payment = await _payments.Find(p => p.PaymentCode == paymentCode).FirstOrDefaultAsync();
        if (payment == null) return false;
        if (payment.Status == "PAID") return true; // idempotent

        await _payments.UpdateOneAsync(p => p.Id == payment.Id,
            Builders<PaymentEntity>.Update.Set(p => p.Status, "PAID"));

        _bus.Publish(new PaymentSucceededEvent(payment.OrderId));
        return true;
    }

    public async Task<PaymentView?> GetByOrderAsync(string orderId)
    {
        var p = await _payments.Find(x => x.OrderId == orderId).FirstOrDefaultAsync();
        return p == null ? null : new PaymentView
        {
            OrderId = p.OrderId,
            CheckoutUrl = p.CheckoutUrl,
            QrCodeDataUri = p.QrCodeDataUri,
            Status = p.Status
        };
    }
}
