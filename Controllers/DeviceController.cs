using Microsoft.AspNetCore.Mvc;
using NoteApi.Models;
using Supabase;
using System.Threading.Tasks;

namespace NoteApi.Controllers
{
    [ApiController]
    [Route("api/device")]
    public class DeviceController(Client supabase) : ControllerBase
    {
        private readonly Client _supabase = supabase;

        [HttpPost("save-token")]
        public async Task<IActionResult> SaveToken([FromBody] SaveDeviceTokenDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId))
                return BadRequest(new { error = "UserId is required" });

            if (string.IsNullOrWhiteSpace(dto.FCMToken))
                return BadRequest(new { error = "FCMToken is required" });
            var existing = await _supabase
                .From<UserDevice>()
                .Where(d => d.UserId == dto.UserId)
                .Single();

            if (existing != null)
            {
                // Update the existing token
                existing.FCMToken = dto.FCMToken;
                existing.UpdatedAt = DateTime.UtcNow;

                await _supabase
                    .From<UserDevice>()
                    .Where(d => d.UserId == dto.UserId)
                    .Update(existing);
            }
            else
            {
                // Insert a new record
                var device = new UserDevice
                {
                    UserId = dto.UserId,
                    FCMToken = dto.FCMToken,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabase
                    .From<UserDevice>()
                    .Insert(device);
            }

            return Ok(new { message = "Token saved successfully" });
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteToken(string userId)
        {
            await _supabase
                .From<UserDevice>()
                .Where(d => d.UserId == userId)
                .Delete();

            return NoContent();
        }
    }
}
