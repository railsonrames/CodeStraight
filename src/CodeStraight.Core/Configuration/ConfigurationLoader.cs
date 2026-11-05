using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace CodeStraight.Core.Configuration;

/// <summary>The configuration that applies to one file: feature switches plus the <c>.editorconfig</c> values.</summary>
public sealed class FileConfiguration
{
	public FileConfiguration(string rootDirectory, CodeStraightSettings settings, StyleOptions style)
	{
		RootDirectory = rootDirectory;
		Settings = settings;
		Style = style;
	}

	/// <summary>Folder that contains the <c>.codestraight</c> file (normally the solution folder).</summary>
	public string RootDirectory { get; }

	public CodeStraightSettings Settings { get; }

	public StyleOptions Style { get; }

	/// <summary>Combines the <c>.editorconfig</c> values with the style values that only live in <c>.codestraight</c>.</summary>
	public static FileConfiguration Create(
		string rootDirectory,
		CodeStraightSettings settings,
		IReadOnlyDictionary<string, string> editorConfigProperties) =>
		new(
			rootDirectory,
			settings,
			StyleOptions.FromProperties(editorConfigProperties) with
			{
				TrailingComma = settings.TrailingCommaStyle,
				BinaryOperatorNewLine = settings.BinaryOperatorNewLine,
				WrapAttributes = settings.WrapAttributes,
				SplitLongStrings = settings.SplitLongStrings,
				WrapListStyle = settings.WrapListStyle,
				WrapTolerance = settings.WrapTolerance,
				ScreamingCaseConstants = settings.ScreamingCaseConstants,
			});
}

/// <summary>
/// Finds the <c>.codestraight</c> file walking up from a source file and reads it together with the
/// <c>.editorconfig</c> of the same folder. Results are cached and refreshed when either file changes.
/// </summary>
public sealed class ConfigurationLoader
{
	private readonly ConcurrentDictionary<string, CachedRoot> _roots = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>Returns null when no <c>.codestraight</c> file exists above <paramref name="filePath"/>.</summary>
	public FileConfiguration? Load(string filePath)
	{
		string? root = FindRoot(Path.GetDirectoryName(Path.GetFullPath(filePath)));
		if (root is null)
		{
			return null;
		}

		CachedRoot cached = _roots.AddOrUpdate(
			root,
			LoadRoot,
			(directory, existing) => existing.IsCurrent() ? existing : LoadRoot(directory));

		return FileConfiguration.Create(root, cached.Settings, cached.EditorConfig.GetProperties(filePath));
	}

	private static string? FindRoot(string? directory)
	{
		while (!string.IsNullOrEmpty(directory))
		{
			if (File.Exists(Path.Combine(directory, CodeStraightSettings.FileName)))
			{
				return directory;
			}

			directory = Path.GetDirectoryName(directory);
		}

		return null;
	}

	private static CachedRoot LoadRoot(string directory)
	{
		string settingsPath = Path.Combine(directory, CodeStraightSettings.FileName);
		string editorConfigPath = Path.Combine(directory, ".editorconfig");

		CodeStraightSettings settings = CodeStraightSettings.Parse(ReadAll(settingsPath));
		EditorConfigFile editorConfig = File.Exists(editorConfigPath)
			? EditorConfigFile.Parse(directory, ReadAll(editorConfigPath))
			: EditorConfigFile.Empty(directory);

		return new CachedRoot(settingsPath, editorConfigPath, settings, editorConfig);
	}

	private static string ReadAll(string path)
	{
		// The editor may be writing the file at the same time; share access and treat errors as an empty file.
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}
		catch (IOException)
		{
			return string.Empty;
		}
		catch (UnauthorizedAccessException)
		{
			return string.Empty;
		}
	}

	private sealed class CachedRoot
	{
		private readonly string _settingsPath;
		private readonly string _editorConfigPath;
		private readonly DateTime _settingsStamp;
		private readonly DateTime _editorConfigStamp;

		public CachedRoot(string settingsPath, string editorConfigPath, CodeStraightSettings settings, EditorConfigFile editorConfig)
		{
			_settingsPath = settingsPath;
			_editorConfigPath = editorConfigPath;
			_settingsStamp = Stamp(settingsPath);
			_editorConfigStamp = Stamp(editorConfigPath);
			Settings = settings;
			EditorConfig = editorConfig;
		}

		public CodeStraightSettings Settings { get; }

		public EditorConfigFile EditorConfig { get; }

		public bool IsCurrent() => Stamp(_settingsPath) == _settingsStamp && Stamp(_editorConfigPath) == _editorConfigStamp;

		private static DateTime Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
	}
}
