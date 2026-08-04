using System;
using System.Linq;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class PresentationBoundaryTests
{
    [Fact]
    public void PresentationAssembly_DoesNotReferenceWinUiOrSpectre()
    {
        var references = typeof(TrackMeUp.Presentation.MainViewModel).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

        Assert.DoesNotContain(references, name => name?.Contains("WinUI", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(references, name => name?.Contains("Spectre", StringComparison.OrdinalIgnoreCase) == true);
    }
}
