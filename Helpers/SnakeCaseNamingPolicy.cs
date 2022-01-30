using System.Text.Json;

namespace Backend.Helpers;

internal class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static SnakeCaseNamingPolicy Instance { get; } = new();

    public override string ConvertName(string name) => name.ToSnakeCase();
}
