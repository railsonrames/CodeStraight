using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Replaces <c>var</c> with the type the compiler infers, written as it would be in the file
/// (<c>List&lt;Cliente&gt;</c>, never <c>object</c>). Anonymous types, tuples with anonymous elements and
/// declarations whose type cannot be resolved keep <c>var</c>.
/// </summary>
public static class ExplicitTypeReplacer
{
	private static readonly SymbolDisplayFormat Format = SymbolDisplayFormat.MinimallyQualifiedFormat
		.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	public static async Task<Document> ReplaceAsync(Document document, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		SemanticModel? model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (root is null || model is null)
		{
			return document;
		}

		var replacements = new Dictionary<TypeSyntax, TypeSyntax>();
		foreach (TypeSyntax type in root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(name => name.IsVar))
		{
			ITypeSymbol? inferred = InferredType(type, model, cancellationToken);
			if (inferred is null || !CanBeWritten(inferred))
			{
				continue;
			}

			string text = inferred.ToMinimalDisplayString(model, type.SpanStart, Format);
			replacements[type] = SyntaxFactory.ParseTypeName(text).WithTriviaFrom(type);
		}

		return replacements.Count == 0
			? document
			: document.WithSyntaxRoot(root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]));
	}

	/// <summary>
	/// The type to write: for initialized locals, the type of the initializer (so <c>var s = "x"</c> becomes
	/// <c>string</c> and not <c>string?</c> in nullable contexts); otherwise the type the compiler bound to <c>var</c>.
	/// </summary>
	private static ITypeSymbol? InferredType(TypeSyntax type, SemanticModel model, CancellationToken cancellationToken)
	{
		switch (type.Parent)
		{
			case VariableDeclarationSyntax { Variables.Count: 1 } declaration
				when declaration.Variables[0].Initializer is { } initializer:
				ITypeSymbol? fromValue = model.GetTypeInfo(initializer.Value, cancellationToken).Type;
				ITypeSymbol? declared = model.GetTypeInfo(type, cancellationToken).Type;
				return fromValue is not null && declared is not null
					&& SymbolEqualityComparer.Default.Equals(fromValue.WithNullableAnnotation(NullableAnnotation.None), declared.WithNullableAnnotation(NullableAnnotation.None))
					? fromValue
					: declared;
			case ForEachStatementSyntax:
			case DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax }:
				return model.GetTypeInfo(type, cancellationToken).Type;
			default:
				// var (a, b) = …, "is var x" patterns and lambdas keep var: the explicit form is longer or invalid.
				return null;
		}
	}

	private static bool CanBeWritten(ITypeSymbol type) =>
		type.TypeKind != TypeKind.Error
		&& !type.IsAnonymousType
		&& type.SpecialType != SpecialType.System_Void
		&& !ContainsAnonymous(type);

	private static bool ContainsAnonymous(ITypeSymbol type) => type switch
	{
		{ IsAnonymousType: true } => true,
		IArrayTypeSymbol array => ContainsAnonymous(array.ElementType),
		INamedTypeSymbol named => named.TypeArguments.Any(ContainsAnonymous)
			|| (named.IsTupleType && named.TupleElements.Any(element => ContainsAnonymous(element.Type))),
		IPointerTypeSymbol pointer => ContainsAnonymous(pointer.PointedAtType),
		_ => false,
	};
}
