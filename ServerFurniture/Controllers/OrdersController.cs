using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly FurnitureContext _db;

    public OrdersController(FurnitureContext db)
    {
        _db = db;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    // POST /api/orders
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (request == null || request.CartIds == null || request.CartIds.Count == 0)
            return BadRequest(new { message = "Danh sách sản phẩm không được để trống" });

        if (string.IsNullOrWhiteSpace(request.ShippingAddress))
            return BadRequest(new { message = "Địa chỉ giao hàng không được để trống" });

        var userId = GetUserId();

        // Lấy các sản phẩm từ giỏ hàng
        var cartItems = await _db.Carts
            .Where(c => c.UserId == userId && request.CartIds.Contains(c.CartId))
            .Include(c => c.Product)
            .ToListAsync();

        if (cartItems.Count == 0)
            return NotFound(new { message = "Không tìm thấy sản phẩm trong giỏ hàng" });

        try
        {
            decimal totalAmount = 0;

            // Tạo đơn hàng
            var order = new Order
            {
                UserId = userId,
                OrderStatus = "Chờ xác nhận",
                ShippingAddress = request.ShippingAddress,
                DeliveryDate = request.DeliveryDate,
                DeliveryTimeSlot = request.DeliveryTimeSlot,
                CreatedAt = DateTime.UtcNow
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            // Tạo các order item
            foreach (var cartItem in cartItems)
            {
                var unitPrice = cartItem.Product.DiscountPrice ?? cartItem.Product.Price;
                var itemTotal = unitPrice * cartItem.Quantity;
                totalAmount += itemTotal;

                var orderItem = new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = cartItem.ProductId,
                    Quantity = cartItem.Quantity,
                    UnitPrice = unitPrice,
                    ProductName = cartItem.Product.ProductName,
                    Color = cartItem.Product.Color,
                    UpholsteryMaterial = cartItem.Product.UpholsteryMaterial
                };

                _db.OrderItems.Add(orderItem);
            }

            // Cập nhật tổng tiền
            order.TotalAmount = totalAmount;

            // Xóa các item khỏi giỏ hàng
            _db.Carts.RemoveRange(cartItems);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Tạo đơn hàng thành công",
                orderId = order.OrderId,
                orderStatus = order.OrderStatus,
                totalAmount = order.TotalAmount,
                shippingAddress = order.ShippingAddress,
                createdAt = order.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tạo đơn hàng", error = ex.Message });
        }
    }

    // GET /api/orders
    [HttpGet]
    public async Task<IActionResult> GetOrders(int? skip = null, int? take = null, string? status = null)
    {
        var userId = GetUserId();
        int skipCount = skip ?? 0;
        int takeCount = take ?? 10;

        var query = _db.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product);

        // Lọc theo status nếu có
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.OrderStatus == status);
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skipCount)
            .Take(takeCount)
            .Select(o => new
            {
                o.OrderId,
                o.OrderStatus,
                o.TotalAmount,
                o.ShippingAddress,
                o.DeliveryDate,
                o.DeliveryTimeSlot,
                o.CreatedAt,
                ItemCount = o.OrderItems.Count
            })
            .ToListAsync();

        var totalCount = await query.CountAsync();

        return Ok(new
        {
            message = "Lấy danh sách đơn hàng thành công",
            totalCount,
            count = orders.Count,
            orders
        });
    }

    // GET /api/orders/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetOrderDetail(int id)
    {
        var userId = GetUserId();

        var order = await _db.Orders
            .Where(o => o.OrderId == id && o.UserId == userId)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync();

        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        return Ok(new
        {
            orderId = order.OrderId,
            userId = order.UserId,
            orderStatus = order.OrderStatus,
            totalAmount = order.TotalAmount,
            shippingAddress = order.ShippingAddress,
            deliveryDate = order.DeliveryDate,
            deliveryTimeSlot = order.DeliveryTimeSlot,
            createdAt = order.CreatedAt,
            items = order.OrderItems.Select(oi => new
            {
                oi.OrderItemId,
                oi.ProductId,
                oi.ProductName,
                oi.Quantity,
                oi.UnitPrice,
                oi.Color,
                oi.UpholsteryMaterial,
                itemTotal = oi.Quantity * oi.UnitPrice
            })
        });
    }

    // PUT /api/orders/{id}/status
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
            return BadRequest(new { message = "Trạng thái không được để trống" });

        var userId = GetUserId();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        order.OrderStatus = request.Status;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái đơn hàng thành công",
            orderId = order.OrderId,
            orderStatus = order.OrderStatus
        });
    }

    // PUT /api/orders/{id}/delivery
    [HttpPut("{id:int}/delivery")]
    public async Task<IActionResult> UpdateDeliveryInfo(int id, [FromBody] UpdateDeliveryRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Thông tin giao hàng không hợp lệ" });

        var userId = GetUserId();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        if (!string.IsNullOrWhiteSpace(request.ShippingAddress))
            order.ShippingAddress = request.ShippingAddress;

        if (request.DeliveryDate.HasValue)
            order.DeliveryDate = request.DeliveryDate;

        if (!string.IsNullOrWhiteSpace(request.DeliveryTimeSlot))
            order.DeliveryTimeSlot = request.DeliveryTimeSlot;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin giao hàng thành công",
            orderId = order.OrderId,
            shippingAddress = order.ShippingAddress,
            deliveryDate = order.DeliveryDate,
            deliveryTimeSlot = order.DeliveryTimeSlot
        });
    }

    // DELETE /api/orders/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var userId = GetUserId();
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId);

        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng" });

        // Chỉ có thể hủy đơn hàng đang chờ xác nhận
        if (order.OrderStatus != "Chờ xác nhận")
            return BadRequest(new { message = "Chỉ có thể hủy đơn hàng đang chờ xác nhận" });

        order.OrderStatus = "Đã hủy";
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Hủy đơn hàng thành công",
            orderId = order.OrderId,
            orderStatus = order.OrderStatus
        });
    }

    // GET /api/orders/statistics
    [HttpGet("statistics")]
    public async Task<IActionResult> GetOrderStatistics()
    {
        var userId = GetUserId();

        var stats = await _db.Orders
            .Where(o => o.UserId == userId)
            .GroupBy(o => o.OrderStatus)
            .Select(g => new
            {
                status = g.Key,
                count = g.Count(),
                totalAmount = g.Sum(o => o.TotalAmount)
            })
            .ToListAsync();

        var totalOrders = await _db.Orders
            .Where(o => o.UserId == userId)
            .CountAsync();

        var totalSpent = await _db.Orders
            .Where(o => o.UserId == userId)
            .SumAsync(o => o.TotalAmount);

        return Ok(new
        {
            message = "Lấy thống kê đơn hàng thành công",
            totalOrders,
            totalSpent,
            byStatus = stats
        });
    }
}

public class CreateOrderRequest
{
    public List<int>? CartIds { get; set; }
    public string? ShippingAddress { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryTimeSlot { get; set; }
}

public class UpdateOrderStatusRequest
{
    public string? Status { get; set; }
}

public class UpdateDeliveryRequest
{
    public string? ShippingAddress { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public string? DeliveryTimeSlot { get; set; }
}
