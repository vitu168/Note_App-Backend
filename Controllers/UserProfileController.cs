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

        private static int ParseContentRangeCount(string? contentRange)
        {
            if (string.IsNullOrEmpty(contentRange))
                return 0;
            var parts = contentRange.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[1], out var count))
                return count;
            return 0;
        }

        [HttpGet]
        public async Task<ActionResult<PageProfilesResult>> GetProfiles([FromQuery] UserProfileQueryParams query)
        {
            query ??= new UserProfileQueryParams();
            var page = query.Page > 0 ? query.Page : 1;
            var pageSize = query.PageSize > 0 ? query.PageSize : 0;
            var baseQuery = (Supabase.Postgrest.Interfaces.IPostgrestTable<UserProfile>)_supabase.From<UserProfile>();

            if (!string.IsNullOrEmpty(query.Search))
            {
                var searchTerm = query.Search.Trim();
                baseQuery = baseQuery.Where(u => u.Name!.Contains(searchTerm) || u.Email!.Contains(searchTerm));
            }

            if (query.IsNote.HasValue)
            {
                baseQuery = baseQuery.Where(u => u.IsNote == query.IsNote.Value);
            }

            var response = pageSize > 0
                ? await baseQuery
                    .Range((page - 1) * pageSize, (page - 1) * pageSize + pageSize - 1)
                    .Get()
                : await baseQuery.Get();

            var parsedTotal = response.ResponseMessage?.Content.Headers.TryGetValues("Content-Range", out var values) == true
                ? ParseContentRangeCount(values.FirstOrDefault())
                : response.Models.Count;
            var totalCount = parsedTotal > 0 ? parsedTotal : response.Models.Count;

            var profiles = response.Models.Select(u => new UserProfileDto
            {
                Id = u.Id,
                Name = u.Name,
                AvatarUrl = u.AvatarUrl,
                CreatedAt = u.CreatedAt,
                Email = u.Email,
                IsNote = u.IsNote
            }).ToList();

            return Ok(new PageProfilesResult
            {
                Items = profiles,
                Page = pageSize > 0 ? page : null,
                PageSize = pageSize > 0 ? pageSize : null,
                TotalCount = totalCount
            });
        }

        [HttpGet("{id}/notes")]
        public async Task<ActionResult<UserProfileWithNotesDto>> GetProfileWithNotes(string id)
        {
            var profileResponse = await _supabase.From<UserProfile>().Where(u => u.Id == id).Get();
            var profile = profileResponse.Models.Count > 0 ? profileResponse.Models[0] : null;

            var notesResponse = await _supabase.From<Noteinfo>().Where(n => n.UserId == id).Get();
            var notes = notesResponse.Models.Select(n => new NoteinfoDto
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                UserId = n.UserId,
                IsFavorites = n.IsFavorites
            }).ToList();

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            return Ok(new UserProfileWithNotesDto
            {
                Id = profile.Id,
                Name = profile.Name,
                AvatarUrl = profile.AvatarUrl,
                CreatedAt = profile.CreatedAt,
                Email = profile.Email,
                IsNote = profile.IsNote,
                Notes = notes
            });
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(string id)
        {
            var response = await _supabase.From<UserProfile>().Where(u => u.Id == id).Get();
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
            await _supabase.From<UserProfile>().Where(u => u.Id == id).Delete();
            return NoContent();
        }
    }
}