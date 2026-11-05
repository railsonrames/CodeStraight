using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Makes the accessibility of types and members explicit (<c>dotnet_style_require_accessibility_modifiers</c>
/// set to <c>always</c> or <c>for_non_interface_members</c>). The modifier added is the one the compiler
/// already applies, so the meaning of the code never changes.
/// </summary>
public static class AccessibilityModifierAdder
{
	public static async Task<Document> AddAsync(Document document, StyleOptions style, CancellationToken cancellationToken)
	{
		if (style.AccessibilityModifiers is not (AccessibilityModifiersStyle.Always or AccessibilityModifiersStyle.ForNonInterfaceMembers))
		{
			return document;
		}

		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		SemanticModel? model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (root is null || model is null)
		{
			return document;
		}

		var changes = new Dictionary<SyntaxNode, Accessibility>();
		foreach (MemberDeclarationSyntax declaration in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
		{
			if (!NeedsModifier(declaration))
			{
				continue;
			}

			ISymbol? symbol = declaration switch
			{
				BaseFieldDeclarationSyntax field => field.Declaration.Variables.Count > 0
					? model.GetDeclaredSymbol(field.Declaration.Variables[0], cancellationToken)
					: null,
				_ => model.GetDeclaredSymbol(declaration, cancellationToken),
			};

			if (symbol is not null && symbol.DeclaredAccessibility != Accessibility.NotApplicable)
			{
				changes[declaration] = symbol.DeclaredAccessibility;
			}
		}

		if (changes.Count == 0)
		{
			return document;
		}

		SyntaxNode newRoot = root.ReplaceNodes(
			changes.Keys,
			(original, rewritten) => AddModifier((MemberDeclarationSyntax)rewritten, changes[original]));

		return document.WithSyntaxRoot(newRoot);
	}

	/// <summary>Inserts the modifier before the existing ones (after the attributes), moving the leading trivia to it.</summary>
	private static MemberDeclarationSyntax AddModifier(MemberDeclarationSyntax declaration, Accessibility accessibility)
	{
		SyntaxKind[] kinds = Keywords(accessibility);
		if (kinds.Length == 0)
		{
			return declaration;
		}

		SyntaxToken anchor = declaration.AttributeLists.Count > 0
			? declaration.AttributeLists.Last().GetLastToken().GetNextToken()
			: declaration.GetFirstToken();
		SyntaxTriviaList leading = anchor.LeadingTrivia;
		declaration = declaration.ReplaceToken(anchor, anchor.WithLeadingTrivia(SyntaxTriviaList.Empty));

		var tokens = new List<SyntaxToken>();
		for (int i = 0; i < kinds.Length; i++)
		{
			SyntaxToken token = SyntaxFactory.Token(kinds[i]).WithTrailingTrivia(SyntaxFactory.Space);
			tokens.Add(i == 0 ? token.WithLeadingTrivia(leading) : token);
		}

		return declaration.WithModifiers(SyntaxFactory.TokenList(tokens.Concat(declaration.Modifiers)));
	}

	private static SyntaxKind[] Keywords(Accessibility accessibility) => accessibility switch
	{
		Accessibility.Public => new[] { SyntaxKind.PublicKeyword },
		Accessibility.Private => new[] { SyntaxKind.PrivateKeyword },
		Accessibility.Internal => new[] { SyntaxKind.InternalKeyword },
		Accessibility.Protected => new[] { SyntaxKind.ProtectedKeyword },
		Accessibility.ProtectedOrInternal => new[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword },
		Accessibility.ProtectedAndInternal => new[] { SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword },
		_ => System.Array.Empty<SyntaxKind>(),
	};

	private static bool NeedsModifier(MemberDeclarationSyntax declaration)
	{
		if (declaration is not (BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or BaseFieldDeclarationSyntax
			or PropertyDeclarationSyntax or MethodDeclarationSyntax or EventDeclarationSyntax
			or IndexerDeclarationSyntax or ConstructorDeclarationSyntax))
		{
			return false;
		}

		SyntaxTokenList modifiers = declaration.Modifiers;
		if (modifiers.Any(modifier => modifier.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword
				or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword or SyntaxKind.FileKeyword))
		{
			return false;
		}

		// Interface members, explicit implementations, static constructors and partial methods without
		// accessibility have special rules: an added modifier could change or break them.
		if (declaration.Parent is InterfaceDeclarationSyntax
			|| (declaration is ConstructorDeclarationSyntax && modifiers.Any(SyntaxKind.StaticKeyword))
			|| (declaration is MethodDeclarationSyntax method
				&& (method.ExplicitInterfaceSpecifier is not null || modifiers.Any(SyntaxKind.PartialKeyword)))
			|| (declaration is PropertyDeclarationSyntax property && property.ExplicitInterfaceSpecifier is not null)
			|| (declaration is EventDeclarationSyntax eventDeclaration && eventDeclaration.ExplicitInterfaceSpecifier is not null)
			|| (declaration is IndexerDeclarationSyntax indexer && indexer.ExplicitInterfaceSpecifier is not null))
		{
			return false;
		}

		return true;
	}
}
