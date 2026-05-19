using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RasaApi.Data;
using RasaApi.Models;
using System.Security.Claims;

namespace RasaApi.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly RasaDbContext _context;

        public UsersController(RasaDbContext context)
        {
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
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

            // 2. Cari user di database
            User? user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User tidak ditemukan"
                });
            }

            // 3. Response profil
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
    }
}