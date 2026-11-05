using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Breaks lines longer than <c>max_line_length</c> at syntactic points: after the commas of parameter and
/// argument lists, inside initializers, before <c>=&gt;</c>, around binary operators, before the dots of call
/// chains and, as a last resort, after <c>=</c>. String literals are never split.
/// </summary>
public static class LineWrapper
{
	private const int MaxIterations = 500;

	public static SourceText Wrap(SyntaxTree tree, StyleOptions style, CancellationToken cancellationToken)
	{
		SourceText text = tree.GetText(cancellationToken);
		if (style.MaxLineLength is not int maxLength)
		{
			return text;
		}

		string newLine = style.NewLine ?? NamespaceConverter.DetectNewLine(text);

		// Operators already on a line of their own or on the wrong side are moved first.
		SourceText normalized = NormalizeOperators(tree, style);
		if (!ReferenceEquals(normalized, text))
		{
			text = normalized;
			tree = tree.WithChangedText(text);
		}

		// wrap_tolerance: lines up to this many columns over the limit are accepted as they are.
		int capacity = maxLength + style.WrapTolerance;
		for (int iteration = 0; iteration < MaxIterations; iteration++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SyntaxNode root = tree.GetRoot(cancellationToken);
			List<TextChange>? changes = null;

			foreach (TextLine line in text.Lines)
			{
				if (VisualLength(text, line, style.TabWidth) <= capacity)
				{
					continue;
				}

				changes = PlanWrap(root, text, line, style, newLine)
					?? (style.SplitLongStrings ? PlanStringSplit(root, text, line, style) : null);
				if (changes is not null)
				{
					break;
				}
			}

			if (changes is null)
			{
				break;
			}

			text = text.WithChanges(changes);
			tree = tree.WithChangedText(text);
		}

		return text;
	}

	internal static int VisualLength(SourceText text, TextLine line, int tabWidth)
	{
		int column = 0;
		for (int position = line.Start; position < line.End; position++)
		{
			column = text[position] == '\t' ? column + tabWidth - (column % tabWidth) : column + 1;
		}

		return column;
	}

	private static List<TextChange>? PlanWrap(SyntaxNode root, SourceText text, TextLine line, StyleOptions style, string newLine)
	{
		Candidate? best = root.DescendantNodes(line.Span)
			.Where(node => node.Span.IntersectsWith(line.Span))
			.Where(node => !node.Ancestors().Any(ancestor => ancestor is InterpolatedStringExpressionSyntax))
			.Where(node => style.WrapAttributes || !IsInAttribute(node))
			.Select(node => Candidate.Create(node, style))
			.OfType<Candidate>()
			.Select(candidate => candidate.WithOpenPoints(text))
			.Where(candidate => candidate.Points.Count > 0 && candidate.Points.Any(point => line.Span.Contains(point.Position)))
			.OrderBy(candidate => candidate.Priority)
			.ThenByDescending(candidate => candidate.Node.Span.Length)
			.FirstOrDefault();

		if (best is null)
		{
			return null;
		}

		string baseIndent = style.Canonical(Indentation(text, text.Lines.GetLineFromPosition(best.Node.SpanStart)));
		string innerIndent = baseIndent + style.IndentUnit;

		if (style.WrapListStyle == WrapListStyle.Fill && PlanFill(best.Node, text, style, newLine, innerIndent) is { } fill)
		{
			return fill;
		}

		var changes = best.Points
			.Select(point => new TextChange(point.Gap, newLine + (point.Outdent ? baseIndent : innerIndent)))
			.ToList();

		// The other side of a moved operator: "a +⏎b" becomes "a⏎+ b", never an operator alone on a line.
		changes.AddRange(best.Points
			.Where(point => point.Join is { } join && IsJoinable(text, join))
			.Select(point => new TextChange(point.Join!.Value, " ")));
		return changes;
	}

	/// <summary>
	/// <c>wrap_list_style = fill</c>: puts as many parameters or arguments on each line as fit (up to the limit plus
	/// <c>wrap_tolerance</c>) and breaks after the comma only when the next one does not fit. The first one stays on the
	/// line of the opening parenthesis when it fits there. Returns null when the list cannot be filled (an element that
	/// spans several lines or comments between elements); the one-per-line layout is used then.
	/// </summary>
	private static List<TextChange>? PlanFill(SyntaxNode node, SourceText text, StyleOptions style, string newLine, string innerIndent)
	{
		(SyntaxToken open, IReadOnlyList<SyntaxNode> items, IReadOnlyList<SyntaxToken> separators)? list = node switch
		{
			BaseParameterListSyntax parameters => (parameters.GetFirstToken(), parameters.Parameters.ToList<SyntaxNode>(), parameters.Parameters.GetSeparators().ToList()),
			BaseArgumentListSyntax arguments => (arguments.GetFirstToken(), arguments.Arguments.ToList<SyntaxNode>(), arguments.Arguments.GetSeparators().ToList()),
			AttributeArgumentListSyntax attribute => (attribute.OpenParenToken, attribute.Arguments.ToList<SyntaxNode>(), attribute.Arguments.GetSeparators().ToList()),
			_ => null,
		};

		if (list is not { } value || value.items.Count == 0 || value.separators.Count < value.items.Count - 1)
		{
			return null;
		}

		(SyntaxToken open, IReadOnlyList<SyntaxNode> items, IReadOnlyList<SyntaxToken> separators) = value;
		var gaps = new List<TextSpan> { TextSpan.FromBounds(open.Span.End, items[0].SpanStart) };
		for (int i = 1; i < items.Count; i++)
		{
			gaps.Add(TextSpan.FromBounds(separators[i - 1].Span.End, items[i].SpanStart));
		}

		if (items.Any(item => text.ToString(item.Span).IndexOf('\n') >= 0)
			|| gaps.Any(gap => !IsPlainWhitespaceOrLineBreak(text.ToString(gap))))
		{
			return null;
		}

		int capacity = style.MaxLineLength!.Value + style.WrapTolerance;
		int innerColumn = Columns(innerIndent, style.TabWidth);
		int column = ColumnAt(text, text.Lines.GetLineFromPosition(open.SpanStart), open.Span.End, style.TabWidth);
		var desired = new List<string>();
		for (int i = 0; i < items.Count; i++)
		{
			// Each element is followed by its comma, or by the closing parenthesis for the last one.
			int width = items[i].Span.Length + 1;
			int separatorWidth = i == 0 ? 0 : 1;
			if (column + separatorWidth + width <= capacity)
			{
				desired.Add(i == 0 ? string.Empty : " ");
				column += separatorWidth + width;
			}
			else
			{
				desired.Add(newLine + innerIndent);
				column = innerColumn + width;
			}
		}

		var changes = new List<TextChange>();
		for (int i = 0; i < gaps.Count; i++)
		{
			if (text.ToString(gaps[i]) != desired[i])
			{
				changes.Add(new TextChange(gaps[i], desired[i]));
			}
		}

		return changes.Count == 0 ? null : changes;
	}

	private static bool IsPlainWhitespaceOrLineBreak(string gap) => gap.All(c => c is ' ' or '\t' or '\r' or '\n');

	private static int Columns(string whitespace, int tabWidth)
	{
		int column = 0;
		foreach (char c in whitespace)
		{
			column = c == '\t' ? column + tabWidth - (column % tabWidth) : column + 1;
		}

		return column;
	}

	private static bool IsInAttribute(SyntaxNode node) => node.AncestorsAndSelf().Any(ancestor => ancestor is AttributeListSyntax);

	private static bool IsJoinable(SourceText text, TextSpan gap)
	{
		string content = text.ToString(gap);
		return content.IndexOf('\n') >= 0 && content.IndexOf("//", System.StringComparison.Ordinal) < 0
			&& content.IndexOf("/*", System.StringComparison.Ordinal) < 0 && content.IndexOf('#') < 0;
	}

	/// <summary>
	/// Puts every <c>+</c>, <c>&amp;&amp;</c>, <c>||</c> and <c>??</c> that already starts or ends a line on the side set by
	/// <c>binary_operator_new_line</c>, joined to its operand: no operator is left alone on a line and blank lines inside
	/// the expression disappear. Indentation is fixed afterwards by the continuation indenter.
	/// </summary>
	internal static SourceText NormalizeOperators(SyntaxTree tree, StyleOptions style)
	{
		SourceText text = tree.GetText();
		var changes = new List<TextChange>();
		bool operatorFirst = style.BinaryOperatorNewLine == OperatorNewLine.Before;
		foreach (BinaryExpressionSyntax binary in tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>())
		{
			if (binary.Kind() is not (SyntaxKind.AddExpression or SyntaxKind.LogicalAndExpression
					or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression)
				|| binary.Ancestors().Any(ancestor => ancestor is InterpolatedStringExpressionSyntax))
			{
				continue;
			}

			SyntaxToken op = binary.OperatorToken;
			TextSpan before = TextSpan.FromBounds(op.GetPreviousToken().Span.End, op.SpanStart);
			TextSpan after = TextSpan.FromBounds(op.Span.End, op.GetNextToken().SpanStart);
			TextSpan wrongSide = operatorFirst ? after : before;
			TextSpan rightSide = operatorFirst ? before : after;
			string right = text.ToString(rightSide);
			bool rightOk = right.IndexOf('\n') >= 0 ? IsJoinable(text, rightSide) : right.All(c => c == ' ' || c == '\t');
			if (!IsJoinable(text, wrongSide) || !rightOk)
			{
				continue;
			}

			changes.Add(new TextChange(rightSide, LastLineBreak(text.ToString(wrongSide))));
			changes.Add(new TextChange(wrongSide, " "));
		}

		return changes.Count == 0 ? text : text.WithChanges(changes);
	}

	/// <summary>The last line break of a gap with the indentation that follows it (blank lines are dropped).</summary>
	private static string LastLineBreak(string gap)
	{
		int index = gap.LastIndexOf('\n');
		string lineBreak = index > 0 && gap[index - 1] == '\r' ? "\r\n" : "\n";
		return lineBreak + gap.Substring(index + 1);
	}

	/// <summary>
	/// Last resort for a line that is still too long (<c>split_long_strings</c>): splits the string literal that crosses
	/// the limit at the last space that fits, as <c>"first part " + "rest"</c>. The value does not change and constants
	/// stay constants (attribute arguments keep compiling); the next iteration wraps at the new <c>+</c>.
	/// </summary>
	private static List<TextChange>? PlanStringSplit(SyntaxNode root, SourceText text, TextLine line, StyleOptions style)
	{
		int maxLength = style.MaxLineLength!.Value;
		foreach (SyntaxToken token in root.DescendantTokens(line.Span))
		{
			if (!token.IsKind(SyntaxKind.StringLiteralToken) || token.Span.Start < line.Start || token.Span.End > line.End
				|| token.Parent is not LiteralExpressionSyntax literal || !CanBeConcatenated(literal)
				|| (!style.WrapAttributes && IsInAttribute(literal)))
			{
				continue;
			}

			string raw = token.Text;
			bool verbatim = raw.StartsWith("@\"", System.StringComparison.Ordinal);
			if (!(verbatim || raw.StartsWith("\"", System.StringComparison.Ordinal))
				|| !raw.EndsWith("\"", System.StringComparison.Ordinal) || raw.IndexOf('\n') >= 0)
			{
				continue;
			}

			int startColumn = ColumnAt(text, line, token.SpanStart, style.TabWidth);
			if (startColumn + raw.Length <= maxLength)
			{
				continue;
			}

			// The first part, with its closing quote, has to end before the limit.
			int contentStart = verbatim ? 2 : 1;
			int lastAllowed = Math.Min(maxLength - startColumn - 2, raw.Length - 2);
			int split = lastAllowed > contentStart ? raw.LastIndexOf(' ', lastAllowed) : -1;
			if (split < contentStart)
			{
				continue;
			}

			string first = raw.Substring(0, split + 1) + "\"";
			string rest = (verbatim ? "@\"" : "\"") + raw.Substring(split + 1);
			return new List<TextChange> { new(token.Span, first + " + " + rest) };
		}

		return null;
	}

	/// <summary>Only where <c>"a" + "b"</c> means the same as <c>"ab"</c> without adding parentheses.</summary>
	private static bool CanBeConcatenated(LiteralExpressionSyntax literal) => literal.Parent switch
	{
		ArgumentSyntax or AttributeArgumentSyntax or EqualsValueClauseSyntax or ReturnStatementSyntax
			or ArrowExpressionClauseSyntax or InitializerExpressionSyntax or ExpressionElementSyntax => true,
		AssignmentExpressionSyntax assignment => assignment.Right == literal,
		BinaryExpressionSyntax binary => binary.IsKind(SyntaxKind.AddExpression),
		ConditionalExpressionSyntax conditional => conditional.Condition != literal,
		_ => false,
	};

	private static int ColumnAt(SourceText text, TextLine line, int position, int tabWidth)
	{
		int column = 0;
		for (int index = line.Start; index < position; index++)
		{
			column = text[index] == '\t' ? column + tabWidth - (column % tabWidth) : column + 1;
		}

		return column;
	}

	private static string Indentation(SourceText text, TextLine line)
	{
		int end = line.Start;
		while (end < line.End && (text[end] == ' ' || text[end] == '\t'))
		{
			end++;
		}

		return text.ToString(TextSpan.FromBounds(line.Start, end));
	}

	/// <summary>A place where a line break can be inserted, replacing the whitespace in <see cref="Gap"/>.</summary>
	private sealed class WrapPoint
	{
		public WrapPoint(TextSpan gap, bool outdent, TextSpan? join = null)
		{
			Gap = gap;
			Outdent = outdent;
			Join = join;
		}

		public TextSpan Gap { get; }

		/// <summary>Whitespace on the other side of a wrapped operator, collapsed to a space if it holds a line break.</summary>
		public TextSpan? Join { get; }

		/// <summary>The new line goes back to the base indentation (closing braces).</summary>
		public bool Outdent { get; }

		public int Position => Gap.Start;
	}

	private sealed class Candidate
	{
		private Candidate(SyntaxNode node, int priority, IReadOnlyList<WrapPoint> points)
		{
			Node = node;
			Priority = priority;
			Points = points;
		}

		public SyntaxNode Node { get; }

		/// <summary>Lower runs first; assignments are only wrapped when nothing else helps.</summary>
		public int Priority { get; }

		public IReadOnlyList<WrapPoint> Points { get; }

		/// <summary>Keeps only the points that are not already followed by a line break and contain no comments.</summary>
		public Candidate WithOpenPoints(SourceText text) =>
			new(Node, Priority, Points.Where(point => IsOpen(text, point.Gap)).ToList());

		private static bool IsOpen(SourceText text, TextSpan gap)
		{
			string content = text.ToString(gap);
			return content.IndexOf('\n') < 0 && content.IndexOf("//", System.StringComparison.Ordinal) < 0
				&& content.IndexOf("/*", System.StringComparison.Ordinal) < 0;
		}

		public static Candidate? Create(SyntaxNode node, StyleOptions style)
		{
			switch (node)
			{
				case BaseParameterListSyntax parameters when parameters.Parameters.Count > 0:
					return List(node, OpenToken(parameters), parameters.Parameters.GetSeparators(), minimumItems: 1, parameters.Parameters.Count);

				case BaseArgumentListSyntax arguments when arguments.Arguments.Count > 1:
					return List(node, OpenToken(arguments), arguments.Arguments.GetSeparators(), minimumItems: 2, arguments.Arguments.Count);

				case AttributeArgumentListSyntax attributeArguments when attributeArguments.Arguments.Count > 1:
					return List(node, attributeArguments.OpenParenToken, attributeArguments.Arguments.GetSeparators(), minimumItems: 2, attributeArguments.Arguments.Count);

				case InitializerExpressionSyntax initializer when initializer.Expressions.Count > 0:
					return Braced(node, initializer.OpenBraceToken, initializer.Expressions.GetSeparators(), initializer.CloseBraceToken, braceOnOwnLine: true);

				case AnonymousObjectCreationExpressionSyntax anonymous when anonymous.Initializers.Count > 0:
					return Braced(node, anonymous.OpenBraceToken, anonymous.Initializers.GetSeparators(), anonymous.CloseBraceToken, braceOnOwnLine: true);

				case CollectionExpressionSyntax collection when collection.Elements.Count > 0:
					return Braced(node, collection.OpenBracketToken, collection.Elements.GetSeparators(), collection.CloseBracketToken, braceOnOwnLine: false);

				case AccessorListSyntax accessors when accessors.Accessors.Count > 0:
					return AccessorList(accessors);

				// "get => value" inside an accessor list: the list is wrapped instead (one accessor per line).
				case ArrowExpressionClauseSyntax arrow when arrow.Parent is not AccessorDeclarationSyntax:
					return new Candidate(node, 0, new[] { Before(arrow.ArrowToken) });

				case BinaryExpressionSyntax binary when IsChainOperator(binary) && !IsSameChain(binary.Parent, binary):
					return BinaryChain(binary, style);

				case ConditionalExpressionSyntax conditional:
					return new Candidate(node, 0, new[] { Before(conditional.QuestionToken), Before(conditional.ColonToken) });

				case InvocationExpressionSyntax invocation when !IsInsideChain(invocation):
					return CallChain(invocation);

				case EqualsValueClauseSyntax equals:
					return new Candidate(node, 1, new[] { After(equals.EqualsToken) });

				case AssignmentExpressionSyntax assignment:
					return new Candidate(node, 1, new[] { After(assignment.OperatorToken) });

				default:
					return null;
			}
		}

		private static SyntaxToken OpenToken(BaseParameterListSyntax list) => list.GetFirstToken();

		private static SyntaxToken OpenToken(BaseArgumentListSyntax list) => list.GetFirstToken();

		private static Candidate? List(SyntaxNode node, SyntaxToken open, IEnumerable<SyntaxToken> separators, int minimumItems, int count)
		{
			if (count < minimumItems || open.GetNextToken().IsKind(SyntaxKind.CloseParenToken))
			{
				return null;
			}

			var points = new List<WrapPoint> { After(open) };
			points.AddRange(separators.Select(After));
			return new Candidate(node, 0, points);
		}

		/// <summary>
		/// Braces of initializers go on their own line at the base indentation (Visual Studio's default style);
		/// the brackets of collection expressions stay where they are.
		/// </summary>
		private static Candidate Braced(SyntaxNode node, SyntaxToken open, IEnumerable<SyntaxToken> separators, SyntaxToken close, bool braceOnOwnLine)
		{
			var points = new List<WrapPoint>();
			if (braceOnOwnLine && !open.GetPreviousToken().IsKind(SyntaxKind.None))
			{
				points.Add(new WrapPoint(Gap(open.GetPreviousToken(), open), outdent: true));
			}

			points.Add(After(open));
			points.AddRange(separators.Where(separator => !separator.GetNextToken().Equals(close)).Select(After));
			points.Add(new WrapPoint(Gap(close.GetPreviousToken(), close), outdent: true));
			return new Candidate(node, 0, points);
		}

		/// <summary><c>{ get; set; }</c> becomes a block with one accessor per line and the braces at the property level.</summary>
		private static Candidate AccessorList(AccessorListSyntax accessors)
		{
			var points = new List<WrapPoint>
			{
				new(Gap(accessors.OpenBraceToken.GetPreviousToken(), accessors.OpenBraceToken), outdent: true),
				After(accessors.OpenBraceToken),
			};

			points.AddRange(accessors.Accessors.Skip(1).Select(accessor => Before(accessor.GetFirstToken())));
			points.Add(new WrapPoint(Gap(accessors.CloseBraceToken.GetPreviousToken(), accessors.CloseBraceToken), outdent: true));
			return new Candidate(accessors, 0, points);
		}

		private static Candidate BinaryChain(BinaryExpressionSyntax top, StyleOptions style)
		{
			var operators = new List<SyntaxToken>();
			ExpressionSyntax current = top;
			while (current is BinaryExpressionSyntax binary && binary.IsKind(top.Kind()))
			{
				operators.Add(binary.OperatorToken);
				current = binary.Left;
			}

			operators.Reverse();
			IEnumerable<WrapPoint> points = style.BinaryOperatorNewLine == OperatorNewLine.After
				? operators.Select(op => new WrapPoint(Gap(op, op.GetNextToken()), outdent: false, join: Gap(op.GetPreviousToken(), op)))
				: operators.Select(op => new WrapPoint(Gap(op.GetPreviousToken(), op), outdent: false, join: Gap(op, op.GetNextToken())));
			return new Candidate(top, 0, points.ToList());
		}

		private static Candidate? CallChain(InvocationExpressionSyntax top)
		{
			var dots = new List<SyntaxToken>();
			ExpressionSyntax current = top;
			while (current is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax member)
			{
				dots.Add(member.OperatorToken);
				current = member.Expression;
			}

			if (dots.Count < 2)
			{
				return null;
			}

			dots.Reverse();
			return new Candidate(top, 0, dots.Select(Before).ToList());
		}

		private static bool IsChainOperator(BinaryExpressionSyntax binary) =>
			binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.LogicalAndExpression
				or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression;

		private static bool IsSameChain(SyntaxNode? parent, BinaryExpressionSyntax child) =>
			parent is BinaryExpressionSyntax binary && binary.IsKind(child.Kind()) && binary.Left == child;

		/// <summary>An invocation that is the receiver of another call in the same chain is handled by the outer one.</summary>
		private static bool IsInsideChain(InvocationExpressionSyntax invocation) =>
			invocation.Parent is MemberAccessExpressionSyntax member && member.Expression == invocation
			&& member.Parent is InvocationExpressionSyntax;

		private static WrapPoint After(SyntaxToken token) => new(Gap(token, token.GetNextToken()), outdent: false);

		private static WrapPoint Before(SyntaxToken token) => new(Gap(token.GetPreviousToken(), token), outdent: false);

		private static TextSpan Gap(SyntaxToken left, SyntaxToken right) =>
			TextSpan.FromBounds(left.Span.End, System.Math.Max(left.Span.End, right.SpanStart));
	}
}
