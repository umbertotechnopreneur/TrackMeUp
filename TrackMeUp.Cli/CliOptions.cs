namespace TrackMeUp.Cli;

/// <summary>Defines the supported automation-safe output modes.</summary>
public enum CliFormat
{
    /// <summary>Uses rich Spectre.Console widgets on an interactive terminal.</summary>
    Rich,
    /// <summary>Uses simple text with no ANSI control sequences.</summary>
    Plain,
    /// <summary>Writes one machine-readable JSON document.</summary>
    Json
}

/// <summary>Stores parsed global CLI options independently from command-specific arguments.</summary>
public sealed record CliOptions(
    CliFormat Format,
    string Language,
    bool NoColor,
    bool NoEmoji,
    bool NoAnimation,
    bool Quiet,
    bool Yes,
    int TimeoutSeconds,
    bool Verbose,
    IReadOnlyList<string> CommandArguments)
{
    private static readonly string[] SupportedLanguages = ["system", "en", "it", "vi", "fr", "de", "es"];

    /// <summary>Parses global flags and leaves command arguments intact.</summary>
    public static CliOptions Parse(IReadOnlyList<string> arguments, bool redirected)
    {
        var format = redirected ? CliFormat.Plain : CliFormat.Rich;
        var language = "system";
        var noColor = redirected;
        var noEmoji = false;
        var noAnimation = redirected;
        var quiet = false;
        var yes = false;
        var timeout = 5;
        var verbose = false;
        var remaining = new List<string>();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--format":
                    format = ParseFormat(ReadRequiredValue(arguments, ref index, "format"));
                    break;
                case "--json": format = CliFormat.Json; break;
                case "--language": language = ReadRequiredValue(arguments, ref index, "language"); break;
                case "--no-color": noColor = true; break;
                case "--no-emoji": noEmoji = true; break;
                case "--no-animation": noAnimation = true; break;
                case "--quiet": quiet = true; break;
                case "--yes": yes = true; break;
                case "--timeout":
                    var timeoutValue = ReadRequiredValue(arguments, ref index, "timeout");
                    if (!int.TryParse(timeoutValue, out var parsed) || parsed is <= 0 or > 300)
                    {
                        throw new ArgumentException("timeout must be between 1 and 300 seconds");
                    }
                    timeout = parsed;
                    break;
                case "--verbose": verbose = true; break;
                default: remaining.Add(argument); break;
            }
        }

        if (format is CliFormat.Plain or CliFormat.Json)
        {
            noColor = true;
            noAnimation = true;
        }

        if (!SupportedLanguages.Contains(language, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("language must be system, en, it, vi, fr, de, or es");
        }

        return new CliOptions(format, language.ToLowerInvariant(), noColor, noEmoji, noAnimation, quiet, yes, timeout, verbose, remaining);
    }

    private static string ReadRequiredValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (index + 1 < arguments.Count)
        {
            return arguments[++index];
        }

        throw new ArgumentException($"{option} requires a value");
    }

    private static CliFormat ParseFormat(string value) => value.ToLowerInvariant() switch
    {
        "rich" => CliFormat.Rich,
        "plain" => CliFormat.Plain,
        "json" => CliFormat.Json,
        _ => throw new ArgumentException("format must be rich, plain, or json")
    };
}

/// <summary>Maps stable application result codes to documented process exit codes.</summary>
public static class ExitCodeMapper
{
    /// <summary>Maps a stable result code to one automation-safe exit code.</summary>
    public static int Map(string code) => code switch
    {
        "operation.cancelled" => 130,
        "command.invalid" or "command.arguments.invalid" => 2,
        var value when value.Contains("validation", StringComparison.Ordinal) || value.EndsWith(".invalid", StringComparison.Ordinal) || value.EndsWith(".required", StringComparison.Ordinal) => 3,
        "runtime.unavailable" => 4,
        "privacy.blocked" => 5,
        "ai.disabled" or "ai.configuration.invalid" => 6,
        "ai.cost_guardrail" => 7,
        "ipc.protocol.unsupported" => 9,
        var value when value.EndsWith(".failed", StringComparison.Ordinal) => 8,
        _ => 10
    };
}
