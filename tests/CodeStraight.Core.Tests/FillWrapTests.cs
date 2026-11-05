using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class FillWrapTests
{
	private const string EditorConfig = "[*.cs]\nindent_style = tab\ntab_width = 4\nmax_line_length = 120";

	/// <summary>
	/// Tolerance 12: the first line of example 2 has 131 columns (fits up to 132); in example 1 the first parameter on the
	/// opening line would give 134 (does not fit), so it breaks after the parenthesis.
	/// </summary>
	private const string Fill = "wrap_long_lines = true\nwrap_list_style = fill\nwrap_tolerance = 12";

	private const string Types = """
		using System;
		using System.Collections.Generic;
		using System.Threading;
		using System.Threading.Tasks;

		public sealed class FromQueryAttribute : Attribute { }
		public sealed class BindRequiredAttribute : Attribute { }
		public sealed class ActionResult<T> { }
		public sealed class Result<T> { }
		public sealed class HistoricoUsoReservasResponse { }

		""";

	[Fact]
	public void Example1_FirstParameterDoesNotFit_BreaksAfterTheParenthesisAndFillsTheLines()
	{
		string source = Types + "public class C\n{\n"
			+ "\tpublic async Task<ActionResult<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAsync([FromQuery, BindRequired] string annos, [FromQuery] string idProductos, [FromQuery, BindRequired] int proyecto, CancellationToken cancellationToken)\n"
			+ "\t{\n\t\tawait Task.Delay(1);\n\t\treturn new();\n\t}\n}\n";

		string result = Apply(source, Fill);

		Assert.Contains(
			"\tpublic async Task<ActionResult<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAsync(\n"
			+ "\t\t[FromQuery, BindRequired] string annos, [FromQuery] string idProductos, [FromQuery, BindRequired] int proyecto,\n"
			+ "\t\tCancellationToken cancellationToken)\n",
			result);
	}

	[Fact]
	public void Example2_FirstParameterFitsWithTheTolerance_StaysOnTheOpeningLine()
	{
		string source = Types + "public class C\n{\n"
			+ "\tpublic async Task<Result<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAnnosAsync(IReadOnlyCollection<int> annosParsed, IReadOnlyCollection<int> elementosParsed, int proyecto, CancellationToken cancellationToken)\n"
			+ "\t{\n\t\tawait Task.Delay(1);\n\t\treturn new();\n\t}\n}\n";

		string result = Apply(source, Fill);

		Assert.Contains(
			"\tpublic async Task<Result<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAnnosAsync(IReadOnlyCollection<int> annosParsed,\n"
			+ "\t\tIReadOnlyCollection<int> elementosParsed, int proyecto, CancellationToken cancellationToken)\n",
			result);
	}

	[Fact]
	public void Example2_IsKeptAsItIs_WhenItIsAlreadyWithinTheTolerance()
	{
		string source = Types + "public class C\n{\n"
			+ "\tpublic async Task<Result<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAnnosAsync(IReadOnlyCollection<int> annosParsed,\n"
			+ "\t\tIReadOnlyCollection<int> elementosParsed, int proyecto, CancellationToken cancellationToken)\n"
			+ "\t{\n\t\tawait Task.Delay(1);\n\t\treturn new();\n\t}\n}\n";

		Assert.Equal(source, Apply(source, Fill));
	}

	[Fact]
	public void OnePerLine_RemainsTheDefault()
	{
		string source = Types + "public class C\n{\n"
			+ "\tpublic async Task<Result<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAnnosAsync(IReadOnlyCollection<int> annosParsed, IReadOnlyCollection<int> elementosParsed, int proyecto, CancellationToken cancellationToken)\n"
			+ "\t{\n\t\tawait Task.Delay(1);\n\t\treturn new();\n\t}\n}\n";

		string result = Apply(source, "wrap_long_lines = true");

		Assert.Contains(
			"GetHistoricoUsoReservasAnnosAsync(\n\t\tIReadOnlyCollection<int> annosParsed,\n\t\tIReadOnlyCollection<int> elementosParsed,\n"
			+ "\t\tint proyecto,\n\t\tCancellationToken cancellationToken)\n",
			result);
	}

	[Fact]
	public void Fill_IsIdempotentAndArgumentsAreFilledToo()
	{
		string source = Types + "public class C\n{\n"
			+ "\tint Sumar(int primero, int segundo, int tercero, int cuarto, int quinto, int sexto, int septimo, int octavo) => primero;\n"
			+ "\tint Usar() => Sumar(1000000001, 1000000002, 1000000003, 1000000004, 1000000005, 1000000006, 1000000007, 1000000008, 1000000009);\n"
			+ "}\n";

		string once = Apply(source, Fill.Replace("wrap_tolerance = 12", "wrap_tolerance = 0"));
		string twice = Apply(once, Fill.Replace("wrap_tolerance = 12", "wrap_tolerance = 0"));

		Assert.Equal(once, twice);
		Assert.All(once.Split('\n'), line => Assert.True(line.Replace("\t", "    ").Length <= 120, line));
	}

	private static string Apply(string source, string settings) =>
		TestWorkspace.ApplyAsync(source, settings, EditorConfig).GetAwaiter().GetResult();
}
