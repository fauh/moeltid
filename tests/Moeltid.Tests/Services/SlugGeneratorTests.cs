using Moeltid.Services;
using Shouldly;

namespace Moeltid.Tests.Services;

public class SlugGeneratorTests
{
    private readonly SlugGenerator _sut = new(new TokenGenerator());

    [Fact]
    public void Generate_NormalTitle_ProducesKebabCaseWithSuffix()
    {
        var slug = _sut.Generate("Friday Lunch");
        slug.ShouldMatch(@"^friday-lunch-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_NullTitle_ProducesEventFallback()
    {
        var slug = _sut.Generate(null);
        slug.ShouldMatch(@"^event-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_WhitespaceTitle_ProducesEventFallback()
    {
        var slug = _sut.Generate("   ");
        slug.ShouldMatch(@"^event-[A-Za-z0-9_-]{6}$");
    }

    [Fact]
    public void Generate_LongTitle_TruncatesKebabPart()
    {
        var title = new string('a', 100);
        var slug = _sut.Generate(title);
        // kebab part should be at most 40 chars, plus dash, plus 6 char suffix
        slug.Length.ShouldBeLessThanOrEqualTo(47);
    }

    [Fact]
    public void Generate_TitleWithSpecialChars_StripsNonAlphanumeric()
    {
        var slug = _sut.Generate("Hello, World!");
        slug.ShouldStartWith("hello-world-");
    }

    [Fact]
    public void Generate_SwedishCharacters_StripsThemKnownLimitation()
    {
        // Swedish chars are currently stripped — slug won't contain å/ä/ö.
        // Captured as a known limitation, not a failure to fix here.
        var slug = _sut.Generate("Måltid");
        slug.ShouldNotContain("å");
    }

    [Fact]
    public void Generate_ProducesUniqueSlugsSameTitle()
    {
        var slugs = Enumerable.Range(0, 50)
            .Select(_ => _sut.Generate("Same Title"))
            .ToList();

        slugs.Distinct().Count().ShouldBe(50);
    }
}
