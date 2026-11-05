using System.Collections.Generic;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Removes surplus blank lines following the Roslyn options of the <c>.editorconfig</c>:
/// <c>dotnet_style_allow_multiple_blank_lines_experimental = false</c> keeps at most one blank line in a row
/// (IDE2000) and <c>csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false</c> removes
/// blank lines between closing braces (IDE2002). Lines inside multi-line strings are never touched.
/// </summary>
public static class BlankLineRemover
{
	public static SourceText Remove(SyntaxTree tree, StyleOptions style)
	{
		SourceText text = tree.GetText();
		if (style.AllowMultipleBlankLines && style.AllowBlankLinesBetweenConsecutiveBraces)
		{
			return text;
		}

		SyntaxNode root = tree.GetRoot();
		var removals = new List<TextChange>();
		int previousBlanks = 0;

		for (int number = 0; number < text.Lines.Count; number++)
		{
			TextLine line = text.Lines[number];
			if (!IsBlank(text, line) || IsInsideToken(root, line.Start))
			{
				previousBlanks = 0;
				continue;
			}

			previousBlanks++;
			bool surplus = !style.AllowMultipleBlankLines && previousBlanks > 1;
			bool betweenBraces = !style.AllowBlankLinesBetweenConsecutiveBraces && IsBetweenClosingBraces(text, number);
			if (surplus || betweenBraces)
			{
				removals.Add(new TextChange(TextSpan.FromBounds(line.Start, line.EndIncludingLineBreak), string.Empty));
			}
		}

		return removals.Count == 0 ? text : text.WithChanges(removals);
	}

	/// <summary>The blank run containing this line sits between a line ending in <c>}</c> and one starting with <c>}</c>.</summary>
	private static bool IsBetweenClosingBraces(SourceText text, int lineNumber)
	{
		int before = lineNumber - 1;
		while (before >= 0 && IsBlank(text, text.Lines[before]))
		{
			before--;
		}

		int after = lineNumber + 1;
		while (after < text.Lines.Count && IsBlank(text, text.Lines[after]))
		{
			after++;
		}

		return before >= 0 && after < text.Lines.Count
			&& text.ToString(text.Lines[before].Span).TrimEnd().EndsWith("}")
			&& text.ToString(text.Lines[after].Span).TrimStart().StartsWith("}");
	}

	private static bool IsBlank(SourceText text, TextLine line)
	{
		for (int position = line.Start; position < line.End; position++)
		{
			if (!char.IsWhiteSpace(text[position]))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>True when the line starts inside a token (a verbatim, raw or interpolated multi-line string).</summary>
	private static bool IsInsideToken(SyntaxNode root, int position)
	{
		SyntaxToken token = root.FindToken(position, findInsideTrivia: false);
		return token.Span.Start < position && token.Span.End > position && !token.IsKind(SyntaxKind.EndOfFileToken);
	}
}
