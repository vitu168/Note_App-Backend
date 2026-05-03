namespace NoteApi.Models
{
    public static class NoteRoles
    {
        public const string Owner   = "owner";
        public const string Deleter = "deleter";
        public const string Editor  = "editor";
        public const string Viewer  = "viewer";

        public static bool IsValid(string? role) =>
            role == Owner || role == Deleter || role == Editor || role == Viewer;

        // Roles a non-owner can be assigned to. Owner is granted only via transfer.
        public static bool IsAssignable(string? role) =>
            role == Deleter || role == Editor || role == Viewer;
    }

    public class ShareNoteDto
    {
        public string? UserId { get; set; }
        public string? Role   { get; set; }
    }

    public class ChangeRoleDto
    {
        public string? Role { get; set; }
    }

    public class TransferOwnerDto
    {
        public string? NewOwnerId { get; set; }
    }

    public class NoteShareDto
    {
        public string UserId { get; set; } = null!;
        public string Role   { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
    }
}
