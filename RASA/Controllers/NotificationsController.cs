using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public NotificationsController(RasaDbContext context)
        {
            _context = context;
        }

        [HttpPost("token")]
        public async Task<IActionResult> SaveNotificationToken([FromBody] NotificationTokenRequest request)
        {
            // 1. Ambil user id dan role dari token login
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token login tidak valid"
                });
            }

            // 2. Untuk sekarang token notifikasi dipakai oleh keluarga
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat menyimpan token notifikasi"
                });
            }

            // 3. Validasi FCM token
            if (string.IsNullOrWhiteSpace(request.FcmToken))
            {
                return BadRequest(new
                {
                    message = "FCM token wajib diisi"
                });
            }

            Guid familyId = Guid.Parse(userId);
            string fcmToken = request.FcmToken.Trim();

            // 4. Cek apakah token ini sudah pernah disimpan
            NotificationToken? existingToken = await _context.NotificationTokens
                .FirstOrDefaultAsync(t => t.FcmToken == fcmToken);

            if (existingToken != null)
            {
                // Kalau token sudah ada, update pemilik dan device name
                existingToken.UserId = familyId;
                existingToken.DeviceName = request.DeviceName;
                existingToken.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Token notifikasi sudah ada dan berhasil diperbarui",
                    token = new
                    {
                        id = existingToken.Id,
                        user_id = existingToken.UserId,
                        fcm_token = existingToken.FcmToken,
                        device_name = existingToken.DeviceName,
                        created_at = existingToken.CreatedAt,
                        updated_at = existingToken.UpdatedAt
                    }
                });
            }

            // 5. Simpan token baru
            NotificationToken newToken = new NotificationToken
            {
                Id = Guid.NewGuid(),
                UserId = familyId,
                FcmToken = fcmToken,
                DeviceName = request.DeviceName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.NotificationTokens.Add(newToken);
            await _context.SaveChangesAsync();

            // 6. Response berhasil
            return Created("", new
            {
                message = "Token notifikasi berhasil disimpan",
                token = new
                {
                    id = newToken.Id,
                    user_id = newToken.UserId,
                    fcm_token = newToken.FcmToken,
                    device_name = newToken.DeviceName,
                    created_at = newToken.CreatedAt,
                    updated_at = newToken.UpdatedAt
                }
            });
        }
    }
}