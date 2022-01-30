namespace Backend.Models;

public static class PermissionUtils
{
    public static List<PermissionGroup> GetPermissionGroups()
    {
        var list = new List<PermissionGroup>()
        {
            new()
            {
                Title = "Categories",
                Description = "Category Permissions",
                Permissions = new()
                {
                    new("read_categories", PermissionType.Select, "View Categories"),
                    new("insert_categories", PermissionType.Insert, "Add Category"),
                    new("update_categories", PermissionType.Update, "Update Category"),
                    new("delete_categories", PermissionType.Delete, "Delete Category"),
                }
            },
            new()
            {
                Title = "Items",
                Description = "Item Permissions",
                Permissions = new()
                {
                    new("read_items", PermissionType.Select, "View Items"),
                    new("insert_items", PermissionType.Insert, "Add Items"),
                    new("update_items", PermissionType.Update, "Update Items"),
                    new("delete_items", PermissionType.Delete, "Delete Items"),
                    new("review_items", PermissionType.Execute, "Review Items"),
                }
            },
            new()
            {
                Title = "Profiles",
                Description = "Profile Permissions",
                Permissions = new()
                {
                    new("read_profiles", PermissionType.Select, "View Profiles"),
                    new("insert_profiles", PermissionType.Insert, "Add Profiles"),
                    new("update_profiles", PermissionType.Update, "Update Profiles"),
                    new("delete_profiles", PermissionType.Delete, "Delete Profiles"),
                    new("block_profiles", PermissionType.Execute, "Block Profiles"),
                }
            },
        };

        return list;
    }
}