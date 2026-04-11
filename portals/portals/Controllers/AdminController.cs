using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace portals.Controllers
{
    [Authorize(Roles = "Admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")] // Route will be api/admin
    public class AdminController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isLocked = await _userManager.IsLockedOutAsync(user); 

                userList.Add(new { 
                    user.Id, 
                    user.Email, 
                    Role = roles.FirstOrDefault(),
                    IsDisabled = isLocked 
                });
            }
            return Ok(userList);
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.UserName == User.Identity.Name)
                return BadRequest("You cannot delete your own admin account.");

            await _userManager.DeleteAsync(user);
            return Ok(new { message = "User deleted successfully" });
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPost("users/{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.UserName == User.Identity.Name)
                return BadRequest("You cannot disable your own admin account.");

            // Check if user is currently locked out
            var isLockedOut = await _userManager.IsLockedOutAsync(user);

            if (isLockedOut)
            {
                // Unlocking: Set lockout end to null
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                // Locking: Set lockout end to 100 years from now
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }

            return Ok(new { message = isLockedOut ? "Account Enabled" : "Account Disabled" });
        }
    }
}