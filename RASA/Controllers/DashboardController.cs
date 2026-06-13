using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen data dashboard
    /// </summary>
    [Route("api/dashboard")]
    [ApiController]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public DashboardController(RasaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Mengambil dashboard lansia (role lansia)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk melihat ringkasan data dashboard miliknya.
        /// </remarks>
        [HttpGet("elderly")]
        public async Task<IActionResult> GetElderlyDashboard()
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

            // 2. Hanya lansia yang boleh akses dashboard lansia
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengakses dashboard lansia"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 3. Ambil data profil lansia
            User? elderly = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == elderlyId);

            if (elderly == null)
            {
                return NotFound(new
                {
                    message = "Data lansia tidak ditemukan"
                });
            }

            // 4. Ambil keluarga yang terhubung
            Connection? connection = await _context.Set<Connection>()
                .Where(c =>
                    c.ElderlyId == elderlyId &&
                    c.Status == "accepted"
                )
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();

            User? family = null;

            if (connection != null)
            {
                family = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == connection.FamilyId);
            }

            // 5. Ambil lokasi terakhir
            ElderlyLocation? latestLocation = await _context.Set<ElderlyLocation>()
                .Where(l => l.ElderlyId == elderlyId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            // 6. Ambil aktivitas terakhir
            ElderlyActivity? latestActivity = await _context.Set<ElderlyActivity>()
                .Where(a => a.ElderlyId == elderlyId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            // 7. Ambil data udara terakhir
            EnvironmentRecord? latestEnvironment = await _context.Set<EnvironmentRecord>()
                .Where(e => e.ElderlyId == elderlyId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            // 8. Ambil alert terakhir yang dikirim lansia
            Alert? latestAlert = await _context.Set<Alert>()
                .Where(a => a.ElderlyId == elderlyId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            // 9. Hitung total alert darurat yang pernah dikirim
            int totalEmergencyAlerts = await _context.Set<Alert>()
                .CountAsync(a =>
                    a.ElderlyId == elderlyId &&
                    a.RiskLevel == "darurat"
                );

            // 10. Response dashboard
            return Ok(new
            {
                message = "Dashboard lansia berhasil diambil",

                elderly = new
                {
                    id = elderly.Id,
                    name = elderly.Name,
                    email = elderly.Email,
                    phone = elderly.Phone,
                    role = elderly.Role,
                    photo_url = elderly.PhotoUrl,
                    created_at = elderly.CreatedAt
                },

                connected_family = family == null ? null : new
                {
                    id = family.Id,
                    name = family.Name,
                    email = family.Email,
                    phone = family.Phone,
                    photo_url = family.PhotoUrl,
                    connection_status = connection?.Status
                },

                latest_location = latestLocation == null ? null : new
                {
                    id = latestLocation.Id,
                    latitude = latestLocation.Latitude,
                    longitude = latestLocation.Longitude,
                    accuracy = latestLocation.Accuracy,
                    created_at = latestLocation.CreatedAt,
                    created_date = FormatDate(latestLocation.CreatedAt),
                    created_time = FormatTime(latestLocation.CreatedAt)
                },

                latest_activity = latestActivity == null ? null : new
                {
                    id = latestActivity.Id,
                    x_axis = latestActivity.XAxis,
                    y_axis = latestActivity.YAxis,
                    z_axis = latestActivity.ZAxis,
                    acceleration_value = latestActivity.AccelerationValue,
                    activity_status = latestActivity.ActivityStatus,
                    risk_level = latestActivity.RiskLevel,
                    created_at = latestActivity.CreatedAt,
                    created_date = FormatDate(latestActivity.CreatedAt),
                    created_time = FormatTime(latestActivity.CreatedAt)
                },

                latest_environment = latestEnvironment == null ? null : new
                {
                    id = latestEnvironment.Id,
                    temperature = latestEnvironment.Temperature,
                    humidity = latestEnvironment.Humidity,
                    air_quality = latestEnvironment.AirQuality,
                    risk_level = latestEnvironment.RiskLevel,
                    latitude = latestEnvironment.Latitude,
                    longitude = latestEnvironment.Longitude,
                    created_at = latestEnvironment.CreatedAt,
                    created_date = FormatDate(latestEnvironment.CreatedAt),
                    created_time = FormatTime(latestEnvironment.CreatedAt)
                },

                latest_alert = latestAlert == null ? null : new
                {
                    id = latestAlert.Id,
                    alert_type = latestAlert.AlertType,
                    message = latestAlert.Message,
                    risk_level = latestAlert.RiskLevel,
                    //status = latestAlert.Status,
                    created_at = latestAlert.CreatedAt,
                    created_date = FormatDate(latestAlert.CreatedAt),
                    created_time = FormatTime(latestAlert.CreatedAt)
                },

                summary = new
                {
                    total_emergency_alerts = totalEmergencyAlerts
                }
            });
        }

        /// <summary>
        /// Mengambil dashboard keluarga (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat ringkasan data dashboard keluarga.
        /// </remarks>
        [HttpGet("family")]
        public async Task<IActionResult> GetFamilyDashboard()
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

            // 2. Hanya keluarga yang boleh akses dashboard keluarga
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat mengakses dashboard keluarga"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Ambil data profil keluarga
            User? family = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == familyId);

            if (family == null)
            {
                return NotFound(new
                {
                    message = "Data keluarga tidak ditemukan"
                });
            }

            // 4. Cari koneksi accepted dengan lansia
            Connection? connection = await _context.Set<Connection>()
                .Where(c =>
                    c.FamilyId == familyId &&
                    c.Status == "accepted"
                )
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();

            if (connection == null)
            {
                return Ok(new
                {
                    message = "Dashboard keluarga berhasil diambil",
                    family = new
                    {
                        id = family.Id,
                        name = family.Name,
                        email = family.Email,
                        phone = family.Phone,
                        role = family.Role,
                        photo_url = family.PhotoUrl,
                        created_at = family.CreatedAt
                    },
                    elderly = (object?)null,
                    latest_location = (object?)null,
                    latest_activity = (object?)null,
                    latest_environment = (object?)null,
                    latest_alert = (object?)null,
                    summary = new
                    {
                        connection_status = "belum_terhubung",
                        total_alerts = 0,
                        unread_alerts = 0,
                        total_emergency_alerts = 0
                    }
                });
            }

            Guid elderlyId = connection.ElderlyId;

            // 5. Ambil data lansia yang terhubung
            User? elderly = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == elderlyId);

            // 6. Ambil lokasi terbaru lansia
            ElderlyLocation? latestLocation = await _context.Set<ElderlyLocation>()
                .Where(l => l.ElderlyId == elderlyId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            // 7. Ambil aktivitas terbaru lansia
            ElderlyActivity? latestActivity = await _context.Set<ElderlyActivity>()
                .Where(a => a.ElderlyId == elderlyId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            // 8. Ambil data udara terbaru lansia
            EnvironmentRecord? latestEnvironment = await _context.Set<EnvironmentRecord>()
                .Where(e => e.ElderlyId == elderlyId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            // 9. Ambil alert terbaru untuk keluarga ini
            Alert? latestAlert = await _context.Set<Alert>()
                .Where(a =>
                    a.ElderlyId == elderlyId &&
                    a.FamilyId == familyId
                )
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            // 10. Hitung jumlah alert
            int totalAlerts = await _context.Set<Alert>()
                .CountAsync(a =>
                    a.ElderlyId == elderlyId &&
                    a.FamilyId == familyId
                );

            //int unreadAlerts = await _context.Set<Alert>()
            //    .CountAsync(a =>
            //        a.ElderlyId == elderlyId &&
            //        a.FamilyId == familyId &&
            //        //a.Status == "unread"
            //    );

            int totalEmergencyAlerts = await _context.Set<Alert>()
                .CountAsync(a =>
                    a.ElderlyId == elderlyId &&
                    a.FamilyId == familyId &&
                    a.RiskLevel == "darurat"
                );

            // 11. Response dashboard keluarga
            return Ok(new
            {
                message = "Dashboard keluarga berhasil diambil",

                family = new
                {
                    id = family.Id,
                    name = family.Name,
                    email = family.Email,
                    phone = family.Phone,
                    role = family.Role,
                    photo_url = family.PhotoUrl,
                    created_at = family.CreatedAt
                },

                elderly = elderly == null ? null : new
                {
                    id = elderly.Id,
                    name = elderly.Name,
                    email = elderly.Email,
                    phone = elderly.Phone,
                    role = elderly.Role,
                    photo_url = elderly.PhotoUrl,
                    connection_status = connection.Status,
                    connected_at = connection.UpdatedAt
                },

                latest_location = latestLocation == null ? null : new
                {
                    id = latestLocation.Id,
                    latitude = latestLocation.Latitude,
                    longitude = latestLocation.Longitude,
                    accuracy = latestLocation.Accuracy,
                    created_at = latestLocation.CreatedAt,
                    created_date = FormatDate(latestLocation.CreatedAt),
                    created_time = FormatTime(latestLocation.CreatedAt)
                },

                latest_activity = latestActivity == null ? null : new
                {
                    id = latestActivity.Id,
                    x_axis = latestActivity.XAxis,
                    y_axis = latestActivity.YAxis,
                    z_axis = latestActivity.ZAxis,
                    acceleration_value = latestActivity.AccelerationValue,
                    activity_status = latestActivity.ActivityStatus,
                    risk_level = latestActivity.RiskLevel,
                    created_at = latestActivity.CreatedAt,
                    created_date = FormatDate(latestActivity.CreatedAt),
                    created_time = FormatTime(latestActivity.CreatedAt)
                },

                latest_environment = latestEnvironment == null ? null : new
                {
                    id = latestEnvironment.Id,
                    temperature = latestEnvironment.Temperature,
                    humidity = latestEnvironment.Humidity,
                    air_quality = latestEnvironment.AirQuality,
                    risk_level = latestEnvironment.RiskLevel,
                    latitude = latestEnvironment.Latitude,
                    longitude = latestEnvironment.Longitude,
                    created_at = latestEnvironment.CreatedAt,
                    created_date = FormatDate(latestEnvironment.CreatedAt),
                    created_time = FormatTime(latestEnvironment.CreatedAt)
                },

                latest_alert = latestAlert == null ? null : new
                {
                    id = latestAlert.Id,
                    alert_type = latestAlert.AlertType,
                    message = latestAlert.Message,
                    risk_level = latestAlert.RiskLevel,
                    //latitude = latestAlert.Latitude,
                    //longitude = latestAlert.Longitude,
                    //status = latestAlert.Status,
                    created_at = latestAlert.CreatedAt,
                    created_date = FormatDate(latestAlert.CreatedAt),
                    created_time = FormatTime(latestAlert.CreatedAt)
                },

                summary = new
                {
                    connection_status = "accepted",
                    total_alerts = totalAlerts,
                    //unread_alerts = unreadAlerts,
                    total_emergency_alerts = totalEmergencyAlerts
                }
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
    }
}