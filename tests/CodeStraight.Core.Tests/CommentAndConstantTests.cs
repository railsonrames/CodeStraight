using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class CommentAndConstantTests
{
	private const string RemoveComments = "remove_comments = true";

	private const string Screaming = "screaming_case_constants = true";

	[Fact]
	public async Task RemovesComments_KeepsDocumentationComments()
	{
		const string source = """
			// File header.
			using System;

			namespace N;

			/// <summary>Used by Swagger.</summary>
			public class C
			{
				// A field.
				private int _x = 1; // trailing
				/* block */ private int _y = 2;

				/**
				 * <summary>Also documentation.</summary>
				 */
				public int Sum(int a /* first */, int b) => a + b;
			}
			""";

		const string expected = """
			using System;

			namespace N;

			/// <summary>Used by Swagger.</summary>
			public class C
			{
				private int _x = 1;
				private int _y = 2;

				/**
				 * <summary>Also documentation.</summary>
				 */
				public int Sum(int a, int b) => a + b;
			}
			""";

		Assert.Equal(expected, await TestWorkspace.ApplyAsync(source, RemoveComments, "[*.cs]\n"));
	}

	[Fact]
	public async Task RemovedCommentLines_DoNotLeaveExtraBlankLines()
	{
		const string source = "class C\n{\n\t// first\n\n\tint a;\n\n\t/* multi\n\t   line */\n\n\tint b;\n\n\t// last\n}\n";

		Assert.Equal(
			"class C\n{\n\tint a;\n\n\tint b;\n}\n",
			await TestWorkspace.ApplyAsync(source, RemoveComments, "[*.cs]\n"));
	}

	[Fact]
	public async Task ToolDirectivesAndStrings_AreKept()
	{
		const string source = """
			class C
			{
				// ReSharper disable once UnusedMember.Local
				// language=regex
				private const string Pattern = "a // not a comment /* either */";
			}
			""";

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, RemoveComments, "[*.cs]\n"));
	}

	[Fact]
	public async Task ScreamingCaseConstants_RenamesConstantsAndReferences_EvenAgainstTheEditorConfigRule()
	{
		const string source = """
			class C
			{
				private const int MinhaConstanteValorMaximoPadrao = 10;
				private const string _httpTimeout = "30";
				private const int MAX_VALUE = 1;
				private int _contador = MinhaConstanteValorMaximoPadrao;

				private int Get() => MinhaConstanteValorMaximoPadrao + MAX_VALUE + _httpTimeout.Length + _contador;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, Screaming, Samples.NamingRules);

		Assert.Contains("private const int MINHA_CONSTANTE_VALOR_MAXIMO_PADRAO = 10;", result);
		Assert.Contains("private const string HTTP_TIMEOUT = \"30\";", result);
		Assert.Contains("private const int MAX_VALUE = 1;", result);
		Assert.Contains("private int _contador = MINHA_CONSTANTE_VALOR_MAXIMO_PADRAO;", result);
		Assert.Contains("MINHA_CONSTANTE_VALOR_MAXIMO_PADRAO + MAX_VALUE + HTTP_TIMEOUT.Length + _contador", result);
	}

	[Fact]
	public async Task ScreamingCaseConstants_WithoutRenameSymbols_LeavesOtherSymbolsAlone()
	{
		const string source = """
			class C
			{
				private const int ValorPadrao = 1;
				private int contador = ValorPadrao;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, Screaming, Samples.NamingRules);

		Assert.Contains("private const int VALOR_PADRAO = 1;", result);
		Assert.Contains("private int contador = VALOR_PADRAO;", result);
	}

	[Fact]
	public async Task ScreamingCaseConstants_WithRenameSymbols_AlsoAppliesTheOtherRules()
	{
		const string source = """
			class C
			{
				private const int ValorPadrao = 1;
				private int contador = ValorPadrao;
			}
			""";

		string result = await TestWorkspace.ApplyAsync(source, "rename_symbols = true\n" + Screaming, Samples.NamingRules);

		Assert.Contains("private const int VALOR_PADRAO = 1;", result);
		Assert.Contains("private int _contador = VALOR_PADRAO;", result);
	}
}
