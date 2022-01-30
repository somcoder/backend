namespace Backend.Models;

public record Role
{
    public string Name { get; set; } = "";

    public List<Permission> Permissions { get; set; } = new();
}

public record Permission(string Name, PermissionType Type = PermissionType.Usage, string Title = "");

public enum PermissionType
{
    Usage,
    Select,
    Insert,
    Update,
    Delete,
    Execute
}

public record PermissionGroup
{
    public string Title { get; set; } = "Permission Group";

    public string Description { get; set; } = "Basic Permission Group";

    public List<Permission> Permissions { get; set; } = new();
}
