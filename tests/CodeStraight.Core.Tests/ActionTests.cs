using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class ActionTests
{
	[Fact]
	public async Task WithoutEnabledFeatures_NothingChanges()
	{
		const string source = "using System.Text;\nnamespace A\n{\n    class C { int x; }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, "format_on_save = true", Samples.NamingRules);

		Assert.Equal(source, result);
	}

	[Fact]
	public async Task RenamePrivateFields_RenamesTheFieldAndItsUsages_AndKeepsConstants()
	{
		const string source = """
			namespace A;

			public class Servicio
			{
			    private int contador;
			    private readonly string nombre = "x";
			    private const string Prefijo = "p";
			    private static readonly int Maximo = 3;
			    public int Publico;

			    public int Obtener()
			    {
			        contador++;
			        return contador + nombre.Length + Prefijo.Length + Maximo + Publico;
			    }
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "rename_symbols = true", Samples.NamingRules);

		Assert.Contains("private int _contador;", result);
		Assert.Contains("private readonly string _nombre", result);
		Assert.Contains("_contador++;", result);
		Assert.Contains("return _contador + _nombre.Length + Prefijo.Length + Maximo + Publico;", result);
		Assert.Contains("private const string Prefijo", result);
		Assert.Contains("private static readonly int Maximo", result);
	}

	[Fact]
	public async Task RenamePrivateFields_AppliesTheRuleOfEachKindOfPrivateField()
	{
		const string source = """
			class C
			{
			    private const string prefijo_texto = "p";
			    private static readonly int maximo_valor = 3;
			    public const string publica = "x";
			    int M() => prefijo_texto.Length + maximo_valor + publica.Length;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "rename_symbols = true", Samples.NamingRules);

		Assert.Contains("private const string PrefijoTexto", result);
		Assert.Contains("private static readonly int MaximoValor", result);
		Assert.Contains("public const string publica", result);
		Assert.Contains("PrefijoTexto.Length + MaximoValor + publica.Length", result);
	}

	[Fact]
	public async Task RenamePrivateFields_SkipsWhenTheNewNameAlreadyExists()
	{
		const string source = """
			class C
			{
			    private int valor;
			    private int _valor;
			    int Suma() => valor + _valor;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "rename_symbols = true", Samples.NamingRules);

		Assert.Contains("private int valor;", result);
	}

	[Fact]
	public async Task NamespaceDeclarations_ConvertsBlockToFileScoped_AndDedents()
	{
		const string source = """
			using System;

			// Comment before the namespace
			namespace A.B
			{
			    public class C
			    {
			        public string Texto = @"line 1
			    line 2";
			    }
			}
			""";

		string result = await TestWorkspace.ApplyAsync(
			source,
			"namespace_declarations = true",
			"[*.cs]\ncsharp_style_namespace_declarations = file_scoped:warning\nindent_size = 4");

		Assert.Equal("""
			using System;

			// Comment before the namespace
			namespace A.B;

			public class C
			{
			    public string Texto = @"line 1
			    line 2";
			}

			""", result);
	}

	[Fact]
	public async Task NamespaceDeclarations_LeavesFilesWithSeveralNamespaces()
	{
		const string source = "namespace A\n{\n}\nnamespace B\n{\n}\n";

		string result = await TestWorkspace.ApplyAsync(
			source,
			"namespace_declarations = true",
			"[*.cs]\ncsharp_style_namespace_declarations = file_scoped");

		Assert.Equal(source, result);
	}

	[Fact]
	public async Task Usings_RemovesUnusedAndSortsWithSystemFirst()
	{
		const string source = """
			// Header
			using Zeta.Tools;
			using System.Text;
			using System;
			using Alpha;

			namespace Alpha { public class A { } }
			namespace Zeta.Tools { public class Z { } }

			public class C
			{
			    public A Valor = new A();
			    public Z Otro = new Z();
			    public string Texto = String.Empty;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "remove_unused_usings = true\nsort_usings = true", "[*.cs]\n");

		Assert.StartsWith("// Header\nusing System;\nusing Alpha;\nusing Zeta.Tools;\n\nnamespace Alpha", result);
	}

	[Fact]
	public async Task AccessibilityModifiers_AddsTheModifierTheCompilerAlreadyUses()
	{
		const string source = """
			class C
			{
			    int campo;
			    void Metodo() { }
			    class Anidada { }
			    static C() { }
			}

			interface I
			{
			    void Metodo();
			}
			""";

		string result = await TestWorkspace.ApplyAsync(
			source,
			"accessibility_modifiers = true",
			"[*.cs]\ndotnet_style_require_accessibility_modifiers = for_non_interface_members:warning");

		Assert.Contains("internal class C", result);
		Assert.Contains("private int campo;", result);
		Assert.Contains("private void Metodo() { }", result);
		Assert.Contains("private class Anidada", result);
		Assert.Contains("static C() { }", result);
		Assert.Contains("internal interface I", result);
		Assert.Contains("    void Metodo();", result);
	}

	[Theory]
	[InlineData("omit", "Edad = 3\n", "Edad = 3,\n")]
	[InlineData("include", "Edad = 3,\n", "Edad = 3\n")]
	public async Task TrailingComma_FollowsTheConfiguredStyle(string style, string expected, string input)
	{
		string source = "class P { public int Nombre; public int Edad; }\nclass C\n{\n    P Crear() => new P\n    {\n        Nombre = 1,\n        " + input + "    };\n    int[] Lista = { 1, 2, 3 };\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, $"trailing_comma = true\ntrailing_comma_style = {style}", "[*.cs]\n");

		Assert.Contains(expected, result);
		Assert.Contains("{ 1, 2, 3 }", result);
	}

	[Fact]
	public async Task FormatDocument_UsesTabsFromTheEditorConfig()
	{
		const string source = "class C\n{\n    void M()\n    {\n        int x = 1;\n    }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, "format_document = true", "[*.cs]\nindent_style = tab\ntab_width = 4");

		Assert.Equal("class C\n{\n\tvoid M()\n\t{\n\t\tint x = 1;\n\t}\n}\n", result);
	}
}
