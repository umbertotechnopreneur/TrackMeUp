using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using TrackMeUp.Application;

namespace TrackMeUp;

/// <summary>Displays the bounded, topmost progress and result surface for an explicit AI connection check.</summary>
internal sealed partial class AiConnectionTestDialogWindow : Window
{
    private const int LogicalWidth = 560;
    private const int LogicalHeight = 480;
    private const int LogicalScreenMargin = 24;
    private static readonly TimeSpan TypeDelay = TimeSpan.FromMilliseconds(14);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private static readonly IntPtr HwndTopMost = new(-1);
    private readonly ITrackMeUpApplication _application;
    private readonly AppWindow _appWindow;
    private readonly WindowPlacementService _placement;
    private readonly IntPtr _windowHandle;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _testCancellation = new(TimeSpan.FromSeconds(30));
    private readonly DispatcherQueueTimer _countdownTimer;
    private readonly DateTimeOffset _deadline = DateTimeOffset.Now.AddSeconds(30);
    private readonly StringBuilder _terminalBuffer = new();
    private bool _isClosing;

    /// <summary>Creates the passive acrylic test surface using the shared application facade.</summary>
    internal AiConnectionTestDialogWindow(ITrackMeUpApplication application, ElementTheme theme, AppWindow ownerAppWindow, IntPtr ownerHandle)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        ArgumentNullException.ThrowIfNull(ownerAppWindow);
        InitializeComponent();
        RootGrid.RequestedTheme = theme;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleDragRegion);
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        _placement = new WindowPlacementService(application, this, _appWindow, WindowStateKeys.AiConnectionTest, LogicalWidth, LogicalHeight, LogicalScreenMargin, ownerAppWindow.Id);
        SetWindowOwner(_windowHandle, ownerHandle);
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }

        _countdownTimer = DispatcherQueue.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromMilliseconds(200);
        _countdownTimer.Tick += (_, _) => UpdateCountdown();
        Closed += (_, _) =>
        {
            _isClosing = true;
            _testCancellation.Cancel();
            _countdownTimer.Stop();
            _testCancellation.Dispose();
            _completion.TrySetResult();
        };
    }

    /// <summary>Activates the topmost dialog and completes after the user dismisses it.</summary>
    internal Task ShowAsync()
    {
        SetWindowPos(_windowHandle, HwndTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        Activate();
        return _completion.Task;
    }

    internal IntPtr WindowHandle => _windowHandle;

    internal void DisposePlacement() => _placement.Dispose();

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _placement.ApplyDefaultBounds(RootGrid);
        await _placement.RestoreAndCenterAsync(RootGrid, CancellationToken.None);
        _countdownTimer.Start();
        await RunTestAsync();
    }

    private async Task RunTestAsync()
    {
        try
        {
            var requestTask = _application.TestAiConnectionAsync(_testCancellation.Token);
            await AppendTerminalAsync($"$ prompt{Environment.NewLine}{AiConnectionTestProtocol.Prompt}", _testCancellation.Token);
            TerminalStateText.Text = "WAITING";
            var result = await requestTask;
            if (_isClosing)
            {
                return;
            }

            if (result.Succeeded && result.Value is not null)
            {
                CompleteTest(success: true);
                TitleText.Text = "AI provider connected";
                StatusText.Text = $"{ProviderDisplayName(result.Value.Provider)} · {result.Value.Model} · {result.Value.ElapsedMilliseconds:N0} ms";
                TerminalStateText.Text = "RESPONSE";
                var output = string.IsNullOrWhiteSpace(result.Value.Output) ? "<empty response>" : result.Value.Output.Trim();
                await AppendTerminalAsync($"{Environment.NewLine}{Environment.NewLine}> response{Environment.NewLine}{output}", CancellationToken.None);
                return;
            }

            CompleteTest(success: false);
            TitleText.Text = "AI provider connection failed";
            StatusText.Text = result.Code == "ai.connection.key.missing"
                ? "No usable API key is available for the selected AI provider."
                : result.Code == "ai.connection.configuration.invalid"
                    ? "Save a valid AI provider configuration before testing it."
                    : "The AI provider could not confirm the connection. Check the endpoint, model and key.";
            TerminalStateText.Text = "ERROR";
            await AppendTerminalAsync($"{Environment.NewLine}{Environment.NewLine}! error{Environment.NewLine}{StatusText.Text}", CancellationToken.None);
        }
        catch (OperationCanceledException) when (_isClosing)
        {
            // Closing the surface cancels both the provider request and the presentation-only teletype animation.
        }
        catch (OperationCanceledException) when (!_isClosing)
        {
            CompleteTest(success: false);
            TitleText.Text = "AI provider timed out";
            StatusText.Text = "The AI provider did not respond within 30 seconds.";
            TerminalStateText.Text = "TIMEOUT";
            await AppendTerminalAsync($"{Environment.NewLine}{Environment.NewLine}! timeout{Environment.NewLine}{StatusText.Text}", CancellationToken.None);
        }
    }

    private void UpdateCountdown()
    {
        var remaining = Math.Max(0, (int)Math.Ceiling((_deadline - DateTimeOffset.Now).TotalSeconds));
        CountdownText.Text = $"00:{remaining:00}";
    }

    private void CompleteTest(bool success)
    {
        _countdownTimer.Stop();
        Progress.IsActive = false;
        Progress.Visibility = Visibility.Collapsed;
        CountdownPanel.Visibility = Visibility.Collapsed;
        ResultIcon.Glyph = success ? "\uE73E" : "\uEA39";
        ResultIcon.Foreground = new SolidColorBrush(success ? Colors.ForestGreen : Colors.IndianRed);
        ResultIcon.Visibility = Visibility.Visible;
        CloseButton.Content = "Close";
    }

    private async Task AppendTerminalAsync(string text, CancellationToken cancellationToken)
    {
        foreach (var character in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _terminalBuffer.Append(character);
            TerminalText.Text = _terminalBuffer + "▋";
            TerminalScrollViewer.UpdateLayout();
            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null, disableAnimation: true);
            await Task.Delay(TypeDelay, cancellationToken);
        }
    }

    private static string ProviderDisplayName(string provider) => provider.Trim().ToLowerInvariant() switch
    {
        "openai" or "open-ai" => "OpenAI",
        "openrouter" => "OpenRouter",
        "anthropic" => "Anthropic",
        _ => provider
    };

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _isClosing = true;
        _testCancellation.Cancel();
        CloseButton.IsEnabled = false;
        await _placement.SaveAsync(CancellationToken.None);
        Close();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static void SetWindowOwner(IntPtr windowHandle, IntPtr ownerHandle)
    {
        const int GwlHwndParent = -8;
        _ = SetWindowLongPtr(windowHandle, GwlHwndParent, ownerHandle);
    }
}
