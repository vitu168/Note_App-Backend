using System;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using System.Text.Json.Serialization;

namespace NoteApi.Models
{
    [Table("userinfo")]
    public class UserProfile : BaseModel
    {
        [PrimaryKey("Id", shouldInsert: true)]
        public string Id { get; set; } = null!;
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Email { get; set; }
        public bool? IsNote { get; set; }
    }

    public class UserProfileQueryParams
    {
        public string? Search { get; set; }
        public bool? IsNote { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class PageProfilesResult
    {
        public List<UserProfileDto> Items { get; set; } = new();

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
}