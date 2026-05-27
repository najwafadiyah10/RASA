using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen data aktivitas lansia
    /// </summary>
    [Route("api/activities")]
    [ApiController]
    [Authorize]
    public class ActivitiesController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public ActivitiesController(RasaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Menambahkan/mengirim data aktivitas lansia (role lansia)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk mengirim data aktivitas terbaru.
        /// Nilai activityStatus yang diperbolehkan:
        /// - aktif
        /// - tidak_aktif
        /// - indikasi_jatuh
        /// 
        /// Nilai riskLevel yang diperbolehkan:
        /// - normal
        /// - waspada
        /// - darurat
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> CreateActivity([FromBody] ActivityRequest request)
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

            // 2. Hanya lansia yang boleh mengirim aktivitas
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim aktivitas"
                });
            }

            // 3. Rapikan input
            string activityStatus = request.ActivityStatus.Trim().ToLower();
            string riskLevel = request.RiskLevel.Trim().ToLower();

            // 4. Validasi activity_status
            if (activityStatus != "aktif" &&
                activityStatus != "tidak_aktif" &&
                activityStatus != "indikasi_jatuh")
            {
                return BadRequest(new
                {
                    message = "Activity status harus aktif, tidak_aktif, atau indikasi_jatuh"
                });
            }

            // 5. Validasi risk_level
            if (riskLevel != "normal" &&
                riskLevel != "waspada" &&
                riskLevel != "darurat")
            {
                return BadRequest(new
                {
                    message = "Risk level harus normal, waspada, atau darurat"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 6. Simpan aktivitas baru
            double? accelerationValue = request.AccelerationValue;

            if (accelerationValue == null &&
                request.XAxis != null &&
                request.YAxis != null &&
                request.ZAxis != null)
            {
                accelerationValue = Math.Sqrt(
                    Math.Pow(request.XAxis.Value, 2) +
                    Math.Pow(request.YAxis.Value, 2) +
                    Math.Pow(request.ZAxis.Value, 2)
                );
            }

            ElderlyActivity newActivity = new ElderlyActivity
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                XAxis = request.XAxis,
                YAxis = request.YAxis,
                ZAxis = request.ZAxis,
                AccelerationValue = accelerationValue,
                ActivityStatus = activityStatus,
                RiskLevel = riskLevel,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<ElderlyActivity>().Add(newActivity);
            await _context.SaveChangesAsync();

            // 7. Response berhasil
            return Created("", new
            {
                message = "Aktivitas berhasil disimpan",
                activity = new
                {
                    id = newActivity.Id,
                    elderly_id = newActivity.ElderlyId,
                    x_axis = newActivity.XAxis,
                    y_axis = newActivity.YAxis,
                    z_axis = newActivity.ZAxis,
                    acceleration_value = newActivity.AccelerationValue,
                    activity_status = newActivity.ActivityStatus,
                    risk_level = newActivity.RiskLevel,
                    created_at = newActivity.CreatedAt,
                    created_date = FormatDate(newActivity.CreatedAt),
                    created_time = FormatTime(newActivity.CreatedAt)
                }
            });
        }

        /// <summary>
        /// Mengambil data aktivitas terbaru lansia (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat aktivitas terbaru dari lansia yang sudah terhubung.
        /// Keluarga hanya dapat melihat aktivitas lansia jika koneksi sudah berstatus accepted.
        ///
        /// Data yang ditampilkan merupakan aktivitas terbaru berdasarkan waktu createdAt paling baru.
        /// </remarks>
        [HttpGet("~/api/elderlies/{elderlyId}/activities/latest")]
        [Authorize]
        public async Task<IActionResult> GetLatestActivity(Guid elderlyId)
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

            // 2. Hanya keluarga yang boleh melihat aktivitas lansia
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat aktivitas lansia"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Cek apakah keluarga sudah terhubung dengan lansia
            bool isConnected = await _context.Set<Connection>().AnyAsync(c =>
                c.ElderlyId == elderlyId &&
                c.FamilyId == familyId &&
                c.Status == "accepted"
            );

            if (!isConnected)
            {
                return Forbid();
            }

            // 4. Ambil aktivitas terbaru lansia
            ElderlyActivity? latestActivity = await _context.Set<ElderlyActivity>()
                .Where(a => a.ElderlyId == elderlyId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestActivity == null)
            {
                return NotFound(new
                {
                    message = "Aktivitas lansia belum tersedia"
                });
            }

            // 5. Response
            return Ok(new
            {
                message = "Aktivitas terbaru berhasil diambil",
                activity = new
                {
                    id = latestActivity.Id,
                    elderly_id = latestActivity.ElderlyId,
                    x_axis = latestActivity.XAxis,
                    y_axis = latestActivity.YAxis,
                    z_axis = latestActivity.ZAxis,
                    acceleration_value = latestActivity.AccelerationValue,
                    activity_status = latestActivity.ActivityStatus,
                    risk_level = latestActivity.RiskLevel,
                    created_at = latestActivity.CreatedAt,
                    created_date = FormatDate(latestActivity.CreatedAt),
                    created_time = FormatTime(latestActivity.CreatedAt)
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