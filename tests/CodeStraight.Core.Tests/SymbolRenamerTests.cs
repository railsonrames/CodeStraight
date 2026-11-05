using CodeStraight.Core.Configuration;
using Xunit;

namespace CodeStraight.Core.Tests;

public sealed class SymbolRenamerTests
{
	private static readonly string Rules = Samples.NamingRules + """

		dotnet_naming_rule.async_methods_end_in_async.symbols = async_methods
		dotnet_naming_rule.async_methods_end_in_async.style = end_in_async
		dotnet_naming_rule.async_methods_end_in_async.severity = warning
		dotnet_naming_symbols.async_methods.applicable_kinds = method
		dotnet_naming_symbols.async_methods.required_modifiers = async
		dotnet_naming_style.end_in_async.capitalization = pascal_case
		dotnet_naming_style.end_in_async.required_suffix = Async

		dotnet_naming_rule.interfaces_begin_with_i.symbols = interfaces
		dotnet_naming_rule.interfaces_begin_with_i.style = begin_with_i
		dotnet_naming_rule.interfaces_begin_with_i.severity = warning
		dotnet_naming_symbols.interfaces.applicable_kinds = interface
		dotnet_naming_style.begin_with_i.capitalization = pascal_case
		dotnet_naming_style.begin_with_i.required_prefix = I
		""";

	private const string Service = """
		using System.Threading.Tasks;

		public interface Repositorio
		{
		    Task<int> Contar();
		}

		public class Servicio : Repositorio
		{
		    public async Task<int> Contar()
		    {
		        await Task.Delay(1);
		        return 1;
		    }
		}
		""";

	private const string Caller = """
		using System.Threading.Tasks;

		public class Consumidor
		{
		    public Task<int> Usar(Repositorio repositorio) => repositorio.Contar();
		}
		""";

	[Fact]
	public async Task ScopeAll_RenamesAsyncMethodsAndInterfaces_InEveryFile_LikeCtrlRR()
	{
		string[] result = await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = all", Rules, Service, Caller);

		Assert.Contains("public interface IRepositorio", result[0]);
		Assert.Contains("Task<int> ContarAsync();", result[0]);
		Assert.Contains("public class Servicio : IRepositorio", result[0]);
		Assert.Contains("public async Task<int> ContarAsync()", result[0]);
		Assert.Contains("Usar(IRepositorio repositorio) => repositorio.ContarAsync();", result[1]);
	}

	[Fact]
	public async Task ScopePrivate_LeavesPublicSymbolsAlone()
	{
		string[] result = await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true", Rules, Service, Caller);

		Assert.Contains("public interface Repositorio", result[0]);
		Assert.Contains("public async Task<int> Contar()", result[0]);
	}

	[Fact]
	public async Task ScopeInternal_RenamesInternalButNotPublic()
	{
		const string source = """
			using System.Threading.Tasks;

			internal interface Interno { }
			public interface Publico { }
			internal class C
			{
			    internal const string valor_interno = "a";
			    public const string valor_publico = "b";
			    internal async Task Procesar() => await Task.Delay(1);
			}
			""";

		string[] result = await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = internal", Rules, source);

		Assert.Contains("internal interface IInterno", result[0]);
		Assert.Contains("public interface Publico", result[0]);
		Assert.Contains("internal const string ValorInterno", result[0]);
		Assert.Contains("public const string valor_publico", result[0]);
		Assert.Contains("internal async Task ProcesarAsync()", result[0]);
	}

	[Fact]
	public async Task ControllerActions_AreSkippedUnlessEnabled()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Microsoft.AspNetCore.Mvc { public class ControllerBase { } }

			public class PedidosController : Microsoft.AspNetCore.Mvc.ControllerBase
			{
			    public async Task<int> Obtener() { await Task.Delay(1); return 1; }
			    private async Task Auxiliar() => await Task.Delay(1);
			}
			""";

		string skipped = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = all", Rules, source))[0];
		string renamed = (await TestWorkspace.ApplyToSolutionAsync(
			"rename_symbols = true\nrename_scope = all\nrename_controller_actions = true", Rules, source))[0];

		Assert.Contains("public async Task<int> Obtener()", skipped);
		Assert.Contains("private async Task AuxiliarAsync()", skipped);
		Assert.Contains("public async Task<int> ObtenerAsync()", renamed);
	}

	[Theory]
	[InlineData("Item", "IItem")]
	[InlineData("IRepositorio", "IRepositorio")]
	[InlineData("Repositorio", "IRepositorio")]
	public void InterfacePrefix_IsNotConfusedWithAnInitialI(string name, string expected)
	{
		var style = new NamingStyle { Prefix = "I", Capitalization = Capitalization.PascalCase };

		Assert.Equal(expected, style.Apply(name));
	}

	[Theory]
	[InlineData("Obtener", "ObtenerAsync")]
	[InlineData("ObtenerAsync", "ObtenerAsync")]
	public void AsyncSuffix_IsAddedOnce(string name, string expected)
	{
		var style = new NamingStyle { Suffix = "Async", Capitalization = Capitalization.PascalCase };

		Assert.Equal(expected, style.Apply(name));
	}

	/// <summary>The naming section of a team <c>.editorconfig</c>: every kind has a rule, local functions are methods.</summary>
	private const string TeamRules = """
		root = true
		[*.cs]
		dotnet_naming_rule.async_methods_end_in_async.symbols = async_methods
		dotnet_naming_rule.async_methods_end_in_async.style = end_in_async
		dotnet_naming_rule.async_methods_end_in_async.severity = warning
		dotnet_naming_symbols.async_methods.applicable_kinds = method
		dotnet_naming_symbols.async_methods.required_modifiers = async
		dotnet_naming_style.end_in_async.capitalization = pascal_case
		dotnet_naming_style.end_in_async.required_suffix = Async

		dotnet_naming_rule.constants_pascal.symbols = constant_fields
		dotnet_naming_rule.constants_pascal.style = pascal
		dotnet_naming_rule.constants_pascal.severity = warning
		dotnet_naming_symbols.constant_fields.applicable_kinds = field
		dotnet_naming_symbols.constant_fields.required_modifiers = const
		dotnet_naming_style.pascal.capitalization = pascal_case

		dotnet_naming_rule.private_fields_underscore.symbols = private_fields
		dotnet_naming_rule.private_fields_underscore.style = underscore_camel
		dotnet_naming_rule.private_fields_underscore.severity = warning
		dotnet_naming_symbols.private_fields.applicable_kinds = field
		dotnet_naming_symbols.private_fields.applicable_accessibilities = private
		dotnet_naming_style.underscore_camel.capitalization = camel_case
		dotnet_naming_style.underscore_camel.required_prefix = _

		dotnet_naming_rule.members_pascal.symbols = members
		dotnet_naming_rule.members_pascal.style = pascal
		dotnet_naming_rule.members_pascal.severity = warning
		dotnet_naming_symbols.members.applicable_kinds = method, property, event, local_function

		dotnet_naming_rule.locals_camel.symbols = locals
		dotnet_naming_rule.locals_camel.style = camel
		dotnet_naming_rule.locals_camel.severity = warning
		dotnet_naming_symbols.locals.applicable_kinds = parameter, local
		dotnet_naming_style.camel.capitalization = camel_case

		dotnet_naming_rule.type_parameters_t.symbols = type_parameters
		dotnet_naming_rule.type_parameters_t.style = begin_with_t
		dotnet_naming_rule.type_parameters_t.severity = warning
		dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter
		dotnet_naming_style.begin_with_t.capitalization = pascal_case
		dotnet_naming_style.begin_with_t.required_prefix = T
		""";

	[Fact]
	public async Task LocalsParametersAndTypeParameters_AreRenamedWithEveryReference()
	{
		const string source = """
			using System;
			using System.Collections.Generic;

			public class Repositorio<Entity>
			{
			    private readonly List<Entity> _items = new();

			    /// <param name="IdRuta">Ruta.</param>
			    public int Buscar(int IdRuta, Func<int, int> Transformar)
			    {
			        int Total = _items.Count;
			        foreach (Entity Item in _items) { Total += Item is null ? 0 : 1; }
			        if (IdRuta is int Valor && int.TryParse("1", out int Leido)) { Total += Valor + Leido; }
			        try { } catch (Exception Error) { Total += Error.HResult; }
			        using var Lector = new System.IO.StringReader("");
			        return sumar(Transformar(Total));

			        int sumar(int Cantidad) => Cantidad + IdRuta;
			    }
			}
			""";
		const string caller = """
			public class Consumidor
			{
			    public int Usar() => new Repositorio<string>().Buscar(IdRuta: 3, Transformar: Valor => Valor);
			}
			""";

		string[] result = await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = all", TeamRules, source, caller);

		Assert.Contains("public class Repositorio<TEntity>", result[0]);
		Assert.Contains("private readonly List<TEntity> _items", result[0]);
		Assert.Contains("<param name=\"idRuta\">", result[0]);
		Assert.Contains("public int Buscar(int idRuta, Func<int, int> transformar)", result[0]);
		Assert.Contains("int total = _items.Count;", result[0]);
		Assert.Contains("foreach (TEntity item in _items) { total += item is null ? 0 : 1; }", result[0]);
		Assert.Contains("if (idRuta is int valor && int.TryParse(\"1\", out int leido)) { total += valor + leido; }", result[0]);
		Assert.Contains("catch (Exception error) { total += error.HResult; }", result[0]);
		Assert.Contains("using var lector =", result[0]);
		Assert.Contains("return Sumar(transformar(total));", result[0]);
		Assert.Contains("int Sumar(int cantidad) => cantidad + idRuta;", result[0]);
		// The lambda parameter is declared in the other file: it is renamed when that file is saved.
		Assert.Contains("Buscar(idRuta: 3, transformar: Valor => Valor)", result[1]);
	}

	[Fact]
	public async Task ScopePrivate_RenamesLocalsButNotParametersOfPublicMembers()
	{
		const string source = """
			public class C
			{
			    public int Publico(int IdRuta) { int Total = IdRuta; return Total; }
			    private int Privado(int IdRuta) => IdRuta;
			}
			""";

		string result = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true", TeamRules, source))[0];

		Assert.Contains("public int Publico(int IdRuta) { int total = IdRuta; return total; }", result);
		Assert.Contains("private int Privado(int idRuta) => idRuta;", result);
	}

	[Fact]
	public async Task ParametersTiedToSomethingElse_KeepTheirNames()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Microsoft.AspNetCore.Mvc { public class ControllerBase { } }

			public record Ruta(int IdRuta);

			public class Punto
			{
			    public override bool Equals(object Obj) => false;
			    public override int GetHashCode() => 0;
			}

			public class RutasController : Microsoft.AspNetCore.Mvc.ControllerBase
			{
			    public int Obtener(int Id_Ruta) => Id_Ruta;
			}
			""";

		string result = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = all", TeamRules, source))[0];

		Assert.Contains("public record Ruta(int IdRuta);", result);
		Assert.Contains("public override bool Equals(object Obj)", result);
		Assert.Contains("public int Obtener(int Id_Ruta) => Id_Ruta;", result);
	}

	[Fact]
	public async Task NewNameAlreadyUsedInTheMember_IsSkipped()
	{
		const string source = """
			public class C
			{
			    private int Fijar(string Valor) { string valor = "x"; return Valor.Length + valor.Length; }
			}
			""";

		string result = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true", TeamRules, source))[0];

		Assert.Contains("private int Fijar(string Valor)", result);
	}

	[Fact]
	public async Task PrivateStaticReadonly_FollowsThePrivateFieldRule_WhenThereIsNoRuleOfItsOwn()
	{
		const string source = """
			public class C
			{
			    private static readonly object Logger = new();
			    private const int MAX_INTENTOS = 3;
			    public object Usar() => Logger ?? MAX_INTENTOS;
			}
			""";

		string result = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true", TeamRules, source))[0];

		Assert.Contains("private static readonly object _logger = new();", result);
		Assert.Contains("private const int MaxIntentos = 3;", result);
		Assert.Contains("_logger ?? MaxIntentos", result);
	}

	[Fact]
	public async Task AsyncImplementationOfARenamedInterfaceMethod_GetsTheSuffixInTheSameSave()
	{
		const string source = """
			using System.Threading.Tasks;

			public interface IRepositorio { Task<int> obtener(int IdRuta); }

			public class Repositorio : IRepositorio
			{
			    public async Task<int> obtener(int IdRuta) { await Task.Delay(1); return IdRuta; }
			}
			""";

		string result = (await TestWorkspace.ApplyToSolutionAsync("rename_symbols = true\nrename_scope = all", TeamRules, source))[0];

		Assert.Contains("public interface IRepositorio { Task<int> ObtenerAsync(int idRuta); }", result);
		Assert.Contains("public async Task<int> ObtenerAsync(int idRuta) { await Task.Delay(1); return idRuta; }", result);
	}

	[Theory]
	[InlineData("T", "T")]
	[InlineData("TKey", "TKey")]
	[InlineData("T1", "T1")]
	[InlineData("Entity", "TEntity")]
	[InlineData("entidad", "TEntidad")]
	public void TypeParameterPrefix_KeepsALoneT(string name, string expected)
	{
		var style = new NamingStyle { Prefix = "T", Capitalization = Capitalization.PascalCase };

		Assert.Equal(expected, style.Apply(name));
	}
}
