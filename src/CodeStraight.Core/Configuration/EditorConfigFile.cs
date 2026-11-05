using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeStraight.Core.Configuration;

/// <summary>
/// The <c>.editorconfig</c> found next to the <c>.codestraight</c> file. Only this root file is read:
/// CodeStraight works with the values defined for the whole solution.
/// </summary>
public sealed class EditorConfigFile
{
	private readonly string _directory;
	private readonly IReadOnlyList<(Regex Pattern, IniSection Section)> _sections;

	private EditorConfigFile(string directory, IReadOnlyList<(Regex, IniSection)> sections)
	{
		_directory = directory;
		_sections = sections;
	}

	public static EditorConfigFile Empty(string directory) => new(directory, Array.Empty<(Regex, IniSection)>());

	public static EditorConfigFile Parse(string directory, string content)
	{
		var sections = new List<(Regex, IniSection)>();
		foreach (IniSection section in IniParser.Parse(content))
		{
			if (section.Name.Length > 0)
			{
				sections.Add((GlobToRegex(section.Name), section));
			}
		}

		return new EditorConfigFile(directory, sections);
	}

	/// <summary>Properties that apply to <paramref name="filePath"/>; later sections override earlier ones.</summary>
	public IReadOnlyDictionary<string, string> GetProperties(string filePath)
	{
		string relative = MakeRelative(filePath);
		var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach ((Regex pattern, IniSection section) in _sections)
		{
			if (!pattern.IsMatch(relative))
			{
				continue;
			}

			foreach (KeyValuePair<string, string> property in section.Properties)
			{
				properties[property.Key] = property.Value;
			}
		}

		return properties;
	}

	private string MakeRelative(string filePath)
	{
		string full = Path.GetFullPath(filePath).Replace('\\', '/');
		string root = Path.GetFullPath(_directory).Replace('\\', '/').TrimEnd('/') + "/";
		return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : Path.GetFileName(full);
	}

	/// <summary>
	/// Translates an editorconfig glob into a regex. Patterns without a slash match the file name in any folder.
	/// Supports <c>*</c>, <c>**</c>, <c>?</c>, <c>{a,b}</c> and <c>[...]</c>.
	/// </summary>
	internal static Regex GlobToRegex(string glob)
	{
		bool matchAnywhere = glob.IndexOf('/') < 0;
		string pattern = glob.TrimStart('/');
		var regex = new StringBuilder("^");
		if (matchAnywhere)
		{
			regex.Append("(?:.*/)?");
		}

		int braceDepth = 0;
		for (int i = 0; i < pattern.Length; i++)
		{
			char c = pattern[i];
			switch (c)
			{
				case '*':
					if (i + 1 < pattern.Length && pattern[i + 1] == '*')
					{
						regex.Append(".*");
						i++;
					}
					else
					{
						regex.Append("[^/]*");
					}

					break;
				case '?':
					regex.Append("[^/]");
					break;
				case '{':
					braceDepth++;
					regex.Append("(?:");
					break;
				case '}' when braceDepth > 0:
					braceDepth--;
					regex.Append(')');
					break;
				case ',' when braceDepth > 0:
					regex.Append('|');
					break;
				case '[':
					int close = pattern.IndexOf(']', i + 1);
					if (close > i)
					{
						string set = pattern.Substring(i + 1, close - i - 1);
						regex.Append('[').Append(set.StartsWith("!") ? "^" + set.Substring(1) : set).Append(']');
						i = close;
					}
					else
					{
						regex.Append("\\[");
					}

					break;
				default:
					regex.Append(Regex.Escape(c.ToString()));
					break;
			}
		}

		regex.Append('$');
		return new Regex(regex.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}
}
