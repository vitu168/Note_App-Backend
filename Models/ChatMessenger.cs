using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NoteApi.Models
{
    [Table("chatmessenger")]
    public class ChatMessenger : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        public int ConversationId { get; set; }

        public string? SenderId { get; set; }

        public string? ReceiverId { get; set; }

        public string? Content { get; set; }

        public string? MessageType { get; set; }

        public bool IsRead { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class ChatMessengerDto
    {
        public int Id { get; set; }

        public int ConversationId { get; set; }

        public string? SenderId { get; set; }

        public string? ReceiverId { get; set; }

        public string? Content { get; set; }

        public string? MessageType { get; set; }

        public bool IsRead { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class ChatMessengerCreateDto
    {
        public string SenderId { get; set; } = string.Empty;

        public string? ReceiverId { get; set; }

        public string? Content { get; set; }

        public string? MessageType { get; set; }

        public bool? IsRead { get; set; }
    }

    public class ChatMessengerUpdateDto
    {
        public string? SenderId { get; set; }

        public string? ReceiverId { get; set; }

        public string? Content { get; set; }

        public string? MessageType { get; set; }

        public bool? IsRead { get; set; }
    }

    public class ChatMessengerQueryParams
    {
        public int? ConversationId { get; set; }

        public string? SenderId { get; set; }

        public bool? IsRead { get; set; }

        public string? Search { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class PageChatMessengerResult
    {
        public List<ChatMessengerDto> Items { get; set; } = new();

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
