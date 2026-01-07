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
        public async Task<ActionResult<PageNotesResult>> GetNotes([FromQuery] NoteQueryParams query)
        {
            var baseQuery = (Supabase.Postgrest.Interfaces.IPostgrestTable<Noteinfo>)_supabase.From<Noteinfo>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                baseQuery = baseQuery.Filter("name", Supabase.Postgrest.Constants.Operator.ILike, $"%{query.Search}%");
            }
            if (query.IsFavorites.HasValue)
            {
                baseQuery = baseQuery.Filter("is_favorites", Supabase.Postgrest.Constants.Operator.Equals, query.IsFavorites.Value);
            }

            int from = query.getSkip();
            int to = from + query.getTake() - 1;
            baseQuery = baseQuery.Range(from, to);
            var responses = await baseQuery.Get();
            var countQuery = (Supabase.Postgrest.Interfaces.IPostgrestTable<Noteinfo>)_supabase.From<Noteinfo>();
            if (!string.IsNullOrEmpty(query.Search))
            {
                countQuery = countQuery.Filter("name", Supabase.Postgrest.Constants.Operator.ILike, $"%{query.Search}%");
            }
            if (query.IsFavorites.HasValue)
            {
                countQuery = countQuery.Filter("is_favorites", Supabase.Postgrest.Constants.Operator.Equals, query.IsFavorites.Value);
            }
            var countResponse = await countQuery.Get();
            long totalCount = countResponse.Models.Count;

            var notes = responses.Models.Select(n => new noteinfoDto
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                UserId = n.UserId,
                IsFavorites = n.IsFavorites
            }).ToList();

            var result = new PageNotesResult
            {
                Items = notes,
                Page = query.page,
                pageSize = query.pageSize,
                TotalCount = (int)totalCount
            };

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<noteinfoDto>> GetNote(int id)
        {
            try
            {
                var response = await _supabase.From<Noteinfo>().Where(n => n.Id == id).Single();
                if (response == null)
                {
                    return NotFound(new { error = "Note not found" });
                }
                return Ok(new noteinfoDto
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
        public async Task<ActionResult<noteinfoDto>> CreateNote([FromBody] noteinfoCreateDto noteDto)
        {
            if (noteDto == null)
            {
                return BadRequest(new { error = "Note data is required" });
            }

            var maxIdResponse = await _supabase.From<Noteinfo>().Select("Id").Order("Id", Supabase.Postgrest.Constants.Ordering.Descending).Limit(1).Get();
            int nextId = maxIdResponse.Models.Count > 0 ? maxIdResponse.Models[0].Id + 1 : 1;

            var note = new Noteinfo
            {
                Id = nextId,
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = noteDto.UserId,
                IsFavorites = noteDto.IsFavorites
            };

            var response = await _supabase.From<Noteinfo>().Insert(note, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
            var createdNote = response.Models[0];
            return CreatedAtAction(nameof(GetNote), new { id = createdNote.Id }, new noteinfoDto
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
        public async Task<IActionResult> UpdateNote(int id, [FromBody] noteinfoUpdateDto noteDto)
        {
            if (noteDto == null)
            {
                return BadRequest(new { error = "Note data is required" });
            }

            var note = new Noteinfo
            {
                Id = id,
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = noteDto.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                UserId = noteDto.UserId,
                IsFavorites = noteDto.IsFavorites
            };

            await _supabase.From<Noteinfo>().Where(n => n.Id == id).Update(note);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            await _supabase.From<Noteinfo>().Where(n => n.Id == id).Delete();
            return NoContent();
        }
    }
}