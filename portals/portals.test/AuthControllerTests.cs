using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using portals.Controllers;
using portals.Data;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;


namespace Portals.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<IdentityUser>> _mockUserManager;
        private readonly Mock<SignInManager<IdentityUser>> _mockSignInManager;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly AuthController _controller;
        private readonly ApplicationDbContext _context;

        private const string TestSecretKey = "a_very_long_secret_key_at_least_32_chars_long";
        private const string TestIssuer = "test_pacs_issuer";
        private const string TestAudience = "test_pacs_audience";

        public AuthControllerTests()
        {
            var store = new Mock<IUserStore<IdentityUser>>();
            _mockUserManager = new Mock<UserManager<IdentityUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
            _mockSignInManager = new Mock<SignInManager<IdentityUser>>(
                _mockUserManager.Object, contextAccessor.Object, claimsFactory.Object,
                null!, null!, null!, null!);

            _mockConfig = new Mock<IConfiguration>();
            _mockConfig.Setup(c => c["Jwt:Key"]).Returns(TestSecretKey);
            _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns(TestIssuer);
            _mockConfig.Setup(c => c["Jwt:Audience"]).Returns(TestAudience);
            _mockConfig.Setup(c => c["Jwt:ExpiresMinutes"]).Returns("60");

            _controller = new AuthController(
                _mockUserManager.Object, _mockSignInManager.Object, _mockConfig.Object, _context);
        }

        // =========================================================================
        // Helpers
        // =========================================================================

        /// <summary>
        /// Sets an authenticated user on the controller context with the given username.
        /// </summary>
        private void SetControllerUser(string username)
        {
            var claims = new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }));
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claims }
            };
        }

        /// <summary>
        /// Builds and returns TokenValidationParameters using the test signing key.
        /// </summary>
        private TokenValidationParameters BuildValidationParams(
            bool validateIssuer = true,
            bool validateAudience = true,
            string? overrideIssuer = null,
            string? overrideKey = null)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = validateIssuer,
                ValidIssuer = overrideIssuer ?? TestIssuer,
                ValidateAudience = validateAudience,
                ValidAudience = TestAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(overrideKey ?? TestSecretKey)),
                ValidateLifetime = false
            };
        }

        // =========================================================================
        // LOGIN — Happy Path
        // =========================================================================

        [Fact]
        public async Task Login_ReturnsCryptographicallySoundAndTimedToken_OnSuccess()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            
            var model = new LoginDto { Email = user.Email, Password = "Password123!" };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Radiologist" });

            // Act
            var result = await _controller.Login(model);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(okResult.Value);

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(response.Token, BuildValidationParams(), out var validatedToken);
            var jwtToken = Assert.IsType<JwtSecurityToken>(validatedToken);

            // Timing-safe expiry check
            var now = DateTime.UtcNow;
            Assert.InRange(jwtToken.ValidTo, now.AddMinutes(55), now.AddMinutes(65));
            Assert.True(jwtToken.ValidFrom <= now);

            // Identity & role verification
            Assert.Equal("user-123", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Contains(principal.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Radiologist");
        }

        [Fact]
        public async Task Login_ReturnsOk_WithCorrectRoleInResponse()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            var model = new LoginDto { Email = user.Email, Password = "Pass123!" };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Clinician" });

            // Act
            var result = await _controller.Login(model);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);
            Assert.Equal("Clinician", response.Role);
            Assert.False(string.IsNullOrWhiteSpace(response.Token));
        }

        // =========================================================================
        // LOGIN — Failure / Edge Cases
        // =========================================================================

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenUserDoesNotExist()
        {
            // Arrange
            _mockUserManager.Setup(x => x.FindByEmailAsync("ghost@test.com"))
                .ReturnsAsync((IdentityUser?)null);

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = "ghost@test.com", Password = "any" });

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
        {
            // Arrange
            var user = new IdentityUser { Email = "user@test.com" };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "WrongPass", true))
                .ReturnsAsync(SignInResult.Failed);

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "WrongPass" });

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_Returns403_WhenAccountIsLockedOut()
        {
            // Arrange
            // Added UserName here to be safe
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };

            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);

            // FIX: Change 'false' to 'true' to match your Controller's Login method
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.LockedOut);

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert
            var statusResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, statusResult.StatusCode);
        }

        [Fact]
        public async Task Login_ReturnsNoRoleFallback_WhenUserHasNoRoles()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);
            Assert.Equal("No Role", response.Role);
        }

        // =========================================================================
        // LOGIN — Token Claim Integrity
        // =========================================================================

        [Fact]
        public async Task Login_TokenContainsEmailAndSubClaims()
        {
            // Arrange
            var user = new IdentityUser
            {
                Email = "claims@test.com",
                Id = "abc-123",
                UserName = "claims@test.com"
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Clinician" });

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
            Assert.Contains(jwtToken.Claims,
                c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
            Assert.Contains(jwtToken.Claims,
                c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id);
        }

        [Fact]
        public async Task Login_TokenContainsUsernameClaim()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Radiologist" });

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
            Assert.Contains(jwtToken.Claims,
                c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == user.UserName);
            Assert.Contains(jwtToken.Claims,
                c => c.Type == ClaimTypes.Name && c.Value == user.UserName);
        }

        [Fact]
        public async Task Login_TokenContainsAllRoles_WhenUserHasMultipleRoles()
        {
            // Arrange
            var user = new IdentityUser 
            { 
                Email = "valid@test.com", 
                Id = "user-123", 
                UserName = "valid@test.com" // ADD THIS LINE
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Radiologist", "Clinician" });

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert — all roles must be embedded, not just the first
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
            var roles = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            Assert.Contains("Radiologist", roles);
            Assert.Contains("Clinician", roles);
        }

        // =========================================================================
        // LOGIN — JWT Cryptographic Security
        // =========================================================================

        [Fact]
        public async Task Login_TokenValidationFails_WhenSignatureIsTampered()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "valid@test.com", 
                Id = "user-123", 
                UserName = "valid@test.com" 
            };
            var model = new LoginDto { Email = user.Email, Password = "Password123!" };

            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, model.Password, true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Radiologist" });

            var result = await _controller.Login(model);
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            // Act & Assert 
            var handler = new JwtSecurityTokenHandler();
            var wrongKeyParams = BuildValidationParams(
                validateIssuer: false,
                validateAudience: false,
                overrideKey: "wrong_secret_key_123_wrong_secret_key");

            // FIXED: Use ThrowsAny to catch the specific security exception thrown by your library version
            Assert.ThrowsAny<SecurityTokenException>(() =>
                handler.ValidateToken(response.Token, wrongKeyParams, out _));
        }

        [Fact]
        public async Task Login_Token_UsesHmacSha256Algorithm()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Clinician" });

            // Act
            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });

            // Assert — algorithm downgrade guard
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);
            var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
            Assert.Equal(SecurityAlgorithms.HmacSha256, jwtToken.Header.Alg);
        }

        [Fact]
        public async Task Login_TokenValidationFails_WhenIssuerIsWrong()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Clinician" });

            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            // Act & Assert — a token from a different issuer must be rejected
            var handler = new JwtSecurityTokenHandler();
            var wrongIssuerParams = BuildValidationParams(overrideIssuer: "evil_issuer");

            Assert.ThrowsAny<SecurityTokenException>(() =>
                handler.ValidateToken(response.Token, wrongIssuerParams, out _));
        }

        [Fact]
        public async Task Login_TokenValidationFails_WhenAudienceIsWrong()
        {
            // Arrange
            var user = new IdentityUser { 
                Email = "locked@test.com", 
                UserName = "locked@test.com", 
                Id = "user-123" 
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Clinician" });

            var result = await _controller.Login(
                new LoginDto { Email = user.Email, Password = "Pass123!" });
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponseDto>(ok.Value);

            // Act & Assert — token must not be accepted by an unintended audience
            var handler = new JwtSecurityTokenHandler();
            var wrongAudParams = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = true,
                ValidAudience = "wrong_audience",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(TestSecretKey)),
                ValidateLifetime = false
            };

            Assert.ThrowsAny<SecurityTokenException>(() =>
                handler.ValidateToken(response.Token, wrongAudParams, out _));
        }

        // =========================================================================
        // REGISTER — Happy Path
        // =========================================================================

        [Fact]
        public async Task Register_AssignsRole_OnSuccessfulCreation()
        {
            // Arrange
            var model = new RegisterDto
            {
                Email = "new@test.com",
                Password = "Pass123!",
                Role = "Radiologist"
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync((IdentityUser?)null);
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), model.Password))
                .ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<IdentityUser>(), model.Role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.Register(model);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockUserManager.Verify(
                x => x.AddToRoleAsync(It.IsAny<IdentityUser>(), "Radiologist"), Times.Once);
        }

        // =========================================================================
        // REGISTER — Failure / Security
        // =========================================================================

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenRoleIsAdmin()
        {
            // Arrange & Act
            var result = await _controller.Register(new RegisterDto
            {
                Email = "admin@test.com",
                Password = "Admin123!",
                Role = "Admin"
            });

            // Assert — privilege escalation via API body must be blocked
            Assert.IsType<BadRequestObjectResult>(result);
            _mockUserManager.Verify(
                x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenEmailAlreadyExists()
        {
            // Arrange
            var existing = new IdentityUser { Email = "dup@test.com" };
            _mockUserManager.Setup(x => x.FindByEmailAsync("dup@test.com")).ReturnsAsync(existing);

            // Act
            var result = await _controller.Register(new RegisterDto
            {
                Email = "dup@test.com",
                Password = "Pass123!",
                Role = "Clinician"
            });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            _mockUserManager.Verify(
                x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Register_DoesNotAssignRole_WhenCreationFails()
        {
            // Arrange
            var model = new RegisterDto
            {
                Email = "fail@test.com",
                Password = "Password123!",
                Role = "Clinician"
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync((IdentityUser?)null);
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Fail" }));

            // Act
            await _controller.Register(model);

            // Assert — role assignment must never be called after a failed create
            _mockUserManager.Verify(
                x => x.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_WhenCreationFails()
        {
            // Arrange
            var model = new RegisterDto
            {
                Email = "badreg@test.com",
                Password = "weak",
                Role = "Clinician"
            };
            _mockUserManager.Setup(x => x.FindByEmailAsync(model.Email))
                .ReturnsAsync((IdentityUser?)null);
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "PasswordTooShort", Description = "Password too short." }));

            // Act
            var result = await _controller.Register(model);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Register_BlocksAdminRole_RegardlessOfCase()
        {
            // Arrange — We are testing "admin" (lowercase)
            var model = new RegisterDto
            {
                Email = "hacker@test.com",
                Password = "Pass123!",
                Role = "admin" // This should now be blocked
            };

            // Act
            var result = await _controller.Register(model);

            // Assert — We now EXPECT a BadRequest because the security fix is working
            Assert.IsType<BadRequestObjectResult>(result);
    
            // Verify: Ensure the code stopped early and didn't try to create the user
            _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()), Times.Never);
        }

        // =========================================================================
        // UPDATE PASSWORD — Happy Path
        // =========================================================================

        [Fact]
        public async Task UpdatePassword_VerifiesCorrectInputFlow_WithRealisticModel()
        {
            // Arrange
            var user = new IdentityUser { UserName = "testuser" };
            SetControllerUser("testuser");
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);

            var model = new UpdatePasswordDto
            {
                CurrentPassword = "CorrectOldPassword123!",
                NewPassword = "VerySecureNewPassword123!"
            };
            _mockUserManager.Setup(x => x.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _controller.UpdatePassword(model);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockUserManager.Verify(
                x => x.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword), Times.Once);
        }

        // =========================================================================
        // UPDATE PASSWORD — Failure / Edge Cases
        // =========================================================================

        [Fact]
        public async Task UpdatePassword_ReturnsBadRequest_WhenCurrentPasswordIsWrong()
        {
            // Arrange
            var user = new IdentityUser { UserName = "testuser" };
            SetControllerUser("testuser");
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongOld!", "NewPass123!"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Description = "Incorrect password." }));

            // Act
            var result = await _controller.UpdatePassword(new UpdatePasswordDto
            {
                CurrentPassword = "WrongOld!",
                NewPassword = "NewPass123!"
            });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdatePassword_ReturnsNotFound_WhenUserClaimDoesNotResolve()
        {
            // Arrange — JWT contains a username that no longer exists in the store
            SetControllerUser("ghost");
            _mockUserManager.Setup(x => x.FindByNameAsync("ghost"))
                .ReturnsAsync((IdentityUser?)null);

            // Act
            var result = await _controller.UpdatePassword(new UpdatePasswordDto
            {
                CurrentPassword = "Old123!",
                NewPassword = "New123!"
            });

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task UpdatePassword_ReturnsBadRequest_WhenNewPasswordFailsComplexity()
        {
            // Arrange
            var user = new IdentityUser { UserName = "testuser" };
            SetControllerUser("testuser");
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "weak"))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "PasswordTooShort", Description = "Password too short." },
                    new IdentityError { Code = "PasswordRequiresNonAlphanumeric", Description = "Requires special char." }));

            // Act
            var result = await _controller.UpdatePassword(new UpdatePasswordDto
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "weak"
            });

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdatePassword_DoesNotCallChangePassword_WhenUserNotFound()
        {
            // Arrange
            SetControllerUser("ghost");
            _mockUserManager.Setup(x => x.FindByNameAsync("ghost"))
                .ReturnsAsync((IdentityUser?)null);

            // Act
            await _controller.UpdatePassword(new UpdatePasswordDto
            {
                CurrentPassword = "Old123!",
                NewPassword = "New123!"
            });

            // Assert — ChangePasswordAsync must never be called with a null user
            _mockUserManager.Verify(
                x => x.ChangePasswordAsync(It.IsAny<IdentityUser>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }
        
        [Fact]
        public async Task Login_FailsValidation_WhenTokenIsExpired()
        {
            // 1. Setup the Config to force an expired token
            _mockConfig.Setup(c => c["Jwt:ExpiresMinutes"]).Returns("-60"); 

            // 2. Setup the User 
            var user = new IdentityUser 
            { 
                Email = "old@test.com", 
                UserName = "old@test.com", 
                Id = "user-123" 
            };

            // 3. Mocks
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Radiologist" });

            // IMPORTANT: This must be 'true' to match your controller
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);

            // 4. Act: Call the controller with the DTO (Email/Pass only)
            var loginDto = new LoginDto { Email = user.Email, Password = "Pass123!" };
            var result = await _controller.Login(loginDto);
    
            var response = Assert.IsType<LoginResponseDto>((result as OkObjectResult)!.Value);

            // 5. Assert: Verify the cryptographic failure
            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters
            {
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey)),
                ValidateIssuer = false, 
                ValidateAudience = false
            };

            Assert.Throws<SecurityTokenExpiredException>(() => 
                handler.ValidateToken(response.Token, validationParams, out _));
        }

        [Fact]
        public async Task Login_ThrowsException_WhenJwtConfigIsMissing()
        {
            // Arrange: Simulate a missing secret key in appsettings.json
            _mockConfig.Setup(c => c["Jwt:Key"]).Returns((string?)null);
    
            var user = new IdentityUser { 
                Email = "test@test.com", 
                UserName = "test@test.com", 
                Id = "123" 
            };
    
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Pass123!", true))
                .ReturnsAsync(SignInResult.Success);

            // FIX: You MUST mock GetRolesAsync so 'roles' isn't null in GenerateJwtToken
            _mockUserManager.Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "Radiologist" });

            // Act & Assert
            // Now it will pass the foreach loop and hit your "throw new ArgumentNullException"
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _controller.Login(new LoginDto { Email = "test@test.com", Password = "Pass123!" }));
        }
        
        [Fact]
        public async Task Register_ReturnsBadRequest_WhenRoleIsInvalid()
        {
            // Arrange: User tries to inject a double role or a made-up role
            var model = new RegisterDto { Email = "hacker@test.com", Password = "Password123!", Role = "Radiologist, Admin" };

            // Act
            var result = await _controller.Register(model);

            // Assert: System should reject roles that aren't in the allowed list
            Assert.IsType<BadRequestObjectResult>(result);
        }
        
        [Fact]
        public async Task UpdatePassword_CallsIdentityManager_ToUpdateSecurityStamp()
        {
            // Arrange
            var user = new IdentityUser { UserName = "testuser" };
            SetControllerUser("testuser");
            _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.ChangePasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            await _controller.UpdatePassword(new UpdatePasswordDto { CurrentPassword = "Old!", NewPassword = "New!" });

            // Assert: Verifying this method proves the SecurityStamp will be refreshed
            _mockUserManager.Verify(x => x.ChangePasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
        
        [Fact]
        public async Task Login_PassesLockoutOnFailureTrue_ToSignInManager()
        {
            // Arrange
            var user = new IdentityUser { Email = "test@test.com", UserName = "test@test.com" };
            _mockUserManager.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
    
            // FIX: Add a Setup so the controller's 'result' variable is not null
            // We can return 'Failed' because the password is "any"
            _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            // Act
            await _controller.Login(new LoginDto { Email = user.Email, Password = "any" });

            // Assert: Verify that the THIRD parameter (lockoutOnFailure) was indeed 'true'
            _mockSignInManager.Verify(x => x.CheckPasswordSignInAsync(
                    user, 
                    It.IsAny<string>(), 
                    true), // This is what we are testing!
                Times.Once);
        }
    }
}