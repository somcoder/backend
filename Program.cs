using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(config => config.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient();
builder.Services.AddScoped<NpgsqlConnection>(_ => new(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHttpContextAccessor();

builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(30));

builder.Services.AddScoped<DbService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(config =>
    {
        config.Audience = "wala_wala";
        config.SaveToken = true;
        config.RequireHttpsMetadata = false;

        var secret = "wala_wala_secret_with_at_least_32_characters_l0ng";
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        config.TokenValidationParameters = new()
        {
            ValidateActor = true,
            ValidIssuer = "https://wala-wala.com",
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RoleClaimType = "role",
            ValidAudience = "wala_wala",
            IssuerSigningKey = key
        };
    });

var app = builder.Build();

app.UseCors();
app.UseAuthorization();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/data", StringComparison.OrdinalIgnoreCase))
    {
        var service = context.RequestServices.GetRequiredService<DbService>();
        var tables = await service.GetTablesAsync(context.RequestAborted);

        if (context.Request.RouteValues.TryGetValue("table", out var table))
        {
            if (tables.Any(t => t.Name.Equals(table!.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                await next();
                return;
            }

            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Requested resource could not be recognized."
            });

            return;
        }
    }

    await next();
});

app.MapGet("/roles/permissions", () =>
{
    var permissions = PermissionUtils.GetPermissionGroups();
    return Results.Ok(permissions);
});

app.MapPost("/roles", async (HttpContext context, [FromBody] NewRole newRole) =>
{
    await Task.CompletedTask;
    var permissions = PermissionUtils.GetPermissionGroups().SelectMany(g => g.Permissions);
    var granted = permissions
    .Where(p => newRole.Permissions.Any(rp => rp.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
    .Select(p => new
    {
        p.Name,
        p.Title,
        Grant = p.ToGrant("admin")
    });

    return Results.Json(granted, statusCode: 201);
});

app.MapGet("/data", async (DbService service, string? table, bool? minimal) =>
{
    var tables = await service.GetTablesAsync();
    if (!string.IsNullOrEmpty(table))
    {
        tables = tables.Where(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    if (minimal is true)
    {
        var result = tables.Select(t => new
        {
            t.Name,
            Columns = t.Columns.Select(c => new { c.NiceName, c.Type })
        });

        return Results.Ok(result);
    }

    return Results.Ok(tables);
});

app.MapGet("/data/{table}", async (string table, RequestParams requestParams, DbService service) =>
{
    var data = await service.GetAsync(requestParams);

    return Results.Content(data, System.Net.Mime.MediaTypeNames.Application.Json, System.Text.Encoding.UTF8);
});

app.MapGet("/data/{table}/{id}", async (string table, int id, DbService service) =>
{
    var data = await service.GetSingleAsync(table, id);
    return data.IsEmpty() switch
    {
        true => Results.NotFound(new { message = "The requested resource could not be found." }),
        false => Results.Content(data, System.Net.Mime.MediaTypeNames.Application.Json, System.Text.Encoding.UTF8)
    };
});

app.MapPost("/data/{table}", async (HttpContext context, string table, DbService service) =>
{
    if (!context.Request.HasJsonContentType())
    {
        return Results.Conflict(new
        {
            message = "Unsupported media type, only json is supported."
        });
    }

    var tables = await service.GetTablesAsync(context.RequestAborted);
    var targetTable = tables.FirstOrDefault(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase));

    var rawData = await context.Request.GetRawBodyStringAsync();
    var result = await service.AddAsync(targetTable!, rawData, context.RequestAborted);

    return Results.Ok(new
    {
        result
    });
});

app.MapPut("/data/{table}/{id}", async (HttpContext context, string table, int id, DbService service) =>
{
    if (!context.Request.HasJsonContentType())
    {
        return Results.Conflict(new
        {
            message = "Unsupported media type, only json is supported."
        });
    }

    var data = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>(context.RequestAborted);
    var result = await service.UpdateAsync(table, id, data!, context.RequestAborted);

    return Results.Ok(new
    {
        result,
        data
    });
});

// RPC PostgreSQL Functions.
app.MapPost("/rpc/{function}", async (HttpContext context, Function? function, DbService service) =>
{
    if (function is null)
    {
        return Results.NotFound(new { message = "Cannot recognize the requested function!" });
    }

    global::System.Console.WriteLine($"Run RPC: {function.Name}");
    if (!context.Request.HasJsonContentType() && function.Parameters.Any())
    {
        return Results.Conflict(new
        {
            message = $"Function '{function.Name}' requires {function.Parameters.Count} arguments which was not supplied!"
        });
    }

    var data = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>(context.RequestAborted) ?? new();
    var result = await service.RunRpcAsync(function, data, default);

    return Results.Ok(new
    {
        message = "Function performed successfully.",
        result
    });
});

// Login.
app.MapPost("auth/login", async (HttpContext context, [FromBody] LoginViewModel input, AuthService service) =>
{
    if (string.IsNullOrEmpty(input.Email))
    {
        return Results.BadRequest(new
        {
            message = "Login provider is required."
        });
    }

    if (string.IsNullOrEmpty(input.Password))
    {
        return Results.BadRequest(new
        {
            message = "Login failed, please try again."
        });
    }

    var result = await service.LoginAsync(input.Email, input.Password, context.RequestAborted);
    if (result.Token.IsEmpty())
    {
        if (result.Message.IsEmpty())
        {
            result = result with
            {
                Message = "Login failed, please try again."
            };
        }

        return Results.BadRequest(result);
    }

    return Results.Ok(result);
});

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    AllowStatusCode404Response = true,
    ExceptionHandler = async (context) =>
    {
        var message = "An error has occurred while processing your request, please try again.";
        var trace = "";
        var code = StatusCodes.Status500InternalServerError;

        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error ?? new Exception($"Unknown exception occurred in: {context.Request.Path}");
        if (exception is PostgresException postgresException)
        {
            if (postgresException.SqlState.Equals("42501", StringComparison.OrdinalIgnoreCase))
            {
                message = "You are not allowed to access this recourse!";
                trace = postgresException.MessageText;
                code = StatusCodes.Status403Forbidden;

                if (context.User?.Identity?.IsAuthenticated == false)
                {
                    message = "Your session has been expired, please login again.";
                    code = StatusCodes.Status401Unauthorized;
                }
            }
            else if (postgresException.SqlState.Equals("P0001", StringComparison.OrdinalIgnoreCase))
            {
                message = "Invalid input state!";
                trace = postgresException.MessageText;
                code = StatusCodes.Status400BadRequest;
            }
            else if (postgresException.SqlState.StartsWith("22", StringComparison.OrdinalIgnoreCase))
            {
                message = "Invalid input state!";
                trace = postgresException.MessageText;
                code = StatusCodes.Status400BadRequest;
            }
            else if (postgresException.SqlState.Equals("28P01", StringComparison.OrdinalIgnoreCase))
            {
                code = StatusCodes.Status400BadRequest;
                message = postgresException.MessageText;
            }
            else if (postgresException.SqlState.Equals("08P01", StringComparison.OrdinalIgnoreCase))
            {
                message = postgresException.MessageText;
            }
            else if (postgresException.SqlState.Equals("42883", StringComparison.OrdinalIgnoreCase))
            {
                code = StatusCodes.Status400BadRequest;
                message = postgresException.MessageText;
            }
            else
            {
                message = "Something went wrong, please try again.";
                trace = postgresException.MessageText;
            }
        }
        else if (exception is ArgumentNullException argumentException)
        {
            code = StatusCodes.Status400BadRequest;
            message = exception.Message;
        }
        else
        {
            message = exception.Message;
            trace = exception.StackTrace;
        }

        context.Response.StatusCode = code;

        object result = app.Environment.IsDevelopment() switch
        {
            true => new { message, trace },
            false => new { message, trace } // TODO: Remove trace in production.
        };

        await context.Response.WriteAsJsonAsync(result);
    }
});

app.Run();
