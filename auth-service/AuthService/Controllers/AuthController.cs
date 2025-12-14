using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AuthService.Data;
using AuthService.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (_db.Users.Any(u => u.Email == request.Email))
            return BadRequest("Email already exists");

        // Simple password hash (in real app use random salt!)
        byte[] salt = new byte[16];
        var hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: request.Password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 32));

        var user = new User
        {
            Email = request.Email,
            PasswordHash = hashed,
            Role = request.Role
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        return Ok(new { message = "User registered successfully" });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var user = _db.Users.FirstOrDefault(u => u.Email == request.Email);
        if (user == null)
            return Unauthorized("Invalid email or password");

        byte[] salt = new byte[16];
        var hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: request.Password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 32));

        if (hashed != user.PasswordHash)
            return Unauthorized("Invalid email or password");

        // ----------------------------
        // Generate JWT token
        // ----------------------------
        var key = _config["Jwt:Key"] ?? "super_secret_key_123!";
        var issuer = _config["Jwt:Issuer"] ?? "SmartPacsAuth";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim("id", user.Id.ToString()),
            new Claim("role", user.Role)
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            Token = tokenString
        });
    }


	[HttpGet("hello")]
	[Authorize] // requires a valid JWT token
	public IActionResult Hello()
	{
	    // Extract email and role from JWT claims
	    var email = User.FindFirstValue(ClaimTypes.NameIdentifier); // sub claim
	    var role = User.FindFirstValue(ClaimTypes.Role);

	    return Ok(new { message = $"Hello {role} user: {email}" });
	}
}
