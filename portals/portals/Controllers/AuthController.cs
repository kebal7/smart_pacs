using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using portals.Data;
using portals.Models;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;


namespace portals.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _config;
        private readonly SignInManager<IdentityUser> _signInManager;

        private readonly ApplicationDbContext _context;
        
        public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IConfiguration config, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (string.Equals(model.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Cannot register as Admin via API.");

            var allowedRoles = new[] { "Radiographer","Radiologist", "Clinician", "RegistrationDesk" };
            if (string.IsNullOrEmpty(model.Role) || !allowedRoles.Contains(model.Role))
                return BadRequest("Invalid role specified.");
            
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("User already exists.");

            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);
            
            await _userManager.AddToRoleAsync(user, model.Role);
            
            var profile = new StaffProfile
            {
                UserId = user.Id, // Link to the ID we just created
                FullName = model.FullName,
                ContactNo = model.ContactNo,
                Address = model.Address,
                ProfessionalEmail = model.ProfessionalEmail, // Default to login email, can be different
                LicenseNumber = model.LicenseNumber,
                DepartmentOrModality = model.DepartmentOrModality,
                CurrentPosition = model.CurrentPosition,
                StaffType = model.Role, // Use the role chosen during registration
                CareerStartDate = model.CareerStartDate,
                HospitalJoinDate = model.HospitalJoinDate,
                CreatedAt = DateTime.UtcNow
            };

            try 
            {
                _context.StaffProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // If profile fails, we should technically delete the Identity user 
                // to prevent "Ghost Users" without profiles.
                await _userManager.DeleteAsync(user);
                return StatusCode(500, "Profile creation failed. Registration rolled back.");
            }
            
            return CreatedAtAction(nameof(Register), new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return Unauthorized("Invalid credentials.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, true);
            
            if (result.IsLockedOut)
                return StatusCode(403, "This account has been disabled. Please contact the administrator.");
            
            if (!result.Succeeded) return Unauthorized("Invalid credentials.");
            
            var roles = await _userManager.GetRolesAsync(user);
            var token = GenerateJwtToken(user, roles);

            Console.WriteLine(roles);
            //Returning a strongly-typed DTO
            return Ok(new LoginResponseDto 
            { 
                Token = token, 
                Role = roles.FirstOrDefault() ?? "No Role" 
            });
        }

        [Authorize]
        [HttpPut("update-password")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordDto model)
        {
            var username = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized();
            
            var user = await _userManager.FindByNameAsync(username);
            
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { message = "Password updated successfully" });
        }

        private string GenerateJwtToken(IdentityUser user, IList<string> roles)
        {
            var email = user.Email ?? throw new InvalidOperationException("User email missing");
            var username = user.UserName ?? throw new InvalidOperationException("Username missing");
            

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(ClaimTypes.Name, username)
            };
            
            roles ??= new List<string>(); 

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var secret = _config["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentNullException("Jwt:Key is missing from configuration");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //var expires = int.TryParse(_config["Jwt:ExpiresMinutes"], out var min) ? min : 60;
            
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.TryParse(_config["Jwt:ExpiresMinutes"], out var min) ? min : 60),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // --- DTOs ---
    public class RegisterDto
    {
        // --- ACCOUNT INFO ---
        [Required]
        [EmailAddress]
        public string Email { get; set; } 
    
        [Required]
        [MinLength(6)]
        public string Password { get; set; } 
    
        [Required]
        public string Role { get; set; } // "Radiologist", "Radiographer", "Clinician"

        //staff profile
        // --- PERSONAL INFO ---
        [Required]
        public string FullName { get; set; }
        public string ContactNo { get; set; }
        public string Address { get; set; }
        
        [EmailAddress]
        public string ProfessionalEmail { get; set; }

        // --- PROFESSIONAL INFO ---
        [Required]
        public string LicenseNumber { get; set; } // NMC / NHPC Number
        public string DepartmentOrModality { get; set; } // e.g. "MRI Dept" or "Orthopedics"
        public string CurrentPosition { get; set; } // e.g. "Consultant"

        // --- EXPERIENCE INFO ---
        [Required]
        public DateTime CareerStartDate { get; set; }
        [Required]
        public DateTime HospitalJoinDate { get; set; }
    }
    
    public class LoginDto { public string Email { get; set; } public string Password { get; set; } }
    public class UpdatePasswordDto { public string CurrentPassword { get; set; } public string NewPassword { get; set; } }
    
    public class LoginResponseDto { public string Token { get; set; } public string Role { get; set; } }
}