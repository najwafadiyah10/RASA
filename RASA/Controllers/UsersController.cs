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
                    photo_url = user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }

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
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Phone))
            {
                return BadRequest(new
                {
                    message = "Nama, email, dan nomor HP wajib diisi"
                });
            }

            Guid id = Guid.Parse(userId);
            string email = request.Email.Trim().ToLower();

            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            bool emailUsedByOtherUser = await _context.Users.AnyAsync(u =>
                u.Email == email &&
                u.Id != id
            );

            if (emailUsedByOtherUser)
            {
                return Conflict(new
                {
                    message = "Email sudah digunakan oleh akun lain"
                });
            }

            user.Name = request.Name.Trim();
            user.Email = email;
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
                    photo_url = user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }

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
                    photo_url = user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }
        [HttpDelete("profile/photo")]
        public async Task<IActionResult> DeleteProfilePhoto()
        {
            // 1. Ambil user id dari token
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    message = "Token tidak valid"
                });
            }

            Guid id = Guid.Parse(userId);

            // 2. Cari user
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            // 3. Cek apakah user punya foto
            if (string.IsNullOrWhiteSpace(user.PhotoUrl))
            {
                return BadRequest(new
                {
                    message = "User belum memiliki foto profil"
                });
            }

            // 4. Ambil config Supabase Storage
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

            // 5. Ambil path file dari photo_url
            // Contoh photo_url:
            // https://uddubuyrwxltkxxweeot.supabase.co/storage/v1/object/public/profile-photos/users/id/file.jpg

            string publicUrlPrefix = $"{supabaseUrl}/storage/v1/object/public/{bucket}/";

            if (!user.PhotoUrl.StartsWith(publicUrlPrefix))
            {
                // Jika photo_url masih versi lokal lama, cukup kosongkan dari database
                user.PhotoUrl = null;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Foto profil lama berhasil dihapus dari database",
                    user = new
                    {
                        id = user.Id,
                        name = user.Name,
                        email = user.Email,
                        phone = user.Phone,
                        role = user.Role,
                        photo_url = user.PhotoUrl,
                        created_at = user.CreatedAt
                    }
                });
            }

            string storagePath = user.PhotoUrl.Replace(publicUrlPrefix, "");

            // 6. Hapus file dari Supabase Storage
            var client = _httpClientFactory.CreateClient();

            string deleteUrl = $"{supabaseUrl}/storage/v1/object/{bucket}/{storagePath}";

            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
            deleteRequest.Headers.Add("apikey", serviceRoleKey);
            deleteRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceRoleKey);

            HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

            if (!deleteResponse.IsSuccessStatusCode &&
                deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                string errorContent = await deleteResponse.Content.ReadAsStringAsync();

                return StatusCode((int)deleteResponse.StatusCode, new
                {
                    message = "Gagal menghapus foto dari Supabase Storage",
                    error = errorContent
                });
            }

            // 7. Kosongkan photo_url di database
            user.PhotoUrl = null;

            await _context.SaveChangesAsync();

            // 8. Response
            return Ok(new
            {
                message = "Foto profil berhasil dihapus",
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.PhotoUrl,
                    created_at = user.CreatedAt
                }
            });
        }
    }
}