namespace TrackMeUp.Cli;

/// <summary>Describes presentation-only help for one CLI command family.</summary>
internal sealed record CliCommandHelp(
    string Name,
    string Summary,
    IReadOnlyList<string> Usage,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Aliases);

/// <summary>Keeps slash-command normalization and help routing in one presentation-only catalog.</summary>
internal static class CliCommandCatalog
{
    private static readonly IReadOnlyList<CliCommandHelp> CanonicalCommands =
    [
        new("status", "Show the live tracking dashboard.", ["/status [--watch] [--interval <1-60>]"], ["JSON output supports a single snapshot only."], []),
        new("runtime", "Inspect the shared runtime endpoint.", ["/runtime health"], ["Reports protocol, ownership, version, advertised capabilities, local logging, and redacted Sentry status."], []),
        new("tracking", "Start, pause, or toggle activity tracking.", ["/tracking start [--safe-mode]", "/tracking pause", "/tracking toggle"], ["Every action is sent to the single shared runtime."], []),
        new("session", "Read recent activity summaries.", ["/session last", "/session today"], [], []),
        new("focus", "Operate an objective-based focus session.", ["/focus start --objective <text>", "/focus status", "/focus stop [--summarize]"], [], []),
        new("system", "Capture a CPU, GPU, memory, network, and disk snapshot.", ["/system snapshot [--watch] [--interval <1-60>]"], ["JSON output supports a single snapshot only."], []),
        new("screenshot", "Capture or inspect privacy-checked screenshots.", ["/screenshot capture [--mode <all-screens|active-window>] [--keep] [--watermark]", "/screenshot latest", "/screenshot open-folder"], ["Capture policy and privacy checks remain enforced by the application service."], []),
        new("ai", "Inspect and operate the configured AI integration.", ["/ai status", "/ai enable", "/ai disable", "/ai configure [--provider <name>] [--model <name>] [--endpoint <uri>] [--output-detail <compact|balanced|detailed>] [--reasoning-effort <auto|none|low|medium|high|xhigh|max>]", "/ai analyze [--no-capture]", "/ai key set [--variable <allowed-name>]"], ["The API key is requested with a hidden interactive prompt. Without --variable, the configured provider key-variable name is loaded through the application facade. Secret command-line arguments are never accepted."], []),
        new("report", "Generate activity reports and daily digests.", ["/report today [--output <directory>] [--open]", "/report digest [--date <yyyy-MM-dd>] [--open]"], [], []),
        new("privacy", "List, add, remove, and test privacy rules.", ["/privacy list", "/privacy add --type <process|title|hint> --value <text>", "/privacy remove --id <id>", "/privacy test-current"], [], []),
        new("retention", "Inspect or execute the configured retention policy.", ["/retention status", "/retention preview", "/retention run --yes"], ["Deletion is rejected unless explicit confirmation is present."], []),
        new("plugins", "Inspect and toggle application-context providers.", ["/plugins list", "/plugins show <id>", "/plugins enable <id>", "/plugins disable <id>"], [], []),
        new("config", "Read or patch whitelisted application settings.", ["/config list", "/config get <key>", "/config set <key> <value>", "/config wizard"], [CliSettingsCatalog.HelpSummary, "Secret values, installation identity, privacy-rule contents, history markers, and accumulated cost state are outside this surface."], ["settings"]),
        new("startup", "Inspect or change Windows startup registration.", ["/startup status", "/startup enable", "/startup disable"], [], []),
        new("open", "Open a safe application surface or managed folder.", ["/open ui", "/open reports", "/open screenshots"], [], []),
        new("about", "Show product, license, and safe link information.", ["/about"], [], []),
        new("doctor", "Run a read-only diagnostic sweep through the application facade.", ["/doctor"], ["Checks runtime, logging/Sentry status, dashboard, AI, retention, startup, and plugins without printing secrets or private paths."], ["diagnostics"]),
        new("version", "Show CLI and runtime protocol versions without starting the runtime.", ["/version", "--version"], [], []),
        new("help", "Show general or command-specific help without starting the runtime.", ["/help", "/help /command", "/command --help"], ["The leading slash is optional for the first command token."], [])
    ];

    private static readonly IReadOnlyDictionary<string, CliCommandHelp> Lookup = BuildLookup();

    /// <summary>Gets canonical commands in stable display order.</summary>
    internal static IReadOnlyList<CliCommandHelp> Commands => CanonicalCommands;

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
            topic = normalized.Count > 1 ? NormalizeCommandToken(normalized[1]) : null;
            return true;
        }

        if (normalized.Count == 1 && IsHelpFlag(root))
        {
            topic = null;
            return true;
        }

        if (normalized.Skip(1).Any(IsHelpFlag) || normalized.Count == 2 && IsGeneralHelpToken(NormalizeCommandToken(normalized[1])))
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
