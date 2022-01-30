namespace Backend.Models;

public record AppUser
{
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string SocialId { get; set; } = "";

    public bool IsActive { get; set; }
}
