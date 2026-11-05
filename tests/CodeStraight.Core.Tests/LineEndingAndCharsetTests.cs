using CodeStraight.Core.Actions;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class LineEndingAndCharsetTests
{
	[Fact]
	public async Task NormalizeLineEndings_MakesEveryLineBreakTheConfiguredOne()
	{
		const string mixed = "class C\r\n{\n\tint x;\r\tint y;\r\n}\n";

		string result = await TestWorkspace.ApplyRawAsync(mixed, "normalize_line_endings = true", "[*]\nend_of_line = crlf");

		Assert.Equal("class C\r\n{\r\n\tint x;\r\n\tint y;\r\n}\r\n", result);
	}

	[Fact]
	public async Task NormalizeLineEndings_IsOffByDefault()
	{
		const string mixed = "class C\r\n{\n}\n";

		Assert.Equal(mixed, await TestWorkspace.ApplyRawAsync(mixed, "format_on_save = true", "[*]\nend_of_line = crlf"));
	}

	[Fact]
	public void LineEndingNormalizer_WorksOnAnyText()
	{
		SourceText text = SourceText.From("{\n  \"a\": 1\r\n}\n");

		Assert.Equal("{\r\n  \"a\": 1\r\n}\r\n", LineEndingNormalizer.Normalize(text, "\r\n").ToString());
	}

	[Theory]
	[InlineData("utf-8", Charset.Utf8)]
	[InlineData("utf-8-bom", Charset.Utf8Bom)]
	[InlineData("latin1", Charset.Latin1)]
	[InlineData("", Charset.NotSet)]
	public void Charset_IsReadFromTheEditorConfig(string value, Charset expected)
	{
		FileConfiguration configuration = TestWorkspace.Configuration("normalize_charset = true", $"[*]\ncharset = {value}");

		Assert.Equal(expected, configuration.Style.Charset);
		Assert.True(configuration.Settings.NormalizeCharset);
	}

	[Theory]
	[InlineData("crlf", "\r\n")]
	[InlineData("lf", "\n")]
	public void EndOfLine_IsReadFromTheEditorConfig(string value, string expected) =>
		Assert.Equal(expected, TestWorkspace.Configuration(string.Empty, $"[*]\nend_of_line = {value}").Style.NewLine);
}
