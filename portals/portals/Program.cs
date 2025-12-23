using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using portals.Data;
using Microsoft.AspNetCore.Cors.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// Configure PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity with Roles
builder.Services.AddDefaultIdentity<IdentityUser>(options => 
        options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

//allow requests from other port
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost5500", policy =>
        policy.WithOrigins("http://localhost:5500") // HTML origin
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowLocalhost5500");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages(); // for Identity pages

app.Run();