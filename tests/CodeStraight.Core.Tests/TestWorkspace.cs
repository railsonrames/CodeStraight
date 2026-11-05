using CodeStraight.Core;
using CodeStraight.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CodeStraight.Core.Tests;

/// <summary>Builds an in-memory solution with one C# document and runs CodeStraight on it.</summary>
internal static class TestWorkspace
{
	private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
		.Split(Path.PathSeparator)
		.Where(path => Path.GetFileName(path) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.Linq.dll"
			or "System.Collections.dll" or "netstandard.dll" or "System.Console.dll")
		.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
		.ToArray();

	public static Document CreateDocument(string source, params string[] otherSources)
	{
		var workspace = new AdhocWorkspace();
		ProjectId projectId = ProjectId.CreateNewId();
		Solution solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(
				projectId,
				VersionStamp.Default,
				"Test",
				"Test",
				LanguageNames.CSharp,
				compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
				parseOptions: new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose),
				metadataReferences: References));

		DocumentId documentId = DocumentId.CreateNewId(projectId);
		solution = solution.AddDocument(documentId, "Test.cs", SourceText.From(source), filePath: "C:/repo/Test.cs");
		for (int i = 0; i < otherSources.Length; i++)
		{
			solution = solution.AddDocument(DocumentId.CreateNewId(projectId), $"Other{i}.cs", SourceText.From(otherSources[i]), filePath: $"C:/repo/Other{i}.cs");
		}

		return solution.GetDocument(documentId)!;
	}

	public static async Task<string> ApplyAsync(string source, string codestraight, string editorconfig)
	{
		Document document = CreateDocument(Normalize(source));
		Solution result = await CodeStraightEngine.ApplyAsync(document, Configuration(codestraight, editorconfig), CancellationToken.None);
		return (await result.GetDocument(document.Id)!.GetTextAsync()).ToString();
	}

	/// <summary>Like <see cref="ApplyAsync"/> but keeps the line breaks of the source exactly as given.</summary>
	public static async Task<string> ApplyRawAsync(string source, string codestraight, string editorconfig)
	{
		Document document = CreateDocument(source);
		Solution result = await CodeStraightEngine.ApplyAsync(document, Configuration(codestraight, editorconfig), CancellationToken.None);
		return (await result.GetDocument(document.Id)!.GetTextAsync()).ToString();
	}

	/// <summary>Runs CodeStraight on the first source and returns the text of every source afterwards.</summary>
	public static async Task<string[]> ApplyToSolutionAsync(string codestraight, string editorconfig, params string[] sources)
	{
		Document document = CreateDocument(Normalize(sources[0]), sources.Skip(1).Select(Normalize).ToArray());
		Solution result = await CodeStraightEngine.ApplyAsync(document, Configuration(codestraight, editorconfig), CancellationToken.None);
		var texts = new List<string> { (await result.GetDocument(document.Id)!.GetTextAsync()).ToString() };
		foreach (Document other in result.GetProject(document.Project.Id)!.Documents.Where(other => other.Id != document.Id).OrderBy(other => other.Name))
		{
			texts.Add((await other.GetTextAsync()).ToString());
		}

		return texts.ToArray();
	}

	public static FileConfiguration Configuration(string codestraight, string editorconfig) =>
		FileConfiguration.Create(
			"C:/repo",
			CodeStraightSettings.Parse(codestraight),
			EditorConfigFile.Parse("C:/repo", editorconfig).GetProperties("C:/repo/Test.cs"));

	/// <summary>Raw string literals in the tests use LF; the engine keeps whatever line break the file has.</summary>
	public static string Normalize(string source) => source.Replace("\r\n", "\n");
}
