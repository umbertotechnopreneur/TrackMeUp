// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Identifies the currently activated main window and already-created peer windows.</summary>
public sealed record WindowRevealRequest(long MainWindowHandle, IReadOnlyList<long> OpenWindowHandles);
