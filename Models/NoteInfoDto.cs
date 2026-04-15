namespace NoteApi.Models
{
    public class NoteinfoDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? UserId { get; set; }

        public bool? IsFavorites { get; set; }

        public DateTime? Reminder { get; set; }
    }
}