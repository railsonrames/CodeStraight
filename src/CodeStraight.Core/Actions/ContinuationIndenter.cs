using System.Collections.Generic;
using System.Linq;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Re-indents continuation lines inside expressions, which Roslyn's formatter leaves untouched:
/// arguments and parameters on their own lines, elements of initializers and collections, call chains and
/// operators that start a line. Rules:
/// <list type="bullet">
/// <item>Inside an open <c>(</c>, <c>[</c> or <c>{</c>: one level more than the line that opened it.</item>
/// <item>A closing bracket at the start of a line: same level as the line that opened it.</item>
/// <item>A line starting with <c>.</c>, <c>?.</c> or an operator inside brackets: one extra level.</item>
/// <item>Outside brackets: one level more than the statement, except braces/brackets that open an initializer
/// or collection, which stay at the statement level.</item>
/// </list>
/// Only lines whose syntax path to the statement or member goes through expressions are touched; blocks,
/// switch sections, clauses and declarations keep the indentation of the formatter.
/// </summary>
public static class ContinuationIndenter
{
	public static SourceText Normalize(SyntaxTree tree, StyleOptions style)
	{
		SourceText text = tree.GetText();
		SyntaxNode root = tree.GetRoot();
		var indents = new Dictionary<int, string>();

		foreach (TextLine line in text.Lines)
		{
			int firstChar = FirstNonWhitespace(text, line);
			if (firstChar < 0)
			{
				continue;
			}

			SyntaxToken token = root.FindToken(firstChar);
			if (token.SpanStart != firstChar || token.IsKind(SyntaxKind.EndOfFileToken))
			{
				// Comment, directive or a line inside a multi-line string.
				continue;
			}

			string? desired = DesiredIndent(token, text, style, indents);
			if (desired is not null && desired != Indent(text, line, indents))
			{
				indents[line.LineNumber] = desired;
			}
		}

		if (indents.Count == 0)
		{
			return text;
		}

		IEnumerable<TextChange> changes = indents.Select(entry =>
		{
			TextLine line = text.Lines[entry.Key];
			return new TextChange(TextSpan.FromBounds(line.Start, FirstNonWhitespace(text, line)), entry.Value);
		});

		return text.WithChanges(changes);
	}

	private static string? DesiredIndent(SyntaxToken token, SourceText text, StyleOptions style, Dictionary<int, string> indents)
	{
		SyntaxNode? anchor = FindAnchor(token, out SyntaxToken anchorToken);
		if (anchor is null)
		{
			return null;
		}

		int tokenLine = text.Lines.GetLineFromPosition(token.SpanStart).LineNumber;
		int anchorLine = text.Lines.GetLineFromPosition(anchorToken.SpanStart).LineNumber;
		if (tokenLine <= anchorLine)
		{
			return null;
		}

		// Open brackets between the start of the anchor and this token.
		var open = new Stack<SyntaxToken>();
		foreach (SyntaxToken current in anchor.DescendantTokens())
		{
			if (current.SpanStart >= token.SpanStart)
			{
				break;
			}

			if (current.SpanStart < anchorToken.SpanStart || IsBlockBrace(current))
			{
				continue;
			}

			if (current.Kind() is SyntaxKind.OpenParenToken or SyntaxKind.OpenBracketToken or SyntaxKind.OpenBraceToken)
			{
				open.Push(current);
			}
			else if (current.Kind() is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.CloseBraceToken
				&& open.Count > 0)
			{
				open.Pop();
			}
		}

		// ".Method()", "|| condition", "? a : b": one level inside the line where that expression or chain starts.
		if (ContinuedExpression(token) is { } expression)
		{
			int startLine = text.Lines.GetLineFromPosition(expression.SpanStart).LineNumber;
			if (startLine < tokenLine)
			{
				return LineIndent(startLine, text, indents, style) + style.IndentUnit;
			}
		}

		string anchorIndent = LineIndent(anchorLine, text, indents, style);
		if (open.Count == 0)
		{
			return OpensInitializer(token) ? anchorIndent : anchorIndent + style.IndentUnit;
		}

		string openerIndent = LineIndent(text.Lines.GetLineFromPosition(open.Peek().SpanStart).LineNumber, text, indents, style);
		if (token.Kind() is SyntaxKind.CloseParenToken or SyntaxKind.CloseBracketToken or SyntaxKind.CloseBraceToken)
		{
			return openerIndent;
		}

		return openerIndent + style.IndentUnit;
	}

	/// <summary>
	/// For a line that starts with a member-access dot, a binary operator or the <c>?</c>/<c>:</c> of a conditional,
	/// the whole expression it continues: the top of the call chain, of the operator chain or the conditional.
	/// </summary>
	private static SyntaxNode? ContinuedExpression(SyntaxToken token)
	{
		SyntaxNode? parent = token.Parent;
		switch (parent)
		{
			case BinaryExpressionSyntax binary when binary.OperatorToken == token:
				SyntaxNode top = binary;
				while (top.Parent is BinaryExpressionSyntax outer && outer.IsKind(binary.Kind()))
				{
					top = outer;
				}

				return top;
			case ConditionalExpressionSyntax conditional
				when token == conditional.QuestionToken || token == conditional.ColonToken:
				return conditional;
			case MemberAccessExpressionSyntax or MemberBindingExpressionSyntax when token.IsKind(SyntaxKind.DotToken):
				SyntaxNode chain = parent;
				while (chain.Parent is MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax
					or ConditionalAccessExpressionSyntax or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression })
				{
					chain = chain.Parent;
				}

				return chain;
			default:
				return null;
		}
	}

	/// <summary>
	/// The statement, member or attribute list the token continues, provided every node in between is part
	/// of an expression (so the line is a continuation and not a nested statement or clause).
	/// </summary>
	private static SyntaxNode? FindAnchor(SyntaxToken token, out SyntaxToken anchorToken)
	{
		anchorToken = default;
		if (IsBlockBrace(token))
		{
			return null;
		}

		for (SyntaxNode? node = token.Parent; node is not null; node = node.Parent)
		{
			switch (node)
			{
				case AttributeListSyntax attributes:
					anchorToken = attributes.GetFirstToken();
					return attributes;
				case MemberDeclarationSyntax member when member is not (BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax or EnumDeclarationSyntax):
					anchorToken = FirstTokenAfterAttributes(member);
					return member;
				case StatementSyntax statement when statement is not BlockSyntax:
					anchorToken = statement.GetFirstToken();
					return statement;
			}

			if (!IsExpressionPart(node))
			{
				return null;
			}
		}

		return null;
	}

	private static bool IsExpressionPart(SyntaxNode node) =>
		node is ExpressionSyntax or ArgumentSyntax or BaseArgumentListSyntax or AttributeArgumentListSyntax or AttributeSyntax
			or AttributeArgumentSyntax or BaseParameterListSyntax or ParameterSyntax or EqualsValueClauseSyntax
			or VariableDeclaratorSyntax or VariableDeclarationSyntax or ArrowExpressionClauseSyntax
			or CollectionElementSyntax or AnonymousObjectMemberDeclaratorSyntax or SwitchExpressionArmSyntax
			or TypeArgumentListSyntax or PatternSyntax or SubpatternSyntax or WhenClauseSyntax
			or InterpolationSyntax or BracketedArgumentListSyntax;

	private static SyntaxToken FirstTokenAfterAttributes(MemberDeclarationSyntax member) =>
		member.AttributeLists.Count > 0 ? member.AttributeLists.Last().GetLastToken().GetNextToken() : member.GetFirstToken();

	/// <summary>Braces of blocks, accessors, types and switch statements are positioned by the formatter.</summary>
	private static bool IsBlockBrace(SyntaxToken token) =>
		token.Kind() is SyntaxKind.OpenBraceToken or SyntaxKind.CloseBraceToken
		&& token.Parent is BlockSyntax or AccessorListSyntax or BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax
			or SwitchStatementSyntax or PropertyPatternClauseSyntax;

	/// <summary>A brace or bracket that starts an initializer or a collection stays at the statement level.</summary>
	private static bool OpensInitializer(SyntaxToken token) =>
		(token.IsKind(SyntaxKind.OpenBraceToken) && token.Parent is InitializerExpressionSyntax or AnonymousObjectCreationExpressionSyntax)
		|| (token.IsKind(SyntaxKind.OpenBracketToken) && token.Parent is CollectionExpressionSyntax);

	/// <summary>Reference indentation of a line: the new one if it was already fixed, else the current one as whole levels.</summary>
	private static string LineIndent(int lineNumber, SourceText text, Dictionary<int, string> indents, StyleOptions style) =>
		indents.TryGetValue(lineNumber, out string? indent) ? indent : style.Canonical(Indent(text, text.Lines[lineNumber], indents));

	private static string Indent(SourceText text, TextLine line, Dictionary<int, string> indents)
	{
		if (indents.TryGetValue(line.LineNumber, out string? indent))
		{
			return indent;
		}

		int first = FirstNonWhitespace(text, line);
		return first < 0 ? string.Empty : text.ToString(TextSpan.FromBounds(line.Start, first));
	}

	private static int FirstNonWhitespace(SourceText text, TextLine line)
	{
		for (int position = line.Start; position < line.End; position++)
		{
			if (text[position] != ' ' && text[position] != '\t')
			{
				return position;
			}
		}

		return -1;
	}
}
