namespace NoteApi.Models
{
    public class NoteInfoUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UserId { get; set; }
        public bool? IsFavorites { get; set; }
    }
}