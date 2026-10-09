using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;
using Xunit;

namespace ProtonPassCliExtension.Tests;

public class QueryMatcherTests
{
    [Theory]
    [InlineData("github.com", "github")]
    [InlineData("https://www.github.com/login?x=1", "github")]
    [InlineData("login.example.co.uk", "example")]
    [InlineData("WWW.Example.org", "example")]
    [InlineData("localhost:3000", null)]
    [InlineData("github", null)]
    [InlineData("two words.com", null)]
    [InlineData("a.io", null)]
    [InlineData("", null)]
    public void ExtractToken(string query, string? expected)
    {
        Assert.Equal(expected, QueryMatcher.ExtractToken(query));
    }

    [Fact]
    public void BestLogin_PrefersExactThenPrefixThenSubstring_AndIgnoresNonLogins()
    {
        var items = new[]
        {
            new CachedItem("s", "1", "My GitHub backup", "V", "login"),
            new CachedItem("s", "2", "GitHub", "V", "login"),
            new CachedItem("s", "3", "GitHub Enterprise", "V", "login"),
            new CachedItem("s", "4", "github note", "V", "note"),
        };

        Assert.Equal("2", QueryMatcher.BestLogin(items, "github")!.ItemId);
        Assert.Equal("3", QueryMatcher.BestLogin(items.Where(i => i.ItemId != "2"), "github")!.ItemId);
        Assert.Null(QueryMatcher.BestLogin(items, "gitlab"));
    }
}

public class CreateAndGenerateTests
{
    [Fact]
    public async Task GeneratePassword_UsesLengthAndTrimsNewline()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok("Zx9!kLmQ\n"));
        var client = FakeRunner.ClientFor(runner);

        var result = await client.GeneratePasswordAsync(24);

        Assert.Equal("Zx9!kLmQ", result.Value);
        Assert.Equal(["password", "generate", "random", "--length=24"], runner.Calls[0]);
    }

    [Fact]
    public async Task CreateLogin_NeverPassesAPassword_AndUsesEqualsFormsForUserText()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok(string.Empty));
        var client = FakeRunner.ClientFor(runner);

        var result = await client.CreateLoginAsync(new NewLogin("-Odd title é", "-SHARE==", null, "  me  ", null, "https://example.invalid"));

        Assert.True(result.IsSuccess);
        var args = runner.Calls[0];
        Assert.Contains("--generate-password", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--password", StringComparison.Ordinal));
        Assert.Contains("--share-id=-SHARE==", args);
        Assert.Contains("--title=-Odd title é", args);
        Assert.Contains("--username=me", args);
        Assert.Contains("--url=https://example.invalid", args);
        Assert.DoesNotContain(args, a => a.StartsWith("--email", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateLogin_ByVaultName_WhenNoShareId()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok(string.Empty));
        var client = FakeRunner.ClientFor(runner);

        await client.CreateLoginAsync(new NewLogin("T", null, "Personal", null, null, null));

        Assert.Contains("--vault-name=Personal", runner.Calls[0]);
    }

    [Fact]
    public async Task CreateLogin_PropagatesFailure()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(1, "Error: This operation requires an authenticated client")));

        var result = await client.CreateLoginAsync(new NewLogin("T", "s", null, null, null, null));

        Assert.Equal(PassCliErrorKind.NotLoggedIn, result.Error!.Kind);
    }

    [Fact]
    public void ItemCache_PeekReturnsStaleButNotInvalidated()
    {
        var cache = new ItemCache();
        cache.Set([new CachedItem("s", "i", "T", "V", "login")]);
        Assert.Single(cache.Peek());
        cache.Invalidate();
        Assert.Empty(cache.Peek());
    }
}
