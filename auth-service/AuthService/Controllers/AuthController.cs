using Microsoft.AspNetCore.Mvc;
using AuthService.Data;
using AuthService.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuthController(AppDbContext db) => _db = db;

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
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Radiographer";
}

