namespace CodeStraight.Core.Tests;

internal static class Samples
{
	/// <summary>Naming rules equivalent to the ones a real project uses (PascalCase constants, _camelCase private fields).</summary>
	public const string NamingRules = """
		root = true
		[*.cs]
		dotnet_naming_rule.constants_pascal.symbols = constant_fields
		dotnet_naming_rule.constants_pascal.style = pascal
		dotnet_naming_rule.constants_pascal.severity = suggestion
		dotnet_naming_symbols.constant_fields.applicable_kinds = field
		dotnet_naming_symbols.constant_fields.required_modifiers = const

		dotnet_naming_rule.static_readonly_pascal.symbols = static_readonly_fields
		dotnet_naming_rule.static_readonly_pascal.style = pascal
		dotnet_naming_rule.static_readonly_pascal.severity = suggestion
		dotnet_naming_symbols.static_readonly_fields.applicable_kinds = field
		dotnet_naming_symbols.static_readonly_fields.required_modifiers = static, readonly

		dotnet_naming_style.pascal.capitalization = pascal_case

		dotnet_naming_rule.private_fields_underscore.symbols = private_fields
		dotnet_naming_rule.private_fields_underscore.style = underscore_camel
		dotnet_naming_rule.private_fields_underscore.severity = suggestion
		dotnet_naming_symbols.private_fields.applicable_kinds = field
		dotnet_naming_symbols.private_fields.applicable_accessibilities = private
		dotnet_naming_style.underscore_camel.capitalization = camel_case
		dotnet_naming_style.underscore_camel.required_prefix = _
		""";

	public const string Spaces = """
		root = true
		[*.cs]
		indent_style = space
		indent_size = 4
		""";
}
