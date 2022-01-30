using System.Security.Claims;
using System.Text;

namespace Backend.Data;
public class DbService : IAsyncDisposable
{
    private static readonly SortedList<string, Table> _tableNames = new();
    private static readonly Dictionary<string, Function> _functions = new();

    private readonly NpgsqlConnection _connection;
    private readonly IHttpContextAccessor _contextAccessor;

    public DbService(NpgsqlConnection connection, IHttpContextAccessor contextAccessor)
    {
        _connection = connection;
        _contextAccessor = contextAccessor;

        _connection.Notice += Connection_Notice;
    }

    private void Connection_Notice(object sender, NpgsqlNoticeEventArgs e)
    {
        Console.WriteLine($"Notice received from {sender}!");
        Console.WriteLine($"Notice: {e.Notice.MessageText}");
    }

    public async Task<string> GetAsync(RequestParams requestParams, CancellationToken token = default)
    {
        await OpenAsync(token);
        var builder = new QueryBuilder(requestParams, _tableNames.Values.ToList());
        var (query, parameters) = builder.ToQuery();

        Console.WriteLine($"Final query: {query}");
        using var command = new NpgsqlCommand(query, _connection);
        command.Parameters.AddRange(parameters);

        return await command.ExecuteScalarAsync(token) as string ?? "[]";
    }

    public async Task<string> GetSingleAsync(string tableName, int id, CancellationToken token = default)
    {
        var table = _tableNames[tableName]!;
        var idType = table.Columns.First().Type;
        var columns = string.Join(',', table.Columns.Select(c => $"'{c.NiceName}', {c.Name}"));

        using var command = new NpgsqlCommand($"SELECT json_build_object({columns}) FROM {tableName} WHERE id = @id::{idType}", _connection);
        command.Parameters.AddWithValue("@id", id);

        await OpenAsync(token);
        return await command.ExecuteScalarAsync(token) as string ?? "";
    }

    public async Task<bool> AddAsync(Table table, Dictionary<string, object> data, CancellationToken token = default)
    {
        var (columns, values, parameters) = table.GetParameters(data);

        using var command = new NpgsqlCommand($"INSERT INTO {table.Name} ({columns}) VALUES({values})", _connection);
        command.Parameters.AddRange(parameters);

        await OpenAsync(token);
        return await command.ExecuteNonQueryAsync(token) > 0;
    }

    public async Task<bool> AddAsync(Table table, string data, CancellationToken token = default)
    {
        var keys = JsonSerializer.Deserialize<Dictionary<string, object>>(data)!.Keys;

        var columns = new StringBuilder();
        var columnDefinitions = new StringBuilder();
        var counter = 0;
        foreach (var key in keys)
        {
            var column = table.Columns.FirstOrDefault(c => c.NiceName.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                continue;
            }

            if (counter > 0)
            {
                columns.Append(',');
                columnDefinitions.Append(',');
            }

            columns.Append(column.Name);
            columnDefinitions.Append($"{column.Name} {column.Type}");
            counter++;
        }

        using var command = new NpgsqlCommand($"INSERT INTO {table.Name} ({columns}) SELECT * FROM jsonb_to_record('{data}') AS x({columnDefinitions})", _connection);
        Console.WriteLine($"final query: {command.CommandText}");

        await OpenAsync(token);
        return await command.ExecuteNonQueryAsync(token) > 0;
    }
    
    public async Task<bool> UpdateAsync(string tableName, int id, Dictionary<string, object> data, CancellationToken token = default)
    {
        var table = _tableNames[tableName]!;
        var (columns, _, parameters) = table.GetParameters(data, true);

        using var command = new NpgsqlCommand($"UPDATE {table.Name} SET {columns} WHERE id = {id}", _connection);
        command.Parameters.AddRange(parameters);

        await OpenAsync(token);
        return await command.ExecuteNonQueryAsync(token) > 0;
    }

    public async Task<object?> RunRpcAsync(Function function, Dictionary<string, object> data, CancellationToken token = default)
    {
        var (query, parameters) = function.ToQuery(data);

        using var command = new NpgsqlCommand(query, _connection);
        command.Parameters.AddRange(parameters);
        command.AllResultTypesAreUnknown = true;

        Console.WriteLine($"Final RPC command: {command.CommandText}");

        await OpenAsync(token);
        if (function.IsVoid)
        {
            await command.ExecuteNonQueryAsync(token);
            return null;
        }

        var reader = await command.ExecuteReaderAsync(token);
        object? result = null;
        if (await reader.ReadAsync(token))
        {
            result = reader[0];
        }

        Console.WriteLine($"Result: {result}");
        return result;
    }

    public async Task<IList<Table>> GetTablesAsync(CancellationToken token = default)
    {
        if (!_tableNames.Any())
        {
            using var connection = new NpgsqlConnection(_connection.ConnectionString);
            var query = @"
                        SELECT json_agg(tables) FROM (SELECT c.relname AS name, n.nspname AS schema,
                        json_agg(json_build_object('name', a.attname, 'type', format_type(a.atttypid, NULL), 'nullable', a.attnotnull = false, 'position', a.attnum,
	                        'relation', (SELECT json_build_object('constraint', con.conname, 'table', (SELECT rel.relname FROM pg_class AS rel WHERE rel.oid = con.confrelid)) FROM pg_constraint AS con WHERE con.conrelid = c.oid AND con.contype = 'f' AND ARRAY[a.attnum] <@ con.conkey))
                        ) AS columns
                        FROM pg_class AS c
                        JOIN pg_namespace AS n ON n.oid = c.relnamespace
                        JOIN pg_attribute AS a ON a.attrelid = c.oid AND a.attnum > 0
                        WHERE (c.relkind = 'r' OR c.relkind = 'v') AND n.nspname = 'public'
                        GROUP BY c.relname, n.nspname) AS tables";
            using var command = new NpgsqlCommand(query, connection);

            await connection.OpenAsync(token);

            using var reader = await command.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                if (JsonSerializer.Deserialize<List<Table>>(reader.GetString(0), options) is List<Table> tables)
                {
                    foreach (var table in tables)
                    {
                        _tableNames.Add(table.Name, table with { Columns = table.Columns.OrderBy(c => c.Position).ToList() });
                    }
                }
            }
        }

        return _tableNames.Values;
    }

    public async Task<List<Function>> GetFunctionsAsync(CancellationToken token = default)
    {
        if (!_functions.Any())
        {
            using var command = new NpgsqlCommand("SELECT json_build_object('schema', schema, 'name', name, 'type', return_type, 'parameters', args) FROM app_functions", _connection);

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync(token);
            }

            using var reader = await command.ExecuteReaderAsync(token);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            while (await reader.ReadAsync(token))
            {
                if (JsonSerializer.Deserialize<Function>(reader.GetString(0), options) is Function function)
                {
                    _functions.TryAdd(function.Name, function);
                }
            }
        }

        return _functions.Values.ToList();
    }

    private async Task OpenAsync(CancellationToken token = default)
    {
        var user = _contextAccessor.HttpContext?.User ?? new(new ClaimsIdentity(new List<Claim> { new("role", "web_anonymous") }));
        Console.WriteLine($"Claims: {user.Claims.Count()}");
        var batch = new NpgsqlBatch(_connection);

        var role = user.FindFirstValue(ClaimTypes.Role) ?? "web_anonymous";
        batch.BatchCommands.Add(new($"SET ROLE TO {role}"));
        batch.BatchCommands.Add(new($"SET jwt.claims.role TO {role}"));

        var name = user.FindFirstValue(ClaimTypes.Name);
        if (!string.IsNullOrEmpty(name))
        {
            batch.BatchCommands.Add(new($"SET jwt.claims.name TO '{name}'"));
        }
        
        //var userId = user.FindFirstValue("id");
        //if (!string.IsNullOrEmpty(name))
        //{
        //    batch.BatchCommands.Add(new($"SET jwt.claims.id TO {userId}"));
        //}

        var email = user.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(email))
        {
            batch.BatchCommands.Add(new($"SET jwt.claims.email TO '{email}'"));

            // TODO: Run check_token() here to see if the user is still active or not.
        }

        if (_connection.State is not System.Data.ConnectionState.Open and not System.Data.ConnectionState.Connecting)
        {
            await _connection.OpenAsync(token);
        }

        await batch.ExecuteNonQueryAsync(token);
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine($"Closing connection asynchronously!");
        await _connection.CloseAsync();
    }
}
