using FluentAssertions;
using Moeltid.Services;

namespace Moeltid.Tests.Services;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _sut = new();

    [Theory]
    [InlineData(6)]
    [InlineData(22)]
    [InlineData(32)]
    public void RandomUrlSafeString_ReturnsCorrectLength(int length)
    {
        _sut.RandomUrlSafeString(length).Should().HaveLength(length);
    }

    [Fact]
    public void RandomUrlSafeString_ContainsOnlyUrlSafeCharacters()
    {
        for (var i = 0; i < 200; i++)
        {
            var token = _sut.RandomUrlSafeString(22);
            token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        }
    }

    [Fact]
    public void RandomUrlSafeString_ProducesUniqueValues()
    {
        var tokens = Enumerable.Range(0, 1000)
            .Select(_ => _sut.RandomUrlSafeString(22))
            .ToList();

        tokens.Distinct().Should().HaveCount(1000);
    }
}
