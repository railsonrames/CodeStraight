using CodeStraight.Core.Configuration;
using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class RepairAndIndentTests
{
	private const string Tabs = "[*.cs]\nindent_style = tab\ntab_width = 4\nmax_line_length = 120";

	[Fact]
	public async Task TrailingComma_RemovesTheInvalidCommaAfterTheLastArgument()
	{
		const string source = "class C\n{\n\tint Suma(int a, int b) => a + b;\n\tint M() => Suma(\n\t\t1,\n\t\t2,\n\t);\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, "trailing_comma = true", Tabs);

		Assert.Equal("class C\n{\n\tint Suma(int a, int b) => a + b;\n\tint M() => Suma(\n\t\t1,\n\t\t2\n\t);\n}\n", result);
	}

	[Fact]
	public async Task RemoveUnusedUsings_RemovesDuplicatesEvenWhenTheFileDoesNotCompile()
	{
		const string source = "using System.Text;\nusing System.Text;\nclass C { NoExiste x; StringBuilder b; }\n";

		string result = await TestWorkspace.ApplyAsync(source, "remove_unused_usings = true", Tabs);

		Assert.StartsWith("using System.Text;\nclass C", result);
	}

	[Fact]
	public async Task FormatDocument_ReindentsContinuationLines()
	{
		const string source = """
			using System.Collections.Generic;
			using System.Linq;

			class C
			{
				List<int> M(List<int> numeros)
				{
					List<int> resultado =
					[
					.. numeros
			.Where(n => n > 0)
			.Select(n => Doble(
			n,
			2
			))
				];
					return resultado;
				}

				int Doble(int n, int factor) => n * factor;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "format_document = true", Tabs);

		Assert.Contains(
			"\t\tList<int> resultado =\n\t\t[\n\t\t\t.. numeros\n\t\t\t\t.Where(n => n > 0)\n\t\t\t\t.Select(n => Doble(\n"
				+ "\t\t\t\t\tn,\n\t\t\t\t\t2\n\t\t\t\t))\n\t\t];\n",
			result);
	}

	[Fact]
	public async Task WrapLongLines_WrapsAccessorListsOnePerLine()
	{
		const string source = "class C\n{\n\tprivate int _valor;\n\tpublic int Valor { get => _valor; set { _valor = value; System.Console.WriteLine(value); } }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, "wrap_long_lines = true\nformat_document = true", "[*.cs]\nindent_style = tab\ntab_width = 4\nmax_line_length = 80");

		Assert.Contains("\tpublic int Valor\n\t{\n\t\tget => _valor;\n\t\tset { _valor = value; System.Console.WriteLine(value); }\n\t}\n", result);
	}

	[Fact]
	public async Task ExplicitTypes_ReplacesVarWithTheInferredType_ButKeepsAnonymousTypes()
	{
		const string source = """
			using System.Collections.Generic;

			class C
			{
				void M()
				{
					var lista = new List<string>();
					var texto = "x";
					var anonimo = new { Id = 1 };
					foreach (var elemento in lista)
					{
					}

					int.TryParse(texto, out var numero);
				}
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "explicit_types = true", Tabs);

		Assert.Contains("List<string> lista = new List<string>();", result);
		Assert.Contains("string texto = \"x\";", result);
		Assert.Contains("var anonimo = new { Id = 1 };", result);
		Assert.Contains("foreach (string elemento in lista)", result);
		Assert.Contains("out int numero", result);
	}

	[Theory]
	[InlineData("show_guideline = true\nguidelines = 120", new[] { 120 })]
	[InlineData("show_guideline = true\nguidelines = 80, 120", new[] { 80, 120 })]
	[InlineData("show_guideline = false\nguidelines = 120", new int[0])]
	[InlineData("show_guideline = true", new int[0])]
	public void Guidelines_ComeFromTheCodeStraightFile_NeverFromTheEditorConfig(string codestraight, int[] expected)
	{
		FileConfiguration configuration = TestWorkspace.Configuration(codestraight, "[*]\nguidelines = 160\nmax_line_length = 120");

		Assert.Equal(expected, configuration.Settings.GuidelineColumns());
	}
}
