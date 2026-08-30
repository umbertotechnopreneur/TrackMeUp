// SPDX-License-Identifier: MIT

using TrackMeUp.Presentation;
using Xunit;

namespace TrackMeUp.Presentation.Tests;

public sealed class OcrTextSearchTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  ")]
    [InlineData(" a ")]
    public void FindMatches_RequiresAtLeastTwoCharacters(string query)
    {
        Assert.Empty(OcrTextSearch.FindMatches("Alpha alpha", query));
    }

    [Fact]
    public void FindMatches_TrimsOnlyOuterWhitespaceBeforeMatching()
    {
        var matches = OcrTextSearch.FindMatches("Alpha beta", " alpha ");

        Assert.Equal([new OcrTextMatch(0, 5)], matches);
    }

    [Fact]
    public void FindMatches_IsCaseInsensitiveAndReturnsMultipleMatches()
    {
        var matches = OcrTextSearch.FindMatches("Alpha alpha ALPHA", "aLpHa");

        Assert.Equal(
            [new OcrTextMatch(0, 5), new OcrTextMatch(6, 5), new OcrTextMatch(12, 5)],
            matches);
    }

    [Fact]
    public void FindMatches_AdvancesPastEachMatchToResolveOverlapDeterministically()
    {
        var matches = OcrTextSearch.FindMatches("aaaa", "aa");

        Assert.Equal([new OcrTextMatch(0, 2), new OcrTextMatch(2, 2)], matches);
    }
}
