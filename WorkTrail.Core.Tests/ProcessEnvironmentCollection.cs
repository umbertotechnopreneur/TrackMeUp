// SPDX-License-Identifier: MIT

using Xunit;

namespace WorkTrail.Core.Tests;

[CollectionDefinition(ProcessEnvironmentCollection.Name)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment";
}
