using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen data profil user
    /// </summary>
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly RasaDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public UsersController(
            RasaDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Mengambil data profil user yang sedang login
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan untuk mengambil data profil user berdasarkan token yang sedang digunakan.
        /// </remarks>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            Guid id = Guid.Parse(userId);

            User? user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            return Ok(new
            {
                message = "Profil berhasil diambil",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.IsPhotoDeleted ? null : user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Memperbarui data profil user yang sedang login
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan untuk memperbarui data profil user berdasarkan token yang sedang digunakan.
        /// </remarks>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Phone))
            {
                return BadRequest(new
                {
                    message = "Nama dan nomor HP wajib diisi"
                });
            }

            Guid id = Guid.Parse(userId);
            //string email = request.Email.Trim().ToLower();

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            //bool emailUsedByOtherUser = await _context.Users.AnyAsync(u =>
            //    u.Email == email &&
            //    u.Id != id
            //);

            //if (emailUsedByOtherUser)
            //{
            //    return Conflict(new
            //    {
            //        message = "Email sudah digunakan oleh akun lain"
            //    });
            //}

            user.Name = request.Name.Trim();
            //user.Email = email;
            user.Phone = request.Phone.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Profil berhasil diperbarui",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.IsPhotoDeleted ? null : user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Memperbarui foto profil user yang sedang login
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan untuk mengupload atau memperbarui foto profil user.
        /// </remarks>
        [HttpPut("profile/photo")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateProfilePhoto([FromForm] UpdateProfilePhotoRequest request)
        {
            IFormFile photo = request.Photo;

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            if (photo == null || photo.Length == 0)
            {
                return BadRequest(new
                {
                    message = "Foto wajib diupload"
                });
            }

            string[] allowedExtensions = { ".jpg", ".jpeg", ".png" };
            string fileExtension = Path.GetExtension(photo.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new
                {
                    message = "Format foto harus jpg, jpeg, atau png"
                });
            }

            long maxFileSize = 2 * 1024 * 1024;

            if (photo.Length > maxFileSize)
            {
                return BadRequest(new
                {
                    message = "Ukuran foto maksimal 2 MB"
                });
            }

            Guid id = Guid.Parse(userId);

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            string supabaseUrl = _configuration["SupabaseStorage:Url"] ?? "";
            string serviceRoleKey = _configuration["SupabaseStorage:ServiceRoleKey"] ?? "";
            string bucket = _configuration["SupabaseStorage:Bucket"] ?? "";

            if (string.IsNullOrWhiteSpace(supabaseUrl) ||
                string.IsNullOrWhiteSpace(serviceRoleKey) ||
                string.IsNullOrWhiteSpace(bucket))
            {
                return StatusCode(500, new
                {
                    message = "Konfigurasi Supabase Storage belum lengkap"
                });
            }

            string fileName = $"{user.Id}_{DateTime.UtcNow.Ticks}{fileExtension}";
            string storagePath = $"users/{user.Id}/{fileName}";

            byte[] fileBytes;

            using (var memoryStream = new MemoryStream())
            {
                await photo.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            var client = _httpClientFactory.CreateClient();

            string uploadUrl = $"{supabaseUrl}/storage/v1/object/{bucket}/{storagePath}";

            using var content = new ByteArrayContent(fileBytes);

            string contentType = string.IsNullOrWhiteSpace(photo.ContentType)
                ? "application/octet-stream"
                : photo.ContentType;

            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            uploadRequest.Headers.Add("apikey", serviceRoleKey);
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
            uploadRequest.Headers.Add("x-upsert", "true");
            uploadRequest.Content = content;

            HttpResponseMessage uploadResponse = await client.SendAsync(uploadRequest);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                string errorContent = await uploadResponse.Content.ReadAsStringAsync();

                return StatusCode((int)uploadResponse.StatusCode, new
                {
                    message = "Gagal upload foto ke Supabase Storage",
                    error = errorContent
                });
            }

            string photoUrl = $"{supabaseUrl}/storage/v1/object/public/{bucket}/{storagePath}";

            user.PhotoUrl = photoUrl;
            user.IsPhotoDeleted = false;
            user.PhotoDeletedAt = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Foto profil berhasil diperbarui",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.IsPhotoDeleted ? null : user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }

        /// <summary>
        /// Menghapus foto profil user secara soft delete
        /// </summary>
        /// <remarks>
        /// Endpoint ini digunakan untuk menghapus foto profil user yang sedang login secara soft delete.
        /// </remarks>
        [HttpDelete("profile/photo")]
        public async Task<IActionResult> DeleteProfilePhoto()
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            Guid id = Guid.Parse(userId);

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            if (string.IsNullOrWhiteSpace(user.PhotoUrl) || user.IsPhotoDeleted)
            {
                return BadRequest(new
                {
                    message = "User belum memiliki foto profil aktif"
                });
            }

            // Soft delete:
            // Jangan hapus file dari Supabase Storage.
            // Jangan kosongkan PhotoUrl agar URL lama tetap tersimpan di database.
            user.IsPhotoDeleted = true;
            user.PhotoDeletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Foto profil berhasil dihapus secara soft delete",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.IsPhotoDeleted ? null : user.PhotoUrl,
                    photo_deleted_at = user.PhotoDeletedAt,
                    created_at = user.CreatedAt
                }
            });
        }
    }
}