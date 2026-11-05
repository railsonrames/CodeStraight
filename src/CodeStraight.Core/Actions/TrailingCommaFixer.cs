using System.Linq;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Adds or removes the comma after the last element of initializers, anonymous objects, enums, collection
/// expressions and switch expressions, following <c>trailing_comma_style</c> of <c>.codestraight</c>.
/// </summary>
public static class TrailingCommaFixer
{
	public static SyntaxNode Fix(SyntaxNode root, StyleOptions style) =>
		style.TrailingComma == TrailingCommaStyle.NotSet ? root : new Rewriter(style.TrailingComma).Visit(root);

	/// <summary>
	/// Removes commas after the last argument or parameter (<c>Call(a, b,)</c>). C# does not allow them there, so
	/// unlike the style above this repairs a compile error; it runs before the actions that need compilable code.
	/// </summary>
	public static SyntaxNode RemoveInvalid(SyntaxNode root) => new InvalidCommaRewriter().Visit(root);

	private sealed class InvalidCommaRewriter : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitArgumentList(ArgumentListSyntax node)
		{
			var visited = (ArgumentListSyntax)base.VisitArgumentList(node)!;
			return visited.WithArguments(RemoveDangling(visited.Arguments));
		}

		public override SyntaxNode? VisitBracketedArgumentList(BracketedArgumentListSyntax node)
		{
			var visited = (BracketedArgumentListSyntax)base.VisitBracketedArgumentList(node)!;
			return visited.WithArguments(RemoveDangling(visited.Arguments));
		}

		public override SyntaxNode? VisitAttributeArgumentList(AttributeArgumentListSyntax node)
		{
			var visited = (AttributeArgumentListSyntax)base.VisitAttributeArgumentList(node)!;
			return visited.WithArguments(RemoveDangling(visited.Arguments));
		}

		public override SyntaxNode? VisitParameterList(ParameterListSyntax node)
		{
			var visited = (ParameterListSyntax)base.VisitParameterList(node)!;
			return visited.WithParameters(RemoveDangling(visited.Parameters));
		}

		public override SyntaxNode? VisitTypeArgumentList(TypeArgumentListSyntax node)
		{
			var visited = (TypeArgumentListSyntax)base.VisitTypeArgumentList(node)!;
			return visited.WithArguments(RemoveDangling(visited.Arguments));
		}

		/// <summary>The parser turns <c>a, b,)</c> into three elements, the last one made only of missing tokens.</summary>
		private static SeparatedSyntaxList<T> RemoveDangling<T>(SeparatedSyntaxList<T> list)
			where T : SyntaxNode
		{
			if (list.Count < 2 || list.SeparatorCount != list.Count - 1 || !IsMissing(list.Last()))
			{
				return list;
			}

			SyntaxToken comma = list.GetSeparator(list.Count - 2);
			T previous = list[list.Count - 2];
			T newLast = previous.WithTrailingTrivia(previous.GetTrailingTrivia()
				.AddRange(comma.LeadingTrivia)
				.AddRange(comma.TrailingTrivia)
				.AddRange(list.Last().GetLeadingTrivia())
				.AddRange(list.Last().GetTrailingTrivia()));

			T[] nodes = list.Take(list.Count - 2).Append(newLast).ToArray();
			SyntaxToken[] separators = list.GetSeparators().Take(list.Count - 2).ToArray();
			return SyntaxFactory.SeparatedList(nodes, separators);
		}

		private static bool IsMissing(SyntaxNode node) => node.DescendantTokens().All(token => token.IsMissing);
	}

	private sealed class Rewriter : CSharpSyntaxRewriter
	{
		private readonly TrailingCommaStyle _style;

		public Rewriter(TrailingCommaStyle style) => _style = style;

		public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
		{
			var visited = (InitializerExpressionSyntax)base.VisitInitializerExpression(node)!;
			return visited.WithExpressions(Fix(visited.Expressions, visited.CloseBraceToken));
		}

		public override SyntaxNode? VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
		{
			var visited = (AnonymousObjectCreationExpressionSyntax)base.VisitAnonymousObjectCreationExpression(node)!;
			return visited.WithInitializers(Fix(visited.Initializers, visited.CloseBraceToken));
		}

		public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
		{
			var visited = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
			return visited.WithMembers(Fix(visited.Members, visited.CloseBraceToken));
		}

		public override SyntaxNode? VisitSwitchExpression(SwitchExpressionSyntax node)
		{
			var visited = (SwitchExpressionSyntax)base.VisitSwitchExpression(node)!;
			return visited.WithArms(Fix(visited.Arms, visited.CloseBraceToken));
		}

		public override SyntaxNode? VisitCollectionExpression(CollectionExpressionSyntax node)
		{
			var visited = (CollectionExpressionSyntax)base.VisitCollectionExpression(node)!;
			return visited.WithElements(Fix(visited.Elements, visited.CloseBracketToken));
		}

		private SeparatedSyntaxList<T> Fix<T>(SeparatedSyntaxList<T> list, SyntaxToken closeToken)
			where T : SyntaxNode
		{
			if (list.Count == 0 || list.GetWithSeparators().Any(item => item.IsToken && item.AsToken().ContainsDirectives))
			{
				return list;
			}

			bool hasTrailing = list.SeparatorCount == list.Count;
			bool multiLine = IsMultiLine(list, closeToken);
			bool wantTrailing = _style switch
			{
				TrailingCommaStyle.Include => multiLine,
				TrailingCommaStyle.OmitWhenSingleLine => multiLine,
				_ => false,
			};

			// "include" never removes a comma that is already there on a single line.
			if (_style == TrailingCommaStyle.Include && hasTrailing && !multiLine)
			{
				return list;
			}

			if (hasTrailing == wantTrailing)
			{
				return list;
			}

			return wantTrailing ? Add(list) : Remove(list);
		}

		/// <summary>The list is multi-line when the closing brace is on a different line than the last element.</summary>
		private static bool IsMultiLine<T>(SeparatedSyntaxList<T> list, SyntaxToken closeToken)
			where T : SyntaxNode
		{
			string between = list.Last().GetTrailingTrivia().ToFullString()
				+ (list.SeparatorCount == list.Count ? list.GetSeparator(list.Count - 1).ToFullString() : string.Empty)
				+ closeToken.LeadingTrivia.ToFullString();
			return between.Contains("\n");
		}

		private static SeparatedSyntaxList<T> Remove<T>(SeparatedSyntaxList<T> list)
			where T : SyntaxNode
		{
			SyntaxToken comma = list.GetSeparator(list.Count - 1);
			T last = list.Last();

			// Comments that were after the comma stay with the last element.
			T newLast = last.WithTrailingTrivia(last.GetTrailingTrivia().AddRange(comma.LeadingTrivia).AddRange(comma.TrailingTrivia));
			SyntaxToken[] separators = list.GetSeparators().Take(list.Count - 1).ToArray();
			T[] nodes = list.Take(list.Count - 1).Append(newLast).ToArray();
			return SyntaxFactory.SeparatedList(nodes, separators);
		}

		private static SeparatedSyntaxList<T> Add<T>(SeparatedSyntaxList<T> list)
			where T : SyntaxNode
		{
			T last = list.Last();
			SyntaxToken comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(last.GetTrailingTrivia());
			T newLast = last.WithoutTrailingTrivia();
			SyntaxToken[] separators = list.GetSeparators().Append(comma).ToArray();
			T[] nodes = list.Take(list.Count - 1).Append(newLast).ToArray();
			return SyntaxFactory.SeparatedList(nodes, separators);
		}
	}
}
