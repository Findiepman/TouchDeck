using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TouchDeck.Core.Expressions;

/// <summary>Where an expression looks up a name such as <c>var.mode</c> or <c>system.cpu</c>.</summary>
public interface IValueResolver
{
    /// <summary>Resolves a dotted reference to its current value.</summary>
    /// <param name="reference">The whole reference, for example <c>obs.streaming</c>.</param>
    /// <param name="value">The value, written as text, when the reference is known.</param>
    bool TryResolve(string reference, [NotNullWhen(true)] out string? value);
}

/// <summary>A resolver that knows nothing, so every reference is unset.</summary>
public sealed class EmptyResolver : IValueResolver
{
    /// <summary>The single instance.</summary>
    public static EmptyResolver Instance { get; } = new();

    /// <inheritdoc />
    public bool TryResolve(string reference, [NotNullWhen(true)] out string? value)
    {
        value = null;
        return false;
    }
}

/// <summary>
/// A parsed condition, as used by <c>visibleWhen</c>, <c>enabledWhen</c> and the
/// <c>conditional</c> action. Deliberately tiny: comparisons, and, or, not. It is not a
/// scripting language and cannot call anything.
/// </summary>
public sealed class Expression
{
    private readonly Node _root;

    private Expression(Node root, string text)
    {
        _root = root;
        Text = text;
    }

    /// <summary>The expression as it was written.</summary>
    public string Text { get; }

    /// <summary>Parses an expression, reporting why it failed rather than throwing.</summary>
    /// <param name="text">The expression as written in config.</param>
    /// <param name="expression">The parsed expression when parsing succeeds.</param>
    /// <param name="error">A message suitable for showing the user when it fails.</param>
    public static bool TryParse(
        string? text,
        [NotNullWhen(true)] out Expression? expression,
        [NotNullWhen(false)] out string? error)
    {
        expression = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "The condition is empty.";
            return false;
        }

        if (!Tokeniser.TryTokenise(text, out var tokens, out error))
        {
            return false;
        }

        var parser = new Parser(tokens);

        if (!parser.TryParse(out var root, out error))
        {
            return false;
        }

        expression = new Expression(root, text.Trim());
        return true;
    }

    /// <summary>Parses an expression, throwing <see cref="FormatException"/> when it cannot.</summary>
    /// <param name="text">The expression as written in config.</param>
    public static Expression Parse(string? text) =>
        TryParse(text, out var expression, out var error) ? expression : throw new FormatException(error);

    /// <summary>Evaluates the expression against current values.</summary>
    /// <param name="resolver">Where references are looked up.</param>
    /// <returns>True when the condition holds. An unknown reference reads as unset, not as an error.</returns>
    public bool Evaluate(IValueResolver resolver) => Value.IsTrue(_root.Evaluate(resolver));

    /// <summary>The expression as it was written.</summary>
    public override string ToString() => Text;

    /// <summary>A value while an expression is being worked out. Text, number or truth.</summary>
    internal readonly record struct Value(string? Text, double? Number, bool? Truth)
    {
        public static Value Unset { get; } = new(null, null, null);

        public static Value Of(bool truth) => new(truth ? "true" : "false", null, truth);

        public static Value Of(double number) =>
            new(number.ToString(CultureInfo.InvariantCulture), number, null);

        /// <summary>Wraps text, reading it as a number or a truth value where it looks like one.</summary>
        public static Value Of(string? text)
        {
            if (text is null)
            {
                return Unset;
            }

            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
            {
                return new Value(text, number, null);
            }

            if (bool.TryParse(text, out var truth))
            {
                return new Value(text, null, truth);
            }

            return new Value(text, null, null);
        }

        public static bool IsTrue(Value value) =>
            value.Truth ?? (value.Number is { } number ? number != 0 : false);
    }

    private abstract class Node
    {
        public abstract Value Evaluate(IValueResolver resolver);
    }

    private sealed class Literal(Value value) : Node
    {
        public override Value Evaluate(IValueResolver resolver) => value;
    }

    private sealed class Reference(string name) : Node
    {
        public string Name => name;

        public override Value Evaluate(IValueResolver resolver) =>
            resolver.TryResolve(name, out var text) ? Value.Of(text) : Value.Unset;
    }

    private sealed class Not(Node inner) : Node
    {
        public override Value Evaluate(IValueResolver resolver) =>
            Value.Of(!Value.IsTrue(inner.Evaluate(resolver)));
    }

    private sealed class AndOr(Node left, Node right, bool isAnd) : Node
    {
        public override Value Evaluate(IValueResolver resolver)
        {
            var first = Value.IsTrue(left.Evaluate(resolver));

            // Short circuits, so "var.x != null and var.x > 5" cannot blow up on the right.
            if (isAnd && !first)
            {
                return Value.Of(false);
            }

            if (!isAnd && first)
            {
                return Value.Of(true);
            }

            return Value.Of(Value.IsTrue(right.Evaluate(resolver)));
        }
    }

    private sealed class Comparison(Node left, Node right, TokenKind op) : Node
    {
        public override Value Evaluate(IValueResolver resolver)
        {
            var a = left.Evaluate(resolver);
            var b = right.Evaluate(resolver);

            if (op is TokenKind.Equal or TokenKind.NotEqual)
            {
                var same = AreSame(a, b);
                return Value.Of(op == TokenKind.Equal ? same : !same);
            }

            // Ordering only makes sense for numbers. Anything else is simply not greater.
            if (a.Number is not { } first || b.Number is not { } second)
            {
                return Value.Of(false);
            }

            return Value.Of(op switch
            {
                TokenKind.Less => first < second,
                TokenKind.LessOrEqual => first <= second,
                TokenKind.Greater => first > second,
                TokenKind.GreaterOrEqual => first >= second,
                _ => false,
            });
        }

        private static bool AreSame(Value a, Value b)
        {
            if (a.Number is { } x && b.Number is { } y)
            {
                return Math.Abs(x - y) < 1e-9;
            }

            if (a.Truth is { } p && b.Truth is { } q)
            {
                return p == q;
            }

            // Process names and scene names are compared the way a person would write them.
            return string.Equals(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private enum TokenKind
    {
        Reference,
        String,
        Number,
        True,
        False,
        And,
        Or,
        Not,
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
        OpenParen,
        CloseParen,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, double Number, int Position);

    private static class Tokeniser
    {
        public static bool TryTokenise(
            string text,
            [NotNullWhen(true)] out List<Token>? tokens,
            [NotNullWhen(false)] out string? error)
        {
            tokens = new List<Token>();
            error = null;

            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                var start = i;

                switch (c)
                {
                    case '(':
                        tokens.Add(new Token(TokenKind.OpenParen, "(", 0, start));
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new Token(TokenKind.CloseParen, ")", 0, start));
                        i++;
                        continue;
                    case '"':
                    case '\'':
                        if (!TryReadString(text, ref i, c, out var literal, out error))
                        {
                            tokens = null;
                            return false;
                        }

                        tokens.Add(new Token(TokenKind.String, literal, 0, start));
                        continue;
                }

                if (TryReadOperator(text, ref i, out var op))
                {
                    tokens.Add(new Token(op, text[start..i], 0, start));
                    continue;
                }

                if (char.IsDigit(c) || (c == '-' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.' || (i == start && text[i] == '-')))
                    {
                        i++;
                    }

                    var span = text[start..i];

                    if (!double.TryParse(span, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
                    {
                        error = $"\"{span}\" is not a number.";
                        tokens = null;
                        return false;
                    }

                    tokens.Add(new Token(TokenKind.Number, span, number, start));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.'))
                    {
                        i++;
                    }

                    var word = text[start..i];

                    tokens.Add(word.ToLowerInvariant() switch
                    {
                        "and" => new Token(TokenKind.And, word, 0, start),
                        "or" => new Token(TokenKind.Or, word, 0, start),
                        "not" => new Token(TokenKind.Not, word, 0, start),
                        "true" => new Token(TokenKind.True, word, 0, start),
                        "false" => new Token(TokenKind.False, word, 0, start),
                        _ => new Token(TokenKind.Reference, word, 0, start),
                    });

                    continue;
                }

                error = $"\"{c}\" does not belong in a condition (position {start + 1}).";
                tokens = null;
                return false;
            }

            tokens.Add(new Token(TokenKind.End, "", 0, text.Length));
            return true;
        }

        private static bool TryReadString(
            string text,
            ref int i,
            char quote,
            out string literal,
            [NotNullWhen(false)] out string? error)
        {
            literal = "";
            error = null;

            var closing = text.IndexOf(quote, i + 1);

            if (closing < 0)
            {
                error = $"A quote at position {i + 1} was never closed.";
                return false;
            }

            literal = text[(i + 1)..closing];
            i = closing + 1;
            return true;
        }

        private static bool TryReadOperator(string text, ref int i, out TokenKind kind)
        {
            var two = i + 1 < text.Length ? text.Substring(i, 2) : "";

            switch (two)
            {
                case "==":
                    kind = TokenKind.Equal;
                    i += 2;
                    return true;
                case "!=":
                    kind = TokenKind.NotEqual;
                    i += 2;
                    return true;
                case "<=":
                    kind = TokenKind.LessOrEqual;
                    i += 2;
                    return true;
                case ">=":
                    kind = TokenKind.GreaterOrEqual;
                    i += 2;
                    return true;
            }

            switch (text[i])
            {
                case '<':
                    kind = TokenKind.Less;
                    i++;
                    return true;
                case '>':
                    kind = TokenKind.Greater;
                    i++;
                    return true;
                case '=':
                    // A single "=" is such a common slip that it is simply accepted.
                    kind = TokenKind.Equal;
                    i++;
                    return true;
            }

            kind = TokenKind.End;
            return false;
        }
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _at;

        public bool TryParse([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            node = null;

            if (!TryOr(out var root, out error))
            {
                return false;
            }

            if (Current.Kind != TokenKind.End)
            {
                error = $"\"{Current.Text}\" is unexpected here (position {Current.Position + 1}).";
                return false;
            }

            node = root;
            return true;
        }

        private Token Current => tokens[_at];

        private bool TryOr([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            if (!TryAnd(out node, out error))
            {
                return false;
            }

            while (Current.Kind == TokenKind.Or)
            {
                _at++;

                if (!TryAnd(out var right, out error))
                {
                    node = null;
                    return false;
                }

                node = new AndOr(node, right, isAnd: false);
            }

            return true;
        }

        private bool TryAnd([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            if (!TryNot(out node, out error))
            {
                return false;
            }

            while (Current.Kind == TokenKind.And)
            {
                _at++;

                if (!TryNot(out var right, out error))
                {
                    node = null;
                    return false;
                }

                node = new AndOr(node, right, isAnd: true);
            }

            return true;
        }

        private bool TryNot([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            if (Current.Kind != TokenKind.Not)
            {
                return TryComparison(out node, out error);
            }

            _at++;

            if (!TryNot(out var inner, out error))
            {
                node = null;
                return false;
            }

            node = new Not(inner);
            return true;
        }

        private bool TryComparison([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            if (!TryPrimary(out node, out error))
            {
                return false;
            }

            if (Current.Kind is not (TokenKind.Equal or TokenKind.NotEqual or TokenKind.Less
                or TokenKind.LessOrEqual or TokenKind.Greater or TokenKind.GreaterOrEqual))
            {
                return true;
            }

            var op = Current.Kind;
            _at++;

            if (!TryPrimary(out var right, out error))
            {
                node = null;
                return false;
            }

            node = new Comparison(node, right, op);
            return true;
        }

        private bool TryPrimary([NotNullWhen(true)] out Node? node, [NotNullWhen(false)] out string? error)
        {
            node = null;
            error = null;

            var token = Current;

            switch (token.Kind)
            {
                case TokenKind.OpenParen:
                    _at++;

                    if (!TryOr(out node, out error))
                    {
                        return false;
                    }

                    if (Current.Kind != TokenKind.CloseParen)
                    {
                        error = $"A bracket opened at position {token.Position + 1} was never closed.";
                        node = null;
                        return false;
                    }

                    _at++;
                    return true;

                case TokenKind.String:
                    _at++;
                    node = new Literal(Value.Of(token.Text));
                    return true;

                case TokenKind.Number:
                    _at++;
                    node = new Literal(Value.Of(token.Number));
                    return true;

                case TokenKind.True:
                    _at++;
                    node = new Literal(Value.Of(true));
                    return true;

                case TokenKind.False:
                    _at++;
                    node = new Literal(Value.Of(false));
                    return true;

                case TokenKind.Reference:
                    _at++;
                    node = new Reference(token.Text);
                    return true;

                case TokenKind.End:
                    error = "The condition stops before it says anything.";
                    return false;

                default:
                    error = $"\"{token.Text}\" is unexpected here (position {token.Position + 1}).";
                    return false;
            }
        }
    }
}
