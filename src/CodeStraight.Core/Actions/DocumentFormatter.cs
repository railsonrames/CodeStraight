using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Formats the whole document with Roslyn's formatter (the same engine as "Format Document"), forcing the
/// indentation values of the <c>.editorconfig</c> read by CodeStraight.
/// </summary>
public static class DocumentFormatter
{
	public static async Task<Document> FormatAsync(Document document, StyleOptions style, CancellationToken cancellationToken)
	{
#pragma warning disable CS0618 // OptionSet-based overloads are the ones available in every supported Roslyn version.
		OptionSet options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
		options = options
			.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, style.UseTabs)
			.WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, style.TabWidth)
			.WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, style.IndentSize);

		// Without end_of_line, keep the line break the file already uses instead of the platform default.
		string newLine = style.NewLine
			?? NamespaceConverter.DetectNewLine(await document.GetTextAsync(cancellationToken).ConfigureAwait(false));
		options = options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, newLine);

		document = await Formatter.FormatAsync(document, options, cancellationToken).ConfigureAwait(false);

		// The formatter leaves continuation lines inside expressions as they are; fix them, then format again so
		// statements inside lambdas follow the new position of the lines that contain them.
		SyntaxTree? tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
		if (tree is null)
		{
			return document;
		}

		Microsoft.CodeAnalysis.Text.SourceText normalized = ContinuationIndenter.Normalize(tree, style);
		if (normalized.ContentEquals(await tree.GetTextAsync(cancellationToken).ConfigureAwait(false)))
		{
			return document;
		}

		document = document.WithText(normalized);
		return await Formatter.FormatAsync(document, options, cancellationToken).ConfigureAwait(false);
#pragma warning restore CS0618
	}
}
