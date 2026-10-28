using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly FurnitureContext _db;

    public NotificationsController(FurnitureContext db)
    {
        _db = db;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications(int? skip = null, int? take = null)
    {
        var userId = GetUserId();
        int skipCount = skip ?? 0;
        int takeCount = take ?? 20;

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skipCount)
            .Take(takeCount)
            .Select(n => new
            {
                n.NotificationId,
                n.Title,
                n.Body,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();

        var totalCount = await _db.Notifications
            .Where(n => n.UserId == userId)
            .CountAsync();

        return Ok(new
        {
            message = "Lấy danh sách thông báo thành công",
            totalCount,
            count = notifications.Count,
            notifications
        });
    }

    // GET /api/notifications/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetNotificationDetail(int id)
    {
        var userId = GetUserId();

        var notification = await _db.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notification == null)
            return NotFound(new { message = "Không tìm thấy thông báo" });

        return Ok(new
        {
            notificationId = notification.NotificationId,
            title = notification.Title,
            body = notification.Body,
            isRead = notification.IsRead,
            createdAt = notification.CreatedAt
        });
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetUserId();

        var unreadCount = await _db.Notifications
            .Where(n => n.UserId == userId && n.IsRead == false)
            .CountAsync();

        return Ok(new
        {
            message = "Lấy số lượng thông báo chưa đọc thành công",
            unreadCount
        });
    }

    // PUT /api/notifications/{id}/mark-as-read
    [HttpPut("{id:int}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetUserId();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notification == null)
            return NotFound(new { message = "Không tìm thấy thông báo" });

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Đánh dấu thông báo đã đọc thành công",
            notificationId = notification.NotificationId,
            isRead = notification.IsRead
        });
    }

    // PUT /api/notifications/mark-all-as-read
    [HttpPut("mark-all-as-read")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetUserId();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.IsRead == false)
            .ToListAsync();

        if (notifications.Count == 0)
            return Ok(new { message = "Không có thông báo chưa đọc", count = 0 });

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Đánh dấu tất cả thông báo đã đọc thành công",
            count = notifications.Count
        });
    }

    // DELETE /api/notifications/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        var userId = GetUserId();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == id && n.UserId == userId);

        if (notification == null)
            return NotFound(new { message = "Không tìm thấy thông báo" });

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa thông báo thành công",
            notificationId = id
        });
    }

    // DELETE /api/notifications/delete-all
    [HttpDelete("delete-all")]
    public async Task<IActionResult> DeleteAllNotifications()
    {
        var userId = GetUserId();

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync();

        if (notifications.Count == 0)
            return Ok(new { message = "Không có thông báo để xóa", count = 0 });

        _db.Notifications.RemoveRange(notifications);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa tất cả thông báo thành công",
            count = notifications.Count
        });
    }
}
