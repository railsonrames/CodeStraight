using System;
using System.Collections.Generic;
using CodeStraight.Core.Actions;

namespace CodeStraight.Core.Configuration;

/// <summary>
/// Feature switches read from the <c>.codestraight</c> file. Every switch is off unless the file turns it on,
/// so installing the extension never changes a solution that has not opted in.
/// </summary>
public sealed class CodeStraightSettings
{
	public const string FileName = ".codestraight";

	/// <summary>Run the enabled code actions when a C# file is saved.</summary>
	public bool FormatOnSave { get; init; }

	/// <summary>
	/// Rename the symbols declared in the saved file to the naming rules of the <c>.editorconfig</c>
	/// (types, methods, properties, events, fields, parameters, locals, local functions, type parameters), with all their
	/// references, like Rename (Ctrl+R, R).
	/// </summary>
	public bool RenameSymbols { get; init; }

	/// <summary><c>rename_scope</c>: <c>private</c> (default), <c>internal</c> or <c>all</c>.</summary>
	public RenameScope RenameScope { get; init; }

	/// <summary><c>rename_controller_actions</c>: also rename public methods of ASP.NET controllers (off by default).</summary>
	public bool RenameControllerActions { get; init; }

	/// <summary>
	/// <c>screaming_case_constants</c>: rename constants to upper case with words separated by <c>_</c>
	/// (<c>MaxValue</c> → <c>MAX_VALUE</c>), with all their references. Overrides the <c>.editorconfig</c> rule for constants
	/// and works without <see cref="RenameSymbols"/>; <see cref="RenameScope"/> still applies.
	/// </summary>
	public bool ScreamingCaseConstants { get; init; }

	/// <summary>Convert block-scoped namespaces according to <c>csharp_style_namespace_declarations</c>.</summary>
	public bool NamespaceDeclarations { get; init; }

	/// <summary>Remove using directives that are not needed.</summary>
	public bool RemoveUnusedUsings { get; init; }

	/// <summary>Sort using directives (<c>dotnet_sort_system_directives_first</c>).</summary>
	public bool SortUsings { get; init; }

	/// <summary>Add explicit accessibility modifiers (<c>dotnet_style_require_accessibility_modifiers</c>).</summary>
	public bool AccessibilityModifiers { get; init; }

	/// <summary>Add or remove the trailing comma of multi-line lists, following <see cref="TrailingCommaStyle"/>.</summary>
	public bool TrailingComma { get; init; }

	/// <summary><c>trailing_comma_style</c>: <c>omit</c>, <c>include</c> or <c>omit_when_single_line</c>.</summary>
	public TrailingCommaStyle TrailingCommaStyle { get; init; }

	/// <summary><c>binary_operator_new_line</c>: whether a wrapped line starts with the operator (<c>before</c>) or ends with it (<c>after</c>).</summary>
	public OperatorNewLine BinaryOperatorNewLine { get; init; } = OperatorNewLine.Before;

	/// <summary>Replace <c>var</c> with the explicit type inferred by the compiler.</summary>
	public bool ExplicitTypes { get; init; }

	/// <summary><c>remove_comments</c>: remove <c>//</c> and <c>/* */</c> comments, keeping documentation comments (<c>///</c>).</summary>
	public bool RemoveComments { get; init; }

	/// <summary>
	/// <c>remove_braces</c>: remove the braces of <c>if</c>/<c>else</c>, <c>for</c>, <c>foreach</c> and <c>while</c> blocks with a
	/// single simple statement, when <c>csharp_prefer_braces</c> of the <c>.editorconfig</c> is <c>false</c> or <c>when_multiline</c>.
	/// </summary>
	public bool RemoveBraces { get; init; }

	/// <summary>Remove surplus blank lines (IDE2000 / IDE2002 options of the <c>.editorconfig</c>).</summary>
	public bool RemoveExtraBlankLines { get; init; }

	/// <summary>Format the whole document (indentation, spacing) with the <c>.editorconfig</c> options.</summary>
	public bool FormatDocument { get; init; }

	/// <summary>Wrap lines longer than <c>max_line_length</c>.</summary>
	public bool WrapLongLines { get; init; }

	/// <summary><c>wrap_attributes</c>: also wrap lines inside attributes (default true). False lets long attributes stay on one line.</summary>
	public bool WrapAttributes { get; init; } = true;

	/// <summary><c>split_long_strings</c>: split string literals that cross the limit into concatenated parts (default false).</summary>
	public bool SplitLongStrings { get; init; }

	/// <summary><c>wrap_list_style</c>: <c>one_per_line</c> (default) or <c>fill</c> (as many parameters/arguments per line as fit).</summary>
	public WrapListStyle WrapListStyle { get; init; }

	/// <summary><c>wrap_tolerance</c>: columns a line may go over <c>max_line_length</c> before it is wrapped (default 0).</summary>
	public int WrapTolerance { get; init; }

	/// <summary>
	/// <c>normalize_line_endings</c>: make every line break the one of <c>end_of_line</c> in the <c>.editorconfig</c>,
	/// in any text file of the solution.
	/// </summary>
	public bool NormalizeLineEndings { get; init; }

	/// <summary><c>normalize_charset</c>: save every text file with the encoding of <c>charset</c> in the <c>.editorconfig</c>.</summary>
	public bool NormalizeCharset { get; init; }

	/// <summary>Draw vertical guidelines in the editor.</summary>
	public bool ShowGuideline { get; init; }

	/// <summary>Columns of the guidelines: <c>guidelines = 120</c> or <c>guidelines = 80, 120</c>.</summary>
	public IReadOnlyList<int> Guidelines { get; init; } = Array.Empty<int>();

	/// <summary>Write the result of every action to the Output window, not only errors and totals.</summary>
	public bool VerboseLog { get; init; }

	/// <summary>Guideline color as <c>#RRGGBB</c> or <c>#AARRGGBB</c>; null uses the default.</summary>
	public string? GuidelineColor { get; init; }

	/// <summary>Guideline columns to draw: <see cref="Guidelines"/> when <see cref="ShowGuideline"/> is on.</summary>
	public IReadOnlyList<int> GuidelineColumns() => ShowGuideline ? Guidelines : Array.Empty<int>();

	/// <summary>Keys of the switches that are on, in the order the engine runs them (for the status bar tooltip).</summary>
	public IReadOnlyList<string> EnabledSwitches()
	{
		var switches = new List<string>();
		Add(RenameSymbols, "rename_symbols");
		Add(ScreamingCaseConstants, "screaming_case_constants");
		Add(NamespaceDeclarations, "namespace_declarations");
		Add(RemoveUnusedUsings, "remove_unused_usings");
		Add(SortUsings, "sort_usings");
		Add(AccessibilityModifiers, "accessibility_modifiers");
		Add(ExplicitTypes, "explicit_types");
		Add(RemoveComments, "remove_comments");
		Add(RemoveExtraBlankLines, "remove_extra_blank_lines");
		Add(RemoveBraces, "remove_braces");
		Add(FormatDocument, "format_document");
		Add(WrapLongLines, "wrap_long_lines");
		Add(TrailingComma, "trailing_comma");
		Add(NormalizeLineEndings, "normalize_line_endings");
		Add(NormalizeCharset, "normalize_charset");
		Add(ShowGuideline, "show_guideline");
		return switches;

		void Add(bool enabled, string key)
		{
			if (enabled)
			{
				switches.Add(key);
			}
		}
	}

	public static CodeStraightSettings Parse(string content)
	{
		var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (IniSection section in IniParser.Parse(content))
		{
			foreach (KeyValuePair<string, string> property in section.Properties)
			{
				properties[property.Key] = property.Value;
			}
		}

		return new CodeStraightSettings
		{
			FormatOnSave = Flag(properties, "format_on_save"),
			RenameSymbols = Flag(properties, "rename_symbols"),
			RenameScope = (properties.TryGetValue("rename_scope", out string? scope) ? scope.ToLowerInvariant() : null) switch
			{
				"all" => RenameScope.All,
				"internal" => RenameScope.Internal,
				_ => RenameScope.Private,
			},
			RenameControllerActions = Flag(properties, "rename_controller_actions"),
			ScreamingCaseConstants = Flag(properties, "screaming_case_constants"),
			NamespaceDeclarations = Flag(properties, "namespace_declarations"),
			RemoveUnusedUsings = Flag(properties, "remove_unused_usings"),
			SortUsings = Flag(properties, "sort_usings"),
			AccessibilityModifiers = Flag(properties, "accessibility_modifiers"),
			ExplicitTypes = Flag(properties, "explicit_types"),
			RemoveComments = Flag(properties, "remove_comments"),
			RemoveExtraBlankLines = Flag(properties, "remove_extra_blank_lines"),
			RemoveBraces = Flag(properties, "remove_braces"),
			TrailingComma = Flag(properties, "trailing_comma"),
			TrailingCommaStyle = (properties.TryGetValue("trailing_comma_style", out string? comma) ? comma.ToLowerInvariant() : null) switch
			{
				"include" => TrailingCommaStyle.Include,
				"omit" => TrailingCommaStyle.Omit,
				"omit_when_single_line" => TrailingCommaStyle.OmitWhenSingleLine,
				_ => TrailingCommaStyle.NotSet,
			},
			BinaryOperatorNewLine = properties.TryGetValue("binary_operator_new_line", out string? position)
				&& string.Equals(position, "after", StringComparison.OrdinalIgnoreCase)
					? OperatorNewLine.After
					: OperatorNewLine.Before,
			FormatDocument = Flag(properties, "format_document"),
			WrapLongLines = Flag(properties, "wrap_long_lines"),
			WrapAttributes = !properties.TryGetValue("wrap_attributes", out string? wrapAttributes)
				|| !string.Equals(wrapAttributes, "false", StringComparison.OrdinalIgnoreCase),
			SplitLongStrings = Flag(properties, "split_long_strings"),
			WrapListStyle = properties.TryGetValue("wrap_list_style", out string? listStyle)
				&& string.Equals(listStyle, "fill", StringComparison.OrdinalIgnoreCase)
					? WrapListStyle.Fill
					: WrapListStyle.OnePerLine,
			WrapTolerance = properties.TryGetValue("wrap_tolerance", out string? tolerance)
				&& int.TryParse(tolerance, out int columns) && columns > 0
					? columns
					: 0,
			NormalizeLineEndings = Flag(properties, "normalize_line_endings"),
			NormalizeCharset = Flag(properties, "normalize_charset"),
			ShowGuideline = Flag(properties, "show_guideline"),
			Guidelines = ParseColumns(properties.TryGetValue("guidelines", out string? guidelines) ? guidelines : null),
			VerboseLog = properties.TryGetValue("log_level", out string? level)
				&& string.Equals(level, "verbose", StringComparison.OrdinalIgnoreCase),
			GuidelineColor = properties.TryGetValue("guideline_color", out string? color) && color.Length > 0
				? color
				: null,
		};
	}

	private static IReadOnlyList<int> ParseColumns(string? value)
	{
		var columns = new List<int>();
		foreach (string item in (value ?? string.Empty).Split(','))
		{
			if (int.TryParse(item.Trim(), out int column) && column > 0)
			{
				columns.Add(column);
			}
		}

		return columns;
	}

	private static bool Flag(IReadOnlyDictionary<string, string> properties, string key) =>
		properties.TryGetValue(key, out string? value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
