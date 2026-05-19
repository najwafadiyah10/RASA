using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    [Route("api/alerts")]
    [ApiController]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly RasaDbContext _context;


        public AlertsController(RasaDbContext context)
        {
            _context = context;

        }

        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] AlertRequest request)
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

            // 2. Hanya lansia yang boleh membuat alert
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim alert"
                });
            }

            // 3. Validasi data
            if (string.IsNullOrWhiteSpace(request.AlertType) ||
                string.IsNullOrWhiteSpace(request.Message) ||
                string.IsNullOrWhiteSpace(request.RiskLevel))
            {
                return BadRequest(new
                {
                    message = "Alert type, message, dan risk level wajib diisi"
                });
            }

            string alertType = request.AlertType.Trim().ToLower();
            string riskLevel = request.RiskLevel.Trim().ToLower();

            if (riskLevel != "waspada" && riskLevel != "darurat")
            {
                return BadRequest(new
                {
                    message = "Risk level harus waspada atau darurat"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 4. Cari keluarga yang sudah terhubung accepted
            var acceptedConnections = await _context.Set<Connection>()
                .Where(c => c.ElderlyId == elderlyId && c.Status == "accepted")
                .ToListAsync();

            if (acceptedConnections.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Belum ada keluarga yang terhubung"
                });
            }

            // 5. Buat alert untuk semua keluarga yang terhubung
            List<Alert> alerts = acceptedConnections.Select(connection => new Alert
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                FamilyId = connection.FamilyId,
                AlertType = alertType,
                Message = request.Message.Trim(),
                RiskLevel = riskLevel,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Status = "unread",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Set<Alert>().AddRange(alerts);
            await _context.SaveChangesAsync();

            // 6. Response
            return Created("", new
            {
                message = "Alert berhasil dikirim ke keluarga terhubung",
                total_family_notified = alerts.Count,
                alerts = alerts.Select(alert => new
                {
                    id = alert.Id,
                    elderly_id = alert.ElderlyId,
                    family_id = alert.FamilyId,
                    alert_type = alert.AlertType,
                    message = alert.Message,
                    risk_level = alert.RiskLevel,
                    latitude = alert.Latitude,
                    longitude = alert.Longitude,
                    status = alert.Status,
                    created_at = alert.CreatedAt,
                    created_date = FormatDate(alert.CreatedAt),
                    created_time = FormatTime(alert.CreatedAt)
                })
            });
        }

        //[HttpGet("~/api/elderlies/{elderlyId}/alerts")]
        //public async Task<IActionResult> GetAlertsByElderly(Guid elderlyId)
        //{
        //    // 1. Ambil user id dan role dari token
        //    string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    string? role = User.FindFirstValue(ClaimTypes.Role);

        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "Token tidak valid"
        //        });
        //    }

        //    // 2. Hanya keluarga yang boleh melihat alert
        //    if (role != "keluarga")
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Hanya akun keluarga yang dapat melihat alert"
        //        });
        //    }

        //    Guid familyId = Guid.Parse(userId);

        //    // 3. Cek apakah keluarga terhubung dengan lansia ini
        //    bool isConnected = await _context.Set<Connection>().AnyAsync(c =>
        //        c.ElderlyId == elderlyId &&
        //        c.FamilyId == familyId &&
        //        c.Status == "accepted"
        //    );

        //    if (!isConnected)
        //    {
        //        return Forbid();
        //    }

        //    // 4. Ambil daftar alert untuk keluarga ini dari lansia tersebut
        //    var alerts = await _context.Set<Alert>()
        //        .Where(a => a.ElderlyId == elderlyId && a.FamilyId == familyId)
        //        .OrderByDescending(a => a.CreatedAt)
        //        .Select(a => new
        //        {
        //            id = a.Id,
        //            elderly_id = a.ElderlyId,
        //            family_id = a.FamilyId,
        //            alert_type = a.AlertType,
        //            message = a.Message,
        //            risk_level = a.RiskLevel,
        //            latitude = a.Latitude,
        //            longitude = a.Longitude,
        //            status = a.Status,
        //            created_at = a.CreatedAt,
        //            created_date = FormatDate(a.CreatedAt),
        //            created_time = FormatTime(a.CreatedAt)
        //        })
        //        .ToListAsync();

        //    // 5. Response
        //    return Ok(new
        //    {
        //        message = "Daftar alert berhasil diambil",
        //        data = alerts
        //    });
        //}

        [HttpGet("~/api/elderlies/{elderlyId}/alerts")]
        public async Task<IActionResult> GetAlertsByElderly(Guid elderlyId)
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

            // 2. Hanya keluarga yang boleh melihat alert
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat alert"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Cek apakah keluarga terhubung dengan lansia ini
            bool isConnected = await _context.Set<Connection>().AnyAsync(c =>
                c.ElderlyId == elderlyId &&
                c.FamilyId == familyId &&
                c.Status == "accepted"
            );

            if (!isConnected)
            {
                return Forbid();
            }

            // 4. Ambil data alert dari database dulu
            var alertData = await _context.Set<Alert>()
                .Where(a => a.ElderlyId == elderlyId && a.FamilyId == familyId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // 5. Baru format tanggal dan jam setelah data keluar dari database
            var alerts = alertData.Select(a => new
            {
                id = a.Id,
                elderly_id = a.ElderlyId,
                family_id = a.FamilyId,
                alert_type = a.AlertType,
                message = a.Message,
                risk_level = a.RiskLevel,
                latitude = a.Latitude,
                longitude = a.Longitude,
                status = a.Status,
                created_at = a.CreatedAt,
                created_date = FormatDate(a.CreatedAt),
                created_time = FormatTime(a.CreatedAt)
            }).ToList();

            // 6. Response
            return Ok(new
            {
                message = "Daftar alert berhasil diambil",
                data = alerts
            });
        }


        private string FormatDate(DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("dd-MM-yyyy");
        }

        private string FormatTime(DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("HH:mm");
        }

        [HttpPut("{alertId}")]
        public async Task<IActionResult> UpdateAlertStatus(
    Guid alertId,
    [FromBody] UpdateAlertStatusRequest request)
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

            // 2. Hanya keluarga yang boleh update status alert
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat mengubah status alert"
                });
            }

            // 3. Validasi status
            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new
                {
                    message = "Status wajib diisi"
                });
            }

            string status = request.Status.Trim().ToLower();

            if (status != "read" && status != "unread")
            {
                return BadRequest(new
                {
                    message = "Status harus read atau unread"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 4. Cari alert milik keluarga yang sedang login
            Alert? alert = await _context.Set<Alert>()
                .FirstOrDefaultAsync(a =>
                    a.Id == alertId &&
                    a.FamilyId == familyId
                );

            if (alert == null)
            {
                return NotFound(new
                {
                    message = "Alert tidak ditemukan"
                });
            }

            // 5. Update status alert
            alert.Status = status;

            await _context.SaveChangesAsync();

            // 6. Response
            return Ok(new
            {
                message = "Status alert berhasil diubah",
                alert = new
                {
                    id = alert.Id,
                    elderly_id = alert.ElderlyId,
                    family_id = alert.FamilyId,
                    alert_type = alert.AlertType,
                    message = alert.Message,
                    risk_level = alert.RiskLevel,
                    latitude = alert.Latitude,
                    longitude = alert.Longitude,
                    status = alert.Status,
                    created_at = alert.CreatedAt,
                    created_date = FormatDate(alert.CreatedAt),
                    created_time = FormatTime(alert.CreatedAt)
                }
            });
        }
    }
}