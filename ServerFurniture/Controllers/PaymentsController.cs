using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServerFurniture.Controllers;

[ApiController]
[AllowAnonymous] // Webhook từ SePay gửi tới không dùng JWT Bearer của user
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly FurnitureContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(FurnitureContext db, IConfiguration config, ILogger<PaymentsController> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// API hứng Webhook từ SePay khi có biến động số dư (khách chuyển khoản thành công)
    /// URL: POST /api/payments/sepay-webhook
    /// </summary>
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> HandleSepayWebhook([FromBody] SepayWebhookPayload payload)
    {
        if (payload == null)
        {
            return BadRequest(new { success = false, message = "Dữ liệu webhook rỗng" });
        }

        _logger.LogInformation("Nhận webhook SePay: ID {Id}, Số tiền: {Amount}, Nội dung: {Content}",
            payload.Id, payload.TransferAmount, payload.Content);

        // 1. Kiểm tra API Key / Secret của SePay (nếu đã cấu hình trong appsettings.json)
        var configuredApiKey = _config["Sepay:ApiKey"];
        if (!string.IsNullOrWhiteSpace(configuredApiKey))
        {
            // SePay thường truyền qua header "Authorization: Apikey <Key>" hoặc query
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.Contains(configuredApiKey))
            {
                _logger.LogWarning("SePay Webhook: API Key không hợp lệ hoặc không khớp.");
                return Unauthorized(new { success = false, message = "API Key không hợp lệ" });
            }
        }

        // 2. Chỉ xử lý giao dịch TIỀN VÀO (transferType == "in")
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { success = true, message = "Bỏ qua vì không phải giao dịch tiền vào" });
        }

        // 3. Trích xuất mã đơn hàng từ nội dung chuyển khoản
        // Khách chuyển khoản thường theo cú pháp: DH15, SE_15, hoặc 15
        int? orderId = ExtractOrderId(payload.Content);
        if (!orderId.HasValue)
        {
            _logger.LogWarning("Không thể bóc tách OrderId từ nội dung chuyển khoản: {Content}", payload.Content);
            return Ok(new { success = false, message = "Không tìm thấy mã đơn hàng trong nội dung chuyển khoản" });
        }

        // 4. Tìm đơn hàng trong cơ sở dữ liệu
        var order = await _db.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.OrderId == orderId.Value);

        if (order == null)
        {
            _logger.LogWarning("Không tìm thấy đơn hàng với OrderId: {OrderId}", orderId);
            return Ok(new { success = false, message = $"Không tìm thấy đơn hàng #{orderId}" });
        }

        // 5. Kiểm tra nếu đơn hàng đã được thanh toán trước đó
        if (order.Payment != null && order.Payment.PaymentStatus == "Đã thanh toán")
        {
            return Ok(new { success = true, message = "Đơn hàng đã được thanh toán trước đó" });
        }

        try
        {
            // 6. Cập nhật trạng thái đơn hàng và bảng Payment
            order.OrderStatus = "Đã thanh toán";

            if (order.Payment == null)
            {
                order.Payment = new Payment
                {
                    OrderId = order.OrderId,
                    PaymentGateway = "Sepay"
                };
                _db.Payments.Add(order.Payment);
            }

            order.Payment.PaymentStatus = "Đã thanh toán";
            order.Payment.TransactionId = payload.ReferenceCode ?? payload.Id.ToString();
            order.Payment.PaidAt = DateTime.UtcNow;
            order.Payment.WebhookRawData = JsonSerializer.Serialize(payload);

            await _db.SaveChangesAsync();

            _logger.LogInformation("Cập nhật đơn hàng #{OrderId} thành công qua SePay", order.OrderId);

            // SePay yêu cầu trả về HTTP 200 kèm json success = true
            return Ok(new
            {
                success = true,
                message = $"Đã cập nhật đơn hàng #{order.OrderId} thành công"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật đơn hàng qua SePay Webhook");
            return StatusCode(500, new { success = false, message = "Lỗi xử lý server", error = ex.Message });
        }
    }

    /// <summary>
    /// Hàm bóc tách OrderId từ nội dung chuyển khoản
    /// Hỗ trợ cú pháp: DH15, SEPAY15, ORDER15 hoặc số 15
    /// </summary>
    private static int? ExtractOrderId(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        // Ưu tiên tìm cú pháp: DH<số>, SE<số>, ORDER<số>
        var prefixMatch = Regex.Match(content, @"(?:DH|SEPAY|SE|ORDER)\s*([0-9]+)", RegexOptions.IgnoreCase);
        if (prefixMatch.Success && int.TryParse(prefixMatch.Groups[1].Value, out int idWithPrefix))
        {
            return idWithPrefix;
        }

        // Nếu không có tiền tố, tìm chuỗi số đầu tiên
        var numberMatch = Regex.Match(content, @"\b([0-9]+)\b");
        if (numberMatch.Success && int.TryParse(numberMatch.Groups[1].Value, out int id))
        {
            return id;
        }

        return null;
    }
}

/// <summary>
/// Cấu trúc dữ liệu JSON mà SePay bắn qua Webhook
/// </summary>
public class SepayWebhookPayload
{
    public long Id { get; set; }                           // ID giao dịch trên SePay
    public string? Gateway { get; set; }                   // Brand name ngân hàng (MBBank, VCB...)
    public string? TransactionDate { get; set; }           // Thời gian giao dịch
    public string? AccountNumber { get; set; }             // Số tài khoản ngân hàng nhận
    public string? SubAccount { get; set; }
    public string? Code { get; set; }                      // Mã code thanh toán
    public string? Content { get; set; }                   // Nội dung chuyển khoản
    public string? TransferType { get; set; }              // "in" (tiền vào) hoặc "out" (tiền ra)
    public string? Description { get; set; }
    public decimal TransferAmount { get; set; }            // Số tiền chuyển
    public string? ReferenceCode { get; set; }             // Mã tham chiếu ngân hàng
    public decimal Accumulated { get; set; }               // Số dư tích lũy
}

