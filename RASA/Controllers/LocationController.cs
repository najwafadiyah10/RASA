using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen data lokasi lansia
    /// </summary>
    [Route("api/locations")]
    [ApiController]
    [Authorize]
    public class LocationsController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public LocationsController(RasaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Menambahkan/mengirim data lokasi lansia (role lansia)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk mengirim data lokasi terbaru.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] LocationRequest request)
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

            // 2. Hanya lansia yang boleh mengirim lokasi
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim lokasi"
                });
            }

            // 3. Validasi latitude longitude
            if (request.Latitude < -90 || request.Latitude > 90)
            {
                return BadRequest(new
                {
                    message = "Latitude tidak valid"
                });
            }

            if (request.Longitude < -180 || request.Longitude > 180)
            {
                return BadRequest(new
                {
                    message = "Longitude tidak valid"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 4. Simpan lokasi baru
            ElderlyLocation newLocation = new ElderlyLocation
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                CreatedAt = DateTime.UtcNow
            };

            _context.Set<ElderlyLocation>().Add(newLocation);
            await _context.SaveChangesAsync();

            // 5. Response berhasil
            return Created("", new
            {
                message = "Lokasi berhasil disimpan",
                location = new
                {
                    id = newLocation.Id,
                    elderly_id = newLocation.ElderlyId,
                    latitude = newLocation.Latitude,
                    longitude = newLocation.Longitude,
                    accuracy = newLocation.Accuracy,
                    created_at = newLocation.CreatedAt
                }
            });
        }

        /// <summary>
        /// Mengambil data lokasi terbaru lansia (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat lokasi terbaru dari lansia yang sudah terhubung.
        /// </remarks>
        [HttpGet("~/api/elderlies/{elderlyId}/locations/latest")]
        public async Task<IActionResult> GetLatestLocation(Guid elderlyId)
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

            // 2. Hanya keluarga yang boleh mengambil lokasi lansia
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat lokasi lansia"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Cek apakah keluarga sudah terhubung dengan lansia ini
            bool isConnected = await _context.Set<Connection>().AnyAsync(c =>
                c.ElderlyId == elderlyId &&
                c.FamilyId == familyId &&
                c.Status == "accepted"
            );

            if (!isConnected)
            {
                return Forbid();
            }

            // 4. Ambil lokasi terbaru lansia
            ElderlyLocation? latestLocation = await _context.Set<ElderlyLocation>()
                .Where(l => l.ElderlyId == elderlyId)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestLocation == null)
            {
                return NotFound(new
                {
                    message = "Lokasi lansia belum tersedia"
                });
            }

            // 5. Response berhasil
            return Ok(new
            {
                message = "Lokasi terbaru berhasil diambil",
                location = new
                {
                    id = latestLocation.Id,
                    elderly_id = latestLocation.ElderlyId,
                    latitude = latestLocation.Latitude,
                    longitude = latestLocation.Longitude,
                    accuracy = latestLocation.Accuracy,
                    created_at = latestLocation.CreatedAt
                }
            });
        }


    }
}