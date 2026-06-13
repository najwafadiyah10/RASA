using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using RasaApi.Services;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen data alert lansia
    /// </summary>
    [Route("api/alerts")]
    [ApiController]
    [Authorize]
    public class AlertsController : ControllerBase
    {
        private readonly RasaDbContext _context;
        private readonly FirebaseNotificationService _firebaseNotificationService;

        public AlertsController(
            RasaDbContext context,
            FirebaseNotificationService firebaseNotificationService
        )
        {
            _context = context;
            _firebaseNotificationService = firebaseNotificationService;
        }

        /// <summary>
        /// Menambahkan/mengirim alert lansia ke keluarga terhubung (role lansia)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk mengirim alert kepada keluarga yang sudah terhubung.
        /// Alert hanya dapat dikirim jika lansia sudah memiliki koneksi accepted dengan keluarga.
        ///
        /// Nilai riskLevel yang diperbolehkan:
        ///
        /// - waspada
        /// - darurat
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] AlertRequest request)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim alert"
                });
            }

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

            var acceptedConnections = await _context.Connections
                .Where(connection =>
                    connection.ElderlyId == elderlyId &&
                    connection.Status == "accepted"
                )
                .ToListAsync();

            if (acceptedConnections.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Belum ada keluarga yang terhubung"
                });
            }

            List<Alert> alerts = acceptedConnections.Select(connection => new Alert
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                FamilyId = connection.FamilyId,
                AlertType = alertType,
                Message = request.Message.Trim(),
                RiskLevel = riskLevel,
                //Latitude = request.Latitude,
                //Longitude = request.Longitude,
                //Status = "unread",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Alerts.AddRange(alerts);
            await _context.SaveChangesAsync();

            bool firebaseNotificationSent = false;
            int fcmTokenCount = 0;
            string? firebaseNotificationError = null;

            if (riskLevel == "darurat")
            {
                try
                {
                    var familyIds = acceptedConnections
                        .Select(connection => connection.FamilyId)
                        .ToList();

                    var fcmTokens = await _context.NotificationTokens
                        .Where(token => familyIds.Contains(token.UserId))
                        .Where(token => !string.IsNullOrWhiteSpace(token.FcmToken))
                        .Select(token => token.FcmToken)
                        .Distinct()
                        .ToListAsync();

                    fcmTokenCount = fcmTokens.Count;

                    if (fcmTokens.Count > 0)
                    {
                        var elderly = await _context.Users
                            .FirstOrDefaultAsync(user => user.Id == elderlyId);

                        string elderlyName = elderly?.Name ?? "Lansia";

                        await _firebaseNotificationService.SendFallAlertNotificationAsync(
                            fcmTokens,
                            elderlyName
                        );

                        firebaseNotificationSent = true;
                    }
                }
                catch (Exception ex)
                {
                    firebaseNotificationError = ex.Message;
                    Console.WriteLine($"Gagal mengirim Firebase notification: {ex.Message}");
                }
            }

            return Created("", new
            {
                message = "Alert berhasil dikirim ke keluarga terhubung",
                total_family_notified = alerts.Count,
                firebase_notification_sent = firebaseNotificationSent,
                fcm_token_count = fcmTokenCount,
                firebase_notification_error = firebaseNotificationError,
                alerts = alerts.Select(alert => new
                {
                    id = alert.Id,
                    elderly_id = alert.ElderlyId,
                    family_id = alert.FamilyId,
                    alert_type = alert.AlertType,
                    message = alert.Message,
                    risk_level = alert.RiskLevel,
                    //latitude = alert.Latitude,
                    //longitude = alert.Longitude,
                    //status = alert.Status,
                    created_at = alert.CreatedAt,
                    created_date = FormatDate(alert.CreatedAt),
                    created_time = FormatTime(alert.CreatedAt)
                })
            });
        }

        /// <summary>
        /// Mengambil daftar alert dari lansia tertentu (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat daftar alert dari lansia yang sudah terhubung.
        /// Keluarga hanya dapat melihat alert jika koneksi dengan lansia sudah berstatus accepted.
        /// </remarks>
        [HttpGet("~/api/elderlies/{elderlyId}/alerts")]
        public async Task<IActionResult> GetAlertsByElderly(Guid elderlyId)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? role = User.FindFirstValue(ClaimTypes.Role);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat alert"
                });
            }

            Guid familyId = Guid.Parse(userId);

            bool isConnected = await _context.Connections.AnyAsync(connection =>
                connection.ElderlyId == elderlyId &&
                connection.FamilyId == familyId &&
                connection.Status == "accepted"
            );

            if (!isConnected)
            {
                return Forbid();
            }

            var alertData = await _context.Alerts
                .Where(alert =>
                    alert.ElderlyId == elderlyId &&
                    alert.FamilyId == familyId
                )
                .OrderByDescending(alert => alert.CreatedAt)
                .ToListAsync();

            var alerts = alertData.Select(alert => new
            {
                id = alert.Id,
                elderly_id = alert.ElderlyId,
                family_id = alert.FamilyId,
                alert_type = alert.AlertType,
                message = alert.Message,
                risk_level = alert.RiskLevel,
                //latitude = alert.Latitude,
                //longitude = alert.Longitude,
                //status = alert.Status,
                created_at = alert.CreatedAt,
                created_date = FormatDate(alert.CreatedAt),
                created_time = FormatTime(alert.CreatedAt)
            }).ToList();

            return Ok(new
            {
                message = "Daftar alert berhasil diambil",
                data = alerts
            });
        }

        /// <summary>
        /// Mengubah status alert menjadi read atau unread (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk mengubah status alert miliknya.
        /// Alert hanya dapat diubah oleh keluarga yang menerima alert tersebut.
        ///
        /// Nilai status yang diperbolehkan:
        ///
        /// - read
        /// - unread
        /// </remarks>
        //        [HttpPut("{alertId}")]
        //        public async Task<IActionResult> UpdateAlertStatus(
        //            Guid alertId,
        //            [FromBody] UpdateAlertStatusRequest request
        //        )
        //        {
        //            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //            string? role = User.FindFirstValue(ClaimTypes.Role);

        //            if (string.IsNullOrEmpty(userId))
        //            {
        //                return Unauthorized(new
        //                {
        //                    message = "Token tidak valid"
        //                });
        //            }

        //            if (role != "keluarga")
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "Hanya akun keluarga yang dapat mengubah status alert"
        //                });
        //            }

        //            if (string.IsNullOrWhiteSpace(request.Status))
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "Status wajib diisi"
        //                });
        //            }

        //            string status = request.Status.Trim().ToLower();

        //            if (status != "read" && status != "unread")
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "Status harus read atau unread"
        //                });
        //            }

        //            Guid familyId = Guid.Parse(userId);

        //            Alert? alert = await _context.Alerts
        //                .FirstOrDefaultAsync(alert =>
        //                    alert.Id == alertId &&
        //                    alert.FamilyId == familyId
        //                );

        //            if (alert == null)
        //            {
        //                return NotFound(new
        //                {
        //                    message = "Alert tidak ditemukan"
        //                });
        //            }

        //            //alert.Status = status;

        //            await _context.SaveChangesAsync();

        //            return Ok(new
        //            {
        //                message = "Status alert berhasil diubah",
        //                alert = new
        //                {
        //                    id = alert.Id,
        //                    elderly_id = alert.ElderlyId,
        //                    family_id = alert.FamilyId,
        //                    alert_type = alert.AlertType,
        //                    message = alert.Message,
        //                    risk_level = alert.RiskLevel,
        //                    //latitude = alert.Latitude,
        //                    //longitude = alert.Longitude,
        //                    //status = alert.Status,
        //                    created_at = alert.CreatedAt,
        //                    created_date = FormatDate(alert.CreatedAt),
        //                    created_time = FormatTime(alert.CreatedAt)
        //                }
        //            });
        //        }

        private string FormatDate(DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("dd-MM-yyyy");
        }

        private string FormatTime(DateTime dateTime)
        {
            return dateTime.ToLocalTime().ToString("HH:mm");
        }
        //    }
    }
}