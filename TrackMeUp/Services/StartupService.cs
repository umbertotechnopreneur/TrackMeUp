using Microsoft.Win32;
using System;
using System.IO;

namespace TrackMeUp.Services;

/// <summary>
/// Controls Windows startup registration for HKCU Run key.
/// </summary>
public sealed class StartupService
{
    private const string RunKeyName = "TrackMeUp";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _commandLine;

    /// <summary>
    /// Creates service using the current process path.
    /// </summary>
    public StartupService()
    {
        var exePath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.exe");
        }

        // Quote path so spaces in install/user paths do not break automatic startup parsing.
        _commandLine = $"\"{exePath}\" --start-with-windows";
    }

    /// <summary>
    /// Saves or removes the autorun entry for this app in the current user profile.
    /// </summary>
    /// <param name="enabled">True to create/keep startup entry, false to remove it.</param>
    /// <returns>True when operation succeeds; false when registry is unavailable.</returns>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            if (enabled)
            {
                key.SetValue(RunKeyName, _commandLine, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(RunKeyName, false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if TrackMeUp startup entry is currently present.
    /// </summary>
    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            return key.GetValue(RunKeyName) is not null;
        }
        catch
        {
            return false;
        }
    }
}
