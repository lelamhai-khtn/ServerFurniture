using System.Globalization;
using System.Text.Json;

namespace ServerFurniture.Services;

public class OSRMService : IOSRMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OSRMService> _logger;
    private readonly string _baseUrl;

    public OSRMService(HttpClient httpClient, IConfiguration configuration, ILogger<OSRMService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["OSRM:BaseUrl"]?.TrimEnd('/') ?? "https://router.project-osrm.org";

        // Thiết lập User-Agent để tránh bị OSRM public server chặn hoặc rate limit
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ServerFurniture-App/1.0 (dotnet-csharp)");
        }
    }

    public async Task<RouteResult> GetDistanceAsync(
        double originLat,
        double originLng,
        double destLat,
        double destLng,
        string profile = "driving",
        CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tính hợp lệ của tọa độ
        if (originLat < -90 || originLat > 90 || destLat < -90 || destLat > 90)
        {
            return new RouteResult
            {
                Success = false,
                Message = "Vĩ độ (Latitude) không hợp lệ. Giá trị phải nằm trong khoảng [-90, 90]."
            };
        }

        if (originLng < -180 || originLng > 180 || destLng < -180 || destLng > 180)
        {
            return new RouteResult
            {
                Success = false,
                Message = "Kinh độ (Longitude) không hợp lệ. Giá trị phải nằm trong khoảng [-180, 180]."
            };
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            profile = "driving";
        }

        // 2. Định dạng URL OSRM
        // QUAN TRỌNG: OSRM yêu cầu thứ tự tọa độ là: {longitude},{latitude} (Kinh độ trước, Vĩ độ sau)
        var lon1 = originLng.ToString("0.######", CultureInfo.InvariantCulture);
        var lat1 = originLat.ToString("0.######", CultureInfo.InvariantCulture);
        var lon2 = destLng.ToString("0.######", CultureInfo.InvariantCulture);
        var lat2 = destLat.ToString("0.######", CultureInfo.InvariantCulture);

        var requestUri = $"{_baseUrl}/route/v1/{profile}/{lon1},{lat1};{lon2},{lat2}?overview=false&steps=false";

        try
        {
            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM request failed with status code {StatusCode}: {Response}", response.StatusCode, content);
                return new RouteResult
                {
                    Success = false,
                    Message = $"Lỗi khi gọi OSRM server (Status {response.StatusCode}): {content}"
                };
            }

            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var code = root.TryGetProperty("code", out var codeElem) ? codeElem.GetString() : null;
            if (code != "Ok")
            {
                var message = code switch
                {
                    "NoRoute" => "Không tìm thấy đường đi giữa 2 địa điểm được chọn.",
                    "InvalidQuery" => "Tọa độ hoặc cú pháp yêu cầu không hợp lệ đối với OSRM.",
                    _ => $"OSRM trả về mã lỗi: {code}"
                };

                return new RouteResult
                {
                    Success = false,
                    Message = message
                };
            }

            if (!root.TryGetProperty("routes", out var routesElem) || routesElem.GetArrayLength() == 0)
            {
                return new RouteResult
                {
                    Success = false,
                    Message = "Không có tuyến đường nào được tìm thấy."
                };
            }

            var primaryRoute = routesElem[0];
            var distanceMeters = primaryRoute.GetProperty("distance").GetDouble();
            var durationSeconds = primaryRoute.GetProperty("duration").GetDouble();

            // Lấy tên địa điểm từ waypoints (nếu có)
            string? originName = null;
            string? destinationName = null;
            if (root.TryGetProperty("waypoints", out var waypointsElem) && waypointsElem.GetArrayLength() >= 2)
            {
                originName = waypointsElem[0].TryGetProperty("name", out var wp1Name) ? wp1Name.GetString() : null;
                destinationName = waypointsElem[1].TryGetProperty("name", out var wp2Name) ? wp2Name.GetString() : null;
            }

            var distanceKm = Math.Round(distanceMeters / 1000.0, 2);
            var durationMinutes = Math.Round(durationSeconds / 60.0, 1);

            return new RouteResult
            {
                Success = true,
                Message = "Tính toán khoảng cách đường đi thành công qua OSRM",
                DistanceMeters = Math.Round(distanceMeters, 1),
                DistanceKm = distanceKm,
                DurationSeconds = Math.Round(durationSeconds, 1),
                DurationMinutes = durationMinutes,
                FormattedDistance = distanceKm >= 1 ? $"{distanceKm} km" : $"{Math.Round(distanceMeters)} m",
                FormattedDuration = FormatDuration(durationSeconds),
                Origin = new RouteCoordinate(originLat, originLng),
                Destination = new RouteCoordinate(destLat, destLng),
                OriginName = string.IsNullOrWhiteSpace(originName) ? null : originName,
                DestinationName = string.IsNullOrWhiteSpace(destinationName) ? null : destinationName,
                Profile = profile
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi OSRM server tại URL: {Uri}", requestUri);
            return new RouteResult
            {
                Success = false,
                Message = $"Không thể kết nối tới dịch vụ bản đồ OSRM: {ex.Message}"
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout khi chờ phản hồi từ OSRM server.");
            return new RouteResult
            {
                Success = false,
                Message = "Dịch vụ OSRM phản hồi quá lâu (Timeout)."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi tính khoảng cách qua OSRM.");
            return new RouteResult
            {
                Success = false,
                Message = $"Lỗi xử lý: {ex.Message}"
            };
        }
    }

    private static string FormatDuration(double totalSeconds)
    {
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        if (timeSpan.TotalHours >= 1)
        {
            var hours = (int)timeSpan.TotalHours;
            var minutes = timeSpan.Minutes;
            return minutes > 0 ? $"{hours} giờ {minutes} phút" : $"{hours} giờ";
        }
        var totalMinutes = (int)Math.Round(timeSpan.TotalMinutes);
        return totalMinutes <= 0 ? "Dưới 1 phút" : $"{totalMinutes} phút";
    }
}

