using System;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace NoteApi.Models
{
    [Table("noteinfodetail")]
    public class NoteInfoDetail : BaseModel
    {
        public int NoteId { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UserId { get; set; }

        public bool? IsFavorites { get; set; }

        public string? UserName { get; set; }

        public string? UserAvatarUrl { get; set; }

        public DateTime? UserCreatedAt { get; set; }
    }
}