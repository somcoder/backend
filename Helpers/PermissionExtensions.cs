namespace Backend.Helpers;

public static class PermissionExtensions
{
    public static string ToGrant(this Permission permission, string role)
    {
        var obj = permission.Type switch
        {
            PermissionType.Execute => permission.Name,
            _ => permission.Name.Split('_', StringSplitOptions.RemoveEmptyEntries)[1]
        };

        var operation = permission.Type switch
        {
            PermissionType.Usage => "USAGE",
            PermissionType.Select => "SELECT",
            PermissionType.Insert => "INSERT",
            PermissionType.Update => "UPDATE",
            PermissionType.Delete => "DELETE",
            PermissionType.Execute => "EXECUTE",
            _ => "USAGE"
        };

        return $"GRANT {operation} ON {obj} TO {role}";
    }
}