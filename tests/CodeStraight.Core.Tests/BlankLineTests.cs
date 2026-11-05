using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class BlankLineTests
{
	private const string Settings = "remove_extra_blank_lines = true";

	private const string Both = """
		[*.cs]
		dotnet_style_allow_multiple_blank_lines_experimental = false:warning
		csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false:warning
		""";

	[Fact]
	public async Task KeepsAtMostOneBlankLine_AndNoneBetweenClosingBraces()
	{
		const string source = "class C\n{\n\tvoid A()\n\t{\n\t\tif (true)\n\t\t{\n\t\t}\n\n\t}\n\n\n\n\tvoid B() { }\n\n}\n";

		string result = await TestWorkspace.ApplyAsync(source, Settings, Both);

		Assert.Equal("class C\n{\n\tvoid A()\n\t{\n\t\tif (true)\n\t\t{\n\t\t}\n\t}\n\n\tvoid B() { }\n}\n", result);
	}

	[Fact]
	public async Task LinesInsideMultiLineStrings_AreKept()
	{
		const string source = "class C\n{\n\tstring T = @\"a\n\n\n\nb\";\n}\n";

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, Both));
	}

	[Fact]
	public async Task WithoutTheEditorConfigOptions_NothingIsRemoved()
	{
		const string source = "class C\n{\n\n\n\tint x;\n}\n";

		Assert.Equal(source, await TestWorkspace.ApplyAsync(source, Settings, "[*.cs]\n"));
	}
}
