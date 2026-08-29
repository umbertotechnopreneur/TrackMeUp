using Xunit;

namespace TrackMeUp.Core.Tests;

[CollectionDefinition(ProcessEnvironmentCollection.Name)]
public sealed class ProcessEnvironmentCollection
{
    public const string Name = "Process environment";
}
