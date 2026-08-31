// SPDX-License-Identifier: MIT

using System.Linq;
using TrackMeUp.Services;
using Xunit;

namespace TrackMeUp.Core.Tests;

public sealed class SystemMessageBoxRequestTests
{
    [Theory]
    [InlineData(SystemMessageBoxSeverity.Information)]
    [InlineData(SystemMessageBoxSeverity.Warning)]
    [InlineData(SystemMessageBoxSeverity.Error)]
    public void Informative_PreservesLocalizedContentAndSeverity(SystemMessageBoxSeverity severity)
    {
        var request = SystemMessageBoxRequest.Informative("Localized title", "Localized message", severity);

        Assert.Equal("Localized title", request.Title);
        Assert.Equal("Localized message", request.Message);
        Assert.Equal(severity, request.Severity);
    }

    [Fact]
    public void Confirmation_UsesWarningAndLeavesButtonsToWindows()
    {
        var request = SystemMessageBoxRequest.Confirmation("Confirm", "Proceed?");
        var propertyNames = typeof(SystemMessageBoxRequest)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(SystemMessageBoxSeverity.Warning, request.Severity);
        Assert.Equal(new[] { "Message", "Severity", "Title" }, propertyNames);
    }
}
