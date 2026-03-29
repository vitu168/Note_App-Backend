using Microsoft.AspNetCore.Mvc;
using Supabase;
using System.Threading.Tasks;
using System.Collections.Generic;
using NoteApi.Models;

namespace NoteApi.Controllers;

class NoteInfoDetailsController(Client supbase) : ControllerBase
{
    private readonly Client _supabase = supbase;
    private static int ParseContentRangeCount(string? contentRange)
    {
        if (string.IsNullOrEmpty(contentRange))
            return 0;

        var parts = contentRange.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[1], out var count))
            return count;

        return 0;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NoteinfoDto>> GetNoteById(int id)
    {
        var response = await _supabase.From<Noteinfo>().Where(n => n.Id == id).Get();
        var note = response.Models.FirstOrDefault();
        if (note == null)
        {
            return NotFound();
        }

        var noteDto = new NoteinfoDto
        {
            Id = note.Id,
            Name = note.Name,
            Description = note.Description,
            IsFavorites = note.IsFavorites
        };

        return Ok(noteDto);
    }
}