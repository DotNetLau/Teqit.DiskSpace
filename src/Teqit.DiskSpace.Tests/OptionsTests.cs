using Xunit;

namespace Teqit.DiskSpace.Tests;

public sealed class OptionsTests
{
    [Fact]
    public void Defaults_are_sensible()
    {
        var o = Options.Parse([]);

        Assert.Equal(30, o.Top);
        Assert.True(o.Interactive);
        Assert.False(o.NoCache);
        Assert.False(o.CrossMounts);
        Assert.Empty(o.Excludes);
        Assert.Equal(Options.DefaultRoot(), o.Path);
    }

    [Fact]
    public void First_positional_argument_is_the_path()
    {
        Assert.Equal("some-folder", Options.Parse(["some-folder"]).Path);
    }

    [Fact]
    public void Parses_all_flags()
    {
        var o = Options.Parse(["--path", "x", "-n", "5", "--exclude", "skip", "--no-interactive", "--no-cache", "--cross-mounts"]);

        Assert.Equal("x", o.Path);
        Assert.Equal(5, o.Top);
        Assert.Single(o.Excludes);
        Assert.False(o.Interactive);
        Assert.True(o.NoCache);
        Assert.True(o.CrossMounts);
    }

    [Fact]
    public void Exclude_can_be_repeated_and_is_made_absolute()
    {
        var o = Options.Parse(["--exclude", "a", "--exclude", "b"]);

        Assert.Equal(2, o.Excludes.Count);
        Assert.All(o.Excludes, e => Assert.True(Path.IsPathRooted(e)));
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Recognises_help(string flag) => Assert.True(Options.Parse([flag]).Help);

    [Fact]
    public void Recognises_version() => Assert.True(Options.Parse(["--version"]).ShowVersion);

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--top")]
    [InlineData("--path")]
    [InlineData("--exclude")]
    public void Rejects_unknown_or_incomplete_arguments(string arg) =>
        Assert.Throws<ArgumentException>(() => Options.Parse([arg]));

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public void Rejects_non_positive_top(string value) =>
        Assert.Throws<ArgumentException>(() => Options.Parse(["--top", value]));

    [Fact]
    public void Rejects_non_numeric_top() =>
        Assert.Throws<FormatException>(() => Options.Parse(["--top", "many"]));

    [Fact]
    public void Rejects_a_second_positional_argument() =>
        Assert.Throws<ArgumentException>(() => Options.Parse(["a", "b"]));
}
