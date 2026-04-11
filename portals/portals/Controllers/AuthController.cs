using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace portals.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _config;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
        }

        // ---------------- AUTHENTICATION ----------------

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (model.Role == "Admin")
                return BadRequest("Cannot register as Admin via API.");

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("User already exists.");

            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, model.Role);
            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized("Invalid credentials.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            
            if (result.IsLockedOut)
                return StatusCode(403, "This account has been disabled. Please contact the administrator.");
            
            if (!result.Succeeded) return Unauthorized("Invalid credentials.");
            
            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles);

            return Ok(new { token, role = roles.FirstOrDefault() });
        }

        // ---------------- PROFILE MANAGEMENT ----------------

        [Authorize]
        [HttpPut("update-password")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDto model)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { message = "Password updated successfully" });
        }
        

        private string GenerateJwtToken(IdentityUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(ClaimTypes.Name, user.UserName) // Added for User.Identity.Name
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:ExpiresMinutes"] ?? "60")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // --- DTOs ---
    public class RegisterDto { public string Email { get; set; } public string Password { get; set; } public string Role { get; set; } }
    public class LoginDto { public string Email { get; set; } public string Password { get; set; } }
    public class UpdatePasswordDto { public string CurrentPassword { get; set; } public string NewPassword { get; set; } }
}