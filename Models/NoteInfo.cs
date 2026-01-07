using System;
//using Posgrest.Models;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

namespace NoteApi.Models
{
    [Table("noteinfo")]
    public class Noteinfo : BaseModel
    {
        public int Id { get; set; }
        
        public string? Name { get; set; }

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UserId { get; set; }

        public bool? IsFavorites { get; set; }
    }


    public class NoteQueryParams 
    {
        public string? Search {get; set; }
        public bool? IsFavorites {get; set; }
        public int page {get; set; } = 1;
        public int pageSize {get; set; } = 20;

        public int getSkip () => (page -1) * pageSize;
        public int getTake () => pageSize;
    }

    public class PageNotesResult
    {
        public List<noteinfoDto> Items {get; set; } = new();
        public int Page {get; set; }
        public int pageSize {get; set; }
        public int TotalCount {get; set; }
        public bool hasPrevious => Page > 1;
        public bool hasNext => Page * pageSize < TotalCount;
    }
}