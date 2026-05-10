using AuthService.Data;
using AuthService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AuthDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User loginUser)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == loginUser.Email))
                    return BadRequest("User already exists.");

                var user = new User
                {
                    Email = loginUser.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(loginUser.PasswordHash)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "User registered successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Error = "Database Error", 
                    Message = ex.Message, 
                    Inner = ex.InnerException?.Message 
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] User loginUser)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == loginUser.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginUser.PasswordHash, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyStr)) return StatusCode(500, "JWT Key not configured");
            var key = Encoding.ASCII.GetBytes(keyStr);

            var claims = new List<Claim>
            {
                new Claim("id", user.Id.ToString()),
                new Claim("userId", user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // Verificar si es Admin via Variable de Entorno
            var adminEmail = _config["ADMIN_EMAIL"];
            if (!string.IsNullOrEmpty(adminEmail) && user.Email.Equals(adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, "admin"));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return Ok(new { Token = tokenHandler.WriteToken(token) });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users.Select(u => new { u.Id, u.Email }).ToListAsync();
            return Ok(users);
        }
    }
}
