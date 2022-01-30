namespace Backend.ViewModels;

public record RoleViewModel
{

}

public record NewRole(string Name, List<RolePermission>  Permissions);

public record RolePermission(string Name);