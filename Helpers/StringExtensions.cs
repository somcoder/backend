using System.Text;

namespace Backend.Helpers;

public static class StringExtensions
{
    private enum SnakeCaseState
    {
        Start,
        Lower,
        Upper,
        NewWord
    }

    public static string ToSnakeCase(this string input)
    {
        const char separator = '_';
        if (input.IsEmpty())
        {
            return "";
        }

        var sb = new StringBuilder();
        var state = SnakeCaseState.Start;

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] == ' ')
            {
                if (state != SnakeCaseState.Start)
                {
                    state = SnakeCaseState.NewWord;
                }
            }
            else if (char.IsUpper(input[i]))
            {
                if (state == SnakeCaseState.Upper)
                {
                    var hasNext = i + 1 < input.Length;
                    if (i > 0 && hasNext)
                    {
                        var nextChar = input[i + 1];
                        if (!char.IsUpper(nextChar) && nextChar != separator)
                        {
                            sb.Append(separator);
                        }
                    }
                }
                else if (state is SnakeCaseState.Lower or SnakeCaseState.NewWord)
                {
                    sb.Append(separator);
                }

                sb.Append(char.ToLowerInvariant(input[i]));
                state = SnakeCaseState.Upper;
            }
            else if (input[i] == separator)
            {
                sb.Append(separator);
                state = SnakeCaseState.Start;
            }
            else
            {
                if (state == SnakeCaseState.NewWord)
                {
                    sb.Append(separator);
                }

                sb.Append(input[i]);
                state = SnakeCaseState.Lower;
            }
        }

        return sb.ToString();
    }

    public static string ToCamelCase(this string input)
    {
        if (input.IsEmpty())
        {
            return "";
        }

        var parts = input.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Trim().ToLower();
        }

        var sb = new StringBuilder();
        var counter = 0;
        while (counter < parts.Length)
        {
            var word = parts[counter];
            if (counter == 0)
            {
                sb.Append(word.ToLower());
            }
            else
            {
                sb.Append(word[0..1].ToUpper() + word[1..].ToLower());
            }

            counter++;
        }

        return sb.ToString();
    }

    public static bool IsEmpty(this string? input) => string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input);

    public static string Capitalize(this string input)
        => input.IsEmpty() ? "" : input[0..1].ToUpper() + input[1..].ToLower();
}
