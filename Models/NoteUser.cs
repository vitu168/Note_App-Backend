using System;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace NoteApi.Models
{
    [Table("note_users")]
    public class NoteUser : BaseModel
    {
        public int NoteId { get; set; }

        public string UserId { get; set; } = null!;

        public string Role { get; set; } = "viewer";

        public DateTime? CreatedAt { get; set; }
    }
}
