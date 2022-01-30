using System.Reflection;

namespace Backend.Models;

public record RequestParams(int Page = 0, int Size = 25)
{
    public Dictionary<string, Table> Tables { get; set; } = new();

    public List<Filter> Filters { get; set; } = new();

    public List<Sort> Sorts { get; set; } = new();

    public static async ValueTask<RequestParams> BindAsync(HttpContext context, ParameterInfo _)
    {
        var service = context.RequestServices.GetRequiredService<DbService>();

        var tables = await service.GetTablesAsync(context.RequestAborted);
        var requestParams = new RequestParams();

        if (!context.Request.RouteValues.TryGetValue("table", out var tableName) || tableName is null)
        {
            return requestParams;
        }

        if (int.TryParse(context.Request.Query[nameof(Page)], out var page))
        {
            page = page > 0 ? page : 0;
            requestParams = requestParams with { Page = page };
        }
        
        if (int.TryParse(context.Request.Query[nameof(Size)], out var size))
        {
            size = size > 0 ? size : 1;
            requestParams = requestParams with { Size = size };
        }

        var sorting = context.Request.Query["sort"];
        foreach (var sort in sorting)
        {
            var sortBy = Sort.GetSort(sort);
            if (sortBy is not null)
            {
                requestParams.Sorts.Add(sortBy);
            }
        }

        var table = tables.First(t => t.Name.Equals(tableName.ToString(), StringComparison.OrdinalIgnoreCase));
        var columns = table.Columns;

        var parts = table.SelectParts(context.Request.Query["select"]);
        if (parts.Any())
        {
            foreach (var part in parts)
            {
                var selectionParts = part.Split('=', StringSplitOptions.RemoveEmptyEntries);
                var selectionTableName = selectionParts[0];
                var alias = selectionTableName;
                if (selectionTableName.Contains('.', StringComparison.OrdinalIgnoreCase))
                {
                    var nameParts = selectionTableName.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    selectionTableName = nameParts[0];
                    alias = nameParts[1];
                }

                var partTable = tables.FirstOrDefault(t => t.Name.Equals(selectionTableName, StringComparison.OrdinalIgnoreCase));
                if (partTable is null)
                {
                    continue;
                }

                requestParams.Tables.Add(alias, partTable.Select(selectionParts[1]));
            }
        }
        else
        {
            requestParams.Tables.Add(table.Name, table);
        }

        var filters = context.Request.Query
            .Where(q => q.Key is not "page" and not "select" and not "size" and not "sort")
            .ToList();
        foreach (var f in filters)
        {
            if (Filter.GetFilter(f.Key, f.Value) is Filter filter)
            {
                requestParams.Filters.Add(filter);
            }
        }

        return requestParams;
    }
}
