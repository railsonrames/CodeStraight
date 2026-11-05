using CodeStraight.Core.Configuration;
using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class ConfigurationTests
{
	[Fact]
	public void Settings_AreOffUnlessEnabled()
	{
		CodeStraightSettings settings = CodeStraightSettings.Parse("""
			# comment
			format_on_save = true
			wrap_long_lines = false
			show_guideline = TRUE
			guideline_color = #FF0000
			""");

		Assert.True(settings.FormatOnSave);
		Assert.False(settings.WrapLongLines);
		Assert.True(settings.ShowGuideline);
		Assert.False(settings.RenameSymbols);
		Assert.Equal("#FF0000", settings.GuidelineColor);
	}

	[Theory]
	[InlineData("*.cs", "Controllers/Foo.cs", true)]
	[InlineData("*.cs", "Foo.vb", false)]
	[InlineData("*.{cs,vb}", "src/Foo.vb", true)]
	[InlineData("src/**/*.cs", "src/a/b/Foo.cs", true)]
	[InlineData("src/*.cs", "src/a/Foo.cs", false)]
	[InlineData("*", "anything.json", true)]
	public void Globs_MatchLikeEditorConfig(string glob, string path, bool expected) =>
		Assert.Equal(expected, EditorConfigFile.GlobToRegex(glob).IsMatch(path));

	[Fact]
	public void EditorConfig_LaterSectionsOverrideEarlierOnes()
	{
		EditorConfigFile file = EditorConfigFile.Parse("C:/repo", """
			root = true
			[*]
			indent_style = space
			max_line_length = 80
			[*.cs]
			indent_style = tab
			max_line_length = 120 # inline comment
			""");

		StyleOptions style = StyleOptions.FromProperties(file.GetProperties("C:/repo/src/Foo.cs"));

		Assert.True(style.UseTabs);
		Assert.Equal(120, style.MaxLineLength);
	}

	[Fact]
	public void Options_IgnoreTheLegacySeveritySuffix()
	{
		StyleOptions style = StyleOptions.FromProperties(new Dictionary<string, string>
		{
			["csharp_style_namespace_declarations"] = "file_scoped:warning",
		});

		Assert.Equal(NamespaceStyle.FileScoped, style.NamespaceStyle);
	}

	[Fact]
	public void CodeStraightOnlyValues_ComeFromTheCodeStraightFile_NotFromTheEditorConfig()
	{
		FileConfiguration fromCodeStraight = TestWorkspace.Configuration(
			"trailing_comma_style = omit\nbinary_operator_new_line = after",
			"[*.cs]\n");
		FileConfiguration fromEditorConfig = TestWorkspace.Configuration(
			string.Empty,
			"[*.cs]\nroslynator_trailing_comma_style = omit\nroslynator_binary_operator_new_line = after");

		Assert.Equal(TrailingCommaStyle.Omit, fromCodeStraight.Style.TrailingComma);
		Assert.Equal(OperatorNewLine.After, fromCodeStraight.Style.BinaryOperatorNewLine);
		Assert.Equal(TrailingCommaStyle.NotSet, fromEditorConfig.Style.TrailingComma);
		Assert.Equal(OperatorNewLine.Before, fromEditorConfig.Style.BinaryOperatorNewLine);
	}

	[Fact]
	public void NamingRules_MostSpecificRuleWins()
	{
		IReadOnlyList<NamingRule> rules = StyleOptions.FromProperties(EditorConfigFile.Parse("C:/repo", Samples.NamingRules)
			.GetProperties("C:/repo/Test.cs")).NamingRules;

		Assert.Equal("private_fields_underscore", NamingRule.Find(rules, "field", "private", [])!.Name);
		Assert.Equal("constants_pascal", NamingRule.Find(rules, "field", "private", ["const"])!.Name);
		Assert.Equal("static_readonly_pascal", NamingRule.Find(rules, "field", "private", ["static", "readonly"])!.Name);
		Assert.Equal("private_fields_underscore", NamingRule.Find(rules, "field", "private", ["readonly"])!.Name);
	}

	[Theory]
	[InlineData("contador", "_contador")]
	[InlineData("_contador", "_contador")]
	[InlineData("m_Nombre", "_nombre")]
	[InlineData("NombreCompleto", "_nombreCompleto")]
	[InlineData("HTTPClient", "_httpClient")]
	public void NamingStyle_UnderscoreCamelCase(string name, string expected)
	{
		var style = new NamingStyle { Prefix = "_", Capitalization = Capitalization.CamelCase };

		Assert.Equal(expected, style.Apply(name));
	}

	[Theory]
	[InlineData("MAX_VALUE", "MaxValue")]
	[InlineData("NOMBRE_TABLA_2", "NombreTabla2")]
	[InlineData("MAXIMO", "Maximo")]
	[InlineData("SCREAMCASE", "Screamcase")]
	[InlineData("URL", "Url")]
	[InlineData("ID", "ID")]
	[InlineData("HTTPClient", "HTTPClient")]
	[InlineData("MaxValue", "MaxValue")]
	[InlineData("max_value", "MaxValue")]
	public void NamingStyle_PascalCase_ScreamingCase(string name, string expected)
	{
		var style = new NamingStyle { Capitalization = Capitalization.PascalCase };

		Assert.Equal(expected, style.Apply(name));
	}

	[Fact]
	public void NamingStyle_UnderscoreCamelCase_ScreamingCase()
	{
		var style = new NamingStyle { Prefix = "_", Capitalization = Capitalization.CamelCase };

		Assert.Equal("_maxValue", style.Apply("MAX_VALUE"));
	}
}
