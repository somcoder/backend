using System.Reflection;
using System.Text;

namespace Backend.Models;

public record Function()
{
    public string Name { get; set; } = "";

    public string Schema { get; set; } = "";

    public Dictionary<string, string> Parameters { get; set; } = new();

    public string Type { get; set; } = "";

    public bool IsVoid => Type.Equals("void", StringComparison.OrdinalIgnoreCase);

    public (string query, NpgsqlParameter[] parameters) ToQuery(Dictionary<string, object> data)
    {
        var query = new StringBuilder();
        query.Append($"SELECT {Name}(");

        if (!Parameters.Any())
        {
            query.Append(')');
            return (query.ToString(), Array.Empty<NpgsqlParameter>());
        }

        var parameters = new List<NpgsqlParameter>(Parameters.Count);
        var counter = 0;
        foreach (var parameter in Parameters)
        {
            if (counter > 0)
            {
                query.Append(',');
            }

            query.Append($"${counter + 1}");

            if (!data.TryGetValue(parameter.Key, out var value))
            {
                throw new ArgumentNullException(null, $"Missing required parameter: {parameter.Key}");
            }

            if (value is JsonElement element)
            {
                value = element.GetString();
            }

            if (value is null)
            {
                value = DBNull.Value;
            }

            parameters.Add(new()
            {
                Value = value
            });

            counter++;
        }

        query.Append(')');
        return (query.ToString(), parameters.ToArray());
    }

    public static async ValueTask<Function?> BindAsync(HttpContext context, ParameterInfo _)
    {
        if (context.GetRouteValue("function") is not string functionName || functionName.IsEmpty())
        {
            return null;
        }

        var service = context.RequestServices.GetRequiredService<DbService>();

        var functions = await service.GetFunctionsAsync(context.RequestAborted);
        return functions.FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
    }
}
