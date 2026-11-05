using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class LongStringTests
{
	private const string EditorConfig = "[*.cs]\nindent_style = tab\ntab_width = 4\nmax_line_length = 120";

	private const string Attribute = """
		using System;

		[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
		public sealed class SwaggerResponseAttribute(int code, string description) : Attribute
		{
		}

		""";

	/// <summary>A real-world controller example, with operators alone on their lines and a blank line.</summary>
	private const string BrokenConcatenation = Attribute + """
		public class C
		{
			[SwaggerResponse(400,
		 "Parámetros no válidos. Devuelve `RespuestaComun` con `state` = \"error\" y uno de estos mensajes: "
		  +

		 "\"El parámetro fecha debe contener años entre 1 y 9999.\". "
		  +
		 "Si falta `fecha` o `proyecto`, o `proyecto` no es numérico, la validación automática de ASP.NET Core "
		  +
		 "responde con `ValidationProblemDetails` (application/problem+json).")]
			public void M() { }
		}
		""";

	private const string OneLongLine = Attribute + """
		public class C
		{
			[SwaggerResponse(400, "Parámetros no válidos. Devuelve RespuestaComun con state = error y uno de estos mensajes: El parámetro fecha no es válido, El parámetro idProductos no es válido o El parámetro fecha debe contener años entre 1 y 9999.")]
			public void M() { }
		}
		""";

	[Fact]
	public async Task OperatorsAloneOnALine_AreJoinedToTheNextOperand()
	{
		string result = await TestWorkspace.ApplyAsync(BrokenConcatenation, "wrap_long_lines = true\nformat_document = true", EditorConfig);

		Assert.Contains(
			"\t[SwaggerResponse(400,\n"
			+ "\t\t\"Parámetros no válidos. Devuelve `RespuestaComun` con `state` = \\\"error\\\" y uno de estos mensajes: \"\n"
			+ "\t\t\t+ \"\\\"El parámetro fecha debe contener años entre 1 y 9999.\\\". \"\n"
			+ "\t\t\t+ \"Si falta `fecha` o `proyecto`, o `proyecto` no es numérico, la validación automática de ASP.NET Core \"\n"
			+ "\t\t\t+ \"responde con `ValidationProblemDetails` (application/problem+json).\")]\n",
			result);
	}

	[Fact]
	public async Task WrapAttributesFalse_LeavesLongAttributesOnOneLine()
	{
		string result = await TestWorkspace.ApplyAsync(
			OneLongLine,
			"wrap_long_lines = true\nformat_document = true\nwrap_attributes = false\nsplit_long_strings = true",
			EditorConfig);

		Assert.Equal(TestWorkspace.Normalize(OneLongLine), result);
	}

	[Fact]
	public async Task SplitLongStrings_SplitsTheLiteralIntoPartsThatFit_AndTheValueIsTheSame()
	{
		string result = await TestWorkspace.ApplyAsync(
			OneLongLine,
			"wrap_long_lines = true\nformat_document = true\nsplit_long_strings = true",
			EditorConfig);

		Assert.All(result.Split('\n'), line => Assert.True(line.Replace("\t", "    ").Length <= 120, line));
		Assert.Contains("\t\t\t+ \"", result);
		Assert.Equal(await ConstantValue(OneLongLine), await ConstantValue(result));
		Assert.Equal(result, await TestWorkspace.ApplyAsync(result, "wrap_long_lines = true\nformat_document = true\nsplit_long_strings = true", EditorConfig));
	}

	[Fact]
	public async Task WithoutSplitLongStrings_TheLiteralIsKeptWhole()
	{
		string result = await TestWorkspace.ApplyAsync(OneLongLine, "wrap_long_lines = true\nformat_document = true", EditorConfig);

		Assert.Contains("\"Parámetros no válidos. Devuelve RespuestaComun con state = error y uno de estos mensajes: El parámetro fecha no es válido, El parámetro idProductos no es válido o El parámetro fecha debe contener años entre 1 y 9999.\"", result);
	}

	/// <summary>The constant value of the description argument, as the compiler sees it.</summary>
	private static async Task<string> ConstantValue(string source)
	{
		Document document = TestWorkspace.CreateDocument(TestWorkspace.Normalize(source));
		SemanticModel model = (await document.GetSemanticModelAsync())!;
		var argument = (await document.GetSyntaxRootAsync())!
			.DescendantNodes()
			.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeArgumentSyntax>()
			.Last(argument => argument.Parent!.Parent is Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax { Name: var name } && name.ToString() == "SwaggerResponse");
		Assert.Empty(model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
		return (string)model.GetConstantValue(argument.Expression).Value!;
	}
}
