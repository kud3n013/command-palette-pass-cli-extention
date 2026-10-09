using ProtonPassCliExtension.PassCli;
using ProtonPassCliExtension.Services;
using Xunit;

namespace ProtonPassCliExtension.Tests;

public class PassCliClientTests
{
    [Fact]
    public async Task ProcessNotFound_MapsToNotInstalled()
    {
        var client = new PassCliClient(new ThrowingRunner(), new PassCliOptions());

        var result = await client.CheckSessionAsync();

        Assert.Equal(PassCliErrorKind.NotInstalled, result.Error!.Kind);
    }

    [Fact]
    public async Task TimedOut_MapsToTimeout()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(new ProcessOutput(-1, "", "", TimedOut: true)));

        var result = await client.ListVaultsAsync();

        Assert.Equal(PassCliErrorKind.Timeout, result.Error!.Kind);
    }

    [Theory]
    [InlineData("Error: Not logged in. Run 'pass-cli login'.")]
    [InlineData("error: you must log in first")]
    [InlineData("Error: session expired")]
    public async Task LoginProblems_MapToNotLoggedIn(string stderr)
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(1, stderr)));

        var result = await client.ListVaultsAsync();

        Assert.Equal(PassCliErrorKind.NotLoggedIn, result.Error!.Kind);
    }

    [Fact]
    public async Task UnknownFailure_MapsToFailedWithExitCodeAndStdErr()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(7, "Error: something odd\n")));

        var result = await client.ListVaultsAsync();

        Assert.Equal(PassCliErrorKind.Failed, result.Error!.Kind);
        Assert.Equal(7, result.Error.ExitCode);
        Assert.Equal("Error: something odd", result.Error.StdErr);
    }

    [Fact]
    public async Task LongStdErr_IsTruncated()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(1, new string('x', 5000))));

        var result = await client.ListVaultsAsync();

        Assert.Equal(500, result.Error!.StdErr!.Length);
    }

    [Fact]
    public async Task Failure_NeverCarriesStdOut()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(new ProcessOutput(1, "TOP-SECRET-OUT", "boom", false)));

        var result = await client.GetFieldAsync("s", "i", "password");

        Assert.DoesNotContain("TOP-SECRET-OUT", result.Error!.ToString());
    }

    [Fact]
    public async Task NoTotp_IsDetected()
    {
        var client = FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(1, "Error: No TOTP fields found in this item")));

        var result = await client.GetTotpAsync("s", "i");

        Assert.Equal(PassCliErrorKind.NoTotp, result.Error!.Kind);
    }

    [Fact]
    public async Task Username_FallsBackToEmailWhenFieldMissing()
    {
        var runner = new FakeRunner(args => args[^1].EndsWith("/username", StringComparison.Ordinal)
            ? FakeRunner.Err(1, "Error: Field does not exist: username")
            : FakeRunner.Ok("me@example.invalid\n"));
        var client = FakeRunner.ClientFor(runner);

        var result = await client.GetUsernameAsync("s", "i");

        Assert.Equal("me@example.invalid", result.Value);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public async Task Username_DoesNotFallBackOnOtherErrors()
    {
        var runner = FakeRunner.Always(FakeRunner.Err(1, "Error: Not logged in"));
        var client = FakeRunner.ClientFor(runner);

        var result = await client.GetUsernameAsync("s", "i");

        Assert.Equal(PassCliErrorKind.NotLoggedIn, result.Error!.Kind);
        Assert.Single(runner.Calls);
    }

    [Fact]
    public async Task DashLeadingIds_AreNeverPassedAsBareArguments()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok("x\n"));
        var client = FakeRunner.ClientFor(runner);

        await client.GetFieldAsync("-SHARE==", "-ITEM==", "password");
        await client.GetTotpAsync("-SHARE==", "-ITEM==");
        await client.ListItemsAsync("-SHARE==");
        await client.GetUrlsAsync("-SHARE==", "-ITEM==");

        foreach (var call in runner.Calls)
        {
            Assert.DoesNotContain(call, a => a is "-SHARE==" or "-ITEM==");
        }

        Assert.Equal("pass://-SHARE==/-ITEM==/password", runner.Calls[0][^1]);
        Assert.Contains("--share-id=-SHARE==", runner.Calls[2]);
        Assert.Contains("--item-id=-ITEM==", runner.Calls[3]);
    }

    [Fact]
    public async Task ListItems_RequestsActiveItemsOnly()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok(Fixture.Read("item-list.json")));
        var client = FakeRunner.ClientFor(runner);

        await client.ListItemsAsync("s");

        Assert.Equal(["active"], runner.Calls[0].SkipWhile(a => a != "--filter-state").Skip(1).Take(1));
    }

    [Fact]
    public async Task ConfiguredExecutablePath_IsUsed()
    {
        var runner = FakeRunner.Always(FakeRunner.Ok("{}"));
        var client = new PassCliClient(runner, new PassCliOptions { ExecutablePath = @"C:\tools\pass-cli.exe" });

        await client.CheckSessionAsync();

        Assert.Equal(@"C:\tools\pass-cli.exe", runner.LastFileName);
    }

    [Fact]
    public async Task ItemLoader_FansOutPerVault_AndAttachesVaultNames()
    {
        var runner = new FakeRunner(args => args[0] == "vault"
            ? FakeRunner.Ok(Fixture.Read("vault-list.json"))
            : FakeRunner.Ok(Fixture.Read("item-list.json")));
        var loader = new ItemLoader(FakeRunner.ClientFor(runner));

        var result = await loader.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(6, result.Value!.Count);
        Assert.Contains(result.Value, i => i.VaultName == "Work Équipe");
        Assert.Equal(2, runner.Calls.Count(c => c[0] == "item"));
    }

    [Fact]
    public async Task ItemLoader_PropagatesErrors()
    {
        var loader = new ItemLoader(FakeRunner.ClientFor(FakeRunner.Always(FakeRunner.Err(1, "Error: Not logged in"))));

        var result = await loader.LoadAsync();

        Assert.Equal(PassCliErrorKind.NotLoggedIn, result.Error!.Kind);
    }

    [Fact]
    public void ItemCache_ExpiresAndInvalidates()
    {
        var time = new ManualTime();
        var cache = new ItemCache(time);
        cache.Set([new CachedItem("s", "i", "Title", "Vault", "login")]);

        Assert.True(cache.TryGet(TimeSpan.FromMinutes(5), out _));
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.False(cache.TryGet(TimeSpan.FromMinutes(5), out _));

        cache.Set([new CachedItem("s", "i", "Title", "Vault", "login")]);
        cache.Invalidate();
        Assert.False(cache.TryGet(TimeSpan.FromMinutes(5), out _));
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
