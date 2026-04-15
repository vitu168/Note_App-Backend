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
    public class NoteInfoController(Client supabase) : ControllerBase
    {
        private readonly Client _supabase = supabase;

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
        public async Task<ActionResult<PageNotesResult>> GetNotes([FromQuery] NoteQueryParams query)
        {
            query ??= new NoteQueryParams();
            var page = query.Page > 0 ? query.Page : 1;
            var pageSize = query.PageSize > 0 ? query.PageSize : 0;
            var baseQuery = (Supabase.Postgrest.Interfaces.IPostgrestTable<Noteinfo>)_supabase.From<Noteinfo>();
            
            if (!string.IsNullOrEmpty(query.Search))
            {
                var searchTerm = query.Search.Trim();
                baseQuery = baseQuery.Where(n => n.Name!.Contains(searchTerm) || n.Description!.Contains(searchTerm));
            }
            
            if (query.IsFavorites.HasValue)
            {
                baseQuery = baseQuery.Where(n => n.IsFavorites == query.IsFavorites.Value);
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

            var notes = response.Models.Select(n => new NoteinfoDto
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                UserId = n.UserId,
                IsFavorites = n.IsFavorites
            }).ToList();

            return Ok(new PageNotesResult
            {
                Items = notes,
                Page = pageSize > 0 ? page : null,
                PageSize = pageSize > 0 ? pageSize : null,
                TotalCount = totalCount
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NoteinfoDto>> GetNote(int id)
        {
            var response = await _supabase.From<Noteinfo>().Where(n => n.Id == id).Single();
            
            if (response == null)
            {
                return NotFound(new { error = "Note not found" });
            }

            return Ok(new NoteinfoDto
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

        [HttpPost]
        public async Task<ActionResult<NoteinfoDto>> CreateNote([FromBody] NoteinfoCreateDto noteDto)
        {
            var maxIdResponse = await _supabase
                .From<Noteinfo>()
                .Select("Id")
                .Order("Id", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();
                
            var nextId = maxIdResponse.Models.Count > 0 ? maxIdResponse.Models[0].Id + 1 : 1;

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

            var response = await _supabase
                .From<Noteinfo>()
                .Insert(note, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
                
            var createdNote = response.Models[0];
            
            return CreatedAtAction(nameof(GetNote), new { id = createdNote.Id }, new NoteinfoDto
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
        public async Task<IActionResult> UpdateNote(int id, [FromBody] NoteinfoUpdateDto noteDto)
        {
            var noteUpdate = new Noteinfo
            {
                Id = id,
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = noteDto.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
                UserId = noteDto.UserId,
                IsFavorites = noteDto.IsFavorites
            };

            await _supabase
                .From<Noteinfo>()
                .Where(n => n.Id == id)
                .Update(noteUpdate);
                
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            await _supabase
                .From<Noteinfo>()
                .Where(n => n.Id == id)
                .Delete();
                
            return NoContent();
        }

        [HttpPost("batchCreateNotes")]
        public async Task<ActionResult<BatchCreateNotesResponse>> BatchCreateNotes([FromBody] BatchNoteinfo request)
        {
            if (request?.notes == null || request.notes.Count == 0)
            {
                return BadRequest(new { error = "Notes data is required" });
            }

            var response = new BatchCreateNotesResponse
            {
                TotalCount = request.notes.Count
            };

            var maxIdResponse = await _supabase
                .From<Noteinfo>()
                .Select("Id")
                .Order("Id", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(1)
                .Get();
                
            var nextId = maxIdResponse.Models.Count > 0 ? maxIdResponse.Models[0].Id + 1 : 1;

            for (int i = 0; i < request.notes.Count; i++)
            {
                var noteDto = request.notes[i];
                var note = new Noteinfo
                {
                    Id = nextId++,
                    Name = noteDto.Name,
                    Description = noteDto.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UserId = noteDto.UserId,
                    IsFavorites = noteDto.IsFavorites
                };

                var createResponse = await _supabase
                    .From<Noteinfo>()
                    .Insert(note, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
                    
                var createdNote = createResponse.Models[0];

                response.Results.Add(new BatchCreateItemResult
                {
                    Index = i,
                    IsSuccess = true,
                    CreatedId = createdNote.Id
                });
            }
            
            return Ok(response);
        }
    }
}