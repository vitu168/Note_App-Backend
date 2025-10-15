using Microsoft.AspNetCore.Mvc;
using Supabase;
using System.Collections.Generic;
using System.Threading.Tasks;
using NoteApi.Models;
using System.Linq;

namespace NoteApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfileController : ControllerBase
    {
        private readonly Client _supabase;

        public UserProfileController(Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetProfiles()
        {
            var response = await _supabase.From<UserProfile>().Get();
            return Ok(response.Models.Select(u => new UserProfileDto
            {
                Id = u.Id,
                Name = u.Name,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                Email = u.Email,
                IsNote = u.IsNote
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(string id)
        {
            var response = await _supabase.From<UserProfile>().Filter("Id", Supabase.Postgrest.Constants.Operator.Equals, id).Get();
            var profile = response.Models.Count > 0 ? response.Models[0] : null;
            if (profile == null) return NotFound();
            return Ok(new UserProfileDto
            {
                Id = profile.Id,
                Name = profile.Name,
                AvatarUrl = profile.AvatarUrl,
                CreatedAt = profile.CreatedAt,
                Email = profile.Email,
                IsNote = profile.IsNote
            });
        }

        [HttpPost]
        public async Task<ActionResult<UserProfileDto>> CreateProfile([FromBody] UserProfileDto profileDto)
        {
            var profile = new UserProfile
            {
                Id = profileDto.Id,
                Name = profileDto.Name,
                AvatarUrl = profileDto.AvatarUrl,
                CreatedAt = DateTime.UtcNow,
                Email = profileDto.Email,
                IsNote = profileDto.IsNote
            };
            var response = await _supabase.From<UserProfile>().Insert(profile);
            var createdProfile = response.Models[0];
            return CreatedAtAction(nameof(GetProfile), new { id = createdProfile.Id }, new UserProfileDto
            {
                Id = createdProfile.Id,
                Name = createdProfile.Name,
                AvatarUrl = createdProfile.AvatarUrl,
                CreatedAt = createdProfile.CreatedAt,
                Email = createdProfile.Email,
                IsNote = createdProfile.IsNote
            });
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateProfile(string id, [FromBody] UserProfileDto profileDto)
        {
            if (id != profileDto.Id) return BadRequest();
            var profile = new UserProfile
            {
                Id = profileDto.Id,
                Name = profileDto.Name,
                AvatarUrl = profileDto.AvatarUrl,
                CreatedAt = profileDto.CreatedAt,
                Email = profileDto.Email,
                IsNote = profileDto.IsNote
            };
            var response = await _supabase.From<UserProfile>().Where(u => u.Id == id).Update(profile);
            return response.Models.Count > 0 ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteProfile(string id)
        {
            await _supabase.From<UserProfile>().Filter("Id", Supabase.Postgrest.Constants.Operator.Equals, id).Delete();
            return NoContent();
        }
    }
}