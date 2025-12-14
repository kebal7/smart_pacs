// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using AuthService.Models;

namespace AuthService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
	    modelBuilder.Entity<User>(entity =>
	    {
		entity.ToTable("users"); // lowercase table
		entity.Property(e => e.Id).HasColumnName("id");
		entity.Property(e => e.Email).HasColumnName("email");
		entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
		entity.Property(e => e.Role).HasColumnName("role");
	    });	
    }
}

