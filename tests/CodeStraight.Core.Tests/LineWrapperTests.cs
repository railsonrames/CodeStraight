using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class LineWrapperTests
{
	private const string Settings = "wrap_long_lines = true";

	private static string EditorConfig(int maxLength = 60, string extra = "") =>
		$"[*.cs]\nindent_style = space\nindent_size = 4\nmax_line_length = {maxLength}\n{extra}";

	[Fact]
	public async Task ShortLines_AreNotTouched()
	{
		const string source = "class C\n{\n    int Sum(int a, int b) => a + b;\n}\n";

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, EditorConfig()));
	}

	[Fact]
	public async Task LongParameterList_IsWrappedAfterEachComma()
	{
		const string source = "class C\n{\n    void Crear(string nombreCompleto, string apellidoCompleto, int edad)\n    {\n    }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, EditorConfig());

		Assert.Equal(
			"class C\n{\n    void Crear(\n        string nombreCompleto,\n        string apellidoCompleto,\n        int edad)\n    {\n    }\n}\n",
			result);
	}

	[Fact]
	public async Task ExpressionBody_WrapsBeforeTheArrowAndThenTheArguments()
	{
		const string source = "class C\n{\n    string Texto(int a, int b) => string.Concat(\"Parametros no validos: \", a.ToString(), \" y \", b.ToString());\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, EditorConfig(70));

		Assert.Equal(
			"class C\n{\n    string Texto(int a, int b)\n        => string.Concat(\n            \"Parametros no validos: \",\n            a.ToString(),\n            \" y \",\n            b.ToString());\n}\n",
			result);
		Assert.All(result.Split('\n'), line => Assert.True(line.Length <= 70, line));
	}

	[Fact]
	public async Task StringConcatenation_WrapsAtTheOperator_WithTheConfiguredPosition()
	{
		const string source = "class C\n{\n    string T = \"primera parte del texto \" + \"segunda parte del texto\" + \"tercera\";\n}\n";

		string before = await TestWorkspace.ApplyAsync(source, Settings, EditorConfig());
		string after = await TestWorkspace.ApplyAsync(source, Settings + "\nbinary_operator_new_line = after", EditorConfig());

		Assert.Contains("\"primera parte del texto \"\n        + \"segunda parte del texto\"\n        + \"tercera\";", before);
		Assert.Contains("\"primera parte del texto \" +\n        \"segunda parte del texto\" +\n        \"tercera\";", after);
	}

	[Fact]
	public async Task ObjectInitializer_PutsEachMemberOnItsOwnLine()
	{
		const string source = "class P { public int Nombre; public int Apellido; public int Edad; }\nclass C\n{\n    P Crear()\n    {\n        return new P { Nombre = 1, Apellido = 2, Edad = 3 };\n    }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, EditorConfig(50));

		Assert.Contains("        return new P\n        {\n            Nombre = 1,\n            Apellido = 2,\n            Edad = 3\n        };\n", result);
	}

	[Fact]
	public async Task LongStringLiteral_IsNeverSplit()
	{
		const string source = "class C\n{\n    string T = \"un literal de texto muy largo que no se puede partir de ninguna forma\";\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, EditorConfig());

		Assert.Contains("\"un literal de texto muy largo que no se puede partir de ninguna forma\"", result);
	}

	[Fact]
	public async Task TabsCountAsTabWidthColumns()
	{
		const string source = "class C\n{\n\tvoid M(int primero, int segundo) { }\n}\n";

		string unchanged = await TestWorkspace.ApplyAsync(source, Settings, "[*.cs]\nindent_style = tab\ntab_width = 4\nmax_line_length = 40");
		string wrapped = await TestWorkspace.ApplyAsync(source, Settings, "[*.cs]\nindent_style = tab\ntab_width = 8\nmax_line_length = 40");

		Assert.Equal(source, unchanged);
		Assert.Contains("void M(\n\t\tint primero,\n\t\tint segundo) { }", wrapped);
	}
}
