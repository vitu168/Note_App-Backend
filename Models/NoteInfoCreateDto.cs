namespace NoteApi.Models
{
    public class NoteInfoCreateDto
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? UserId { get; set; }

        public bool? IsFavorites { get; set; }
    }
}