namespace Backend.Models;

public record Filter(string Name, string Operation, object Value)
{
    public static Filter? GetFilter(string key, string input)
    {
        var parts = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var field = parts[0];
        var operation = parts[1] switch
        {
            "eq" => "=",
            "neq" => "!=",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            "like" => "LIKE",
            _ => "="
        };

        return new(field, operation, input);
    }
}
