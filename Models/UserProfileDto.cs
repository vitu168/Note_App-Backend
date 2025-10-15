namespace NoteApi.Models
{
    public class UserProfileDto
    {
        public string Id { get; set; } = null!;
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Email { get; set; }
        public bool? IsNote { get; set; }
    }
}