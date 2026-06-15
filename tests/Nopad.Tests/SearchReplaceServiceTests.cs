using Noopad.Services;

namespace Nopad.Tests;

public sealed class SearchReplaceServiceTests
{
    [Fact]
    public void ReplaceAll_DecodesCrLfEscapesInLiteralReplacement()
    {
        var service = new SearchReplaceService();

        var result = service.ReplaceAll("alpha|beta", "|", "\\r\\n", matchCase: true, wholeWord: false, regex: false);

        Assert.Equal("alpha\r\nbeta", result);
    }

    [Fact]
    public void ReplaceAll_DecodesCrLfEscapesInRegexReplacement()
    {
        var service = new SearchReplaceService();

        var result = service.ReplaceAll("alpha beta", "\\s+", "\\r\\n", matchCase: true, wholeWord: false, regex: true);

        Assert.Equal("alpha\r\nbeta", result);
    }
}
