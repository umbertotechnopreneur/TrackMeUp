// SPDX-License-Identifier: MIT

namespace TrackMeUp.Application;

/// <summary>Represents native drag handling owned by one frontend window and disposed on that window's UI thread.</summary>
public interface IWindowSnappingRegistration : IDisposable
{
    /// <summary>Indicates that layout must retain the user's drag geometry instead of moving or resizing it into the work area.</summary>
    bool PreserveUserPosition { get; }
}
