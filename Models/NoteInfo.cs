using System;
//using Posgrest.Models;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace NoteApi.Models
{
    [Table("noteinfo")]
    public class NoteInfo : BaseModel
    {
        [Column("Id")]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Description")]
        public string? Description { get; set; }

        [Column("CreatedAt")]
        public DateTime? CreatedAt { get; set; }

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("UserId")]
        public string? UserId { get; set; }

        [Column("isFavorites")]
        public bool? IsFavorites { get; set; }
    }
}