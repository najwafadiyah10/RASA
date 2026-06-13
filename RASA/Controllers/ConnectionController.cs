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
    /// Manajemen koneksi lansia dan keluarga
    /// </summary>
    [Route("api/connections")]
    [ApiController]
    [Authorize]
    public class ConnectionsController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public ConnectionsController(RasaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Mengirim permintaan koneksi ke keluarga (role lansia)
        /// </summary>
        /// /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk mengirim permintaan koneksi ke akun keluarga berdasarkan email keluarga.
        /// </remarks>
        [HttpPost]
        public async Task<IActionResult> CreateConnectionRequest([FromBody] ConnectionRequest request)
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

            // 2. Hanya lansia yang boleh mengirim permintaan
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat mengirim permintaan terhubung"
                });
            }

            // 3. Validasi email keluarga
            if (string.IsNullOrWhiteSpace(request.FamilyEmail))
            {
                return BadRequest(new
                {
                    message = "Email keluarga wajib diisi"
                });
            }

            string familyEmail = request.FamilyEmail.Trim().ToLower();
            Guid elderlyId = Guid.Parse(userId);

            // 4. Cari akun keluarga berdasarkan email
            User? family = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == familyEmail && u.Role == "keluarga");

            if (family == null)
            {
                return NotFound(new
                {
                    message = "Akun keluarga tidak ditemukan"
                });
            }

            // 5. Cek apakah lansia ini sudah punya koneksi aktif / pending
            bool elderlyAlreadyHasConnection = await _context.Connections.AnyAsync(c =>
                c.ElderlyId == elderlyId &&
                (c.Status == "pending" || c.Status == "accepted")
            );

            if (elderlyAlreadyHasConnection)
            {
                return Conflict(new
                {
                    message = "Lansia ini sudah memiliki keluarga terhubung atau permintaan yang masih menunggu respon"
                });
            }

            // 6. Cek apakah keluarga tujuan sudah punya lansia aktif / pending
            bool familyAlreadyHasConnection = await _context.Connections.AnyAsync(c =>
                c.FamilyId == family.Id &&
                (c.Status == "pending" || c.Status == "accepted")
            );

            if (familyAlreadyHasConnection)
            {
                return Conflict(new
                {
                    message = "Keluarga ini sudah memiliki lansia terhubung atau permintaan yang masih menunggu respon"
                });
            }

            // 7. Simpan koneksi baru dengan status pending
            Connection newConnection = new Connection
            {
                Id = Guid.NewGuid(),
                ElderlyId = elderlyId,
                FamilyId = family.Id,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Connections.Add(newConnection);
            await _context.SaveChangesAsync();

            // 8. Response berhasil
            return Created("", new
            {
                message = "Permintaan terhubung berhasil dikirim",
                connection = new
                {
                    id = newConnection.Id,
                    elderly_id = newConnection.ElderlyId,
                    family_id = newConnection.FamilyId,
                    family_name = family.Name,
                    family_email = family.Email,
                    status = newConnection.Status,
                    created_at = newConnection.CreatedAt
                }
            });
        }

        /// <summary>
        /// Mengambil daftar permintaan koneksi masuk (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat daftar permintaan koneksi yang masuk dari akun lansia.
        /// </remarks>
        [HttpGet("incoming")]
        public async Task<IActionResult> GetIncomingRequests()
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

            // 2. Hanya keluarga yang boleh melihat permintaan masuk
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat permintaan masuk"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Ambil data request pending untuk keluarga ini
            var incomingRequests = await (
                from connection in _context.Set<Connection>()
                join elderly in _context.Users
                    on connection.ElderlyId equals elderly.Id
                where connection.FamilyId == familyId
                      && connection.Status == "pending"
                select new
                {
                    connection_id = connection.Id,
                    elderly_id = elderly.Id,
                    elderly_name = elderly.Name,
                    elderly_email = elderly.Email,
                    elderly_phone = elderly.Phone,
                    elderly_photo_url = elderly.IsPhotoDeleted ? null : elderly.PhotoUrl,
                    status = connection.Status,
                    created_at = connection.CreatedAt
                }
            ).ToListAsync();

            // 4. Response
            return Ok(new
            {
                message = "Daftar permintaan masuk berhasil diambil",
                data = incomingRequests
            });
        }


        /// <summary>
        /// Menerima atau menolak permintaan koneksi (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk menerima atau menolak permintaan koneksi dari lansia.
        /// </remarks>
        [HttpPut("{connectionId}")]
        public async Task<IActionResult> UpdateConnectionStatus(
            Guid connectionId,
            [FromBody] UpdateConnectionStatusRequest request)
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

            // 2. Hanya keluarga yang boleh menerima/menolak permintaan
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat merespon permintaan"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return BadRequest(new
                {
                    message = "Status wajib diisi"
                });
            }

            // 3. Validasi status
            string status = request.Status.Trim().ToLower();

            if (status != "accepted" && status != "rejected")
            {
                return BadRequest(new
                {
                    message = "Status harus accepted atau rejected"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 4. Cari data koneksi
            Connection? connection = await _context.Connections
                .FirstOrDefaultAsync(c =>
                    c.Id == connectionId &&
                    c.FamilyId == familyId
                );

            if (connection == null)
            {
                return NotFound(new
                {
                    message = "Permintaan koneksi tidak ditemukan"
                });
            }

            // 5. Cek apakah status masih pending
            if (connection.Status != "pending")
            {
                return BadRequest(new
                {
                    message = "Permintaan ini sudah pernah direspon"
                });
            }

            // 6. Jika keluarga menerima request, validasi aturan 1 keluarga = 1 lansia dan 1 lansia = 1 keluarga
            if (status == "accepted")
            {
                bool elderlyAlreadyAcceptedByOtherFamily = await _context.Connections.AnyAsync(c =>
                    c.Id != connection.Id &&
                    c.ElderlyId == connection.ElderlyId &&
                    c.Status == "accepted"
                );

                if (elderlyAlreadyAcceptedByOtherFamily)
                {
                    return Conflict(new
                    {
                        message = "Lansia ini sudah terhubung dengan keluarga lain"
                    });
                }

                bool familyAlreadyAcceptedOtherElderly = await _context.Connections.AnyAsync(c =>
                    c.Id != connection.Id &&
                    c.FamilyId == familyId &&
                    c.Status == "accepted"
                );

                if (familyAlreadyAcceptedOtherElderly)
                {
                    return Conflict(new
                    {
                        message = "Keluarga ini sudah terhubung dengan lansia lain"
                    });
                }
            }

            // 7. Update status koneksi yang sedang direspon
            connection.Status = status;
            connection.UpdatedAt = DateTime.UtcNow;

            // 8. Kalau request diterima, tolak otomatis request pending lain yang melibatkan lansia/keluarga ini
            if (status == "accepted")
            {
                var otherPendingConnections = await _context.Connections
                    .Where(c =>
                        c.Id != connection.Id &&
                        c.Status == "pending" &&
                        (c.ElderlyId == connection.ElderlyId || c.FamilyId == connection.FamilyId)
                    )
                    .ToListAsync();

                foreach (Connection otherConnection in otherPendingConnections)
                {
                    otherConnection.Status = "rejected";
                    otherConnection.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // 9. Response berhasil
            return Ok(new
            {
                message = status == "accepted"
                    ? "Permintaan berhasil diterima"
                    : "Permintaan berhasil ditolak",
                connection = new
                {
                    id = connection.Id,
                    elderly_id = connection.ElderlyId,
                    family_id = connection.FamilyId,
                    status = connection.Status,
                    updated_at = connection.UpdatedAt
                }
            });
        }

        /// <summary>
        /// Mengambil data lansia yang terhubung (role keluarga)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role keluarga untuk melihat data lansia yang sudah terhubung.
        /// </remarks>
        [HttpGet("elderlies")]
        public async Task<IActionResult> GetConnectedElderlies()
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

            // 2. Hanya keluarga yang boleh melihat daftar lansia
            if (role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Hanya akun keluarga yang dapat melihat daftar lansia terhubung"
                });
            }

            Guid familyId = Guid.Parse(userId);

            // 3. Ambil daftar lansia yang status koneksinya accepted
            var elderlies = await (
                from connection in _context.Set<Connection>()
                join elderly in _context.Users
                    on connection.ElderlyId equals elderly.Id
                where connection.FamilyId == familyId
                      && connection.Status == "accepted"
                orderby connection.UpdatedAt descending
                select new
                {
                    connection_id = connection.Id,
                    elderly_id = elderly.Id,
                    elderly_name = elderly.Name,
                    elderly_email = elderly.Email,
                    elderly_phone = elderly.Phone,
                    elderly_photo_url = elderly.IsPhotoDeleted ? null : elderly.PhotoUrl,
                    status = connection.Status,
                    connected_at = connection.UpdatedAt
                }
            ).Take(1).ToListAsync();

            // 4. Response
            return Ok(new
            {
                message = "Daftar lansia terhubung berhasil diambil",
                data = elderlies
            });
        }

        /// <summary>
        /// Mengambil data keluarga yang terhubung (role lansia)
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan oleh akun dengan role lansia untuk melihat data keluarga yang sudah terhubung.
        /// </remarks>
        [HttpGet("families")]
        public async Task<IActionResult> GetConnectedFamilies()
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

            // 2. Hanya lansia yang boleh melihat daftar keluarga terhubung
            if (role != "lansia")
            {
                return BadRequest(new
                {
                    message = "Hanya akun lansia yang dapat melihat daftar keluarga terhubung"
                });
            }

            Guid elderlyId = Guid.Parse(userId);

            // 3. Ambil daftar keluarga yang status koneksinya accepted
            var families = await (
                from connection in _context.Set<Connection>()
                join family in _context.Users
                    on connection.FamilyId equals family.Id
                where connection.ElderlyId == elderlyId
                      && connection.Status == "accepted"
                select new
                {
                    connection_id = connection.Id,
                    family_id = family.Id,
                    family_name = family.Name,
                    family_email = family.Email,
                    family_phone = family.Phone,
                    family_photo_url = family.IsPhotoDeleted ? null : family.PhotoUrl,
                    status = connection.Status,
                    connected_at = connection.UpdatedAt
                }
            ).ToListAsync();

            // 4. Response
            return Ok(new
            {
                message = "Daftar keluarga terhubung berhasil diambil",
                data = families
            });
        }
    }
}