using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    [Route("api/environment-records")]
    [ApiController]
    [Authorize]
    public class EnvironmentRecordsController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public EnvironmentRecordsController(RasaDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateEnvironmentRecord([FromBody] EnvironmentRecordRequest request)
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

            // 2. Hanya lansia yang boleh mengirim data lingkungan
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim data lingkungan"
                });
            }

            // 3. Validasi air quality
            if (string.IsNullOrWhiteSpace(request.AirQuality))
            {
                return BadRequest(new
                {
                    message = "Air quality wajib diisi"
                });
            }

            string airQuality = request.AirQuality.Trim().ToLower();

            if (airQuality != "baik" &&
                airQuality != "sedang" &&
                airQuality != "buruk")
            {
                return BadRequest(new
                {
                    message = "Air quality harus baik, sedang, atau buruk"
                });
            }

            // 4. Validasi risk level
            if (string.IsNullOrWhiteSpace(request.RiskLevel))
            {
                return BadRequest(new
                {
                    message = "Risk level wajib diisi"
                });
            }

            string riskLevel = request.RiskLevel.Trim().ToLower();

            if (riskLevel != "normal" &&
                riskLevel != "waspada" &&
                riskLevel != "darurat")
            {
                return BadRequest(new
                {
                    message = "Risk level harus normal, waspada, atau darurat"
                });
            }

            // 5. Validasi latitude dan longitude kalau dikirim
            if (request.Latitude != null &&
                (request.Latitude < -90 || request.Latitude > 90))
            {
                return BadRequest(new
                {
                    message = "Latitude tidak valid"
                });
            }

            if (request.Longitude != null &&
                (request.Longitude < -180 || request.Longitude > 180))
            {
                return BadRequest(new
                {
                    message = "Longitude tidak valid"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 6. Simpan data lingkungan
            EnvironmentRecord newRecord = new EnvironmentRecord
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                Temperature = request.Temperature,
                Humidity = request.Humidity,
                AirQuality = airQuality,
                RiskLevel = riskLevel,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                CreatedAt = DateTime.UtcNow
            };

            _context.EnvironmentRecords.Add(newRecord);
            await _context.SaveChangesAsync();

            // 7. Response berhasil
            return Created("", new
            {
                message = "Data lingkungan berhasil disimpan",
                environment = new
                {
                    id = newRecord.Id,
                    elderly_id = newRecord.ElderlyId,
                    temperature = newRecord.Temperature,
                    humidity = newRecord.Humidity,
                    air_quality = newRecord.AirQuality,
                    risk_level = newRecord.RiskLevel,
                    latitude = newRecord.Latitude,
                    longitude = newRecord.Longitude,
                    created_at = newRecord.CreatedAt,
                    created_date = FormatDate(newRecord.CreatedAt),
                    created_time = FormatTime(newRecord.CreatedAt)
                }
            });


        }
        [HttpGet("~/api/elderlies/{elderlyId}/environment/latest")]
        public async Task<IActionResult> GetLatestEnvironment(Guid elderlyId)
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

            // 2. Hanya keluarga yang boleh melihat data udara lansia
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat data udara lansia"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Cek apakah keluarga terhubung dengan lansia tersebut
            bool isConnected = await _context.Set<Connection>().AnyAsync(c =>
                c.ElderlyId == elderlyId &&
                c.FamilyId == familyId &&
                c.Status == "accepted"
            );

            if (!isConnected)
            {
                return Forbid();
            }

            // 4. Ambil data udara terbaru milik lansia
            EnvironmentRecord? latestEnvironment = await _context.EnvironmentRecords
                .Where(e => e.ElderlyId == elderlyId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestEnvironment == null)
            {
                return NotFound(new
                {
                    message = "Data udara belum tersedia"
                });
            }

            // 5. Response berhasil
            return Ok(new
            {
                message = "Data udara terbaru berhasil diambil",
                environment = new
                {
                    id = latestEnvironment.Id,
                    elderly_id = latestEnvironment.ElderlyId,
                    temperature = latestEnvironment.Temperature,
                    humidity = latestEnvironment.Humidity,
                    air_quality = latestEnvironment.AirQuality,
                    risk_level = latestEnvironment.RiskLevel,
                    latitude = latestEnvironment.Latitude,
                    longitude = latestEnvironment.Longitude,
                    created_at = latestEnvironment.CreatedAt,
                    created_date = FormatDate(latestEnvironment.CreatedAt),
                    created_time = FormatTime(latestEnvironment.CreatedAt)
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