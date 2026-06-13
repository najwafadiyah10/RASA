using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen riwayat notifikasi keluarga
    /// </summary>
    [Route("api/history")]
    [ApiController]
    [Authorize]
    public class HistoryController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public HistoryController(RasaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Mengambil riwayat notifikasi alert (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat riwayat notifikasi alert yang diterima.
        /// </remarks>
        //[HttpGet("alerts")]
        //public async Task<IActionResult> GetAlertHistory(
        //    [FromQuery] DateTime? startDate,
        //    [FromQuery] DateTime? endDate)
        //{
        //    // Ambil user id dan role dari token
        //    string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    string? role = User.FindFirstValue(ClaimTypes.Role);

        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized(new { message = "Token tidak valid" });

        //    if (role != "keluarga")
        //        return Forbid();

        //    Guid familyId = Guid.Parse(userId);

        //    // Ambil semua alert keluarga dari database
        //    var query = _context.Alerts
        //        .Where(a => a.FamilyId == familyId)
        //        .OrderByDescending(a => a.CreatedAt)
        //        .AsQueryable();

        //    // Filter tanggal jika ada
        //    if (startDate.HasValue)
        //        query = query.Where(a => a.CreatedAt >= startDate.Value);

        //    if (endDate.HasValue)
        //        query = query.Where(a => a.CreatedAt <= endDate.Value);

        //    var alertData = await query.ToListAsync();

        //    // Format response
        //    var history = alertData.Select(a => new
        //    {
        //        id = a.Id,
        //        elderly_id = a.ElderlyId,
        //        alert_type = a.AlertType,
        //        message = a.Message,
        //        risk_level = a.RiskLevel,
        //        //status = a.Status,
        //        created_at = a.CreatedAt,
        //        created_date = a.CreatedAt.ToLocalTime().ToString("dd-MM-yyyy"),
        //        created_time = a.CreatedAt.ToLocalTime().ToString("HH:mm")
        //    }).ToList();

        //    return Ok(new
        //    {
        //        message = "Riwayat notifikasi berhasil diambil",
        //        data = history
        //    });

        //}

        /// <summary>
        /// Menghapus riwayat notifikasi alert secara soft delete (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk menghapus riwayat notifikasi alert miliknya.
        /// </remarks>
        [HttpDelete("alerts/{alertId}")]
        public async Task<IActionResult> DeleteAlertHistory(Guid alertId)
        {
            // 1. Ambil user id dan role dari token
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            // 2. Hanya keluarga yang boleh hapus history notifikasi
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat menghapus riwayat notifikasi"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Cari alert berdasarkan id dan pastikan alert itu milik keluarga yang login
            Alert? alert = await _context.Set<Alert>()
                .FirstOrDefaultAsync(a =>
                    a.Id == alertId &&
                    a.FamilyId == familyId
                );

            if (alert == null)
            {
                return NotFound(new
                {
                    message = "Riwayat notifikasi tidak ditemukan"
                });
            }

            // 4. Hapus alert dari database
            alert.IsDeleted = true;
            alert.DeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // 5. Response berhasil
            return Ok(new
            {
                message = "Riwayat notifikasi berhasil dihapus",
                deleted_alert_id = alert.Id
            });
        }
    }
}