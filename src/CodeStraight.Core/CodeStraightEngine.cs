using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core.Actions;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core;

/// <summary>
/// Applies every action enabled in <c>.codestraight</c> to one document, always in the same order, so the
/// result does not depend on which tool happens to run first:
/// rename symbols → namespace → remove usings → sort usings → accessibility modifiers →
/// explicit types → remove comments → blank lines → remove braces → format document → wrap long lines → trailing comma →
/// line endings.
/// Each action is isolated: if one fails, it is reported and the others still run.
/// </summary>
public static class CodeStraightEngine
{
	public static async Task<Solution> ApplyAsync(
		Document document,
		FileConfiguration configuration,
		CancellationToken cancellationToken,
		IList<string>? report = null)
	{
		CodeStraightSettings settings = configuration.Settings;
		StyleOptions style = configuration.Style;
		DocumentId id = document.Id;
		Solution solution = document.Project.Solution;

		if (!IsCSharp(document))
		{
			return solution;
		}

		if (await IsGeneratedAsync(document, cancellationToken).ConfigureAwait(false))
		{
			report?.Add("generated file: skipped");
			return solution;
		}

		var runner = new StepRunner(id, report, cancellationToken);

		// First, repair commas after the last argument: they are compile errors and would block the semantic actions.
		if (settings.TrailingComma)
		{
			solution = await runner.RunAsync(
				"trailing_comma (invalid commas)",
				solution,
				current => TransformRootAsync(current, TrailingCommaFixer.RemoveInvalid, cancellationToken)).ConfigureAwait(false);
		}

		if (settings.RenameSymbols || settings.ScreamingCaseConstants)
		{
			solution = await runner.RunSolutionAsync(
				settings.RenameSymbols ? "rename_symbols" : "screaming_case_constants",
				solution,
				current => SymbolRenamer.RenameAsync(
					current,
					style,
					settings.RenameScope,
					settings.RenameControllerActions,
					cancellationToken,
					constantsOnly: !settings.RenameSymbols)).ConfigureAwait(false);
		}

		if (settings.NamespaceDeclarations)
		{
			solution = await runner.RunAsync("namespace_declarations", solution, async current =>
			{
				SyntaxTree? tree = await current.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
				if (tree is null)
				{
					return current;
				}

				SourceText text = await current.GetTextAsync(cancellationToken).ConfigureAwait(false);
				SourceText converted = NamespaceConverter.Convert(tree, text, style);
				return ReferenceEquals(converted, text) ? current : current.WithText(converted);
			}).ConfigureAwait(false);
		}

		if (settings.RemoveUnusedUsings)
		{
			solution = await runner.RunAsync(
				"remove_unused_usings",
				solution,
				current => UsingDirectivesOrganizer.RemoveUnusedAsync(current, cancellationToken)).ConfigureAwait(false);
		}

		if (settings.SortUsings)
		{
			solution = await runner.RunAsync(
				"sort_usings",
				solution,
				current => TransformRootAsync(current, root => UsingDirectivesOrganizer.Sort(root, style), cancellationToken))
				.ConfigureAwait(false);
		}

		if (settings.AccessibilityModifiers)
		{
			solution = await runner.RunAsync(
				"accessibility_modifiers",
				solution,
				current => AccessibilityModifierAdder.AddAsync(current, style, cancellationToken)).ConfigureAwait(false);
		}

		if (settings.ExplicitTypes)
		{
			solution = await runner.RunAsync(
				"explicit_types",
				solution,
				current => ExplicitTypeReplacer.ReplaceAsync(current, cancellationToken)).ConfigureAwait(false);
		}

		if (settings.RemoveComments)
		{
			solution = await runner.RunAsync("remove_comments", solution, async current =>
			{
				SyntaxTree? tree = await current.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
				return tree is null ? current : current.WithText(CommentRemover.Remove(tree));
			}).ConfigureAwait(false);
		}

		if (settings.RemoveExtraBlankLines)
		{
			solution = await runner.RunAsync("remove_extra_blank_lines", solution, async current =>
			{
				SyntaxTree? tree = await current.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
				return tree is null ? current : current.WithText(BlankLineRemover.Remove(tree, style));
			}).ConfigureAwait(false);
		}

		if (settings.RemoveBraces)
		{
			solution = await runner.RunAsync(
				"remove_braces",
				solution,
				current => TransformRootAsync(current, root => BraceRemover.Remove(root, style), cancellationToken))
				.ConfigureAwait(false);
		}

		if (settings.FormatDocument)
		{
			solution = await runner.RunAsync(
				"format_document",
				solution,
				current => DocumentFormatter.FormatAsync(current, style, cancellationToken)).ConfigureAwait(false);
		}

		if (settings.WrapLongLines)
		{
			solution = await runner.RunAsync("wrap_long_lines", solution, async current =>
			{
				SyntaxTree? tree = await current.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
				if (tree is null)
				{
					return current;
				}

				Document wrapped = current.WithText(LineWrapper.Wrap(tree, style, cancellationToken));
				if (!settings.FormatDocument)
				{
					return wrapped;
				}

				// Later wraps can move lines that earlier wraps used as reference; re-indent continuations once more.
				SyntaxTree? wrappedTree = await wrapped.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
				return wrappedTree is null ? wrapped : wrapped.WithText(ContinuationIndenter.Normalize(wrappedTree, style));
			}).ConfigureAwait(false);
		}

		if (settings.TrailingComma)
		{
			solution = await runner.RunAsync(
				"trailing_comma",
				solution,
				current => TransformRootAsync(current, root => TrailingCommaFixer.Fix(root, style), cancellationToken))
				.ConfigureAwait(false);
		}

		if (settings.NormalizeLineEndings && style.NewLine is string newLine)
		{
			solution = await runner.RunAsync("normalize_line_endings", solution, async current =>
			{
				SourceText text = await current.GetTextAsync(cancellationToken).ConfigureAwait(false);
				SourceText normalized = LineEndingNormalizer.Normalize(text, newLine);
				return ReferenceEquals(normalized, text) ? current : current.WithText(normalized);
			}).ConfigureAwait(false);
		}

		return solution;
	}

	private static bool IsCSharp(Document document) => document.Project.Language == LanguageNames.CSharp;

	private static async Task<Document> TransformRootAsync(
		Document document,
		Func<SyntaxNode, SyntaxNode> transform,
		CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
		{
			return document;
		}

		SyntaxNode newRoot = transform(root);
		return ReferenceEquals(newRoot, root) ? document : document.WithSyntaxRoot(newRoot);
	}

	/// <summary>Generated files are left alone: the generator would overwrite the changes anyway.</summary>
	private static async Task<bool> IsGeneratedAsync(Document document, CancellationToken cancellationToken)
	{
		string fileName = Path.GetFileName(document.FilePath ?? document.Name);
		if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
			|| fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
		string head = text.ToString(new TextSpan(0, Math.Min(text.Length, 500)));
		return head.IndexOf("<auto-generated", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>Runs one action, records whether it changed the text and keeps going if it throws.</summary>
	private sealed class StepRunner
	{
		private readonly DocumentId _id;
		private readonly IList<string>? _report;
		private readonly CancellationToken _cancellationToken;

		public StepRunner(DocumentId id, IList<string>? report, CancellationToken cancellationToken)
		{
			_id = id;
			_report = report;
			_cancellationToken = cancellationToken;
		}

		public Task<Solution> RunAsync(string name, Solution solution, Func<Document, Task<Document>> action) =>
			RunSolutionAsync(name, solution, async document => (await action(document).ConfigureAwait(false)).Project.Solution);

		public async Task<Solution> RunSolutionAsync(string name, Solution solution, Func<Document, Task<Solution>> action)
		{
			Document? document = solution.GetDocument(_id);
			if (document is null)
			{
				return solution;
			}

			try
			{
				SourceText before = await document.GetTextAsync(_cancellationToken).ConfigureAwait(false);
				Solution updated = await action(document).ConfigureAwait(false);
				Document? after = updated.GetDocument(_id);
				bool changed = after is not null
					&& !(await after.GetTextAsync(_cancellationToken).ConfigureAwait(false)).ContentEquals(before);
				_report?.Add($"{name}: {(changed ? "changed" : "no changes")}");
				return updated;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				_report?.Add($"{name}: ERROR {exception.GetType().Name}: {exception.Message}");
				return solution;
			}
		}
	}
}
