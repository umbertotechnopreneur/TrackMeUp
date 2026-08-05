using Spectre.Console;
using TrackMeUp.Application;

namespace TrackMeUp.Cli;

/// <summary>Routes one-shot commands and the REPL through the same application facade calls.</summary>
public sealed class CliRouter
{
    private readonly ITrackMeUpApplication _application;
    private readonly CliOutput _output;
    private readonly CliOptions _options;

    /// <summary>Initializes a router with presentation-only dependencies.</summary>
    public CliRouter(ITrackMeUpApplication application, CliOutput output, CliOptions options)
    {
        _application = application;
        _output = output;
        _options = options;
    }

    /// <summary>Runs a command token sequence, or opens the persistent shell when none was supplied.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.Count == 0)
        {
            return await RunShellAsync(cancellationToken);
        }

        try
        {
            return await DispatchAsync(CliCommandCatalog.Normalize(arguments), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _output.WriteResult(OperationResult<object>.Failure("operation.cancelled", "OperationCancelled"));
            return 130;
        }
    }

    private async Task<int> DispatchAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (CliCommandCatalog.TryGetHelpTopic(arguments, out var helpTopic))
        {
            return WriteHelp(helpTopic);
        }

        var root = arguments[0].ToLowerInvariant();
        return root switch
        {
            "status" => await StatusAsync(arguments, cancellationToken),
            "runtime" => await RuntimeAsync(arguments, cancellationToken),
            "tracking" => await TrackingAsync(arguments, cancellationToken),
            "session" => await SessionAsync(arguments, cancellationToken),
            "focus" => await FocusAsync(arguments, cancellationToken),
            "system" => await SystemAsync(arguments, cancellationToken),
            "screenshot" => await ScreenshotAsync(arguments, cancellationToken),
            "ai" => await AiAsync(arguments, cancellationToken),
            "report" => await ReportAsync(arguments, cancellationToken),
            "privacy" => await PrivacyAsync(arguments, cancellationToken),
            "retention" => await RetentionAsync(arguments, cancellationToken),
            "plugins" => await PluginsAsync(arguments, cancellationToken),
            "config" => await ConfigAsync(arguments, cancellationToken),
            "settings" => await ConfigAsync(arguments, cancellationToken),
            "startup" => await StartupAsync(arguments, cancellationToken),
            "open" => await OpenAsync(arguments, cancellationToken),
            "about" => await WriteAsync(_application.GetProductInformationAsync(cancellationToken)),
            "doctor" => await DoctorAsync(cancellationToken),
            "diagnostics" => arguments.Count == 1 ? await DoctorAsync(cancellationToken) : InvalidCommand(),
            "version" => WriteResult(CliBootstrap.CreateVersionResult()),
            _ => InvalidCommand()
        };
    }

    private async Task<int> RuntimeAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "health" => await WriteAsync(_application.GetRuntimeHealthAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> StatusAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var watch = arguments.Contains("--watch", StringComparer.OrdinalIgnoreCase);
        var interval = ReadIntegerOption(arguments, "--interval", 2, 1, 60);
        if (!watch)
        {
            var result = await _application.GetDashboardAsync(cancellationToken);
            _output.WriteResult(result, result.Value is null ? null : _output.RenderDashboard(result.Value));
            return result.Succeeded ? 0 : ExitCodeMapper.Map(result.Code);
        }

        return await WatchAsync(
            _application.GetDashboardAsync,
            _output.RenderDashboard,
            interval,
            cancellationToken);
    }

    private async Task<int> TrackingAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "start" => await WriteAsync(_application.StartTrackingAsync(new StartTrackingRequest(arguments.Contains("--safe-mode", StringComparer.OrdinalIgnoreCase), "cli"), cancellationToken)),
        "pause" => await WriteAsync(_application.PauseTrackingAsync(cancellationToken)),
        "toggle" => await WriteAsync(_application.ToggleTrackingAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> SessionAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "last" => await WriteAsync(_application.GetLastSessionAsync(cancellationToken)),
        "today" => await WriteAsync(_application.GetTodaySummaryAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> FocusAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "start" => await WriteAsync(_application.StartFocusSessionAsync(new StartFocusSessionRequest(ReadOption(arguments, "--objective") ?? string.Empty), cancellationToken)),
        "status" => await WriteAsync(_application.GetFocusSessionAsync(cancellationToken)),
        "stop" => await WriteAsync(_application.StopFocusSessionAsync(arguments.Contains("--summarize", StringComparer.OrdinalIgnoreCase), cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> SystemAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (arguments.ElementAtOrDefault(1)?.Equals("snapshot", StringComparison.OrdinalIgnoreCase) != true)
        {
            return InvalidCommand();
        }

        var watch = arguments.Contains("--watch", StringComparer.OrdinalIgnoreCase);
        if (watch)
        {
            return await WatchAsync(
                _application.CaptureSystemSnapshotAsync,
                _output.RenderSystemSnapshot,
                ReadIntegerOption(arguments, "--interval", 2, 1, 60),
                cancellationToken);
        }

        var result = await _application.CaptureSystemSnapshotAsync(cancellationToken);
        _output.WriteResult(result, result.Value is null ? null : _output.RenderSystemSnapshot(result.Value));
        return result.Succeeded ? 0 : ExitCodeMapper.Map(result.Code);
    }

    private async Task<int> ScreenshotAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "capture" => await WriteAsync(_application.CaptureScreenshotAsync(new CaptureScreenshotRequest(ReadOption(arguments, "--mode") ?? "all-screens", arguments.Contains("--keep", StringComparer.OrdinalIgnoreCase), arguments.Contains("--watermark", StringComparer.OrdinalIgnoreCase), ScreenshotCaptureOrigins.Manual), cancellationToken)),
        "latest" => await WriteAsync(_application.GetLatestScreenshotAsync(cancellationToken)),
        "open-folder" => await WriteAsync(_application.OpenScreenshotFolderAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> AiAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        switch (arguments.ElementAtOrDefault(1)?.ToLowerInvariant())
        {
            case "status": return await WriteAsync(_application.GetAiStatusAsync(cancellationToken));
            case "analyze": return await WriteAsync(_application.AnalyzeCurrentActivityAsync(new AnalyzeCurrentActivityRequest(!arguments.Contains("--no-capture", StringComparer.OrdinalIgnoreCase), "cli.ai"), cancellationToken));
            case "enable": return await WriteAsync(_application.SetAiEnabledAsync(true, cancellationToken));
            case "disable": return await WriteAsync(_application.SetAiEnabledAsync(false, cancellationToken));
            case "configure":
                var values = new Dictionary<string, string?>();
                AddOption(values, "ai.provider", ReadOption(arguments, "--provider"));
                AddOption(values, "ai.model", ReadOption(arguments, "--model"));
                AddOption(values, "ai.endpoint", ReadOption(arguments, "--endpoint"));
                AddOption(values, "ai.output_detail", ReadOption(arguments, "--output-detail"));
                AddOption(values, "ai.reasoning_effort", ReadOption(arguments, "--reasoning-effort"));
                return values.Count == 0 ? InvalidCommand() : await WriteAsync(_application.ConfigureAiAsync(new SettingsPatch(values), cancellationToken));
            case "key" when arguments.ElementAtOrDefault(2)?.Equals("set", StringComparison.OrdinalIgnoreCase) == true:
                if (Console.IsInputRedirected)
                {
                    return InvalidCommand();
                }
                var variable = ReadOption(arguments, "--variable");
                if (string.IsNullOrWhiteSpace(variable))
                {
                    var status = await _application.GetAiStatusAsync(cancellationToken);
                    if (!status.Succeeded || status.Value is null)
                    {
                        return WriteResult(status);
                    }

                    variable = status.Value.KeyVariable;
                }
                var secret = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]API key:[/] ").Secret());
                try { return await WriteAsync(_application.SetAiKeyAsync(variable, secret, cancellationToken)); }
                finally { secret = string.Empty; }
            default: return InvalidCommand();
        }
    }

    private async Task<int> ReportAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var action = arguments.ElementAtOrDefault(1)?.ToLowerInvariant();
        if (action == "today")
        {
            return await WriteAsync(_application.GenerateTodayReportAsync(ReadOption(arguments, "--output"), arguments.Contains("--open", StringComparer.OrdinalIgnoreCase), cancellationToken));
        }

        if (action != "digest")
        {
            return InvalidCommand();
        }

        var rawDate = ReadOption(arguments, "--date") ?? DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        if (!DateOnly.TryParseExact(rawDate, "yyyy-MM-dd", out var date))
        {
            return WriteResult(OperationResult<object>.Failure("command.arguments.invalid", "InvalidDigestDate", new ValidationIssue("date", "invalid", "InvalidDigestDate")));
        }

        return await WriteAsync(_application.GenerateDailyDigestAsync(date, arguments.Contains("--open", StringComparer.OrdinalIgnoreCase), cancellationToken));
    }

    private async Task<int> PrivacyAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "list" => await WriteAsync(_application.GetPrivacyRulesAsync(cancellationToken)),
        "add" => await WriteAsync(_application.AddPrivacyRuleAsync(ReadOption(arguments, "--type") ?? string.Empty, ReadOption(arguments, "--value") ?? string.Empty, cancellationToken)),
        "remove" => await WriteAsync(_application.RemovePrivacyRuleAsync(ReadOption(arguments, "--id") ?? string.Empty, cancellationToken)),
        "test-current" => await WriteAsync(_application.TestCurrentPrivacyAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> RetentionAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "status" => await WriteAsync(_application.GetRetentionStatusAsync(cancellationToken)),
        "preview" => await WriteAsync(_application.PreviewRetentionAsync(cancellationToken)),
        "run" => await WriteAsync(_application.RunRetentionAsync(new RetentionRequest(true, _options.Yes || arguments.Contains("--yes", StringComparer.OrdinalIgnoreCase)), cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> PluginsAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "list" => await WriteAsync(_application.GetPluginsAsync(cancellationToken)),
        "show" when arguments.Count >= 3 => await WriteAsync(_application.GetPluginAsync(arguments[2], cancellationToken)),
        "enable" when arguments.Count >= 3 => await WriteAsync(_application.SetPluginEnabledAsync(arguments[2], true, cancellationToken)),
        "disable" when arguments.Count >= 3 => await WriteAsync(_application.SetPluginEnabledAsync(arguments[2], false, cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> ConfigAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        switch (arguments.ElementAtOrDefault(1)?.ToLowerInvariant())
        {
            case "list" when arguments.Count == 2:
                return await ListSettingsAsync(cancellationToken);
            case "get" when arguments.Count == 3:
                return await GetSettingAsync(arguments[2], cancellationToken);
            case "set" when arguments.Count == 4:
                return await SetSettingAsync(arguments[2], arguments[3], cancellationToken);
            case "wizard":
                if (Console.IsInputRedirected) return InvalidCommand();
                var language = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[cyan]Language[/]").AddChoices("system", "en", "it", "vi", "fr", "de", "es"));
                var theme = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[cyan]Theme[/]").AddChoices("system", "light", "dark"));
                var result = await _application.PatchSettingsAsync(new SettingsPatch(new Dictionary<string, string?> { ["language"] = language, ["theme"] = theme }), cancellationToken);
                return WriteSettingsResult(result, ["language", "theme"]);
            default: return InvalidCommand();
        }
    }

    private async Task<int> ListSettingsAsync(CancellationToken cancellationToken)
    {
        var result = await _application.GetSettingsAsync(cancellationToken);
        return WriteSettingsResult(result, CliSettingsCatalog.Settings.Select(setting => setting.Key));
    }

    private async Task<int> GetSettingAsync(string key, CancellationToken cancellationToken)
    {
        if (!CliSettingsCatalog.TryGet(key, out var descriptor) || descriptor is null)
        {
            return InvalidSetting(key);
        }

        var result = await _application.GetSettingsAsync(cancellationToken);
        return WriteSettingsResult(result, [descriptor.Key]);
    }

    private async Task<int> SetSettingAsync(string key, string value, CancellationToken cancellationToken)
    {
        if (!CliSettingsCatalog.TryGet(key, out var descriptor) || descriptor is null)
        {
            return InvalidSetting(key);
        }

        var result = await _application.PatchSettingsAsync(new SettingsPatch(new Dictionary<string, string?> { [descriptor.Key] = value }), cancellationToken);
        return WriteSettingsResult(result, [descriptor.Key]);
    }

    private int WriteSettingsResult(OperationResult<AppSettings> result, IEnumerable<string> keys)
    {
        if (!result.Succeeded || result.Value is null)
        {
            return WriteResult(result);
        }

        var values = keys
            .Select(key => CliSettingsCatalog.TryGet(key, out var descriptor) ? descriptor : null)
            .Where(descriptor => descriptor is not null)
            .Select(descriptor => CliSettingsCatalog.Read(descriptor!, result.Value))
            .ToArray();
        var safeResult = new OperationResult<IReadOnlyList<CliSettingValue>>(result.Succeeded, result.Code, result.MessageKey, values, result.Issues);
        _output.WriteResult(safeResult, _output.RenderSettings(values));
        return safeResult.Succeeded ? 0 : ExitCodeMapper.Map(safeResult.Code);
    }

    private int InvalidSetting(string key) => WriteResult(OperationResult<object>.Failure(
        "command.arguments.invalid",
        "SettingsFieldInvalid",
        new ValidationIssue(key, "not_allowed", "SettingsFieldInvalid")));

    private async Task<int> StartupAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "status" => await WriteAsync(_application.GetStartupStatusAsync(cancellationToken)),
        "enable" => await WriteAsync(_application.SetStartupEnabledAsync(true, cancellationToken)),
        "disable" => await WriteAsync(_application.SetStartupEnabledAsync(false, cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> OpenAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) => arguments.ElementAtOrDefault(1)?.ToLowerInvariant() switch
    {
        "reports" => await WriteAsync(_application.OpenReportsFolderAsync(cancellationToken)),
        "screenshots" => await WriteAsync(_application.OpenScreenshotFolderAsync(cancellationToken)),
        "ui" => await WriteAsync(_application.OpenUserInterfaceAsync(cancellationToken)),
        _ => InvalidCommand()
    };

    private async Task<int> DoctorAsync(CancellationToken cancellationToken)
    {
        var healthTask = _application.GetRuntimeHealthAsync(cancellationToken);
        var dashboardTask = _application.GetDashboardAsync(cancellationToken);
        var aiTask = _application.GetAiStatusAsync(cancellationToken);
        var retentionTask = _application.GetRetentionStatusAsync(cancellationToken);
        var startupTask = _application.GetStartupStatusAsync(cancellationToken);
        var pluginsTask = _application.GetPluginsAsync(cancellationToken);
        await Task.WhenAll(healthTask, dashboardTask, aiTask, retentionTask, startupTask, pluginsTask);

        var health = await healthTask;
        var dashboard = await dashboardTask;
        var ai = await aiTask;
        var retention = await retentionTask;
        var startup = await startupTask;
        var plugins = await pluginsTask;
        var succeeded = health.Succeeded && dashboard.Succeeded && ai.Succeeded && retention.Succeeded && startup.Succeeded && plugins.Succeeded;
        var issues = new[] { health.Issues, dashboard.Issues, ai.Issues, retention.Issues, startup.Issues, plugins.Issues }
            .SelectMany(group => group)
            .ToArray();
        var value = new
        {
            runtime = health.Value,
            ai = ai.Value,
            tracking = dashboard.Value is null ? null : new { dashboard.Value.IsTracking, dashboard.Value.StatusLabel, dashboard.Value.LastSampleTimestamp },
            retention = retention.Value is null ? null : new { retention.Value.DataRetentionDays, retention.Value.ScreenshotRetentionDays },
            startupEnabled = startup.Value,
            plugins = plugins.Value?.Select(plugin => new { plugin.Id, plugin.Enabled }).ToArray()
        };
        var result = new OperationResult<object>(succeeded, succeeded ? "doctor.healthy" : "doctor.partial", succeeded ? "DoctorHealthy" : "DoctorPartial", value, issues);
        _output.WriteResult(result);
        return result.Succeeded ? 0 : 10;
    }

    private async Task<int> RunShellAsync(CancellationToken cancellationToken)
    {
        if (_options.Format != CliFormat.Rich || Console.IsInputRedirected)
        {
            _output.WriteHelp();
            return 0;
        }

        AnsiConsole.Write(new FigletText("TrackMeUp").Color(Color.IndianRed));
        while (!cancellationToken.IsCancellationRequested)
        {
            var dashboard = await _application.GetDashboardAsync(cancellationToken);
            if (dashboard.Succeeded && dashboard.Value is not null)
            {
                AnsiConsole.Write(_output.RenderDashboard(dashboard.Value));
            }

            var line = AnsiConsole.Prompt(new TextPrompt<string>("[teal]trackmeup>[/] ").AllowEmpty());
            var tokens = CliCommandCatalog.Normalize(Tokenize(line));
            if (tokens.Count == 0)
            {
                continue;
            }
            if (tokens[0].Equals("exit", StringComparison.OrdinalIgnoreCase) || tokens[0].Equals("quit", StringComparison.OrdinalIgnoreCase)) return 0;
            if (tokens[0].Equals("clear", StringComparison.OrdinalIgnoreCase)) { Console.Clear(); continue; }
            await DispatchAsync(tokens, cancellationToken);
        }

        return 130;
    }

    private async Task<int> WriteAsync<T>(Task<OperationResult<T>> task) => WriteResult(await task);

    private async Task<int> WatchAsync<T>(
        Func<CancellationToken, Task<OperationResult<T>>> query,
        Func<T, Spectre.Console.Rendering.IRenderable> render,
        int intervalSeconds,
        CancellationToken cancellationToken)
    {
        if (_options.Format == CliFormat.Json)
        {
            return WriteResult(OperationResult<object>.Failure("command.arguments.invalid", "WatchDoesNotSupportJson", new ValidationIssue("watch", "not_supported", "WatchDoesNotSupportJson")));
        }

        var initial = await query(cancellationToken);
        if (!initial.Succeeded || initial.Value is null)
        {
            _output.WriteResult(initial);
            return initial.Succeeded ? 0 : ExitCodeMapper.Map(initial.Code);
        }

        if (_options.Format != CliFormat.Rich)
        {
            do
            {
                _output.WriteResult(initial);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                initial = await query(cancellationToken);
                if (!initial.Succeeded || initial.Value is null)
                {
                    _output.WriteResult(initial);
                    return initial.Succeeded ? 0 : ExitCodeMapper.Map(initial.Code);
                }
            } while (!cancellationToken.IsCancellationRequested);

            return 130;
        }

        var code = 0;
        await AnsiConsole.Live(render(initial.Value)).StartAsync(async context =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                var next = await query(cancellationToken);
                if (!next.Succeeded || next.Value is null)
                {
                    code = next.Succeeded ? 0 : ExitCodeMapper.Map(next.Code);
                    context.UpdateTarget(new Spectre.Console.Panel(new Spectre.Console.Markup(Spectre.Console.Markup.Escape(next.MessageKey))).BorderColor(Spectre.Console.Color.IndianRed));
                    break;
                }

                context.UpdateTarget(render(next.Value));
            }
        });

        return cancellationToken.IsCancellationRequested ? 130 : code;
    }

    private int WriteResult<T>(OperationResult<T> result)
    {
        _output.WriteResult(result);
        return result.Succeeded ? 0 : ExitCodeMapper.Map(result.Code);
    }

    private int InvalidCommand()
    {
        _output.WriteResult(OperationResult<object>.Failure("command.invalid", "CommandInvalid"));
        return 2;
    }

    private int WriteHelp(string? topic)
    {
        if (_output.WriteHelp(topic))
        {
            return 0;
        }

        return InvalidCommand();
    }

    private static string? ReadOption(IReadOnlyList<string> arguments, string name)
    {
        var index = arguments.ToList().FindIndex(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Count ? arguments[index + 1] : null;
    }

    private static int ReadIntegerOption(IReadOnlyList<string> arguments, string name, int fallback, int minimum, int maximum) => int.TryParse(ReadOption(arguments, name), out var value) && value >= minimum && value <= maximum ? value : fallback;

    private static void AddOption(IDictionary<string, string?> values, string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) values[key] = value; }

    private static IReadOnlyList<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var builder = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in line.Trim())
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(character) && !quoted) { if (builder.Length > 0) { tokens.Add(builder.ToString()); builder.Clear(); } continue; }
            builder.Append(character);
        }
        if (builder.Length > 0) tokens.Add(builder.ToString());
        return tokens;
    }
}
