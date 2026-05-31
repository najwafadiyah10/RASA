//using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RasaApi.Data;
using RasaApi.DTOs;
using RasaApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using FirebaseAdmin.Auth;
using Rasa.DTOs;

namespace RasaApi.Controllers
{
    /// <summary>
    /// Manajemen autentikasi user
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly RasaDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(RasaDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        /// <summary>
        /// Mendaftarkan akun baru untuk lansia atau keluarga
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 1. Validasi data kosong
            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Phone) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Role))
            {
                return BadRequest(new
                {
                    message = "Semua data wajib diisi"
                });
            }

            // 2. Rapikan input
            string email = request.Email.Trim().ToLower();
            string role = request.Role.Trim().ToLower();

            // 3. Validasi role
            if (role != "lansia" && role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Role harus lansia atau keluarga"
                });
            }

            // 4. Validasi password
            if (request.Password.Length < 6)
            {
                return BadRequest(new
                {
                    message = "Password minimal 6 karakter"
                });
            }

            // 5. Cek email sudah dipakai atau belum
            bool emailExists = await _context.Users.AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                return Conflict(new
                {
                    message = "Email sudah digunakan"
                });
            }

            // 6. Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 7. Simpan user baru
            User newUser = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Email = email,
                Phone = request.Phone.Trim(),
                PasswordHash = passwordHash,
                Role = role,
                PhotoUrl = null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 8. Response berhasil, jangan kirim password_hash
            return Created("", new
            {
                message = "Registrasi berhasil",
                user = new
                {
                    id = newUser.Id,
                    name = newUser.Name,
                    email = newUser.Email,
                    phone = newUser.Phone,
                    role = newUser.Role,
                    photo_url = newUser.PhotoUrl,
                    created_at = newUser.CreatedAt
                }
            });
        }

        /// <summary>
        /// Login user dan mendapatkan token JWT
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Validasi data kosong
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Email dan password wajib diisi"
                });
            }

            // 2. Rapikan email
            string email = request.Email.Trim().ToLower();

            // 3. Cari user berdasarkan email
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Email atau password salah"
                });
            }

            // 4. Cek password
            bool passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Email atau password salah"
                });
            }

            // 5. Buat JWT token
            string token = GenerateJwtToken(user);

            // 6. Response berhasil
            return Ok(new
            {
                message = "Login berhasil",
                token = token,
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

            private string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];
            var expireMinutes = Convert.ToInt32(_configuration["Jwt:ExpireMinutes"]);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Name),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role)
    };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest request)
        {
            // 1. Validasi id token dari Firebase
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new
                {
                    message = "ID token Google wajib diisi"
                });
            }

            // 2. Validasi role
            string role = request.Role.Trim().ToLower();

            if (role != "lansia" && role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Role harus lansia atau keluarga"
                });
            }

            // 3. Validasi nomor HP
            if (string.IsNullOrWhiteSpace(request.Phone))
            {
                return BadRequest(new
                {
                    message = "Nomor HP wajib diisi"
                });
            }

            FirebaseToken decodedToken;

            try
            {
                // 4. Verifikasi token Firebase
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
            }
            catch
            {
                return Unauthorized(new
                {
                    message = "ID token Google tidak valid"
                });
            }

            // 5. Ambil data dari token Firebase
            string firebaseUid = decodedToken.Uid;

            string? email = decodedToken.Claims.ContainsKey("email")
                ? decodedToken.Claims["email"]?.ToString()
                : null;

            string? name = decodedToken.Claims.ContainsKey("name")
                ? decodedToken.Claims["name"]?.ToString()
                : null;

            string? picture = decodedToken.Claims.ContainsKey("picture")
                ? decodedToken.Claims["picture"]?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    message = "Email Google tidak ditemukan"
                });
            }

            email = email.Trim().ToLower();

            // 6. Cek apakah user sudah ada
            User? user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.FirebaseUid == firebaseUid ||
                    u.Email == email
                );

            string authStatus;

            if (user == null)
            {
                // 7. Kalau user belum ada, register otomatis
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Name = string.IsNullOrWhiteSpace(name) ? email : name,
                    Email = email,
                    Phone = request.Phone.Trim(),
                    PasswordHash = null,
                    Role = role,
                    PhotoUrl = picture,
                    AuthProvider = "google",
                    FirebaseUid = firebaseUid,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                authStatus = "registered";
            }
            else
            {
                // 8. Kalau user sudah ada, login
                user.FirebaseUid ??= firebaseUid;
                user.AuthProvider = "google";

                if (!string.IsNullOrWhiteSpace(picture) && string.IsNullOrWhiteSpace(user.PhotoUrl))
                {
                    user.PhotoUrl = picture;
                }

                authStatus = "logged_in";
            }

            await _context.SaveChangesAsync();

            // 9. Buat token JWT RASA
            string token = GenerateJwtToken(user);

            // 10. Response
            return Ok(new
            {
                message = authStatus == "registered"
                    ? "Register Google berhasil"
                    : "Login Google berhasil",

                status = authStatus,

                token,

                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    phone = user.Phone,
                    role = user.Role,
                    photo_url = user.PhotoUrl,
                    auth_provider = user.AuthProvider,
                    created_at = user.CreatedAt
                }
            });
        }

        //[Authorize]
        //[HttpGet("me")]
        //public async Task<IActionResult> Me()
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "Token tidak valid"
        //        });
        //    }

        //    User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        //    if (user == null)
        //    {
        //        return NotFound(new
        //        {
        //            message = "User tidak ditemukan"
        //        });
        //    }

        //    return Ok(new
        //    {
        //        message = "Data user berhasil diambil",
        //        user = new
        //        {
        //            id = user.Id,
        //            name = user.Name,
        //            email = user.Email,
        //            phone = user.Phone,
        //            role = user.Role,
        //            photo_url = user.PhotoUrl,
        //            created_at = user.CreatedAt
        //        }
        //    });
        //}


    }


}