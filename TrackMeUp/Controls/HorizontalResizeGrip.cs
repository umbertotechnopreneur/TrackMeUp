using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace TrackMeUp.Controls;

/// <summary>Provides a horizontal drag thumb with the native east-west resize cursor.</summary>
public sealed class HorizontalResizeGrip : Control
{
    /// <summary>Creates the resize grip with a native horizontal-resize pointer.</summary>
    public HorizontalResizeGrip() =>
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
}
