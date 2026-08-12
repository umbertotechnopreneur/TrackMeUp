using System.Globalization;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;
using TrackMeUp.Application;

namespace TrackMeUp.Cli;

/// <summary>Renders application DTOs without querying infrastructure or changing application state.</summary>
/// <remarks>Initializes output rendering for one invocation.</remarks>
public sealed class CliOutput(CliOptions options)
{
    private readonly CliOptions _options = options;
    private bool _jsonWritten;

    /// <summary>Writes a result using the selected output contract.</summary>
    public void WriteResult<T>(OperationResult<T> result, IRenderable? richContent = null)
    {
        if (_options.Format == CliFormat.Json)
        {
            if (_jsonWritten)
            {
                return;
            }

            _jsonWritten = true;
            Console.Out.Write(JsonSerializer.Serialize(new { succeeded = result.Succeeded, code = result.Code, messageKey = result.MessageKey, value = result.Value, issues = result.Issues }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            Console.Out.WriteLine();
            return;
        }

        if (_options.Quiet && result.Succeeded)
        {
            return;
        }

        if (_options.Format == CliFormat.Plain)
        {
            Console.Out.WriteLine($"{(result.Succeeded ? Localize("ok") : Localize("error"))} {result.Code}: {Localize(result.MessageKey)}");
            if (result.Value is not null && !_options.Quiet)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
            return;
        }

        AnsiConsole.Write(RenderResult(result, richContent));
    }

    /// <summary>Writes general or command-specific CLI help without querying the runtime.</summary>
    public bool WriteHelp(string? topic = null)
    {
        if (!CliCommandCatalog.TryGet(topic, out var command))
        {
            return false;
        }

        var text = command is null ? BuildGeneralHelp() : BuildCommandHelp(command);
        if (_options.Format == CliFormat.Json)
        {
            object value = command is null
                ? new
                {
                    usage = "trackmeup.exe -cli /command [arguments] [global options]",
                    slashPrefixOptional = true,
                    commands = CliCommandCatalog.Commands.Select(item => new { command = item.Name, summary = item.Summary, aliases = item.Aliases }),
                    shortcuts = CliCommandCatalog.Shortcuts.Select(item => new { option = item.Option, command = item.Command, summary = item.Summary }),
                    globalOptions = GlobalOptions
                }
                : new
                {
                    command = command.Name,
                    command.Summary,
                    command.Usage,
                    command.Details,
                    command.Aliases
                };
            WriteResult(OperationResult<object>.Success("help.displayed", "HelpDisplayed", value));
        }
        else if (_options.Format == CliFormat.Plain)
        {
            Console.Out.WriteLine(text);
        }
        else
        {
            AnsiConsole.Write(command is null ? RenderGeneralHelp() : RenderCommandHelp(command));
        }

        return true;
    }

    /// <summary>Writes a diagnostic warning only when a human is reading the terminal.</summary>
    public void WriteDiagnostic(string message)
    {
        if (_options.Format == CliFormat.Rich)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}");
        }
        else if (_options.Format != CliFormat.Json && _options.Verbose)
        {
            Console.Error.WriteLine(message);
        }
    }

    /// <summary>Renders current dashboard data in a deliberately compact panel and table.</summary>
    public IRenderable RenderDashboard(DashboardState dashboard)
    {
        var table = new Table().Border(TableBorder.None).AddColumn(Localize("metric")).AddColumn(Localize("value"));
        table.AddRow(Localize("state"), Markup.Escape(dashboard.StatusLabel));
        table.AddRow(Localize("context"), Markup.Escape(dashboard.CurrentContext));
        table.AddRow(Localize("keys"), dashboard.TotalKeyPresses.ToString("N0"));
        table.AddRow(Localize("clicks"), dashboard.TotalMouseClicks.ToString("N0"));
        table.AddRow(Localize("activeSeconds"), dashboard.ActiveSeconds.ToString("N0"));
        table.AddRow(Localize("intensity"), dashboard.Intensity.ToString("F0") + "%");
        return new Panel(table).Header($"[bold coral1]{Markup.Escape(Localize("statusTitle"))}[/]").BorderColor(Color.Teal);
    }

    /// <summary>Writes the branded header used by the interactive command center.</summary>
    public void WriteShellHeader()
    {
        AnsiConsole.Write(new FigletText("TrackMeUp").Color(Color.Teal));
        AnsiConsole.MarkupLine("[grey70]Your local activity command center[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Renders a compact live overview for the interactive command center.</summary>
    public IRenderable RenderShellDashboard(DashboardState dashboard, AiStatus? aiStatus)
    {
        var tracking = dashboard.IsTracking ? "[green]● Tracking[/]" : "[yellow]● Paused[/]";
        var ai = aiStatus is null
            ? "[grey]Unavailable[/]"
            : aiStatus.Enabled
                ? $"[green]Enabled[/] [grey]· {Markup.Escape(aiStatus.Provider)} / {Markup.Escape(aiStatus.Model)}[/]"
                : "[grey]Disabled[/]";
        var metrics = new Table().Border(TableBorder.None)
            .AddColumn(new TableColumn("[grey]Signal[/]"))
            .AddColumn(new TableColumn("[grey]Today[/]").RightAligned());
        metrics.AddRow(Localize("keys"), dashboard.TotalKeyPresses.ToString("N0"));
        metrics.AddRow(Localize("clicks"), dashboard.TotalMouseClicks.ToString("N0"));
        metrics.AddRow(Localize("activeSeconds"), TimeSpan.FromSeconds(dashboard.ActiveSeconds).ToString("hh\\:mm\\:ss"));
        metrics.AddRow(Localize("intensity"), dashboard.Intensity.ToString("F0") + "%");

        var content = new Rows(
            new Markup($"[bold]{tracking}[/] [grey]· AI: {ai}[/]\n[grey]Context:[/] {Markup.Escape(dashboard.CurrentContext)}"),
            new Rule().RuleStyle("grey").LeftJustified(),
            metrics);
        return new Panel(content)
            .Header("[bold cyan]Live workspace[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(dashboard.IsTracking ? Color.Teal : Color.Grey);
    }

    /// <summary>Renders a system snapshot in a stable table.</summary>
    public IRenderable RenderSystemSnapshot(SystemSnapshot snapshot)
    {
        var table = new Table().Border(TableBorder.Rounded).AddColumn(Localize("metric")).AddColumn(Localize("value"));
        table.AddRow("CPU", snapshot.CpuUsagePercent + "%");
        table.AddRow("GPU", snapshot.GpuUsagePercent?.ToString() + "%" ?? Localize("notAvailable"));
        table.AddRow(Localize("memory"), $"{snapshot.MemoryUsedMb:N0}/{snapshot.MemoryTotalMb:N0} MB");
        table.AddRow(Localize("network"), $"↓ {snapshot.Network.DownloadBytesPerSecond:N0} B/s · ↑ {snapshot.Network.UploadBytesPerSecond:N0} B/s");
        foreach (var disk in snapshot.Disks)
        {
            table.AddRow(Markup.Escape(disk.Drive), $"{disk.FreeBytes:N0}/{disk.TotalBytes:N0} bytes");
        }
        return new Panel(table).Header($"[bold cyan]{Markup.Escape(Localize("systemSnapshot"))}[/]");
    }

    /// <summary>Renders the public settings catalog and current typed values without exposing internal fields.</summary>
    internal IRenderable RenderSettings(IReadOnlyList<CliSettingValue> settings)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn(Localize("setting"))
            .AddColumn(Localize("value"))
            .AddColumn(Localize("type"))
            .AddColumn(Localize("allowed"))
            .AddColumn(Localize("restart"));
        foreach (var setting in settings)
        {
            var value = setting.Value switch
            {
                null => Localize("notAvailable"),
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => setting.Value.ToString() ?? Localize("notAvailable")
            };
            table.AddRow(
                Markup.Escape(setting.Key),
                Markup.Escape(value),
                Markup.Escape(setting.ValueType),
                Markup.Escape(setting.AllowedValues.Count == 0 ? "—" : string.Join(" | ", setting.AllowedValues)),
                setting.RequiresRestart ? Localize("yes") : Localize("no"));
        }

        return new Panel(table).Header($"[bold cyan]{Markup.Escape(Localize("settings"))}[/]").BorderColor(Color.Teal);
    }

    /// <summary>Renders a generic application result, including its value when no specialized widget was supplied.</summary>
    internal IRenderable RenderResult<T>(OperationResult<T> result, IRenderable? richContent = null)
    {
        var status = result.Succeeded ? "[green]✓[/]" : "[red]✗[/]";
        var icon = _options.NoEmoji ? status : result.Succeeded ? "[green]✓[/]" : "[red]✗[/]";
        var valueText = result.Value is null
            ? string.Empty
            : "\n\n" + JsonSerializer.Serialize(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        var body = richContent ?? new Markup($"{icon} {Markup.Escape(Localize(result.MessageKey))}\n[grey70]{Markup.Escape(result.Code)}[/]{Markup.Escape(valueText)}");
        return new Panel(body).BorderColor(result.Succeeded ? Color.Teal : Color.IndianRed);
    }

    private string Localize(string key) => CliStrings.Get(_options.Language, key);

    private static readonly (string Option, string Description)[] GlobalOptionDetails =
    [
        ("--format <rich|plain|json>", "Choose the output contract."),
        ("--json", "Shortcut for machine-readable JSON output."),
        ("--language <en|it|vi|fr|de|es>", "Choose CLI display language."),
        ("--no-color", "Disable terminal color."),
        ("--no-emoji", "Use text-only status indicators."),
        ("--no-animation", "Disable animated terminal widgets."),
        ("--quiet", "Suppress successful result output."),
        ("--yes", "Confirm operations that require confirmation."),
        ("--timeout <1-300>", "Set runtime connection timeout in seconds."),
        ("--verbose", "Print diagnostic details in plain mode.")
    ];

    private static readonly string[] GlobalOptions = GlobalOptionDetails.Select(item => item.Option).ToArray();

    private string BuildGeneralHelp()
    {
        var lines = new List<string>
        {
            "TrackMeUp CLI",
            string.Empty,
            $"{Localize("usage")}: trackmeup.exe -cli /command [arguments] [global options]",
            "The leading slash is optional for the first command token.",
            string.Empty,
            $"{Localize("commands")}:"
        };
        lines.AddRange(CliCommandCatalog.Commands.Select(command => $"  /{command.Name,-12} {command.Summary}"));
        lines.AddRange([
            string.Empty,
            "Command help: /help /command or /command --help",
            string.Empty,
            "Quick switches: " + string.Join(", ", CliCommandCatalog.Shortcuts.Select(shortcut => shortcut.Option)),
            string.Empty,
            $"{Localize("globalOptions")}: {string.Join(", ", GlobalOptions)}"
        ]);
        return string.Join(Environment.NewLine, lines);
    }

    private string BuildCommandHelp(CliCommandHelp command)
    {
        var lines = new List<string>
        {
            $"TrackMeUp CLI /{command.Name}",
            command.Summary,
            string.Empty,
            $"{Localize("usage")}:"
        };
        lines.AddRange(command.Usage.Select(usage => $"  trackmeup.exe -cli {usage}"));
        if (command.Aliases.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Aliases: " + string.Join(", ", command.Aliases.Select(alias => $"/{alias}")));
        }
        if (command.Details.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(command.Details);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private IRenderable RenderGeneralHelp()
    {
        var commands = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold cyan]Command[/]"))
            .AddColumn(new TableColumn("[bold cyan]What it does[/]"));
        foreach (var command in CliCommandCatalog.Commands)
        {
            commands.AddRow($"[teal]/{Markup.Escape(command.Name)}[/]", Markup.Escape(command.Summary));
        }

        var shortcuts = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold cyan]Quick switch[/]"))
            .AddColumn(new TableColumn("[bold cyan]Action[/]"));
        foreach (var shortcut in CliCommandCatalog.Shortcuts)
        {
            shortcuts.AddRow($"[teal]{Markup.Escape(shortcut.Option)}[/]", Markup.Escape(shortcut.Summary));
        }

        var globalOptions = new Table().Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold cyan]Option[/]"))
            .AddColumn(new TableColumn("[bold cyan]Purpose[/]"));
        foreach (var option in GlobalOptionDetails)
        {
            globalOptions.AddRow($"[teal]{Markup.Escape(option.Option)}[/]", Markup.Escape(option.Description));
        }

        return new Rows(
            new Rule("[bold cyan]TrackMeUp CLI[/]").LeftJustified(),
            new Markup("[grey70]Control the same local TrackMeUp runtime from your terminal.[/]"),
            new Markup($"[grey]Usage:[/] [teal]trackmeup.exe -cli /command [arguments] [global options][/][grey]  (the first slash is optional)[/]"),
            new Panel(commands).Header("[bold]Commands[/]").BorderColor(Color.Teal),
            new Panel(shortcuts).Header("[bold]Quick switches[/]").BorderColor(Color.Teal),
            new Panel(globalOptions).Header("[bold]Global options[/]").BorderColor(Color.Grey),
            new Markup("[grey]Command help: [/][teal]/help /command[/][grey] or [/][teal]/command --help[/]"));
    }

    private static IRenderable RenderCommandHelp(CliCommandHelp command)
    {
        var usage = new Table().Border(TableBorder.Rounded).AddColumn(new TableColumn("[bold cyan]Usage[/]"));
        foreach (var item in command.Usage)
        {
            usage.AddRow($"[teal]trackmeup.exe -cli {Markup.Escape(item)}[/]");
        }

        var content = new List<IRenderable>
        {
            new Markup($"[bold]{Markup.Escape(command.Summary)}[/]"),
            usage
        };
        if (command.Aliases.Count > 0)
        {
            content.Add(new Markup($"[grey]Aliases:[/] {Markup.Escape(string.Join(", ", command.Aliases.Select(alias => "/" + alias)))}"));
        }
        content.AddRange(command.Details.Select(detail => new Markup($"[grey]{Markup.Escape(detail)}[/]")));
        return new Panel(new Rows([.. content])).Header($"[bold cyan]/{Markup.Escape(command.Name)}[/]").BorderColor(Color.Teal);
    }
}
