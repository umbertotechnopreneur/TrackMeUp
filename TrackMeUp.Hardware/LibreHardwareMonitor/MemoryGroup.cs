// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.
// Modified for TrackMeUp: expose OS memory counters without SPD/SMBus discovery.

using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware.Memory;

internal sealed class MemoryGroup : IGroup
{
    private readonly IReadOnlyList<Hardware> _hardware;

    public MemoryGroup(ISettings settings)
    {
        _hardware = new Hardware[] { new VirtualMemory(settings), new TotalMemory(settings) };
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport() => "Memory: operating-system usage counters; SPD/SMBus discovery disabled.";

    public void Close()
    {
        foreach (var hardware in _hardware) hardware.Close();
    }
}
