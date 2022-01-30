namespace Backend.Data;

public class AuthService : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;

    public AuthService(NpgsqlConnection connection) => _connection = connection;

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken token = default)
    {
        var message = "";
        var jwt = "";
        try
        {
            jwt = await GenerateTokenAsync(email, password, token);
        }
        catch (PostgresException ex)
        {
            message = ex.MessageText;
        }

        return new LoginResult
        {
            Message = message,
            Token = jwt
        };
    }

    private async Task<string> GenerateTokenAsync(string email, string password, CancellationToken token)
    {
        var command = new NpgsqlCommand($"SELECT login($1, $2)", _connection);
        command.Parameters.Add(new() { Value = email });
        command.Parameters.Add(new() { Value = password });

        await OpenAsync(token);
        return await command.ExecuteScalarAsync(token) as string ?? "";
    }

    private async Task<bool> CreateUserAsync(AppUser user, string role = "anonymous", CancellationToken token = default)
    {
        using var command = new NpgsqlCommand("INSERT INTO auth.users (email, full_name, social_id, role) VALUES($1, $2, $3, $4)", _connection);
        command.Parameters.Add(new() { Value = user.Email });
        command.Parameters.Add(new() { Value = user.FullName });
        command.Parameters.Add(new() { Value = user.SocialId });
        command.Parameters.Add(new() { Value = role });

        await OpenAsync(token);
        return await command.ExecuteNonQueryAsync(token) > 0;
    }

    public async Task<AppUser> GetUserAsync(string email, CancellationToken token = default)
    {
        using var command = new NpgsqlCommand("SELECT * FROM auth.users WHERE email = $1", _connection);
        command.Parameters.Add(new() { Value = email });

        await OpenAsync(token);

        using var reader = await command.ExecuteReaderAsync(token);
        var user = new AppUser();
        if (await reader.ReadAsync(token))
        {
            user = user with
            {
                Email = email,
                FullName = reader["full_name"]?.ToString() ?? "",
                SocialId = reader["social_id"]?.ToString() ?? ""
            };
        }

        return user;
    }

    private async Task OpenAsync(CancellationToken token = default)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(token);
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _connection.CloseAsync();
}