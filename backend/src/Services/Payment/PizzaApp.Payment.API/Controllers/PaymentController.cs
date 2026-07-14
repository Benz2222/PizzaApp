using Microsoft.AspNetCore.Mvc;
using PizzaApp.Payment.Core.DTOs;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

    // Gọi nội bộ bởi Order service (REST).
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
    {
        var creation = await _paymentService.CreatePaymentAsync(dto);
        return Ok(new { checkoutUrl = creation.CheckoutUrl, qrCode = creation.QrCodeDataUri });
    }

    // Quét QR mở URL này (GET) -> trang "cổng thanh toán" đẹp, chưa trừ tiền.
    [HttpGet("confirm/{code}")]
    public async Task<IActionResult> Confirm(string code)
    {
        var info = await _paymentService.GetCheckoutAsync(code);
        if (info == null) return Content(NotFoundPage, "text/html; charset=utf-8");
        if (info.Status == "PAID") return Content(SuccessPage(info.Amount), "text/html; charset=utf-8");
        return Content(CheckoutPage(code, info.Amount, info.OrderId), "text/html; charset=utf-8");
    }

    // Bấm "Xác nhận thanh toán" trên trang cổng -> hoàn tất (mark PAID + publish event).
    [HttpPost("confirm/{code}/complete")]
    public async Task<IActionResult> Complete(string code)
    {
        var ok = await _paymentService.ConfirmAsync(code);
        return ok ? Ok(new { status = "PAID" }) : NotFound(new { status = "NOT_FOUND" });
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrder(string orderId)
    {
        var view = await _paymentService.GetByOrderAsync(orderId);
        return view == null ? NotFound() : Ok(view);
    }

    private static string Money(decimal amount) => string.Format("{0:#,0}", amount) + "₫";

    private static string CheckoutPage(string code, decimal amount, string orderId) => CheckoutTemplate
        .Replace("__AMOUNT__", Money(amount))
        .Replace("__ORDER__", orderId)
        .Replace("__CODE__", code);

    private static string SuccessPage(decimal amount) => SuccessTemplate.Replace("__AMOUNT__", Money(amount));

    private const string CheckoutTemplate = """
<!doctype html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Cổng thanh toán PizzaApp</title>
<style>
  *{box-sizing:border-box;font-family:-apple-system,Segoe UI,Roboto,sans-serif}
  body{margin:0;background:#eef1f6;display:flex;min-height:100vh;align-items:center;justify-content:center;padding:16px}
  .card{background:#fff;border-radius:20px;max-width:380px;width:100%;box-shadow:0 12px 40px rgba(0,0,0,.12);overflow:hidden}
  .head{background:linear-gradient(135deg,#e11d48,#f97316);color:#fff;padding:22px 24px}
  .head .m{font-size:14px;opacity:.9}
  .head .b{font-size:20px;font-weight:700;margin-top:2px}
  .body{padding:24px}
  .amt{text-align:center;margin-bottom:18px}
  .amt .l{color:#6b7280;font-size:13px}
  .amt .v{font-size:34px;font-weight:800;color:#111827;margin-top:4px}
  .row{display:flex;justify-content:space-between;font-size:13px;color:#6b7280;padding:8px 0;border-top:1px dashed #e5e7eb}
  .row b{color:#111827;font-weight:600}
  button{width:100%;margin-top:20px;padding:15px;border:0;border-radius:12px;background:#16a34a;color:#fff;font-size:16px;font-weight:700;cursor:pointer}
  button:disabled{background:#9ca3af}
  .spin{display:none;text-align:center;margin-top:18px;color:#6b7280;font-size:14px}
  .dot{display:inline-block;width:8px;height:8px;border-radius:50%;background:#16a34a;margin:0 3px;animation:blink 1s infinite}
  .dot:nth-child(2){animation-delay:.2s}
  .dot:nth-child(3){animation-delay:.4s}
  @keyframes blink{0%,80%,100%{opacity:.3}40%{opacity:1}}
  .ok{display:none;text-align:center;padding:20px 0}
  .ok .ic{font-size:56px}
  .ok .t{font-size:20px;font-weight:700;color:#16a34a;margin-top:8px}
  .ok .s{color:#6b7280;font-size:14px;margin-top:6px}
</style></head><body>
<div class="card">
  <div class="head"><div class="m">PizzaApp Payment</div><div class="b">Xác nhận thanh toán</div></div>
  <div class="body">
    <div id="form">
      <div class="amt"><div class="l">Số tiền cần thanh toán</div><div class="v">__AMOUNT__</div></div>
      <div class="row"><span>Cửa hàng</span><b>PizzaApp</b></div>
      <div class="row"><span>Mã đơn</span><b>__ORDER__</b></div>
      <div class="row"><span>Phương thức</span><b>QR / Chuyển khoản</b></div>
      <button id="pay" onclick="pay()">Xác nhận thanh toán</button>
      <div class="spin" id="spin">Đang xử lý giao dịch <span class="dot"></span><span class="dot"></span><span class="dot"></span></div>
    </div>
    <div class="ok" id="ok"><div class="ic">✅</div><div class="t">Thanh toán thành công</div><div class="s">Bạn có thể quay lại ứng dụng PizzaApp.</div></div>
  </div>
</div>
<script>
async function pay(){
  document.getElementById('pay').disabled=true;
  document.getElementById('spin').style.display='block';
  try{
    const r=await fetch('/api/payment/confirm/__CODE__/complete',{method:'POST'});
    await new Promise(s=>setTimeout(s,1400));
    if(r.ok){document.getElementById('form').style.display='none';document.getElementById('ok').style.display='block';}
    else{alert('Không tìm thấy giao dịch');reset();}
  }catch(e){alert('Lỗi kết nối');reset();}
}
function reset(){document.getElementById('pay').disabled=false;document.getElementById('spin').style.display='none';}
</script></body></html>
""";

    private const string SuccessTemplate = """
<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Đã thanh toán</title><style>
*{font-family:-apple-system,Segoe UI,Roboto,sans-serif}
body{margin:0;background:#eef1f6;display:flex;min-height:100vh;align-items:center;justify-content:center}
.c{background:#fff;border-radius:20px;padding:36px;text-align:center;max-width:360px;box-shadow:0 12px 40px rgba(0,0,0,.12)}
.ic{font-size:56px}
.t{font-size:20px;font-weight:700;color:#16a34a;margin-top:8px}
.s{color:#6b7280;margin-top:6px}
</style></head>
<body><div class="c"><div class="ic">✅</div><div class="t">Đơn này đã được thanh toán</div><div class="s">__AMOUNT__</div></div></body></html>
""";

    private const string NotFoundPage = """
<!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Không tìm thấy</title><style>
*{font-family:-apple-system,Segoe UI,Roboto,sans-serif}
body{margin:0;background:#eef1f6;display:flex;min-height:100vh;align-items:center;justify-content:center}
.c{background:#fff;border-radius:20px;padding:36px;text-align:center;max-width:360px;box-shadow:0 12px 40px rgba(0,0,0,.12)}
.ic{font-size:56px}
.t{font-size:20px;font-weight:700;color:#dc2626;margin-top:8px}
</style></head>
<body><div class="c"><div class="ic">❌</div><div class="t">Không tìm thấy giao dịch</div></div></body></html>
""";
}
