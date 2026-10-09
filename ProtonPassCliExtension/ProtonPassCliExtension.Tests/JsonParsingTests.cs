using ProtonPassCliExtension.PassCli;
using Xunit;

namespace ProtonPassCliExtension.Tests;

public class JsonParsingTests
{
    [Fact]
    public async Task ListVaults_ParsesFixture()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok(Fixture.Read("vault-list.json"))));

        var result = await client.ListVaultsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("Personal", result.Value[0].Name);
        Assert.Equal("Work Équipe", result.Value[1].Name);
        Assert.StartsWith("-", result.Value[1].ShareId);
    }

    [Fact]
    public async Task ListItems_ParsesMetadataAndNonAsciiTitles()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok(Fixture.Read("item-list.json"))));

        var result = await client.ListItemsAsync("SHARE-PERSONAL-REDACTED==");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.Count);
        var cafe = result.Value[1];
        Assert.Equal("Café Zürich – 日本語", cafe.Title);
        Assert.Equal("-ITEMID-2-REDACTED==", cafe.Id);
        Assert.Equal("SHARE-PERSONAL-REDACTED==", cafe.ShareId);
        Assert.Equal("login", cafe.ItemType);
        Assert.Equal("note", result.Value[2].ItemType);
    }

    [Fact]
    public async Task GetUrls_ReadsUrlsFromViewJson()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok(Fixture.Read("item-view-login.json"))));

        var result = await client.GetUrlsAsync("SHARE-PERSONAL-REDACTED==", "-ITEMID-2-REDACTED==");

        Assert.True(result.IsSuccess);
        Assert.Equal(["https://example.invalid/login", "example.invalid"], result.Value!);
    }

    [Fact]
    public async Task GetTotp_ReadsOnlyTheCode()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok(Fixture.Read("totp.json"))));

        var result = await client.GetTotpAsync("s", "i");

        Assert.True(result.IsSuccess);
        Assert.Equal("000000", result.Value);
    }

    [Fact]
    public async Task MalformedJson_YieldsInvalidOutputWithoutLeakingContent()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok("{ \"items\": [ SECRET-GARBAGE")));

        var result = await client.ListItemsAsync("s");

        Assert.False(result.IsSuccess);
        Assert.Equal(PassCliErrorKind.InvalidOutput, result.Error!.Kind);
        Assert.DoesNotContain("SECRET-GARBAGE", result.Error.ToString());
    }

    [Theory]
    [InlineData("hunter2\n", "hunter2")]
    [InlineData("hunter2\r\n", "hunter2")]
    [InlineData("  padded  \n", "  padded  ")]
    [InlineData("two\n\n", "two\n")]
    public async Task GetField_TrimsExactlyOneLineEnding(string stdout, string expected)
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Ok(stdout)));

        var result = await client.GetFieldAsync("s", "i", "password");

        Assert.Equal(expected, result.Value);
    }
}
