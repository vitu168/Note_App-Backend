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
        public int Page {get; set; }
        public int PageSize {get; set; }

        public int GetSkip () => (Page -1) * PageSize;
        public int GetTake () => PageSize;
    }

    public class PageNotesResult
    {
        public List<NoteinfoDto> Items {get; set; } = new();
        public int Page {get; set; }
        public int PageSize {get; set; }
        public int TotalCount {get; set; }
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page * PageSize < TotalCount;
    }
}