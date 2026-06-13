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
using RASA.DTOs;

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
        //[HttpPost("register")]
        //public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        //{
        //    // 1. Validasi data kosong
        //    if (string.IsNullOrWhiteSpace(request.Name) ||
        //        string.IsNullOrWhiteSpace(request.Email) ||
        //        string.IsNullOrWhiteSpace(request.Phone) ||
        //        string.IsNullOrWhiteSpace(request.Password) ||
        //        string.IsNullOrWhiteSpace(request.Role))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Semua data wajib diisi"
        //        });
        //    }

        //    // 2. Rapikan input
        //    string email = request.Email.Trim().ToLower();
        //    string role = request.Role.Trim().ToLower();

        //    // 3. Validasi role
        //    if (role != "lansia" && role != "keluarga")
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Role harus lansia atau keluarga"
        //        });
        //    }

        //    // 4. Validasi password
        //    if (request.Password.Length < 6)
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Password minimal 6 karakter"
        //        });
        //    }

        //    // 5. Cek email sudah dipakai atau belum
        //    bool emailExists = await _context.Users.AnyAsync(u => u.Email == email);

        //    if (emailExists)
        //    {
        //        return Conflict(new
        //        {
        //            message = "Email sudah digunakan"
        //        });
        //    }

        //    // 6. Hash password
        //    string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        //    // 7. Simpan user baru
        //    User newUser = new User
        //    {
        //        Id = Guid.NewGuid(),
        //        Name = request.Name.Trim(),
        //        Email = email,
        //        Phone = request.Phone.Trim(),
        //        PasswordHash = passwordHash,
        //        Role = role,
        //        PhotoUrl = null,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _context.Users.Add(newUser);
        //    await _context.SaveChangesAsync();

        //    // 8. Response berhasil, jangan kirim password_hash
        //    return Created("", new
        //    {
        //        message = "Registrasi berhasil",
        //        user = new
        //        {
        //            id = newUser.Id,
        //            name = newUser.Name,
        //            email = newUser.Email,
        //            phone = newUser.Phone,
        //            role = newUser.Role,
        //            photo_url = newUser.PhotoUrl,
        //            created_at = newUser.CreatedAt
        //        }
        //    });
        //}

        /// <summary>
        /// Login user dan mendapatkan token JWT
        /// </summary>
        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginRequest request)
        //{
        //    // 1. Validasi data kosong
        //    if (string.IsNullOrWhiteSpace(request.Email) ||
        //        string.IsNullOrWhiteSpace(request.Password))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Email dan password wajib diisi"
        //        });
        //    }

        //    // 2. Rapikan email
        //    string email = request.Email.Trim().ToLower();

        //    // 3. Cari user berdasarkan email
        //    User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        //    if (user == null)
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "Email atau password salah"
        //        });
        //    }

        //    // 4. Cek password
        //    //bool passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        //    //if (!passwordValid)
        //    //{
        //    //    return Unauthorized(new
        //    //    {
        //    //        message = "Email atau password salah"
        //    //    });
        //    //}

        //    if (string.IsNullOrWhiteSpace(user.PasswordHash))
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "Akun ini menggunakan Firebase/Google. Silakan login menggunakan Firebase."
        //        });
        //    }

        //    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "Password salah"
        //        });
        //    }

        //    // 5. Buat JWT token
        //    string token = GenerateJwtToken(user);

        //    // 6. Response berhasil
        //    return Ok(new
        //    {
        //        message = "Login berhasil",
        //        token = token,
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

        [HttpPost("firebase/register")]
        public async Task<IActionResult> FirebaseRegister([FromBody] FirebaseRegisterRequest request)
        {
            // 1. Validasi ID token Firebase
            if (string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new
                {
                    message = "ID token Firebase wajib diisi"
                });
            }

            // 2. Validasi nama
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new
                {
                    message = "Nama wajib diisi"
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

            // 4. Validasi role
            string role = request.Role.Trim().ToLower();

            if (role != "lansia" && role != "keluarga")
            {
                return BadRequest(new
                {
                    message = "Role harus lansia atau keluarga"
                });
            }

            FirebaseToken decodedToken;

            try
            {
                // 5. Verifikasi token dari Firebase
                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
            }
            catch
            {
                return Unauthorized(new
                {
                    message = "ID token Firebase tidak valid"
                });
            }

            // 6. Ambil UID Firebase
            string firebaseUid = decodedToken.Uid;

            // 7. Ambil email dari token Firebase
            string? email = decodedToken.Claims.ContainsKey("email")
                ? decodedToken.Claims["email"]?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(email))
            {
                return BadRequest(new
                {
                    message = "Email Firebase tidak ditemukan"
                });
            }

            email = email.Trim().ToLower();

            // 8. Cek email sudah diverifikasi atau belum
            bool emailVerified = false;

            if (decodedToken.Claims.ContainsKey("email_verified"))
            {
                bool.TryParse(decodedToken.Claims["email_verified"]?.ToString(), out emailVerified);
            }

            if (!emailVerified)
            {
                return BadRequest(new
                {
                    message = "Email belum diverifikasi. Silakan verifikasi email terlebih dahulu"
                });
            }

            // 9. Ambil foto dari Firebase kalau ada
            string? picture = decodedToken.Claims.ContainsKey("picture")
                ? decodedToken.Claims["picture"]?.ToString()
                : null;

            // 10. Cek apakah akun sudah terdaftar
            bool userExists = await _context.Users.AnyAsync(u =>
                u.Email == email ||
                u.FirebaseUid == firebaseUid
            );

            if (userExists)
            {
                return Conflict(new
                {
                    message = "Akun sudah terdaftar, silakan login"
                });
            }

            // 11. Simpan user baru ke Supabase
            User newUser = new User
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Email = email,
                Phone = request.Phone.Trim(),
                PasswordHash = null,
                Role = role,
                PhotoUrl = picture,
                AuthProvider = "firebase",
                FirebaseUid = firebaseUid,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 12. Buat JWT RASA
            string token = GenerateJwtToken(newUser);

            // 13. Response berhasil
            return Ok(new
            {
                message = "Register Firebase berhasil",
                token,
                user = new
                {
                    id = newUser.Id,
                    name = newUser.Name,
                    email = newUser.Email,
                    phone = newUser.Phone,
                    role = newUser.Role,
                    photo_url = newUser.PhotoUrl,
                    auth_provider = newUser.AuthProvider,
                    created_at = newUser.CreatedAt
                }
            });
        }


        [HttpPost("firebase/login")]
        public async Task<IActionResult> FirebaseLogin([FromBody] FirebaseLoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.IdToken))
                {
                    return BadRequest(new
                    {
                        message = "ID token Firebase wajib diisi"
                    });
                }

                FirebaseToken decodedToken;

                try
                {
                    decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
                }
                catch (Exception ex)
                {
                    return Unauthorized(new
                    {
                        message = "ID token Firebase tidak valid",
                        detail = ex.Message
                    });
                }

                string firebaseUid = decodedToken.Uid;

                string? email = decodedToken.Claims.ContainsKey("email")
                    ? decodedToken.Claims["email"]?.ToString()
                    : null;

                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new
                    {
                        message = "Email Firebase tidak ditemukan"
                    });
                }

                email = email.Trim().ToLower();

                User? user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.FirebaseUid == firebaseUid || u.Email == email
                );

                if (user == null)
                {
                    return NotFound(new
                    {
                        message = "Akun belum terdaftar, silakan register terlebih dahulu"
                    });
                }

                if (string.IsNullOrWhiteSpace(user.FirebaseUid))
                {
                    user.FirebaseUid = firebaseUid;
                    user.AuthProvider = "firebase";
                    await _context.SaveChangesAsync();
                }

                string token = GenerateJwtToken(user);

                return Ok(new
                {
                    message = "Login Firebase berhasil",
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Terjadi error pada server saat login Firebase",
                    detail = ex.Message,
                    inner = ex.InnerException != null ? ex.InnerException.Message : null
                });
            }
        }


        //login lama

        //        [HttpPost("firebase/login")]
        //        public async Task<IActionResult> FirebaseLogin([FromBody] FirebaseLoginRequest request)
        //        {
        //            // 1. Validasi ID token Firebase
        //            if (string.IsNullOrWhiteSpace(request.IdToken))
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "ID token Firebase wajib diisi"
        //                });
        //            }

        //            FirebaseToken decodedToken;

        //            try
        //            {
        //                // 2. Verifikasi token dari Firebase
        //                decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
        //            }
        //            catch
        //            {
        //                return Unauthorized(new
        //                {
        //                    message = "ID token Firebase tidak valid"
        //                });
        //            }

        //            // 3. Ambil UID Firebase
        //            string firebaseUid = decodedToken.Uid;

        //            // 4. Ambil email dari token Firebase
        //            string? email = decodedToken.Claims.ContainsKey("email")
        //                ? decodedToken.Claims["email"]?.ToString()
        //                : null;

        //            if (string.IsNullOrWhiteSpace(email))
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "Email Firebase tidak ditemukan"
        //                });
        //            }

        //            email = email.Trim().ToLower();

        //            // 5. Cek apakah email sudah diverifikasi
        //            bool emailVerified = false;

        //            if (decodedToken.Claims.ContainsKey("email_verified"))
        //            {
        //                bool.TryParse(decodedToken.Claims["email_verified"]?.ToString(), out emailVerified);
        //            }

        //            if (!emailVerified)
        //            {
        //                return BadRequest(new
        //                {
        //                    message = "Email belum diverifikasi. Silakan verifikasi email terlebih dahulu"
        //                });
        //            }

        //            // 6. Cari user di Supabase berdasarkan firebase_uid
        //            //User? user = await _context.Users.FirstOrDefaultAsync(u =>
        //            //    u.FirebaseUid == firebaseUid
        //            //);

        //            //if (user == null)
        //            //{
        //            //    return NotFound(new
        //            //    {
        //            //        message = "Akun belum terdaftar, silakan register terlebih dahulu"
        //            //    });
        //            //}

        //            User? user = await _context.Users.FirstOrDefaultAsync(u =>
        //    u.FirebaseUid == firebaseUid || u.Email == email
        //);

        //            if (user == null)
        //            {
        //                return NotFound(new
        //                {
        //                    message = "Akun belum terdaftar, silakan register terlebih dahulu"
        //                });
        //            }

        //            // Kalau user lama ketemu dari email tapi firebase_uid masih kosong, isi otomatis
        //            if (string.IsNullOrWhiteSpace(user.FirebaseUid))
        //            {
        //                user.FirebaseUid = firebaseUid;
        //                user.AuthProvider = "firebase";
        //                await _context.SaveChangesAsync();
        //            }

        //            // 7. Buat JWT RASA
        //            string token = GenerateJwtToken(user);

        //            // 8. Response berhasil
        //            return Ok(new
        //            {
        //                message = "Login Firebase berhasil",
        //                token,
        //                user = new
        //                {
        //                    id = user.Id,
        //                    name = user.Name,
        //                    email = user.Email,
        //                    phone = user.Phone,
        //                    role = user.Role,
        //                    photo_url = user.PhotoUrl,
        //                    auth_provider = user.AuthProvider,
        //                    created_at = user.CreatedAt
        //                }
        //            });
        //        }













        //GOOGLE

        //[HttpPost("google/register")]
        //public async Task<IActionResult> GoogleRegister([FromBody] GoogleRegisterRequest request)
        //{
        //    // 1. Validasi id token
        //    if (string.IsNullOrWhiteSpace(request.IdToken))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "ID token Google wajib diisi"
        //        });
        //    }

        //    // 2. Validasi nama
        //    if (string.IsNullOrWhiteSpace(request.Name))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Nama wajib diisi"
        //        });
        //    }

        //    // 3. Validasi nomor HP
        //    if (string.IsNullOrWhiteSpace(request.Phone))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Nomor HP wajib diisi"
        //        });
        //    }

        //    // 4. Validasi role
        //    string role = request.Role.Trim().ToLower();

        //    if (role != "lansia" && role != "keluarga")
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Role harus lansia atau keluarga"
        //        });
        //    }

        //    FirebaseToken decodedToken;

        //    try
        //    {
        //        decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
        //    }
        //    catch
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "ID token Google tidak valid"
        //        });
        //    }

        //    // 5. Ambil data dari Firebase
        //    string firebaseUid = decodedToken.Uid;

        //    string? email = decodedToken.Claims.ContainsKey("email")
        //        ? decodedToken.Claims["email"]?.ToString()
        //        : null;

        //    string? picture = decodedToken.Claims.ContainsKey("picture")
        //        ? decodedToken.Claims["picture"]?.ToString()
        //        : null;

        //    if (string.IsNullOrWhiteSpace(email))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Email Google tidak ditemukan"
        //        });
        //    }

        //    email = email.Trim().ToLower();

        //    // 6. Cek apakah akun sudah pernah terdaftar
        //    bool userExists = await _context.Users.AnyAsync(u =>
        //        u.Email == email ||
        //        u.FirebaseUid == firebaseUid
        //    );

        //    if (userExists)
        //    {
        //        return Conflict(new
        //        {
        //            message = "Akun Google sudah terdaftar, silakan login"
        //        });
        //    }

        //    // 7. Simpan user baru ke Supabase
        //    User newUser = new User
        //    {
        //        Id = Guid.NewGuid(),
        //        Name = request.Name.Trim(),
        //        Email = email,
        //        Phone = request.Phone.Trim(),
        //        PasswordHash = null,
        //        Role = role,
        //        PhotoUrl = picture,
        //        AuthProvider = "google",
        //        FirebaseUid = firebaseUid,
        //        CreatedAt = DateTime.UtcNow
        //    };

        //    _context.Users.Add(newUser);
        //    await _context.SaveChangesAsync();

        //    // 8. Buat JWT RASA
        //    string token = GenerateJwtToken(newUser);

        //    // 9. Response
        //    return Ok(new
        //    {
        //        message = "Register Google berhasil",
        //        token,
        //        user = new
        //        {
        //            id = newUser.Id,
        //            name = newUser.Name,
        //            email = newUser.Email,
        //            phone = newUser.Phone,
        //            role = newUser.Role,
        //            photo_url = newUser.PhotoUrl,
        //            auth_provider = newUser.AuthProvider,
        //            created_at = newUser.CreatedAt
        //        }
        //    });
        //}

        //[HttpPost("google/login")]
        //public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        //{
        //    // 1. Validasi id token
        //    if (string.IsNullOrWhiteSpace(request.IdToken))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "ID token Google wajib diisi"
        //        });
        //    }

        //    FirebaseToken decodedToken;

        //    try
        //    {
        //        decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(request.IdToken);
        //    }
        //    catch
        //    {
        //        return Unauthorized(new
        //        {
        //            message = "ID token Google tidak valid"
        //        });
        //    }

        //    // 2. Ambil data Firebase
        //    string firebaseUid = decodedToken.Uid;

        //    string? email = decodedToken.Claims.ContainsKey("email")
        //        ? decodedToken.Claims["email"]?.ToString()
        //        : null;

        //    if (string.IsNullOrWhiteSpace(email))
        //    {
        //        return BadRequest(new
        //        {
        //            message = "Email Google tidak ditemukan"
        //        });
        //    }

        //    email = email.Trim().ToLower();

        //    // 3. Cari user yang sudah terdaftar Google
        //    User? user = await _context.Users.FirstOrDefaultAsync(u =>
        //        u.FirebaseUid == firebaseUid &&
        //        u.AuthProvider == "google"
        //    );

        //    if (user == null)
        //    {
        //        return NotFound(new
        //        {
        //            message = "Akun belum terdaftar, silakan register terlebih dahulu"
        //        });
        //    }

        //    // 4. Buat JWT RASA
        //    string token = GenerateJwtToken(user);

        //    // 5. Response
        //    return Ok(new
        //    {
        //        message = "Login Google berhasil",
        //        token,
        //        user = new
        //        {
        //            id = user.Id,
        //            name = user.Name,
        //            email = user.Email,
        //            phone = user.Phone,
        //            role = user.Role,
        //            photo_url = user.PhotoUrl,
        //            auth_provider = user.AuthProvider,
        //            created_at = user.CreatedAt
        //        }
        //    });
        //}















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