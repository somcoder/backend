using System.Text;

namespace Backend.Helpers;

public class QueryBuilder
{
    private readonly RequestParams _requestParams;
    private readonly List<Table> _tables;

    public QueryBuilder(RequestParams requestParams, List<Table> tables)
    {
        _requestParams = requestParams;
        _tables = tables;
    }

    public (string query, NpgsqlParameter[] parameters) ToQuery()
    {
        var sb = new StringBuilder("SELECT ");
        var columns = new StringBuilder();
        var joins = new StringBuilder();

        var mainTable = _tables.First(t => t.Name.Equals(_requestParams.Tables.First().Value.Name, StringComparison.OrdinalIgnoreCase));

        var counter = 0;
        foreach (var table in _requestParams.Tables)
        {
            if (counter is 0)
            {
                columns.AppendJoin(',', table.Value.Columns.Select(c => $"'{c.NiceName}',{table.Key}.{c.Name}"));
            }
            else
            {
                var relation = mainTable.Columns.FirstOrDefault(c => c.Relation?.Table.Equals(table.Value.Name, StringComparison.OrdinalIgnoreCase) == true);
                if (relation is null)
                {
                    relation = _tables.First(t => t.Name.Equals(table.Value.Name, StringComparison.OrdinalIgnoreCase))
                        .Columns.FirstOrDefault(c => c.Relation?.Table.Equals(mainTable.Name, StringComparison.OrdinalIgnoreCase) == true);
                    if (relation is null)
                    {
                        continue;
                    }

                    columns.Append(',');

                    var subFields = string.Join(',', table.Value.Columns.Select(c => $"'{c.NiceName}',{table.Value.Name}.{c.Name}"));
                    columns.Append($"'{table.Key}', (SELECT json_agg(json_build_object({subFields})) FROM {table.Value.Name} WHERE {table.Value.Name}.{relation.Name} = {mainTable.Name}.id)");
                    continue;
                }
                else
                {
                    var tableName = relation.Relation!.Table;
                    joins.Append($"JOIN {tableName} ON {tableName}.id = {mainTable.Name}.{relation.Name}");
                }

                columns.Append(',');

                var fields = string.Join(',', table.Value.Columns.Select(c => $"'{c.NiceName}',{table.Value.Name}.{c.Name}"));
                columns.Append($"'{table.Key}', json_build_object({fields})");
            }

            counter++;
        }

        var sort = mainTable.GetSort(_requestParams.Sorts);
        if (!sort.IsEmpty())
        {
            sort = $" ORDER BY {sort}";
        }

        sb.Append($"json_agg(json_build_object({columns}){sort})");
        sb.Append($" FROM {mainTable.Name} ");
        if (joins.Length > 0)
        {
            sb.AppendLine();
            sb.Append(joins);
        }

        var (filter, parameters) = mainTable.GetFilters(_requestParams.Filters);
        if (!filter.IsEmpty())
        {
            sb.AppendLine();
            sb.Append($"WHERE {filter}");
        }

        sb.AppendLine();
        sb.Append($"OFFSET {_requestParams.Page * _requestParams.Size} LIMIT {_requestParams.Size}");

        return (sb.ToString(), parameters);
    }
}
