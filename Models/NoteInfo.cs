using System;
//using Posgrest.Models;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using System.Text.Json.Serialization;

namespace NoteApi.Models
{
    [Table("noteinfo")]
    public class Noteinfo : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }
        
        public string? Name { get; set; }

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UserId { get; set; }

        public bool? IsFavorites { get; set; }

        public DateTime? Reminder { get; set; }
    }
    public class NoteQueryParams 
    {
        public string? Search { get; set; }
        public bool? IsFavorites { get; set; }
        public string? UserId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public int GetSkip() => (Page - 1) * PageSize;
        public int GetTake() => PageSize;
    }

    public class PageNotesResult
    {
        public List<NoteinfoDto> Items { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Page { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? PageSize { get; set; }

        public int TotalCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? HasPrevious => Page.HasValue ? Page > 1 : null;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? HasNext => Page.HasValue && PageSize.HasValue ? Page * PageSize < TotalCount : null;
    }

    public class NoteinfoUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? UserId { get; set; }
        public bool? IsFavorites { get; set; }
        public DateTime? Reminder { get; set; }
    }
}