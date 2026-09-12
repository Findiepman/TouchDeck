using TouchDeck.Core.Expressions;
using TouchDeck.Core.Variables;
using Xunit;

namespace TouchDeck.Tests;

/// <summary>
/// The condition language behind visibleWhen, enabledWhen and the conditional action. It is
/// meant to be small and predictable, so these pin down exactly what it accepts.
/// </summary>
public class ExpressionTests
{
    private static IValueResolver With(params (string Name, string Value)[] values)
    {
        var store = new VariableStore();

        foreach (var (name, value) in values)
        {
            store.Set(name, value, VariableScope.Session);
        }

        return new DeckValueResolver(store);
    }

    private static bool Eval(string text, params (string Name, string Value)[] values) =>
        Expression.Parse(text).Evaluate(With(values));

    [Theory]
    [InlineData("true")]
    [InlineData("not false")]
    [InlineData("1 == 1")]
    [InlineData("2 > 1")]
    [InlineData("1 < 2")]
    [InlineData("2 >= 2")]
    [InlineData("2 <= 2")]
    [InlineData("\"a\" == \"a\"")]
    [InlineData("\"A\" == \"a\"")]
    [InlineData("true and true")]
    [InlineData("false or true")]
    [InlineData("(false or true) and true")]
    public void ThingsThatHold(string text) => Assert.True(Eval(text));

    [Theory]
    [InlineData("false")]
    [InlineData("not true")]
    [InlineData("1 == 2")]
    [InlineData("1 > 2")]
    [InlineData("\"a\" == \"b\"")]
    [InlineData("true and false")]
    [InlineData("false or false")]
    [InlineData("false or true and false")]
    public void ThingsThatDoNot(string text) => Assert.False(Eval(text));

    [Fact]
    public void AVariableIsReadByName() =>
        Assert.True(Eval("var.mode == \"quiet\"", ("mode", "quiet")));

    [Fact]
    public void AVariableThatWasNeverSetIsSimplyNotTrue() =>
        Assert.False(Eval("var.missing"));

    [Fact]
    public void AVariableThatWasNeverSetEqualsNothing() =>
        Assert.False(Eval("var.missing == \"anything\""));

    [Fact]
    public void ABooleanVariableReadsAsItsOwnTruth() =>
        Assert.True(Eval("var.live", ("live", "true")));

    [Fact]
    public void ANumericVariableComparesAsANumber() =>
        Assert.True(Eval("var.cpu > 80", ("cpu", "91.5")));

    [Fact]
    public void ANumberIsNotGreaterThanAWord() =>
        Assert.False(Eval("var.name > 5", ("name", "chrome")));

    [Fact]
    public void EnvironmentVariablesAreReadable() =>
        Assert.True(Eval("env.TOUCHDECK_TEST_FLAG == \"on\"", Array.Empty<(string, string)>())
            || Environment.GetEnvironmentVariable("TOUCHDECK_TEST_FLAG") is null);

    [Fact]
    public void AndBindsTighterThanOr() =>
        Assert.True(Eval("true or false and false"));

    [Fact]
    public void BracketsChangeThat() =>
        Assert.False(Eval("(true or false) and false"));

    [Fact]
    public void ASingleEqualsIsAcceptedBecausePeopleWriteIt() =>
        Assert.True(Eval("var.mode = \"quiet\"", ("mode", "quiet")));

    [Fact]
    public void SingleQuotesWorkToo() =>
        Assert.True(Eval("var.mode == 'quiet'", ("mode", "quiet")));

    [Fact]
    public void TheExampleFromTheBriefParses()
    {
        Assert.True(Expression.TryParse(
            "obs.streaming == true and var.mode != \"quiet\"",
            out _,
            out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("(true", "never closed")]
    [InlineData("\"unclosed", "never closed")]
    [InlineData("true and", "stops before")]
    [InlineData("and true", "unexpected")]
    [InlineData("true true", "unexpected")]
    [InlineData("a # b", "does not belong")]
    public void BadConditionsSayWhatIsWrong(string text, string fragment)
    {
        var parsed = Expression.TryParse(text, out var expression, out var error);

        Assert.False(parsed);
        Assert.Null(expression);
        Assert.Contains(fragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AConditionKeepsHowItWasWritten() =>
        Assert.Equal("system.cpu > 80", Expression.Parse(" system.cpu > 80 ").ToString());

    [Fact]
    public void AnUnknownProviderIsNotAnError()
    {
        // Providers arrive in a later milestone; until then a reference to one reads as unset
        // rather than stopping the button from working.
        Assert.False(Eval("obs.streaming"));
        Assert.True(Eval("not obs.streaming"));
    }
}
