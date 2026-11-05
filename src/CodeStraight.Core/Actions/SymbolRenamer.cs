using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace CodeStraight.Core.Actions;

/// <summary>
/// Renames the symbols declared in a document so they follow the naming rules of the <c>.editorconfig</c>, like
/// Visual Studio's Rename (Ctrl+R, R): the declaration and every reference in the solution, including overrides and
/// interface implementations. Roslyn's own naming fix cannot do this in bulk ("fix all" is not supported).
/// <para>
/// Only symbols whose accessibility is inside <c>rename_scope</c> are renamed. Symbols tied to something outside the
/// solution are skipped: overrides or implementations of members declared in referenced assemblies, explicit interface
/// implementations, operators, entry points and, unless enabled, public members of ASP.NET controllers.
/// </para>
/// <para>
/// Locals and local functions are always in scope. Parameters and type parameters follow the member or type that
/// declares them, and are skipped wherever that member could not be renamed (an override of an external member keeps
/// the parameter names of its contract); parameters of record primary constructors are skipped because they are also
/// the names of the properties. A new name that already appears in the declaring member is skipped, so renaming never
/// makes a local shadow, or be shadowed by, another symbol.
/// </para>
/// <para>
/// With <c>screaming_case_constants</c>, constants follow <see cref="NamingStyle.ScreamingCase"/> instead of their
/// <c>.editorconfig</c> rule; <c>constantsOnly</c> renames nothing else (the flag without <c>rename_symbols</c>).
/// </para>
/// </summary>
public static class SymbolRenamer
{
	public static async Task<Solution> RenameAsync(
		Document document,
		StyleOptions style,
		RenameScope scope,
		bool includeControllerActions,
		CancellationToken cancellationToken,
		bool constantsOnly = false)
	{
		Solution solution = document.Project.Solution;
		if (style.NamingRules.Count == 0 && !style.ScreamingCaseConstants)
		{
			return solution;
		}

		// Each rename produces a new solution, so symbols are planned first and re-resolved afterwards by their position
		// among the declarations of the document, which renames do not change. A name-based key would: renaming a type
		// parameter changes the display name of its type, and locals of different methods can share a name.
		// The new name is computed again from the current one: an earlier rename may have cascaded to this symbol (an
		// interface method renamed to PascalCase renames its async implementation, which still needs the Async suffix),
		// or already renamed it (the second declaration of a partial method).
		List<(int Index, NamingStyle Style)> renames =
			await PlanAsync(document, style, scope, includeControllerActions, constantsOnly, cancellationToken).ConfigureAwait(false);

		foreach ((int index, NamingStyle namingStyle) in renames)
		{
			Document? current = solution.GetDocument(document.Id);
			if (current is null)
			{
				break;
			}

			IReadOnlyList<ISymbol> declared = await DeclaredSymbolsAsync(current, cancellationToken).ConfigureAwait(false);
			ISymbol? symbol = index < declared.Count ? declared[index] : null;
			if (symbol is null || namingStyle.IsCompliant(symbol.Name))
			{
				continue;
			}

			string newName = namingStyle.Apply(symbol.Name);
			if (newName == symbol.Name || !IsValidNewName(newName, symbol))
			{
				continue;
			}

			solution = await Renamer
				.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName, cancellationToken)
				.ConfigureAwait(false);
		}

		return solution;
	}

	private static async Task<List<(int Index, NamingStyle Style)>> PlanAsync(
		Document document,
		StyleOptions style,
		RenameScope scope,
		bool includeControllerActions,
		bool constantsOnly,
		CancellationToken cancellationToken)
	{
		var result = new List<(int, NamingStyle)>();
		IReadOnlyList<ISymbol> declared = await DeclaredSymbolsAsync(document, cancellationToken).ConfigureAwait(false);
		for (int index = 0; index < declared.Count; index++)
		{
			ISymbol symbol = declared[index];
			bool isConstant = symbol is IFieldSymbol { IsConst: true };
			if ((constantsOnly && !isConstant) || !IsRenamable(symbol, scope, includeControllerActions))
			{
				continue;
			}

			NamingStyle? namingStyle = isConstant && style.ScreamingCaseConstants
				? NamingStyle.ScreamingCase
				: NamingRule.Find(style.NamingRules, Kind(symbol), Accessibility(symbol), Modifiers(symbol))?.Style;
			if (namingStyle is null || namingStyle.IsCompliant(symbol.Name))
			{
				continue;
			}

			result.Add((index, namingStyle));
		}

		return result;
	}

	private static async Task<IReadOnlyList<ISymbol>> DeclaredSymbolsAsync(Document document, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		SemanticModel? model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (root is null || model is null)
		{
			return new List<ISymbol>();
		}

		var symbols = new List<ISymbol>();
		foreach (SyntaxNode node in root.DescendantNodes())
		{
			// Variable declarators are fields and events, but also locals of using, for and fixed statements.
			ISymbol? symbol = node switch
			{
				BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax
					or PropertyDeclarationSyntax or EventDeclarationSyntax or LocalFunctionStatementSyntax
					or VariableDeclaratorSyntax or ForEachStatementSyntax or SingleVariableDesignationSyntax
					or CatchDeclarationSyntax or ParameterSyntax or TypeParameterSyntax => model.GetDeclaredSymbol(node, cancellationToken),
				_ => null,
			};

			if (symbol is not null && !symbol.IsImplicitlyDeclared)
			{
				symbols.Add(symbol);
			}
		}

		return symbols;
	}

	private static bool IsRenamable(ISymbol symbol, RenameScope scope, bool includeControllerActions)
	{
		if (!symbol.Locations.All(location => location.IsInSource))
		{
			return false;
		}

		switch (symbol)
		{
			case ILocalSymbol:
			case IMethodSymbol { MethodKind: MethodKind.LocalFunction }:
				return true;
			case IParameterSymbol parameter:
				return !IsRecordPrimaryConstructorParameter(parameter)
					&& OwnsParameterNames(parameter.ContainingSymbol, scope, includeControllerActions);
			case ITypeParameterSymbol typeParameter:
				return OwnsParameterNames(typeParameter.ContainingSymbol, scope, includeControllerActions);
		}

		if (!IsInScope(symbol, scope))
		{
			return false;
		}

		switch (symbol)
		{
			case IMethodSymbol method:
				if (method.MethodKind != MethodKind.Ordinary
					|| method.IsExtern
					|| method.ExplicitInterfaceImplementations.Length > 0
					|| method.Name == "Main"
					|| (method.IsOverride && !IsInSource(method.OverriddenMethod))
					|| ImplementsExternalInterface(method)
					|| (!includeControllerActions && IsControllerAction(method)))
				{
					return false;
				}

				break;
			case IPropertySymbol property:
				if (property.ExplicitInterfaceImplementations.Length > 0
					|| property.IsIndexer
					|| (property.IsOverride && !IsInSource(property.OverriddenProperty))
					|| ImplementsExternalInterface(property))
				{
					return false;
				}

				break;
			case IEventSymbol @event:
				if (@event.ExplicitInterfaceImplementations.Length > 0
					|| (@event.IsOverride && !IsInSource(@event.OverriddenEvent))
					|| ImplementsExternalInterface(@event))
				{
					return false;
				}

				break;
		}

		return true;
	}

	/// <summary>
	/// Whether the parameters and type parameters of <paramref name="owner"/> may be renamed: always for lambdas and
	/// local functions, otherwise when the owner is in scope and its names are not dictated from outside the solution.
	/// </summary>
	private static bool OwnsParameterNames(ISymbol owner, RenameScope scope, bool includeControllerActions) => owner switch
	{
		IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction } => true,
		IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } invoke => IsInScope(invoke.ContainingType, scope),
		IMethodSymbol method => IsInScope(method, scope)
			&& method.ExplicitInterfaceImplementations.Length == 0
			&& !(method.IsOverride && !IsInSource(method.OverriddenMethod))
			&& !ImplementsExternalInterface(method)
			&& (includeControllerActions || !IsControllerAction(method)),
		IPropertySymbol indexer => IsInScope(indexer, scope)
			&& indexer.ExplicitInterfaceImplementations.Length == 0
			&& !(indexer.IsOverride && !IsInSource(indexer.OverriddenProperty))
			&& !ImplementsExternalInterface(indexer),
		INamedTypeSymbol type => IsInScope(type, scope),
		_ => false,
	};

	/// <summary><c>record Ruta(int IdRuta)</c>: the parameter is also the name of the generated property.</summary>
	private static bool IsRecordPrimaryConstructorParameter(IParameterSymbol parameter) =>
		parameter.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax });

	private static bool IsInScope(ISymbol symbol, RenameScope scope) => scope switch
	{
		RenameScope.All => true,
		RenameScope.Internal => symbol.DeclaredAccessibility is Microsoft.CodeAnalysis.Accessibility.Private
			or Microsoft.CodeAnalysis.Accessibility.Internal or Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal,
		_ => symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private,
	};

	private static bool IsInSource(ISymbol? symbol) => symbol is not null && symbol.Locations.All(location => location.IsInSource);

	/// <summary>A member that implements an interface member declared in a referenced assembly must keep its name.</summary>
	private static bool ImplementsExternalInterface(ISymbol member) =>
		member.ContainingType.AllInterfaces
			.SelectMany(@interface => @interface.GetMembers())
			.Where(interfaceMember => !IsInSource(interfaceMember))
			.Any(interfaceMember => SymbolEqualityComparer.Default.Equals(member.ContainingType.FindImplementationForInterfaceMember(interfaceMember), member));

	/// <summary>Public methods of ASP.NET controllers: their names can be used by routes, links and nameof().</summary>
	private static bool IsControllerAction(IMethodSymbol method)
	{
		if (method.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public)
		{
			return false;
		}

		for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
		{
			string name = type.ToDisplayString();
			if (name is "Microsoft.AspNetCore.Mvc.ControllerBase" or "System.Web.Mvc.Controller" or "System.Web.Http.ApiController")
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Kind names used by <c>dotnet_naming_symbols.*.applicable_kinds</c>.</summary>
	private static string Kind(ISymbol symbol) => symbol switch
	{
		INamedTypeSymbol { TypeKind: TypeKind.Interface } => "interface",
		INamedTypeSymbol { TypeKind: TypeKind.Struct } => "struct",
		INamedTypeSymbol { TypeKind: TypeKind.Enum } => "enum",
		INamedTypeSymbol { TypeKind: TypeKind.Delegate } => "delegate",
		INamedTypeSymbol => "class",
		IMethodSymbol { MethodKind: MethodKind.LocalFunction } => "local_function",
		IMethodSymbol => "method",
		IParameterSymbol => "parameter",
		ILocalSymbol => "local",
		ITypeParameterSymbol => "type_parameter",
		IPropertySymbol => "property",
		IEventSymbol => "event",
		IFieldSymbol => "field",
		_ => string.Empty,
	};

	/// <summary>Accessibility names used by <c>dotnet_naming_symbols.*.applicable_accessibilities</c>.</summary>
	private static string Accessibility(ISymbol symbol) => symbol.DeclaredAccessibility switch
	{
		Microsoft.CodeAnalysis.Accessibility.Public => "public",
		Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
		Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
		Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected_internal",
		Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private_protected",
		Microsoft.CodeAnalysis.Accessibility.NotApplicable => "local",
		_ => "private",
	};

	/// <summary>Modifier names used by <c>dotnet_naming_symbols.*.required_modifiers</c>.</summary>
	private static IReadOnlyCollection<string> Modifiers(ISymbol symbol)
	{
		var modifiers = new List<string>();
		if (symbol.IsAbstract)
		{
			modifiers.Add("abstract");
		}

		if (symbol is IMethodSymbol { IsAsync: true })
		{
			modifiers.Add("async");
		}

		if (symbol is IFieldSymbol field)
		{
			if (field.IsConst)
			{
				modifiers.Add("const");
			}

			if (field.IsReadOnly)
			{
				modifiers.Add("readonly");
			}

			if (field.IsVolatile)
			{
				modifiers.Add("volatile");
			}
		}

		if (symbol is ILocalSymbol { IsConst: true })
		{
			modifiers.Add("const");
		}

		if (symbol.IsStatic && symbol is not IFieldSymbol { IsConst: true })
		{
			modifiers.Add("static");
		}

		return modifiers;
	}

	/// <summary>Skips names that are not identifiers or that already exist in the same scope (the rename would clash).</summary>
	private static bool IsValidNewName(string newName, ISymbol symbol)
	{
		if (!SyntaxFacts.IsValidIdentifier(newName)
			|| SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None
			|| SyntaxFacts.GetContextualKeywordKind(newName) != SyntaxKind.None)
		{
			return false;
		}

		if (symbol is ILocalSymbol or IParameterSymbol or ITypeParameterSymbol or IMethodSymbol { MethodKind: MethodKind.LocalFunction })
		{
			return !NameAppearsInDeclaringMember(newName, symbol);
		}

		if (symbol is INamedTypeSymbol type)
		{
			return type.ContainingType is { } outer
				? outer.GetTypeMembers(newName).IsEmpty
				: type.ContainingNamespace.GetTypeMembers(newName).IsEmpty;
		}

		return symbol.ContainingType is null || symbol.ContainingType.GetMembers(newName).IsEmpty;
	}

	/// <summary>
	/// The new name of a local, parameter or type parameter is already used inside the member (or type) that declares it,
	/// as another declaration or as a reference to a field, property, type... Renaming would clash or change which symbol
	/// that reference binds to, so it is skipped. Top-level statements share the whole file.
	/// </summary>
	private static bool NameAppearsInDeclaringMember(string newName, ISymbol symbol) =>
		symbol.DeclaringSyntaxReferences.Any(reference =>
		{
			SyntaxNode declaration = reference.GetSyntax();
			SyntaxNode root = declaration.SyntaxTree.GetRoot();
			SyntaxNode scope = declaration.FirstAncestorOrSelf<GlobalStatementSyntax>() is null
				? declaration.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault() ?? root
				: root;
			return scope.DescendantTokens().Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == newName);
		});
}

/// <summary><c>rename_scope</c> of <c>.codestraight</c>: the widest accessibility that may be renamed.</summary>
public enum RenameScope
{
	Private,
	Internal,
	All,
}
