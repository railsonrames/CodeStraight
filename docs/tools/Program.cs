using CodeStraight.Core;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

// Generates the "before" and "after" of each feature with the real CodeStraight engine, for the documentation images.
string output = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "out");
Directory.CreateDirectory(output);

const string Naming = """
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
	dotnet_naming_rule.constants_pascal.symbols = constant_fields
	dotnet_naming_rule.constants_pascal.style = pascal
	dotnet_naming_rule.constants_pascal.severity = suggestion
	dotnet_naming_symbols.constant_fields.applicable_kinds = field
	dotnet_naming_symbols.constant_fields.required_modifiers = const
	dotnet_naming_style.pascal.capitalization = pascal_case
	dotnet_naming_rule.private_fields_underscore.symbols = private_fields
	dotnet_naming_rule.private_fields_underscore.style = underscore_camel
	dotnet_naming_rule.private_fields_underscore.severity = suggestion
	dotnet_naming_symbols.private_fields.applicable_kinds = field
	dotnet_naming_symbols.private_fields.applicable_accessibilities = private
	dotnet_naming_style.underscore_camel.capitalization = camel_case
	dotnet_naming_style.underscore_camel.required_prefix = _
	""";

const string Base = """
	[*.cs]
	indent_style = tab
	tab_width = 4
	max_line_length = 120
	csharp_style_namespace_declarations = file_scoped:warning
	dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning
	dotnet_sort_system_directives_first = true
	dotnet_style_allow_multiple_blank_lines_experimental = false:warning
	csharp_style_allow_blank_lines_between_consecutive_braces_experimental = false:warning
	""";

string editorConfig = Base + "\n" + Naming;

await Sample("rename", """
	using System.Threading.Tasks;

	public interface Repositorio
	{
		Task<int> Contar();
	}

	public class Servicio : Repositorio
	{
		private const string prefijo_texto = "Total: ";
		private int contador;

		public async Task<int> Contar()
		{
			await Task.Delay(1);
			contador++;
			return contador;
		}
	}
	""", "rename_symbols = true\nrename_scope = all");

await Sample("namespace-usings", """
	using System.Text;
	using System.Linq;
	using System;
	using System.Collections.Generic;

	namespace Demo.Servicios
	{
	    class Catalogo
	    {
	        List<string> nombres = new List<string>();


	        string Primero() => nombres.First();
	    }
	}
	""", "namespace_declarations = true\nremove_unused_usings = true\nsort_usings = true\naccessibility_modifiers = true\nremove_extra_blank_lines = true\nformat_document = true");

const string Signature = """
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public sealed class Result<T> { }
	public sealed class HistoricoUsoReservasResponse { }

	public class HistoricoService
	{
		public async Task<Result<HistoricoUsoReservasResponse>> GetHistoricoUsoReservasAnnosAsync(IReadOnlyCollection<int> annosParsed, IReadOnlyCollection<int> elementosParsed, int proyecto, CancellationToken cancellationToken)
		{
			await Task.Delay(1);
			return new();
		}
	}
	""";

await Sample("wrap-one-per-line", Signature, "wrap_long_lines = true\nformat_document = true");
await Sample("wrap-fill", Signature, "wrap_long_lines = true\nformat_document = true\nwrap_list_style = fill");

await Sample("long-strings", """
	using System;

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public sealed class SwaggerResponseAttribute(int code, string description) : Attribute { }

	public class PedidosController
	{
		[SwaggerResponse(400, "Parámetros no válidos. Devuelve RespuestaComun con state = error y uno de estos mensajes: El parámetro fecha no es válido o El parámetro idProductos no es válido.")]
		public void Obtener() { }
	}
	""", "wrap_long_lines = true\nformat_document = true\nsplit_long_strings = true");

await Sample("operators", """
	public class Mensajes
	{
		public string Texto =
	 "Parámetros no válidos. Devuelve un mensaje con el detalle del error "
	  +

	 "y el identificador de la petición. "
	  +
	 "Revisa los valores enviados.";
	}
	""", "wrap_long_lines = true\nformat_document = true\nbinary_operator_new_line = before");

await Sample("trailing-comma", """
	using System.Threading.Tasks;

	public class Persona { public string Nombre = ""; public int Edad; }

	public class Servicio
	{
		private Task<int> Contar(int proyecto, string filtro) => Task.FromResult(proyecto);

		public async Task<Persona> Crear(string nombre, int edad)
		{
			int total = await Contar(
				edad,
				nombre,
			);
			return new Persona
			{
				Nombre = nombre,
				Edad = total,
			};
		}
	}
	""", "trailing_comma = true\ntrailing_comma_style = omit");

await Sample("continuation", """
	using System.Collections.Generic;
	using System.Linq;

	public class Servicio
	{
		public List<int> Calcular(List<int> numeros)
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

		private int Doble(int n, int factor) => n * factor;
	}
	""", "format_document = true");

await Sample("explicit-types", """
	using System.Collections.Generic;

	public class Servicio
	{
		public void Procesar()
		{
			var nombres = new List<string>();
			var total = nombres.Count;
			foreach (var nombre in nombres)
			{
			}

			int.TryParse("12", out var numero);
			var anonimo = new { Id = 1 };
		}
	}
	""", "explicit_types = true");

// The dynamic of a save, for the animation of the README: the same file with one more action each time, in the order
// of the engine, so every frame is real output. The last run saves the result again to show that nothing changes.
const string SaveDemo = """
	using System.Text;
	using System.Threading.Tasks;
	using System.Linq;
	using System.Collections.Generic;

	namespace Tienda.Pedidos
	{
	    public interface Repositorio
	    {
	        Task<int> Contar(int proyecto);
	    }

	    class Servicio : Repositorio
	    {
	        private const string mensaje_base = "Resumen: ";
	        private int contador;
	        private List<string> nombres = new List<string>();



	        public async Task<int> Contar(int proyecto)
	        {
	            await Task.Delay(1);
	            return ++contador + proyecto;
	        }

	        public async Task<string> ObtenerResumen(int proyectoSeleccionado, string nombreDelCliente, string codigoDeLaRegion, bool incluirHistorico)
	        {
	            int total = await Contar(
	                proyectoSeleccionado,
	            );
	            List<string> lista = nombres
	.Where(n => n.StartsWith(codigoDeLaRegion))
	.Select(n => n.ToUpper())
	.ToList();
	            return mensaje_base + nombreDelCliente + total + lista.Count + incluirHistorico;
	        }
	    }
	}
	""";

string[] saveDemoSteps =
[
	"trailing_comma = true",
	"rename_symbols = true\nrename_scope = all",
	"namespace_declarations = true",
	"remove_unused_usings = true\nsort_usings = true",
	"accessibility_modifiers = true",
	"remove_extra_blank_lines = true",
	"format_document = true",
	"wrap_long_lines = true",
];

string saveDemoSettings = "";
for (int step = 0; step < saveDemoSteps.Length; step++)
{
	saveDemoSettings += saveDemoSteps[step] + "\n";
	await Sample($"save-demo-{step}", SaveDemo, saveDemoSettings);
}

string saveDemoResult = File.ReadAllText(Path.Combine(output, $"save-demo-{saveDemoSteps.Length - 1}.after.cs"));
await Sample("save-demo-again", saveDemoResult, saveDemoSettings);

// The demo of docs/examples, with the configuration next to it, as in a project with ImplicitUsings.
string examples = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "examples");
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
await Sample(
	"examples-demo",
	File.ReadAllText(Path.Combine(examples, "demo", "DemoCodeStraight.antes.cs"), System.Text.Encoding.GetEncoding(1252)),
	File.ReadAllText(Path.Combine(examples, ".codestraight")),
	File.ReadAllText(Path.Combine(examples, ".editorconfig")),
	"""
	global using System;
	global using System.Collections.Generic;
	global using System.IO;
	global using System.Linq;
	global using System.Threading;
	global using System.Threading.Tasks;
	""");
File.Copy(Path.Combine(output, "examples-demo.after.cs"), Path.Combine(examples, "demo", "DemoCodeStraight.despues.cs"), overwrite: true);

Console.WriteLine("ok");

async Task Sample(string name, string source, string codestraight, string? editorConfigOverride = null, string? globalUsings = null)
{
	source = source.Replace("\r\n", "\n");
	var workspace = new AdhocWorkspace();
	ProjectId projectId = ProjectId.CreateNewId();
	MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
		.Split(Path.PathSeparator)
		.Where(path => Path.GetFileName(path) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.Linq.dll"
			or "System.Collections.dll" or "netstandard.dll" or "System.Threading.dll" or "System.Threading.Tasks.dll")
		.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
		.ToArray();
	Solution solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
		projectId, VersionStamp.Default, "Demo", "Demo", LanguageNames.CSharp,
		compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
		parseOptions: new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose),
		metadataReferences: references));
	DocumentId id = DocumentId.CreateNewId(projectId);
	solution = solution.AddDocument(id, name + ".cs", SourceText.From(source), filePath: $"C:/demo/{name}.cs");
	if (globalUsings is not null)
	{
		solution = solution.AddDocument(DocumentId.CreateNewId(projectId), "GlobalUsings.cs", SourceText.From(globalUsings), filePath: "C:/demo/GlobalUsings.cs");
	}

	FileConfiguration configuration = FileConfiguration.Create(
		"C:/demo",
		CodeStraightSettings.Parse(codestraight),
		EditorConfigFile.Parse("C:/demo", editorConfigOverride ?? editorConfig).GetProperties($"C:/demo/{name}.cs"));
	var report = new List<string>();
	Solution result = await CodeStraightEngine.ApplyAsync(solution.GetDocument(id)!, configuration, CancellationToken.None, report);
	string after = (await result.GetDocument(id)!.GetTextAsync()).ToString();

	File.WriteAllText(Path.Combine(output, name + ".before.cs"), source);
	File.WriteAllText(Path.Combine(output, name + ".after.cs"), after);
	File.WriteAllLines(Path.Combine(output, name + ".log"), report);
	Console.WriteLine($"{name}: {string.Join(" | ", report.Where(line => !line.EndsWith("no changes")))}");
}
