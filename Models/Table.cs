using System.Text.Json.Serialization;
using NpgsqlTypes;

namespace Backend.Models;

public record Table
{
    public string Name { get; set; } = "";

    public string NiceName => Name.ToCamelCase();

    public string Schema { get; set; } = "public";

    public List<TableColumn> Columns { get; set; } = new();
}

public record TableColumn
{
    private string _name = "";
    private string _type = "";

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            NiceName = value.ToCamelCase();
        }
    }

    public string NiceName { get; private set; } = "";

    public int Position { get; set; }

    public string Type
    {
        get => _type;
        set
        {
            _type = value;

            if (value.Equals("int4", StringComparison.OrdinalIgnoreCase))
            {
                _type = nameof(NpgsqlDbType.Integer);
            }
            else if (value.Equals("int8", StringComparison.OrdinalIgnoreCase))
            {
                _type = nameof(NpgsqlDbType.Bigint);
            }
            else if (value.Equals("int2", StringComparison.OrdinalIgnoreCase))
            {
                _type = nameof(NpgsqlDbType.Smallint);
            }
            else if (value.Equals("bool", StringComparison.OrdinalIgnoreCase))
            {
                _type = nameof(NpgsqlDbType.Boolean);
            }

            // TODO: Add other types as needed.
        }
    }

    public bool Nullable { get; set; }

    public string UdtName { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Relation? Relation { get; set; }

    public NpgsqlDbType GetDbType()
    {
        if (!Enum.TryParse<NpgsqlDbType>(Type, true, out var type))
        {
            Console.WriteLine($"Unsupported column type detected: {Type} for {Name}");
            return NpgsqlDbType.Unknown;
        }

        return type;
    }

    public NpgsqlDbType GetSubDbType()
    {
        var type = GetDbType();
        if (type == NpgsqlDbType.Array)
        {
            type |= UdtName switch
            {
                "_int4" => NpgsqlDbType.Integer,
                "_text" => NpgsqlDbType.Text,
                _ => NpgsqlDbType.Unknown,
            };
        }

        return type;
    }
}

public record Relation
{
    public string Constraint { get; set; } = "";

    public string Table { get; set; } = "";
}
