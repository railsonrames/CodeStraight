using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class BraceTests
{
	private const string Settings = "remove_braces = true";

	private const string NoBraces = "[*.cs]\nindent_style = tab\ncsharp_prefer_braces = false:silent\n";

	private const string WhenMultiline = "[*.cs]\nindent_style = tab\ncsharp_prefer_braces = when_multiline\n";

	private static string Method(string body) => "class C\n{\n\tint M(int a, int[] items)\n\t{\n" + body + "\t\treturn 0;\n\t}\n}\n";

	[Fact]
	public async Task IfWithOneStatement_LosesItsBraces()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\treturn a;\n\t\t}\n\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(Method("\t\tif (a > 0)\n\t\t\treturn a;\n\n"), result);
	}

	[Fact]
	public async Task SameLineBraceStyle_IsAlsoUnwrapped()
	{
		string source = Method("\t\tif (a > 0) {\n\t\t\treturn a;\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(Method("\t\tif (a > 0)\n\t\t\treturn a;\n"), result);
	}

	[Fact]
	public async Task IfElseChain_LosesAllItsBraces_WhenEveryBranchHasOneStatement()
	{
		string source = Method(
			"\t\tif (a > 0)\n\t\t{\n\t\t\ta++;\n\t\t}\n\t\telse if (a < 0)\n\t\t{\n\t\t\ta--;\n\t\t}\n\t\telse\n\t\t{\n\t\t\tthrow new System.Exception();\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(
			Method("\t\tif (a > 0)\n\t\t\ta++;\n\t\telse if (a < 0)\n\t\t\ta--;\n\t\telse\n\t\t\tthrow new System.Exception();\n"),
			result);
	}

	[Fact]
	public async Task IfElseChain_KeepsItsBraces_WhenOneBranchNeedsThem()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\ta++;\n\t\t}\n\t\telse\n\t\t{\n\t\t\ta--;\n\t\t\ta--;\n\t\t}\n");

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, NoBraces));
	}

	[Fact]
	public async Task NestedIfInsideAnIfWithElse_KeepsTheBraces_ToKeepWhereTheElseBelongs()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\tif (a > 1)\n\t\t\t\ta++;\n\t\t}\n\t\telse\n\t\t{\n\t\t\ta--;\n\t\t}\n");

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, NoBraces));
	}

	[Fact]
	public async Task DeclarationsAndSeveralStatements_KeepTheirBraces()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\tint b = a;\n\t\t}\n\t\tif (a > 1)\n\t\t{\n\t\t\ta++;\n\t\t\ta++;\n\t\t}\n");

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, NoBraces));
	}

	[Fact]
	public async Task CommentsInsideTheBlock_KeepTheBraces_AndTrailingCommentsAreKept()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\t// explicación\n\t\t\ta++;\n\t\t}\n\t\tif (a > 1)\n\t\t{\n\t\t\ta--; // motivo\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(
			Method("\t\tif (a > 0)\n\t\t{\n\t\t\t// explicación\n\t\t\ta++;\n\t\t}\n\t\tif (a > 1)\n\t\t\ta--; // motivo\n"),
			result);
	}

	[Fact]
	public async Task Loops_LoseTheirBraces()
	{
		string source = Method(
			"\t\tfor (int i = 0; i < a; i++)\n\t\t{\n\t\t\ta--;\n\t\t}\n\t\tforeach (int item in items)\n\t\t{\n\t\t\ta += item;\n\t\t}\n\t\twhile (a > 10)\n\t\t{\n\t\t\ta--;\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(
			Method("\t\tfor (int i = 0; i < a; i++)\n\t\t\ta--;\n\t\tforeach (int item in items)\n\t\t\ta += item;\n\t\twhile (a > 10)\n\t\t\ta--;\n"),
			result);
	}

	[Fact]
	public async Task WhenMultiline_KeepsTheBracesOfMultiLineStatements()
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\ta = a\n\t\t\t\t+ 1;\n\t\t}\n\t\tif (a > 1)\n\t\t{\n\t\t\ta++;\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, WhenMultiline);

		Assert.Equal(Method("\t\tif (a > 0)\n\t\t{\n\t\t\ta = a\n\t\t\t\t+ 1;\n\t\t}\n\t\tif (a > 1)\n\t\t\ta++;\n"), result);
	}

	[Fact]
	public async Task NestedBlocks_AreUnwrappedAtEveryLevel()
	{
		string source = Method("\t\tforeach (int item in items)\n\t\t{\n\t\t\tif (item > 0)\n\t\t\t{\n\t\t\t\tcontinue;\n\t\t\t}\n\n\t\t\ta += item;\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(
			Method("\t\tforeach (int item in items)\n\t\t{\n\t\t\tif (item > 0)\n\t\t\t\tcontinue;\n\n\t\t\ta += item;\n\t\t}\n"),
			result);
	}

	[Theory]
	[InlineData("[*.cs]\nindent_style = tab\ncsharp_prefer_braces = true\n")]
	[InlineData("[*.cs]\nindent_style = tab\n")]
	public async Task WithBracesPreferredOrNotConfigured_NothingChanges(string editorconfig)
	{
		string source = Method("\t\tif (a > 0)\n\t\t{\n\t\t\treturn a;\n\t\t}\n");

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, editorconfig));
	}

	[Fact]
	public async Task FileIndentedWithSpaces_KeepsSpaces_EvenIfTheEditorConfigAsksForTabs()
	{
		const string source =
			"class C\n{\n    int M(int a)\n    {\n        if (a > 0)\n        {\n            return a;\n        }\n        else\n        {\n            return 0;\n        }\n    }\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(
			"class C\n{\n    int M(int a)\n    {\n        if (a > 0)\n            return a;\n        else\n            return 0;\n    }\n}\n",
			result);
	}

	[Fact]
	public async Task StatementWrittenAfterTheOpeningBrace_IsMovedToItsOwnLine()
	{
		string source = Method("\t\tif (a > 0) { return a; }\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings, NoBraces);

		Assert.Equal(Method("\t\tif (a > 0)\n\t\t\treturn a;\n"), result);
	}

	[Fact]
	public async Task WithFormatDocument_TheResultIsStable()
	{
		string source = Method("\t\tif (a > 0) {\n\t\t\treturn a;\n\t\t} else {\n\t\t\ta++;\n\t\t}\n");

		string result = await TestWorkspace.ApplyAsync(source, Settings + "\nformat_document = true", NoBraces);

		Assert.Equal(Method("\t\tif (a > 0)\n\t\t\treturn a;\n\t\telse\n\t\t\ta++;\n"), result);
	}
}
