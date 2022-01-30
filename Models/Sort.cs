namespace Backend.Models;

public record Sort(string Name, string Direction = "ASC")
{
    public static Sort? GetSort(string input)
    {
        var parts = input.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var field = parts[0];
        if (parts.Length < 2)
        {
            return new(field);
        }

        var direction = parts[1].Equals("true", StringComparison.OrdinalIgnoreCase)
                || input.Equals("ASC", StringComparison.OrdinalIgnoreCase)
                ? "ASC" : "DESC";

        return new(field, direction);
    }
}