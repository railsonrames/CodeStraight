using System.Linq;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Converts the only namespace of a file to a file-scoped namespace (<c>namespace X;</c>) when the
/// <c>.editorconfig</c> asks for it, removing one indentation level from its content.
/// </summary>
public static class NamespaceConverter
{
	public static SourceText Convert(SyntaxTree tree, SourceText text, StyleOptions style)
	{
		if (style.NamespaceStyle != NamespaceStyle.FileScoped
			|| tree.Options is not CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp10 }
			|| tree.GetRoot() is not CompilationUnitSyntax unit
			|| unit.Members.Count != 1
			|| unit.Members[0] is not NamespaceDeclarationSyntax ns
			|| ns.Members.OfType<BaseNamespaceDeclarationSyntax>().Any()
			|| ns.OpenBraceToken.IsMissing
			|| ns.CloseBraceToken.IsMissing
			|| HasDirectives(ns.OpenBraceToken)
			|| HasDirectives(ns.CloseBraceToken))
		{
			return text;
		}

		string newLine = style.NewLine ?? DetectNewLine(text);

		// Text that stays between the namespace name and the first member, and after the last member.
		// Comments attached to the braces are kept; whitespace and the braces themselves are dropped.
		int bodyStart = ns.OpenBraceToken.Span.End;
		int bodyEnd = ns.CloseBraceToken.SpanStart;
		string body = Dedent(text, TextSpan.FromBounds(bodyStart, bodyEnd), tree, style);
		string trailingComments = string.Concat(ns.CloseBraceToken.TrailingTrivia
			.Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
			.Select(trivia => " " + trivia.ToFullString()));

		string header = text.ToString(TextSpan.FromBounds(ns.SpanStart, ns.Name.Span.End)) + ";";
		string trimmedBody = body.Trim('\r', '\n');
		string replacement = header + newLine + newLine + trimmedBody.TrimEnd() + trailingComments + newLine;

		return text.WithChanges(new TextChange(TextSpan.FromBounds(ns.SpanStart, ns.FullSpan.End), replacement));
	}

	private static bool HasDirectives(SyntaxToken token) =>
		token.LeadingTrivia.Any(trivia => trivia.IsDirective) || token.TrailingTrivia.Any(trivia => trivia.IsDirective);

	/// <summary>Removes one indentation unit from each line of the span, except lines inside multi-line strings.</summary>
	private static string Dedent(SourceText text, TextSpan span, SyntaxTree tree, StyleOptions style)
	{
		SyntaxNode root = tree.GetRoot();
		var builder = new System.Text.StringBuilder();
		int firstLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
		int lastLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

		for (int number = firstLine; number <= lastLine; number++)
		{
			TextLine line = text.Lines[number];
			int start = number == firstLine ? span.Start : line.Start;
			int end = number == lastLine ? span.End : line.EndIncludingLineBreak;
			string content = text.ToString(TextSpan.FromBounds(start, end));

			if (number != firstLine && !IsInsideMultiLineToken(root, line.Start))
			{
				content = RemoveOneIndent(content, style);
			}

			builder.Append(content);
		}

		return builder.ToString();
	}

	private static bool IsInsideMultiLineToken(SyntaxNode root, int position)
	{
		SyntaxToken token = root.FindToken(position, findInsideTrivia: false);
		return token.Span.Start < position && token.Span.End > position;
	}

	private static string RemoveOneIndent(string line, StyleOptions style)
	{
		if (line.StartsWith("\t"))
		{
			return line.Substring(1);
		}

		int spaces = 0;
		while (spaces < line.Length && spaces < style.IndentSize && line[spaces] == ' ')
		{
			spaces++;
		}

		return line.Substring(spaces);
	}

	internal static string DetectNewLine(SourceText text) =>
		text.Lines.Count > 1 && text.Lines[0].EndIncludingLineBreak - text.Lines[0].End == 2 ? "\r\n" : "\n";
}
