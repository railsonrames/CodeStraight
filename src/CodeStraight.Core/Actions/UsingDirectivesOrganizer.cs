using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeStraight.Core.Actions;

/// <summary>Removes unnecessary using directives and sorts the remaining ones.</summary>
public static class UsingDirectivesOrganizer
{
	/// <summary>Compiler diagnostic for an unnecessary using directive (the source of IDE0005).</summary>
	private const string UnnecessaryUsingId = "CS8019";

	public static async Task<Document> RemoveUnusedAsync(Document document, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		SemanticModel? model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (root is null || model is null)
		{
			return document;
		}

		// Repeated directives are always safe to remove. The others only when the file compiles: with errors,
		// "unused" may just mean "not resolved yet".
		var diagnostics = model.GetDiagnostics(cancellationToken: cancellationToken);
		bool hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

		IEnumerable<UsingDirectiveSyntax> unnecessary = hasErrors
			? Enumerable.Empty<UsingDirectiveSyntax>()
			: diagnostics
				.Where(diagnostic => diagnostic.Id == UnnecessaryUsingId)
				.Select(diagnostic => root.FindNode(diagnostic.Location.SourceSpan))
				.Select(node => node.FirstAncestorOrSelf<UsingDirectiveSyntax>())
				.OfType<UsingDirectiveSyntax>();

		UsingDirectiveSyntax[] unused = unnecessary
			.Concat(Duplicates(root))
			.Where(directive => !directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
			.Where(directive => !directive.GetLeadingTrivia().Any(trivia => trivia.IsDirective))
			.Distinct()
			.ToArray();

		if (unused.Length == 0)
		{
			return document;
		}

		SyntaxNode? newRoot = root.RemoveNodes(unused, SyntaxRemoveOptions.KeepUnbalancedDirectives | SyntaxRemoveOptions.KeepLeadingTrivia);
		return newRoot is null ? document : document.WithSyntaxRoot(RemoveBlankLeadingLines(newRoot));
	}

	/// <summary>Every repetition of a directive already present in the same block (the first one is kept).</summary>
	private static IEnumerable<UsingDirectiveSyntax> Duplicates(SyntaxNode root) =>
		root.DescendantNodes()
			.OfType<UsingDirectiveSyntax>()
			.GroupBy(directive => (directive.Parent, Text: directive.WithoutTrivia().ToString()))
			.SelectMany(group => group.Skip(1));

	public static SyntaxNode Sort(SyntaxNode root, StyleOptions style)
	{
		return new SortRewriter(style.SortSystemDirectivesFirst).Visit(root);
	}

	/// <summary>Leading trivia kept from removed usings may leave empty lines at the top of the file.</summary>
	private static SyntaxNode RemoveBlankLeadingLines(SyntaxNode root)
	{
		SyntaxToken first = root.GetFirstToken(includeZeroWidth: true);
		SyntaxTriviaList trivia = first.LeadingTrivia;
		int skip = 0;
		while (skip < trivia.Count && (trivia[skip].IsKind(SyntaxKind.EndOfLineTrivia) || trivia[skip].IsKind(SyntaxKind.WhitespaceTrivia)))
		{
			skip++;
		}

		return skip == 0 ? root : root.ReplaceToken(first, first.WithLeadingTrivia(trivia.Skip(skip)));
	}

	private sealed class SortRewriter : CSharpSyntaxRewriter
	{
		private readonly bool _systemFirst;

		public SortRewriter(bool systemFirst) => _systemFirst = systemFirst;

		public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
		{
			var visited = (CompilationUnitSyntax)base.VisitCompilationUnit(node)!;
			return visited.WithUsings(SortList(visited.Usings));
		}

		public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
		{
			var visited = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;
			return visited.WithUsings(SortList(visited.Usings));
		}

		public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
		{
			var visited = (FileScopedNamespaceDeclarationSyntax)base.VisitFileScopedNamespaceDeclaration(node)!;
			return visited.WithUsings(SortList(visited.Usings));
		}

		private SyntaxList<UsingDirectiveSyntax> SortList(SyntaxList<UsingDirectiveSyntax> usings)
		{
			if (usings.Count < 2 || usings.Any(directive => directive.GetLeadingTrivia().Any(trivia => trivia.IsDirective)))
			{
				return usings;
			}

			List<UsingDirectiveSyntax> sorted = usings
				.OrderBy(Group)
				.ThenBy(directive => IsSystem(directive) && _systemFirst ? 0 : 1)
				.ThenBy(SortKey, StringComparer.OrdinalIgnoreCase)
				.ThenBy(SortKey, StringComparer.Ordinal)
				.ToList();

			if (sorted.SequenceEqual(usings))
			{
				return usings;
			}

			// The first directive keeps the file header (comments before the first using). The others keep only
			// the comments attached to them; blank lines between usings are dropped.
			SyntaxTriviaList header = usings[0].GetLeadingTrivia();
			var result = new List<UsingDirectiveSyntax>(sorted.Count);
			for (int i = 0; i < sorted.Count; i++)
			{
				UsingDirectiveSyntax directive = sorted[i];
				SyntaxTriviaList own = directive == usings[0] ? default : directive.GetLeadingTrivia();
				IEnumerable<SyntaxTrivia> comments = own.SkipWhile(trivia =>
					trivia.IsKind(SyntaxKind.EndOfLineTrivia) || trivia.IsKind(SyntaxKind.WhitespaceTrivia));
				result.Add(directive.WithLeadingTrivia(i == 0 ? header.AddRange(comments) : SyntaxFactory.TriviaList(comments)));
			}

			return SyntaxFactory.List(result);
		}

		/// <summary>global usings, then regular, static and alias directives (Visual Studio's order).</summary>
		private static int Group(UsingDirectiveSyntax directive) =>
			directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) ? 0
			: directive.Alias is not null ? 3
			: directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) ? 2
			: 1;

		private static bool IsSystem(UsingDirectiveSyntax directive)
		{
			string name = directive.NamespaceOrType.ToString();
			return name == "System" || name.StartsWith("System.", StringComparison.Ordinal);
		}

		private static string SortKey(UsingDirectiveSyntax directive) =>
			directive.Alias is not null ? directive.Alias.Name.ToString() : directive.NamespaceOrType.ToString();
	}
}
