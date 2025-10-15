using System;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace NoteApi.Models
{
    [Table("userinfo")]
    public class UserProfile : BaseModel
    {
        [PrimaryKey]
        [Column("Id")]
        public string Id { get; set; } = null!;
        [Column("Name")]
        public string? Name { get; set; }
        [Column("AvatarUrl")]
        public string? AvatarUrl { get; set; }
        [Column("CreatedAt")]
        public DateTime? CreatedAt { get; set; }
        [Column("Email")]
        public string? Email { get; set; }
        [Column("IsNote")]
        public bool? IsNote { get; set; }
    }
}