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

        public enum NoteAction { View, Edit, Delete, ManageShares }

        private string? GetActingUserId()
        {
            var raw = Request.Headers["X-User-Id"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }

        private async Task<string?> GetUserRoleAsync(int noteId, string userId)
        {
            var resp = await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == noteId && nu.UserId == userId)
                .Get();
            return resp.Models.FirstOrDefault()?.Role;
        }

        private static bool RoleAllows(string? role, NoteAction action) => action switch
        {
            NoteAction.View          => role is NoteRoles.Owner or NoteRoles.Deleter or NoteRoles.Editor or NoteRoles.Viewer,
            NoteAction.Edit          => role is NoteRoles.Owner or NoteRoles.Deleter or NoteRoles.Editor,
            NoteAction.Delete        => role is NoteRoles.Owner or NoteRoles.Deleter,
            NoteAction.ManageShares  => role == NoteRoles.Owner,
            _ => false
        };

        private async Task<bool> UserCanAsync(int noteId, string userId, NoteAction action)
        {
            var role = await GetUserRoleAsync(noteId, userId);
            return RoleAllows(role, action);
        }

        // Replace note_users rows for a note with the given list.
        // First UserId in the list = owner; the rest = viewer.
        private async Task SyncNoteUsersAsync(int noteId, List<string> userIds)
        {
            var distinct = userIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            await _supabase.From<NoteUser>().Where(nu => nu.NoteId == noteId).Delete();

            if (distinct.Count == 0) return;

            var rows = distinct.Select((id, idx) => new NoteUser
            {
                NoteId = noteId,
                UserId = id,
                Role = idx == 0 ? "owner" : "viewer",
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _supabase.From<NoteUser>().Insert(rows);
        }

        private async Task<List<string>> GetUserIdsForNoteAsync(int noteId)
        {
            var resp = await _supabase.From<NoteUser>().Where(nu => nu.NoteId == noteId).Get();
            return resp.Models
                .OrderByDescending(m => m.Role == "owner")
                .Select(m => m.UserId)
                .ToList();
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
            
            if (!string.IsNullOrEmpty(query.UserId))
            {
                baseQuery = baseQuery.Where(n => n.UserId == query.UserId);
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

            var noteIds = response.Models.Select(n => n.Id).ToList();
            var userIdsByNote = new Dictionary<int, List<string>>();

            if (noteIds.Count > 0)
            {
                var linksResp = await _supabase
                    .From<NoteUser>()
                    .Filter("NoteId", Supabase.Postgrest.Constants.Operator.In, noteIds)
                    .Get();

                userIdsByNote = linksResp.Models
                    .GroupBy(m => m.NoteId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(m => m.Role == "owner")
                              .Select(m => m.UserId)
                              .ToList());
            }

            var notes = response.Models.Select(n => new NoteinfoDto
            {
                Id = n.Id,
                Name = n.Name,
                Description = n.Description,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                UserId = n.UserId,
                UserIds = userIdsByNote.TryGetValue(n.Id, out var ids) ? ids : new List<string>(),
                IsFavorites = n.IsFavorites,
                Reminder = n.Reminder
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

            var userIds = await GetUserIdsForNoteAsync(id);

            return Ok(new NoteinfoDto
            {
                Id = response.Id,
                Name = response.Name,
                Description = response.Description,
                CreatedAt = response.CreatedAt,
                UpdatedAt = response.UpdatedAt,
                UserId = response.UserId,
                UserIds = userIds,
                IsFavorites = response.IsFavorites,
                Reminder = response.Reminder
            });
        }

        [HttpPost]
        public async Task<ActionResult<NoteinfoDto>> CreateNote([FromBody] NoteinfoCreateDto noteDto)
        {
            // Build the effective UserIds list: explicit UserIds wins; fall back to single UserId.
            var userIds = (noteDto.UserIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            if (userIds.Count == 0 && !string.IsNullOrWhiteSpace(noteDto.UserId))
                userIds.Add(noteDto.UserId);

            var ownerId = userIds.FirstOrDefault();

            var note = new Noteinfo
            {
                Name = noteDto.Name,
                Description = noteDto.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = ownerId,
                IsFavorites = noteDto.IsFavorites,
                Reminder = noteDto.Reminder
            };

            Supabase.Postgrest.Responses.ModeledResponse<Noteinfo> response;
            try
            {
                response = await _supabase
                    .From<Noteinfo>()
                    .Insert(note, new Supabase.Postgrest.QueryOptions { Returning = Supabase.Postgrest.QueryOptions.ReturnType.Representation });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CreateNote] Insert failed: {ex.GetType().Name}: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "Failed to create note",
                    detail = ex.Message,
                    type = ex.GetType().Name
                });
            }

            if (response.Models.Count == 0)
            {
                return StatusCode(500, new { error = "Insert returned no rows" });
            }
            var createdNote = response.Models[0];

            if (userIds.Count > 0)
            {
                try
                {
                    await SyncNoteUsersAsync(createdNote.Id, userIds);
                }
                catch
                {
                    await _supabase.From<Noteinfo>().Where(n => n.Id == createdNote.Id).Delete();
                    throw;
                }
            }

            var linkedUserIds = await GetUserIdsForNoteAsync(createdNote.Id);

            return CreatedAtAction(nameof(GetNote), new { id = createdNote.Id }, new NoteinfoDto
            {
                Id = createdNote.Id,
                Name = createdNote.Name,
                Description = createdNote.Description,
                CreatedAt = createdNote.CreatedAt,
                UpdatedAt = createdNote.UpdatedAt,
                UserId = createdNote.UserId,
                UserIds = linkedUserIds,
                IsFavorites = createdNote.IsFavorites,
                Reminder = createdNote.Reminder
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] NoteinfoUpdateDto noteDto)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            var hasUserIds = noteDto.UserIds != null;
            var requiredAction = hasUserIds ? NoteAction.ManageShares : NoteAction.Edit;
            if (!await UserCanAsync(id, actingUserId, requiredAction))
                return StatusCode(403, new { error = $"User cannot {requiredAction} this note" });

            var userIds = (noteDto.UserIds ?? new List<string>())
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .Distinct()
                .ToList();

            var ownerId = hasUserIds ? userIds.FirstOrDefault() : noteDto.UserId;

            var noteUpdate = new Noteinfo
            {
                Id = id,
                Name = noteDto.Name,
                Description = noteDto.Description,
                UserId = ownerId,
                IsFavorites = noteDto.IsFavorites,
                Reminder = noteDto.Reminder
            };

            try
            {
                await _supabase
                    .From<Noteinfo>()
                    .Where(n => n.Id == id)
                    .Update(noteUpdate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UpdateNote] Update failed: {ex.GetType().Name}: {ex.Message}");
                return StatusCode(500, new
                {
                    error = "Failed to update note",
                    detail = ex.Message,
                    type = ex.GetType().Name
                });
            }

            if (hasUserIds)
            {
                await SyncNoteUsersAsync(id, userIds);
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            if (!await UserCanAsync(id, actingUserId, NoteAction.Delete))
                return StatusCode(403, new { error = "User cannot delete this note" });

            await _supabase
                .From<Noteinfo>()
                .Where(n => n.Id == id)
                .Delete();

            return NoContent();
        }

        // ---------------- Share management endpoints (owner only) ----------------

        [HttpGet("{id}/shares")]
        public async Task<ActionResult<List<NoteShareDto>>> GetShares(int id)
        {
            var resp = await _supabase.From<NoteUser>().Where(nu => nu.NoteId == id).Get();
            var list = resp.Models
                .OrderByDescending(m => m.Role == NoteRoles.Owner)
                .ThenBy(m => m.Role)
                .Select(m => new NoteShareDto
                {
                    UserId    = m.UserId,
                    Role      = m.Role,
                    CreatedAt = m.CreatedAt
                })
                .ToList();
            return Ok(list);
        }

        [HttpPost("{id}/share")]
        public async Task<IActionResult> ShareNote(int id, [FromBody] ShareNoteDto dto)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            if (string.IsNullOrWhiteSpace(dto.UserId) || !NoteRoles.IsAssignable(dto.Role))
                return BadRequest(new { error = "userId required; role must be deleter|editor|viewer" });

            if (!await UserCanAsync(id, actingUserId, NoteAction.ManageShares))
                return StatusCode(403, new { error = "Only the owner can share this note" });

            if (dto.UserId == actingUserId)
                return BadRequest(new { error = "Cannot change your own role here; use transfer-owner" });

            var existing = await GetUserRoleAsync(id, dto.UserId);
            if (existing == NoteRoles.Owner)
                return BadRequest(new { error = "Target is already the owner" });

            await _supabase.From<NoteUser>().Upsert(new NoteUser
            {
                NoteId    = id,
                UserId    = dto.UserId,
                Role      = dto.Role!,
                CreatedAt = DateTime.UtcNow
            });

            return Ok(new { noteId = id, userId = dto.UserId, role = dto.Role });
        }

        [HttpPut("{id}/share/{userId}")]
        public async Task<IActionResult> ChangeShareRole(int id, string userId, [FromBody] ChangeRoleDto dto)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            if (!NoteRoles.IsAssignable(dto.Role))
                return BadRequest(new { error = "role must be deleter|editor|viewer" });

            if (!await UserCanAsync(id, actingUserId, NoteAction.ManageShares))
                return StatusCode(403, new { error = "Only the owner can change roles" });

            var existing = await GetUserRoleAsync(id, userId);
            if (existing == null)
                return NotFound(new { error = "User is not shared on this note" });
            if (existing == NoteRoles.Owner)
                return BadRequest(new { error = "Cannot change owner's role; use transfer-owner" });

            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == userId)
                .Set(nu => nu.Role!, dto.Role!)
                .Update();

            return Ok(new { noteId = id, userId, role = dto.Role });
        }

        [HttpDelete("{id}/share/{userId}")]
        public async Task<IActionResult> RevokeShare(int id, string userId)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            if (!await UserCanAsync(id, actingUserId, NoteAction.ManageShares))
                return StatusCode(403, new { error = "Only the owner can revoke access" });

            var existing = await GetUserRoleAsync(id, userId);
            if (existing == null)
                return NotFound(new { error = "User is not shared on this note" });
            if (existing == NoteRoles.Owner)
                return BadRequest(new { error = "Cannot revoke owner; use transfer-owner first" });

            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == userId)
                .Delete();

            return NoContent();
        }

        // "Remove the note from my account" — per-user soft delete.
        // Non-owner: just unlinks the user.
        // Owner with other users: auto-promotes the oldest other user, then unlinks.
        // Owner alone: blocked (must use DELETE /{id} for full delete).
        [HttpDelete("{id}/leave")]
        public async Task<IActionResult> LeaveNote(int id)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            var myRole = await GetUserRoleAsync(id, actingUserId);
            if (myRole == null)
                return NotFound(new { error = "You are not on this note" });

            if (myRole != NoteRoles.Owner)
            {
                await _supabase.From<NoteUser>()
                    .Where(nu => nu.NoteId == id && nu.UserId == actingUserId)
                    .Delete();
                return NoContent();
            }

            // Owner is leaving — find a successor.
            var allLinks = await _supabase.From<NoteUser>().Where(nu => nu.NoteId == id).Get();
            var successor = allLinks.Models
                .Where(m => m.UserId != actingUserId)
                .OrderBy(m => m.CreatedAt)
                .FirstOrDefault();

            if (successor == null)
                return BadRequest(new
                {
                    error = "You are the only user on this note. " +
                            "Share it with someone first, or call DELETE /api/NoteInfo/{id} to delete for everyone."
                });

            // 1. Demote current owner so the unique-owner slot is free.
            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == actingUserId)
                .Set(nu => nu.Role!, NoteRoles.Viewer)
                .Update();

            // 2. Promote successor.
            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == successor.UserId)
                .Set(nu => nu.Role!, NoteRoles.Owner)
                .Update();

            // 3. Sync noteinfo.UserId to the new owner.
            await _supabase.From<Noteinfo>()
                .Where(n => n.Id == id)
                .Set(n => n.UserId!, successor.UserId)
                .Update();

            // 4. Now remove the leaving user's row.
            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == actingUserId)
                .Delete();

            return Ok(new { noteId = id, newOwnerId = successor.UserId, message = "Left note; ownership transferred" });
        }

        [HttpPost("{id}/transfer-owner")]
        public async Task<IActionResult> TransferOwner(int id, [FromBody] TransferOwnerDto dto)
        {
            var actingUserId = GetActingUserId();
            if (string.IsNullOrEmpty(actingUserId))
                return Unauthorized(new { error = "X-User-Id header required" });

            if (string.IsNullOrWhiteSpace(dto.NewOwnerId))
                return BadRequest(new { error = "newOwnerId required" });

            if (!await UserCanAsync(id, actingUserId, NoteAction.ManageShares))
                return StatusCode(403, new { error = "Only the current owner can transfer ownership" });

            if (dto.NewOwnerId == actingUserId)
                return Ok(new { noteId = id, ownerId = actingUserId, message = "Already the owner" });

            // Step 1: demote current owner to editor (frees the unique-owner slot).
            await _supabase.From<NoteUser>()
                .Where(nu => nu.NoteId == id && nu.UserId == actingUserId)
                .Set(nu => nu.Role!, NoteRoles.Editor)
                .Update();

            // Step 2: promote target (insert if missing, update if already shared).
            await _supabase.From<NoteUser>().Upsert(new NoteUser
            {
                NoteId    = id,
                UserId    = dto.NewOwnerId,
                Role      = NoteRoles.Owner,
                CreatedAt = DateTime.UtcNow
            });

            // Step 3: keep noteinfo.UserId in sync with the new owner.
            await _supabase.From<Noteinfo>()
                .Where(n => n.Id == id)
                .Set(n => n.UserId!, dto.NewOwnerId)
                .Update();

            return Ok(new { noteId = id, newOwnerId = dto.NewOwnerId });
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
                    IsFavorites = noteDto.IsFavorites,
                    Reminder = noteDto.Reminder
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