using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace NoteApi.Models
{
    [Table("userdevices")]
    public class UserDevice : BaseModel
    {
        [PrimaryKey("Id")]
        public int Id { get; set; }

        public string? UserId { get; set; }

        public string? FCMToken { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class SaveDeviceTokenDto
    {
        public string UserId { get; set; } = string.Empty;

        public string FCMToken { get; set; } = string.Empty;
    }
}
