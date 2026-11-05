using System;
using System.Collections.Generic;

namespace CodeStraight.Core.Configuration;

public enum NamespaceStyle
{
	NotSet,
	FileScoped,
	BlockScoped,
}

public enum AccessibilityModifiersStyle
{
	NotSet,
	Always,
	ForNonInterfaceMembers,
	OmitIfDefault,
	Never,
}

public enum TrailingCommaStyle
{
	NotSet,
	Include,
	Omit,
	OmitWhenSingleLine,
}

/// <summary><c>charset</c> of the <c>.editorconfig</c>.</summary>
public enum Charset
{
	NotSet,
	Utf8,
	Utf8Bom,
	Latin1,
	Utf16Be,
	Utf16Le,
}

/// <summary><c>wrap_list_style</c> of <c>.codestraight</c>.</summary>
public enum WrapListStyle
{
	/// <summary>Every parameter or argument on its own line.</summary>
	OnePerLine,

	/// <summary>As many parameters or arguments per line as fit.</summary>
	Fill,
}

public enum OperatorNewLine
{
	Before,
	After,
}

/// <summary><c>csharp_prefer_braces</c> of the <c>.editorconfig</c>.</summary>
public enum PreferBracesStyle
{
	NotSet,
	True,
	False,
	WhenMultiline,
}

/// <summary>
/// Values that drive the actions for a given file. Standard values come from the <c>.editorconfig</c>; the ones that
/// only CodeStraight uses (trailing comma style, operator position) come from the <c>.codestraight</c>.
/// </summary>
public sealed record StyleOptions
{
	public int? MaxLineLength { get; init; }

	public bool UseTabs { get; init; }

	public int IndentSize { get; init; } = 4;

	public int TabWidth { get; init; } = 4;

	/// <summary>Line break from <c>end_of_line</c>; null keeps the one already used by the document.</summary>
	public string? NewLine { get; init; }

	/// <summary>Encoding from <c>charset</c>.</summary>
	public Charset Charset { get; init; }

	public NamespaceStyle NamespaceStyle { get; init; }

	public bool SortSystemDirectivesFirst { get; init; } = true;

	public AccessibilityModifiersStyle AccessibilityModifiers { get; init; }

	/// <summary><c>csharp_prefer_braces</c> (IDE0011): <c>false</c> and <c>when_multiline</c> let <c>remove_braces</c> act.</summary>
	public PreferBracesStyle PreferBraces { get; init; }

	public TrailingCommaStyle TrailingComma { get; init; }

	/// <summary>Where binary operators go when an expression is wrapped (<c>binary_operator_new_line</c> of <c>.codestraight</c>).</summary>
	public OperatorNewLine BinaryOperatorNewLine { get; init; } = OperatorNewLine.Before;

	public IReadOnlyList<NamingRule> NamingRules { get; init; } = Array.Empty<NamingRule>();

	/// <summary><c>screaming_case_constants</c> of <c>.codestraight</c>: constants use <see cref="NamingStyle.ScreamingCase"/>.</summary>
	public bool ScreamingCaseConstants { get; init; }

	/// <summary><c>wrap_attributes</c> of <c>.codestraight</c>: when false, lines inside attributes are never wrapped.</summary>
	public bool WrapAttributes { get; init; } = true;

	/// <summary><c>split_long_strings</c> of <c>.codestraight</c>: split string literals that cross the limit.</summary>
	public bool SplitLongStrings { get; init; }

	/// <summary><c>wrap_list_style</c> of <c>.codestraight</c>: how parameter and argument lists are wrapped.</summary>
	public WrapListStyle WrapListStyle { get; init; }

	/// <summary><c>wrap_tolerance</c> of <c>.codestraight</c>: columns a line may go over the limit before it is wrapped.</summary>
	public int WrapTolerance { get; init; }

	/// <summary><c>dotnet_style_allow_multiple_blank_lines_experimental</c> (IDE2000); true when not set.</summary>
	public bool AllowMultipleBlankLines { get; init; } = true;

	/// <summary><c>csharp_style_allow_blank_lines_between_consecutive_braces_experimental</c> (IDE2002); true when not set.</summary>
	public bool AllowBlankLinesBetweenConsecutiveBraces { get; init; } = true;


	/// <summary>One indentation level, as configured.</summary>
	public string IndentUnit => UseTabs ? "\t" : new string(' ', IndentSize);

	/// <summary>
	/// Rewrites existing leading whitespace (possibly mixing tabs and spaces) as whole indentation levels in the
	/// configured style. Used whenever a line's indentation is taken as the reference for other lines.
	/// </summary>
	public string Canonical(string whitespace)
	{
		int column = 0;
		foreach (char c in whitespace)
		{
			column = c == '\t' ? column + TabWidth - (column % TabWidth) : column + 1;
		}

		int levelWidth = UseTabs ? TabWidth : IndentSize;
		int levels = levelWidth > 0 ? column / levelWidth : 0;
		var builder = new System.Text.StringBuilder();
		for (int i = 0; i < levels; i++)
		{
			builder.Append(IndentUnit);
		}

		return builder.ToString();
	}

	public static StyleOptions FromProperties(IReadOnlyDictionary<string, string> properties)
	{
		bool useTabs = Value(properties, "indent_style") == "tab";
		int tabWidth = Int(properties, "tab_width") ?? Int(properties, "indent_size") ?? 4;
		int indentSize = Value(properties, "indent_size") == "tab" ? tabWidth : Int(properties, "indent_size") ?? tabWidth;

		return new StyleOptions
		{
			MaxLineLength = Int(properties, "max_line_length"),
			UseTabs = useTabs,
			IndentSize = indentSize,
			TabWidth = tabWidth,
			NewLine = Value(properties, "end_of_line") switch
			{
				"lf" => "\n",
				"crlf" => "\r\n",
				"cr" => "\r",
				_ => null,
			},
			Charset = Value(properties, "charset") switch
			{
				"utf-8" => Charset.Utf8,
				"utf-8-bom" => Charset.Utf8Bom,
				"latin1" => Charset.Latin1,
				"utf-16be" => Charset.Utf16Be,
				"utf-16le" => Charset.Utf16Le,
				_ => Charset.NotSet,
			},
			NamespaceStyle = WithoutSeverity(Value(properties, "csharp_style_namespace_declarations")) switch
			{
				"file_scoped" => NamespaceStyle.FileScoped,
				"block_scoped" => NamespaceStyle.BlockScoped,
				_ => NamespaceStyle.NotSet,
			},
			SortSystemDirectivesFirst = Value(properties, "dotnet_sort_system_directives_first") != "false",
			AccessibilityModifiers = WithoutSeverity(Value(properties, "dotnet_style_require_accessibility_modifiers")) switch
			{
				"always" => AccessibilityModifiersStyle.Always,
				"for_non_interface_members" => AccessibilityModifiersStyle.ForNonInterfaceMembers,
				"omit_if_default" => AccessibilityModifiersStyle.OmitIfDefault,
				"never" => AccessibilityModifiersStyle.Never,
				_ => AccessibilityModifiersStyle.NotSet,
			},
			PreferBraces = WithoutSeverity(Value(properties, "csharp_prefer_braces")) switch
			{
				"true" => PreferBracesStyle.True,
				"false" => PreferBracesStyle.False,
				"when_multiline" => PreferBracesStyle.WhenMultiline,
				_ => PreferBracesStyle.NotSet,
			},
			NamingRules = NamingRule.Parse(properties),
			AllowMultipleBlankLines = WithoutSeverity(Value(properties, "dotnet_style_allow_multiple_blank_lines_experimental")) != "false",
			AllowBlankLinesBetweenConsecutiveBraces =
				WithoutSeverity(Value(properties, "csharp_style_allow_blank_lines_between_consecutive_braces_experimental")) != "false",
		};
	}


	private static string? Value(IReadOnlyDictionary<string, string> properties, string key) =>
		properties.TryGetValue(key, out string? value) ? value.Trim().ToLowerInvariant() : null;

	private static int? Int(IReadOnlyDictionary<string, string> properties, string key) =>
		int.TryParse(Value(properties, key), out int number) && number > 0 ? number : null;

	/// <summary>Removes the legacy <c>:severity</c> suffix (<c>file_scoped:warning</c>).</summary>
	private static string? WithoutSeverity(string? value)
	{
		if (value is null)
		{
			return null;
		}

		int colon = value.IndexOf(':');
		return (colon >= 0 ? value.Substring(0, colon) : value).Trim();
	}
}
