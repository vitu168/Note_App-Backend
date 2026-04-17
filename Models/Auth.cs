namespace NoteApi.Models
{
    public class SignUpDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    public class SignInDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SocialAuthDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class AuthResponseDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
    }
}
