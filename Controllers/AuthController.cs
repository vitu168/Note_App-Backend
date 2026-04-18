using Microsoft.AspNetCore.Mvc;
using NoteApi.Models;
using Supabase;

namespace NoteApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(Client supabase) : ControllerBase
    {
        private readonly Client _supabase = supabase;

        private async Task SaveFcmTokenAsync(string userId, string fcmToken)
        {
            var existing = await _supabase
                .From<UserDevice>()
                .Where(d => d.UserId == userId)
                .Single();

            if (existing != null)
            {
                existing.FCMToken = fcmToken;
                existing.UpdatedAt = DateTime.UtcNow;
                await _supabase.From<UserDevice>().Where(d => d.UserId == userId).Update(existing);
            }
            else
            {
                await _supabase.From<UserDevice>().Insert(new UserDevice
                {
                    UserId = userId,
                    FCMToken = fcmToken,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // POST /api/auth/signup
        [HttpPost("signup")]
        public async Task<ActionResult<AuthResponseDto>> SignUp([FromBody] SignUpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Email and Password are required" });

            var session = await _supabase.Auth.SignUp(dto.Email, dto.Password);

            if (session?.User == null)
                return BadRequest(new { error = "Sign up failed" });

            var userId = session.User.Id;

            // If Id is null, email confirmation is required — userinfo will be created on first signin
            if (!string.IsNullOrEmpty(userId))
            {
                var existing = await _supabase
                    .From<UserProfile>()
                    .Where(u => u.Id == userId)
                    .Single();

                if (existing == null)
                {
                    var profile = new UserProfile
                    {
                        Id = userId,
                        Email = session.User.Email,
                        Name = dto.Name,
                        CreatedAt = DateTime.UtcNow
                    };
                await _supabase.From<UserProfile>().Insert(profile);
                }
            }

            return Ok(new AuthResponseDto
            {
                UserId = userId ?? string.Empty,
                Email = session.User.Email,
                Name = dto.Name,
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken,
            });
        }

        // POST /api/auth/signin
        [HttpPost("signin")]
        public async Task<ActionResult<AuthResponseDto>> SignIn([FromBody] SignInDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Email and Password are required" });

            var session = await _supabase.Auth.SignIn(dto.Email, dto.Password);

            if (session?.User == null)
                return Unauthorized(new { error = "Invalid email or password" });

            var userId = session.User.Id!;

            // Fetch user profile from userinfo table
            var profile = await _supabase
                .From<UserProfile>()
                .Where(u => u.Id == userId)
                .Single();

            if (!string.IsNullOrEmpty(dto.FCMToken))
                await SaveFcmTokenAsync(userId, dto.FCMToken);

            return Ok(new AuthResponseDto
            {
                UserId = userId,
                Email = session.User.Email,
                Name = profile?.Name,
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken
            });
        }

        // POST /api/auth/signout
        [HttpPost("signout")]
        public async Task<IActionResult> Logout()
        {
            await _supabase.Auth.SignOut();
            return Ok(new { message = "Signed out successfully" });
        }

        // GET /api/auth/me
        [HttpGet("me")]
        public async Task<ActionResult<UserProfileDto>> Me()
        {
            var user = _supabase.Auth.CurrentUser;

            if (user == null)
                return Unauthorized(new { error = "Not authenticated" });

            var profile = await _supabase
                .From<UserProfile>()
                .Where(u => u.Id == user.Id)
                .Single();

            if (profile == null)
                return NotFound(new { error = "User profile not found" });

            return Ok(new UserProfileDto
            {
                Id = profile.Id,
                Name = profile.Name,
                Email = profile.Email,
                AvatarUrl = profile.AvatarUrl,
                CreatedAt = profile.CreatedAt,
                IsNote = profile.IsNote
            });
        }

        // POST /api/auth/social
        // Called AFTER Flutter completes Google/Facebook OAuth via Supabase SDK
        // Flutter sends the access token it received from Supabase auth
        [HttpPost("social")]
        public async Task<ActionResult<AuthResponseDto>> SocialAuth([FromBody] SocialAuthDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.AccessToken))
                return BadRequest(new { error = "AccessToken is required" });

            // Set session using the token from the mobile app
            var session = await _supabase.Auth.SetSession(dto.AccessToken, string.Empty);

            if (session?.User == null)
                return Unauthorized(new { error = "Invalid token" });

            // Check if userinfo record already exists
            var existing = await _supabase
                .From<UserProfile>()
                .Where(u => u.Id == session.User.Id)
                .Single();

            if (existing == null)
            {
                // First time social login — create userinfo record
                var profile = new UserProfile
                {
                    Id = session.User.Id!,
                    Email = session.User.Email,
                    Name = dto.Name ?? session.User.Email,
                    AvatarUrl = dto.AvatarUrl,
                    CreatedAt = DateTime.UtcNow
                };

                await _supabase.From<UserProfile>().Insert(profile);
            }

            return Ok(new AuthResponseDto
            {
                UserId = session.User.Id!,
                Email = session.User.Email,
                Name = existing?.Name ?? dto.Name,
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken
            });
        }
    }
}
