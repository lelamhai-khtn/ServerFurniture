using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerFurniture.Services;

namespace ServerFurniture.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class DistanceController : ControllerBase
{
    private readonly IOSRMService _osrmService;

    // Tọa độ cố định của công ty / kho hàng: Học viện Công nghệ Bưu chính Viễn thông cơ sở TP.HCM
    private const double CompanyLat = 10.848049697734623;
    private const double CompanyLng = 106.78666312526973;

    // Biểu giá xe Van / Bán tải 500kg (VNĐ)
    private const decimal BaseFeeVnd = 130000m;    // 130.000 đ cho 4 km đầu
    private const double BaseDistanceKm = 4.0;
    private const decimal PricePerKmVnd = 12000m;  // 12.000 đ/km tiếp theo

    // Tỉ giá quy đổi VNĐ sang USD (1 USD ~ 25.000 VNĐ)
    private const decimal UsdExchangeRate = 25000m;

    public DistanceController(IOSRMService osrmService)
    {
        _osrmService = osrmService;
    }

    /// <summary>
    /// Tính phí giao hàng (USD) từ công ty (Học viện Công nghệ Bưu chính Viễn thông) đến địa điểm khách hàng
    /// </summary>
    /// <param name="destLat">Vĩ độ điểm đến của khách hàng</param>
    /// <param name="destLng">Kinh độ điểm đến của khách hàng</param>
    /// <param name="profile">Phương tiện di chuyển: driving (mặc định - ô tô)</param>
    [HttpGet]
    public async Task<IActionResult> GetDistance(
        [FromQuery] double? destLat,
        [FromQuery] double? destLng,
        [FromQuery] string profile = "driving",
        CancellationToken cancellationToken = default)
    {
        if (!destLat.HasValue || !destLng.HasValue)
        {
            return BadRequest(new { message = "Vui lòng cung cấp tọa độ điểm đến của khách (destLat, destLng)." });
        }

        // Điểm xuất phát mặc định cố định từ Học viện Công nghệ Bưu chính Viễn thông
        var result = await _osrmService.GetDistanceAsync(
            CompanyLat,
            CompanyLng,
            destLat.Value,
            destLng.Value,
            profile,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        // 1. Tính cước phí VNĐ (Xe Van / Bán tải 500kg)
        decimal feeVnd = BaseFeeVnd;
        if (result.DistanceKm > BaseDistanceKm)
        {
            feeVnd += (decimal)(result.DistanceKm - BaseDistanceKm) * PricePerKmVnd;
        }
        feeVnd = Math.Round(feeVnd / 1000m) * 1000m; // Làm tròn hàng nghìn đồng

        // 2. Quy đổi sang USD (làm tròn 2 chữ số thập phân)
        decimal deliveryFeeUsd = Math.Round(feeVnd / UsdExchangeRate, 2);

        return Ok(new
        {
            deliveryfee = deliveryFeeUsd,
            distanceKm = result.DistanceKm
        });
    }
}
