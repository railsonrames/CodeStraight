using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Makes every line break of a file the one set by <c>end_of_line</c> in the <c>.editorconfig</c>. Visual Studio only
/// applies that option to the lines typed afterwards, so files with mixed line breaks stay mixed without this.
/// Works on any text file, not only C#.
/// </summary>
public static class LineEndingNormalizer
{
	public static IReadOnlyList<TextChange> Changes(SourceText text, string newLine)
	{
		var changes = new List<TextChange>();
		foreach (TextLine line in text.Lines)
		{
			TextSpan lineBreak = TextSpan.FromBounds(line.End, line.EndIncludingLineBreak);
			if (lineBreak.Length > 0 && text.ToString(lineBreak) != newLine)
			{
				changes.Add(new TextChange(lineBreak, newLine));
			}
		}

		return changes;
	}

	public static SourceText Normalize(SourceText text, string newLine)
	{
		IReadOnlyList<TextChange> changes = Changes(text, newLine);
		return changes.Count == 0 ? text : text.WithChanges(changes);
	}
}
