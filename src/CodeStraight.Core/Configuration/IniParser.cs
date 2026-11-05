using System;
using System.Collections.Generic;

namespace CodeStraight.Core.Configuration;

/// <summary>
/// Minimal parser for the INI-like format shared by <c>.editorconfig</c> and <c>.codestraight</c>:
/// <c>key = value</c> pairs, optional <c>[section]</c> headers and comments starting with <c>#</c> or <c>;</c>.
/// </summary>
public static class IniParser
{
	public static IReadOnlyList<IniSection> Parse(string content)
	{
		var sections = new List<IniSection>();
		var current = new IniSection(string.Empty);
		sections.Add(current);

		foreach (string rawLine in content.Split('\n'))
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line[0] == '#' || line[0] == ';')
			{
				continue;
			}

			if (line[0] == '[' && line[line.Length - 1] == ']')
			{
				current = new IniSection(line.Substring(1, line.Length - 2).Trim());
				sections.Add(current);
				continue;
			}

			int separator = line.IndexOf('=');
			if (separator <= 0)
			{
				continue;
			}

			string key = line.Substring(0, separator).Trim().ToLowerInvariant();
			string value = StripInlineComment(line.Substring(separator + 1).Trim()).Trim();
			current.Properties[key] = value;
		}

		return sections;
	}

	private static string StripInlineComment(string value)
	{
		// The editorconfig spec only allows full-line comments, but many files use "value # comment";
		// tolerate it as long as the # is preceded by a space.
		int index = value.IndexOf(" #", StringComparison.Ordinal);
		return index >= 0 ? value.Substring(0, index) : value;
	}
}

public sealed class IniSection
{
	public IniSection(string name) => Name = name;

	/// <summary>Section header without brackets; empty for the preamble before the first section.</summary>
	public string Name { get; }

	public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
