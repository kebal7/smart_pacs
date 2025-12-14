using AuthService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// PostgreSQL DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "super_secret_key_123!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SmartPacsAuth";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero // optional, removes default 5 min tolerance
    };
});

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

//Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();


// Map controllers
app.MapControllers();

app.Run();

