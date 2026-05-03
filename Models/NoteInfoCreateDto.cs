namespace NoteApi.Models
{
    public class NoteinfoCreateDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? UserId { get; set; }

        public List<string>? UserIds { get; set; }

        public bool? IsFavorites { get; set; }

        public DateTime? Reminder { get; set; }
    }
    public class BatchNoteinfo
    {
        public List<CreateNoteDto> notes { get; set; } = new();
    }
    public class CreateNoteDto
    {
        public string? Name { get; set; }
        public string? UserId { get; set; }
        public string? Description { get; set; }
        public bool? IsFavorites { get; set; }
        public DateTime? Reminder { get; set; }
    }
    public class BatchCreateItemResult
    {
        public int Index { get; set;}
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public int? CreatedId { get; set; }
    }

    public class BatchCreateNotesResponse
    {
        public int TotalCount { get; set; }
        public List<BatchCreateItemResult> Results { get; set; } = new();
        public int SuccessCount => Results.Count(r => r.IsSuccess);
    }
}