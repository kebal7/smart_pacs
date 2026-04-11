using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using Microsoft.IdentityModel.Tokens;
using portals.services;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddScoped<IDicomService, DicomService>();

// Configure PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity with Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    
    options.Lockout.AllowedForNewUsers = true; 
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5); // For wrong password attempts
    options.Lockout.MaxFailedAccessAttempts = 5; 
})
.AddRoles<IdentityRole>() // MUST HAVE THIS FOR ROLES TO WORK
.AddEntityFrameworkStores<ApplicationDbContext>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost5500", policy =>
        policy.WithOrigins("http://localhost:5500") 
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey)) throw new Exception("JWT Key is missing in configuration!");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

// --- SEEDING DATA (ADMIN & ROLES) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // 1. Ensure Roles Exist
    string[] roleNames = ["Admin", "RegistrationDesk", "Radiographer", "Radiologist", "Clinician"];
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // 2. Ensure Super User (Admin) exists
    // Values pulled from appsettings.json for security
    var adminEmail = builder.Configuration["AdminSettings:Email"] ?? "admin@pacs.com";
    var adminPassword = builder.Configuration["AdminSettings:Password"] ?? "Admin123!";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        var user = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var createAdmin = await userManager.CreateAsync(user, adminPassword);
        if (createAdmin.Succeeded)
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }
    }
}

// --- Middleware ---
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowLocalhost5500");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();