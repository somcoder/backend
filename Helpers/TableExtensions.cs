using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Primitives;
using NpgsqlTypes;

namespace Backend.Helpers;

public static class TableExtensions
{
    public static (string columns, string values, NpgsqlParameter[] parameters) GetParameters(this Table table, Dictionary<string, object> data, bool isUpdate = false)
    {
        var columns = new StringBuilder();
        var values = new StringBuilder();
        var result = new SortedList<int, NpgsqlParameter>();

        var counter = 1;
        foreach (var item in data)
        {
            var key = item.Key.ToSnakeCase();
            var column = table.Columns.FirstOrDefault(c => c.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            var type = column.GetDbType();

            if (counter > 1)
            {
                columns.Append(',');
                values.Append(',');
            }

            object? value = null;
            if (item.Value is JsonElement element)
            {
                Console.WriteLine($"Type is: {type}");
                value = type switch
                {
                    NpgsqlDbType.Bigint => element.GetInt64(),
                    NpgsqlDbType.Double => element.GetDouble(),
                    NpgsqlDbType.Integer => element.GetInt32(),
                    NpgsqlDbType.Numeric => element.GetDecimal(),
                    NpgsqlDbType.Real => element.GetDecimal(),
                    NpgsqlDbType.Smallint => element.GetInt16(),
                    NpgsqlDbType.Money => element.GetDecimal(),
                    NpgsqlDbType.Boolean => element.GetBoolean(),
                    NpgsqlDbType.Char => element.GetString(),
                    NpgsqlDbType.Text => element.GetString(),
                    NpgsqlDbType.Varchar => element.GetString(),
                    NpgsqlDbType.Name => element.GetString(),
                    NpgsqlDbType.Citext => element.GetString(),
                    NpgsqlDbType.Date => element.GetDateTime(),
                    NpgsqlDbType.Timestamp => element.GetDateTime(),
                    NpgsqlDbType.Json => element.GetRawText(),
                    NpgsqlDbType.Jsonb => element.GetRawText(),
                    NpgsqlDbType.Array => element.Deserialize<object[]>(),
                    _ => throw new NotImplementedException()
                };

                if (type is NpgsqlDbType.Array)
                {
                    Console.WriteLine($"UdtName: {column.UdtName}");
                    value = column.UdtName switch
                    {
                        "_int4" => element.Deserialize<int[]>(),
                        "_int8" => element.Deserialize<long[]>(),
                        "_text" => element.Deserialize<string[]>(),
                        _ => throw new NotImplementedException()
                    };
                }
            }

            Console.WriteLine($"Type {type} with value: {value}");

            if (isUpdate)
            {
                columns.Append($"{key} = ${counter}");
            }
            else
            {
                columns.Append(key);
                values.Append($"${counter}");
            }

            result.Add(counter, new NpgsqlParameter
            {
                NpgsqlDbType = column.GetSubDbType(),
                Value = value ?? DBNull.Value,
            });

            counter++;
        }

        return (columns.ToString(), values.ToString(), result.Values.ToArray());
    }

    public static (string filter, NpgsqlParameter[] parameters) GetFilters(this Table table, List<Filter> filters)
    {
        if (!filters.Any())
        {
            return ("", Array.Empty<NpgsqlParameter>());
        }

        var parameters = new SortedList<int, NpgsqlParameter>(filters.Count);

        var sb = new StringBuilder();
        var counter = 1;
        foreach (var filter in filters)
        {
            var column = table.Columns.FirstOrDefault(c => c.NiceName.Equals(filter.Name, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            if (counter > 1)
            {
                sb.Append(" AND ");
            }

            var type = column.GetDbType();
            if (!column.TryGetValue(filter.Value, out var value))
            {
                continue;
            }

            sb.Append($"{table.Name}.{column.Name} {filter.Operation} ${counter}");
            parameters.Add(counter, new()
            {
                NpgsqlDbType = type,
                Value = value
            });

            counter++;
        }

        return (sb.ToString(), parameters.Values.ToArray());
    }
    
    public static string GetSort(this Table table, List<Sort> sorts)
    {
        if (!sorts.Any())
        {
            return "";
        }

        var sb = new StringBuilder();
        var counter = 1;
        foreach (var sort in sorts)
        {
            var column = table.Columns.FirstOrDefault(c => c.NiceName.Equals(sort.Name, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            if (counter > 1)
            {
                sb.Append(',');
            }

            sb.Append($"{table.Name}.{column.Name} {sort.Direction}");
   
            counter++;
        }

        return sb.ToString();
    }

    public static bool TryGetValue(this TableColumn column, object givenValue, [NotNullWhen(true)] out object? value)
    {
        if (givenValue is null)
        {
            value = null;
            return false;
        }

        var success = true;
        var type = column.GetDbType();
        if (type is NpgsqlDbType.Integer)
        {
            success = int.TryParse(givenValue.ToString(), out var number);
            value = number;
        }
        else if (type is NpgsqlDbType.Numeric)
        {
            success = decimal.TryParse(givenValue.ToString(), out var number);
            value = number;
        }
        else if (type is NpgsqlDbType.Text)
        {
            value = givenValue.ToString();
        }
        else if (type is NpgsqlDbType.Boolean)
        {
            success = bool.TryParse(givenValue.ToString()!.ToLower(), out var boolean);
            value = boolean;
        }
        else if (type is NpgsqlDbType.Bigint)
        {
            success = long.TryParse(givenValue.ToString(), out var number);
            value = number;
        }
        else if (type is NpgsqlDbType.Double)
        {
            success = double.TryParse(givenValue.ToString(), out var number);
            value = number;
        }
        else if (type is NpgsqlDbType.Name)
        {
            value = givenValue.ToString();
        }
        else if (type is NpgsqlDbType.TimestampTz)
        {
            success = DateTime.TryParse(givenValue.ToString(), out var time);
            value = time;
        }
        else if (type is NpgsqlDbType.Json or NpgsqlDbType.Jsonb)
        {
            value = JsonSerializer.Serialize(givenValue);
        }
        else
        {
            value = givenValue;
        }

        return success;
    }

    public static Table Select(this Table table, StringValues values)
    {
        var sb = new StringBuilder();
        var fields = values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        var columns = fields.Where(n => table.Columns.Any(c => c.NiceName.Equals(n.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(n => table.Columns.First(c => c.NiceName.Equals(n.Trim(), StringComparison.OrdinalIgnoreCase)));
        if (columns is null || !columns.Any())
        {
            columns = table.Columns;
        }

        return table with { Columns = columns.ToList() };
    }

    public static IList<string> SelectParts(this Table table, string value)
    {
        if (value.IsEmpty())
        {
            return new List<string>();
        }

        var list = new SortedList<int, string>
        {
            { 0, value }
        };

        var hasJoins = value.Contains('(', StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder(table.Name + '=');

        while (value.Any())
        {
            var handleJoin = false;
            var index = value.IndexOf(',');
            if (hasJoins)
            {
                handleJoin = index > value.IndexOf('(');
                if (handleJoin)
                {
                    index = value.IndexOf(')');
                }
            }

            if (index == -1)
            {
                index = value.Length;
            }

            var field = value[0..index];
            if (handleJoin)
            {
                var parts = field.Split('(');
                var joinTable = parts[0];
                list.Add(list.Count, $"{joinTable.Trim().Replace(".as.", ".")}={parts[1].Trim().Trim(')')}");
            }
            else
            {
                sb.Append(field + ",");
            }

            value = value.Remove(0, field.Length).TrimStart(',', ')');
        }

        list[0] = sb.ToString();
        return list.Values;
    }
}
