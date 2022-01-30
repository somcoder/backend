using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Backend.ViewModels;

public record LoginViewModel([Required]string Email, string Password);

public record LoginResult
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Token { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }
}
