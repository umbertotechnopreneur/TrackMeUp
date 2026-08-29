namespace TrackMeUp.Cli;

/// <summary>Describes presentation-only help for one CLI command family.</summary>
internal sealed record CliCommandHelp(
    string Name,
    string SummaryKey,
    IReadOnlyList<string> Usage,
    IReadOnlyList<string> DetailKeys,
    IReadOnlyList<string> Aliases);

/// <summary>Describes one top-level shortcut that expands to a canonical command sequence.</summary>
internal sealed record CliShortcut(string Option, IReadOnlyList<string> Command, string SummaryKey);

/// <summary>Keeps slash-command normalization and help routing in one presentation-only catalog.</summary>
internal static class CliCommandCatalog
{
    private static readonly IReadOnlyList<CliCommandHelp> CanonicalCommands =
    [
        new("status", "command.status", ["/status [--watch] [--interval <1-60>]"], ["detail.jsonSnapshot"], []),
        new("runtime", "command.runtime", ["/runtime health"], ["detail.runtime"], []),
        new("tracking", "command.tracking", ["/tracking start [--safe-mode]", "/tracking pause", "/tracking toggle"], ["detail.sharedRuntime"], []),
        new("session", "command.session", ["/session last", "/session today"], [], []),
        new("system", "command.system", ["/system snapshot [--watch] [--interval <1-60>]"], ["detail.jsonSnapshot"], []),
        new("screenshot", "command.screenshot", ["/screenshot capture [--mode <all-screens|active-window>] [--keep]", "/screenshot latest", "/screenshot open-folder"], ["detail.screenshot"], []),
        new("ai", "command.ai", ["/ai status", "/ai enable", "/ai disable", "/ai configure [--provider <name>] [--model <name>] [--endpoint <uri>] [--output-detail <compact|balanced|detailed>] [--reasoning-effort <auto|none|low|medium|high|xhigh|max>]", "/ai analyze [--no-capture]", "/ai key set [--variable <allowed-name>]"], ["detail.aiKey"], []),
        new("report", "command.report", ["/report today [--output <directory>] [--open]", "/report digest [--date <yyyy-MM-dd>] [--open]"], [], []),
        new("privacy", "command.privacy", ["/privacy list", "/privacy add --type <process|title|hint> --value <text>", "/privacy remove --id <id>", "/privacy test-current"], [], []),
        new("retention", "command.retention", ["/retention status", "/retention preview", "/retention run --yes"], ["detail.retention"], []),
        new("plugins", "command.plugins", ["/plugins list", "/plugins show <id>", "/plugins enable <id>", "/plugins disable <id>"], [], []),
        new("config", "command.config", ["/config list", "/config get <key>", "/config set <key> <value>", "/config wizard"], ["detail.configWritable", "detail.configExcluded"], ["settings"]),
        new("startup", "command.startup", ["/startup status", "/startup enable", "/startup disable"], [], []),
        new("open", "command.open", ["/open ui", "/open reports", "/open screenshots"], [], []),
        new("about", "command.about", ["/about"], [], []),
        new("doctor", "command.doctor", ["/doctor"], ["detail.doctor"], ["diagnostics"]),
        new("version", "command.version", ["/version", "--version"], [], []),
        new("help", "command.help", ["/help", "/help /command", "/command --help"], ["help.slashOptional"], [])
    ];

    private static readonly IReadOnlyDictionary<string, CliCommandHelp> Lookup = BuildLookup();

    private static readonly IReadOnlyList<CliShortcut> CanonicalShortcuts =
    [
        new("--status", ["status"], "command.status"),
        new("--start", ["tracking", "start"], "action.start"),
        new("--pause", ["tracking", "pause"], "action.pause"),
        new("--toggle", ["tracking", "toggle"], "action.toggle"),
        new("--ai-on", ["ai", "enable"], "action.aiOn"),
        new("--ai-off", ["ai", "disable"], "action.aiOff"),
        new("--capture", ["screenshot", "capture"], "action.capture"),
        new("--report", ["report", "today"], "action.report"),
        new("--doctor", ["doctor"], "action.doctor")
    ];

    /// <summary>Gets canonical commands in stable display order.</summary>
    internal static IReadOnlyList<CliCommandHelp> Commands => CanonicalCommands;

    /// <summary>Gets documented top-level shortcuts in stable display order.</summary>
    internal static IReadOnlyList<CliShortcut> Shortcuts => CanonicalShortcuts;

    /// <summary>Normalizes an optional slash on the first command token.</summary>
    internal static IReadOnlyList<string> Normalize(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return arguments;
        }

        var normalized = arguments.ToArray();
        normalized[0] = NormalizeCommandToken(normalized[0]);
        return normalized;
    }

    /// <summary>Expands one standalone top-level shortcut while rejecting ambiguous extra arguments.</summary>
    internal static bool TryExpandShortcut(IReadOnlyList<string> arguments, out IReadOnlyList<string> expanded)
    {
        expanded = arguments;
        if (arguments.Count == 0)
        {
            return true;
        }

        var shortcut = CanonicalShortcuts.FirstOrDefault(item => item.Option.Equals(arguments[0], StringComparison.OrdinalIgnoreCase));
        if (shortcut is null)
        {
            return true;
        }

        if (arguments.Skip(1).Any(argument => !IsHelpFlag(argument)))
        {
            return false;
        }

        expanded = shortcut.Command.Concat(arguments.Skip(1)).ToArray();
        return true;
    }

    /// <summary>Recognizes general and command-specific help forms.</summary>
    internal static bool TryGetHelpTopic(IReadOnlyList<string> arguments, out string? topic)
    {
        topic = null;
        if (arguments.Count == 0)
        {
            return false;
        }

        var normalized = Normalize(arguments);
        var root = normalized[0];
        if (IsGeneralHelpToken(root))
        {
            if (normalized.Count > 2)
            {
                return false;
            }

            topic = normalized.Count > 1 ? NormalizeCommandToken(normalized[1]) : null;
            return true;
        }

        if (normalized.Count == 1 && IsHelpFlag(root))
        {
            topic = null;
            return true;
        }

        if (normalized.Skip(1).Count(IsHelpFlag) == 1 && IsHelpFlag(normalized[^1])
            || normalized.Count == 2 && IsGeneralHelpToken(NormalizeCommandToken(normalized[1])))
        {
            topic = root;
            return true;
        }

        return false;
    }

    /// <summary>Looks up canonical help by command name or documented alias.</summary>
    internal static bool TryGet(string? topic, out CliCommandHelp? command)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            command = null;
            return true;
        }

        return Lookup.TryGetValue(NormalizeCommandToken(topic), out command);
    }

    private static string NormalizeCommandToken(string token)
    {
        if (token is "/?" or "?")
        {
            return "help";
        }

        return token.Length > 1 && token[0] == '/' ? token[1..] : token;
    }

    private static bool IsGeneralHelpToken(string token) => token.Equals("help", StringComparison.OrdinalIgnoreCase);

    private static bool IsHelpFlag(string token) => token is "-h" or "--help";

    private static IReadOnlyDictionary<string, CliCommandHelp> BuildLookup()
    {
        var lookup = new Dictionary<string, CliCommandHelp>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in CanonicalCommands)
        {
            lookup[command.Name] = command;
            foreach (var alias in command.Aliases)
            {
                lookup[alias] = command;
            }
        }

        return lookup;
    }
}
