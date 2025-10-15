using Microsoft.AspNetCore.Mvc;
using Supabase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using NoteApi.Models;

namespace NoteApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NoteInfoController : ControllerBase
    {
        private readonly Client _supabase;

        public NoteInfoController(Client supabase)
        {
            _supabase = supabase;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NoteInfoDto>>> GetNotes()
        {
            var response = await _supabase.From<NoteInfo>().Get();
            return Ok(response.Models.Select(n => new NoteInfoDto
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                UserId = n.UserId,
                IsFavorites = n.IsFavorites
            }));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NoteInfoDto>> GetNote(int id)
        {
            try
            {
                var response = await _supabase.From<NoteInfo>().Where(n => n.Id == id).Single();
                return Ok(new NoteInfoDto
                {
                    Id = response.Id,
                    Name = response.Name,
                    Description = response.Description,
                    CreatedAt = response.CreatedAt,
                    UpdatedAt = response.UpdatedAt,
                    UserId = response.UserId,
                    IsFavorites = response.IsFavorites
                });
            }
            catch
            {
                return NotFound(new { error = "Note not found" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<NoteInfoDto>> CreateNote([FromBody] NoteInfoCreateDto noteDto)
        {
            if (noteDto == null)
            {
                return BadRequest(new { error = "Note data is required" });
            }

            // Get the next available Id
            var maxIdResponse = await _supabase.From<NoteInfo>().Select("Id").Order("Id", Supabase.Postgrest.Constants.Ordering.Descending).Limit(1).Get();
            int nextId = maxIdResponse.Models.Count > 0 ? maxIdResponse.Models[0].Id + 1 : 1;

            var note = new NoteInfo
            {
                Id = nextId,
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = noteDto.UserId,
                IsFavorites = noteDto.IsFavorites
            };

            var response = await _supabase.From<NoteInfo>().Insert(note, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
            var createdNote = response.Models[0];
            return CreatedAtAction(nameof(GetNote), new { id = createdNote.Id }, new NoteInfoDto
            {
                Id = createdNote.Id,
                Name = createdNote.Name,
                Description = createdNote.Description,
                CreatedAt = createdNote.CreatedAt,
                UpdatedAt = createdNote.UpdatedAt,
                UserId = createdNote.UserId,
                IsFavorites = createdNote.IsFavorites
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] NoteInfoUpdateDto noteDto)
        {
            if (noteDto == null)
            {
                return BadRequest(new { error = "Note data is required" });
            }

            var note = new NoteInfo
            {
                Id = id,
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = noteDto.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                UserId = noteDto.UserId,
                IsFavorites = noteDto.IsFavorites
            };

            await _supabase.From<NoteInfo>().Where(n => n.Id == id).Update(note);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            await _supabase.From<NoteInfo>().Where(n => n.Id == id).Delete();
            return NoContent();
        }
    }
}