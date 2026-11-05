using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeStraight.Core.Configuration;

/// <summary>
/// A <c>dotnet_naming_rule</c> from the <c>.editorconfig</c>, resolved with its symbols and style.
/// Only the parts needed to rename fields are modelled.
/// </summary>
public sealed class NamingRule
{
	private NamingRule(
		string name,
		int order,
		IReadOnlyCollection<string> kinds,
		IReadOnlyCollection<string> accessibilities,
		IReadOnlyCollection<string> requiredModifiers,
		NamingStyle style)
	{
		Name = name;
		Order = order;
		Kinds = kinds;
		Accessibilities = accessibilities;
		RequiredModifiers = requiredModifiers;
		Style = style;
	}

	public string Name { get; }

	/// <summary>Position of the rule in the file, used as the last tie-breaker.</summary>
	public int Order { get; }

	public IReadOnlyCollection<string> Kinds { get; }

	public IReadOnlyCollection<string> Accessibilities { get; }

	public IReadOnlyCollection<string> RequiredModifiers { get; }

	public NamingStyle Style { get; }

	public bool AppliesTo(string kind, string accessibility, IReadOnlyCollection<string> modifiers) =>
		(Kinds.Contains("*") || Kinds.Contains(kind))
		&& (Accessibilities.Contains("*") || Accessibilities.Contains(accessibility))
		&& RequiredModifiers.All(modifiers.Contains);

	/// <summary>
	/// Picks the rule that applies to a symbol. Like Roslyn, more specific rules win: more required modifiers first,
	/// then explicit accessibilities, then explicit kinds, then the order in the file.
	/// </summary>
	public static NamingRule? Find(
		IReadOnlyList<NamingRule> rules,
		string kind,
		string accessibility,
		IReadOnlyCollection<string> modifiers) =>
		rules
			.Where(rule => rule.AppliesTo(kind, accessibility, modifiers))
			.OrderByDescending(rule => rule.RequiredModifiers.Count)
			.ThenBy(rule => rule.Accessibilities.Contains("*") ? 1 : 0)
			.ThenBy(rule => rule.Kinds.Contains("*") ? 1 : 0)
			.ThenBy(rule => rule.Order)
			.FirstOrDefault();

	public static IReadOnlyList<NamingRule> Parse(IReadOnlyDictionary<string, string> properties)
	{
		const string rulePrefix = "dotnet_naming_rule.";
		var rules = new List<NamingRule>();
		var ruleNames = properties.Keys
			.Where(key => key.StartsWith(rulePrefix, StringComparison.OrdinalIgnoreCase))
			.Select(key => key.Substring(rulePrefix.Length))
			.Select(rest => rest.Substring(0, Math.Max(rest.LastIndexOf('.'), 0)))
			.Where(name => name.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		int order = 0;
		foreach (string ruleName in ruleNames)
		{
			string? severity = Get(properties, $"{rulePrefix}{ruleName}.severity");
			string? symbols = Get(properties, $"{rulePrefix}{ruleName}.symbols");
			string? styleName = Get(properties, $"{rulePrefix}{ruleName}.style");
			if (symbols is null || styleName is null || severity is "none")
			{
				continue;
			}

			rules.Add(new NamingRule(
				ruleName,
				order++,
				List(Get(properties, $"dotnet_naming_symbols.{symbols}.applicable_kinds")),
				List(Get(properties, $"dotnet_naming_symbols.{symbols}.applicable_accessibilities")),
				List(Get(properties, $"dotnet_naming_symbols.{symbols}.required_modifiers"), emptyMeansAll: false),
				NamingStyle.Parse(properties, styleName)));
		}

		return rules;
	}

	private static string? Get(IReadOnlyDictionary<string, string> properties, string key) =>
		properties.TryGetValue(key, out string? value) ? value.Trim().ToLowerInvariant() : null;

	private static IReadOnlyCollection<string> List(string? value, bool emptyMeansAll = true)
	{
		string[] items = (value ?? string.Empty)
			.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(item => item.Trim())
			.Where(item => item.Length > 0)
			.ToArray();

		return items.Length == 0 && emptyMeansAll ? new[] { "*" } : items;
	}
}

public enum Capitalization
{
	PascalCase,
	CamelCase,
	FirstWordUpper,
	AllUpper,
	AllLower,
}

public sealed class NamingStyle
{
	/// <summary>Upper case words separated by <c>_</c>: <c>MAX_VALUE</c> (<c>screaming_case_constants</c>).</summary>
	public static readonly NamingStyle ScreamingCase = new() { WordSeparator = "_", Capitalization = Capitalization.AllUpper };

	public string Prefix { get; init; } = string.Empty;

	public string Suffix { get; init; } = string.Empty;

	public string WordSeparator { get; init; } = string.Empty;

	public Capitalization Capitalization { get; init; } = Capitalization.PascalCase;

	public static NamingStyle Parse(IReadOnlyDictionary<string, string> properties, string styleName)
	{
		string Raw(string key) =>
			properties.TryGetValue($"dotnet_naming_style.{styleName}.{key}", out string? value) ? value.Trim() : string.Empty;

		return new NamingStyle
		{
			Prefix = Raw("required_prefix"),
			Suffix = Raw("required_suffix"),
			WordSeparator = Raw("word_separator"),
			Capitalization = Raw("capitalization").ToLowerInvariant() switch
			{
				"camel_case" => Capitalization.CamelCase,
				"first_word_upper" => Capitalization.FirstWordUpper,
				"all_upper" => Capitalization.AllUpper,
				"all_lower" => Capitalization.AllLower,
				_ => Capitalization.PascalCase,
			},
		};
	}

	/// <summary>
	/// Builds the name that follows this style from an existing one. Known prefixes/suffixes of other styles
	/// (<c>_</c>, <c>m_</c>, <c>s_</c>) are stripped first so the words can be recombined.
	/// </summary>
	public string Apply(string name)
	{
		if (IsCompliant(name))
		{
			return name;
		}

		string core = name;

		// The style prefix is only removed when what follows really starts a word in this style:
		// "_contador" → "contador", but the "I" of an interface called "Item" is part of the word ("IItem").
		if (Prefix.Length > 0 && core.Length > Prefix.Length && core.StartsWith(Prefix, StringComparison.Ordinal)
			&& StartsFirstWord(core[Prefix.Length]))
		{
			core = core.Substring(Prefix.Length);
		}

		if (Suffix.Length > 0 && core.Length > Suffix.Length && core.EndsWith(Suffix, StringComparison.Ordinal))
		{
			core = core.Substring(0, core.Length - Suffix.Length);
		}

		foreach (string legacyPrefix in new[] { "m_", "s_", "t_", "_" })
		{
			if (core.Length > legacyPrefix.Length && core.StartsWith(legacyPrefix, StringComparison.Ordinal))
			{
				core = core.Substring(legacyPrefix.Length);
				break;
			}
		}

		List<string> words = SplitWords(core);

		// All upper case (MAX_VALUE, SCREAMCASE): the words are lowered first and rebuilt in the target
		// capitalization (MaxValue, Screamcase). Acronyms of two letters (ID, IO) keep it, as in the .NET guidelines.
		if (IsAllUpper(core))
		{
			words = words.Select(word => word.ToLowerInvariant()).ToList();
		}

		return words.Count == 0 ? name : Prefix + Build(words) + Suffix;
	}

	/// <summary>The name already has the prefix, the suffix and the capitalization of this style.</summary>
	public bool IsCompliant(string name)
	{
		if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !name.EndsWith(Suffix, StringComparison.Ordinal))
		{
			return false;
		}

		// Only the prefix and the suffix, with nothing to capitalize in between (a type parameter called T): compliant,
		// as in Roslyn. Otherwise T would become TT.
		if (name.Length <= Prefix.Length + Suffix.Length)
		{
			return name.Length == Prefix.Length + Suffix.Length && name.Length > 0;
		}

		string middle = name.Substring(Prefix.Length, name.Length - Prefix.Length - Suffix.Length);
		List<string> words = SplitWords(middle);
		return words.Count > 0 && Build(words) == middle
			&& !(IsAllUpper(middle) && Capitalization is Capitalization.PascalCase or Capitalization.CamelCase);
	}

	/// <summary>A name with more than two letters and no lower-case letter (MAX_VALUE, URL), not an acronym like ID.</summary>
	private static bool IsAllUpper(string name) => name.Count(char.IsLetter) > 2 && !name.Any(char.IsLower);

	private bool StartsFirstWord(char c) => Capitalization switch
	{
		Capitalization.CamelCase or Capitalization.AllLower => char.IsLower(c),
		_ => char.IsUpper(c),
	};

	private string Build(List<string> words)
	{
		var builder = new StringBuilder();
		for (int i = 0; i < words.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(WordSeparator);
			}

			builder.Append(Capitalize(words[i], i));
		}

		return builder.ToString();
	}

	private string Capitalize(string word, int index)
	{
		switch (Capitalization)
		{
			case Capitalization.AllUpper:
				return word.ToUpperInvariant();
			case Capitalization.AllLower:
				return word.ToLowerInvariant();
			case Capitalization.CamelCase when index == 0:
				// An acronym as first word is lowered completely: HTTPClient → httpClient.
				return word.All(char.IsUpper) ? word.ToLowerInvariant() : char.ToLowerInvariant(word[0]) + word.Substring(1);
			case Capitalization.FirstWordUpper when index > 0:
				return word.ToLowerInvariant();
			default:
				return char.ToUpperInvariant(word[0]) + word.Substring(1);
		}
	}

	/// <summary>Splits on separators and camel humps, keeping acronyms together (<c>HTTPClient</c> → HTTP, Client).</summary>
	private static List<string> SplitWords(string name)
	{
		var words = new List<string>();
		var current = new StringBuilder();
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (c == '_' || c == '-')
			{
				Flush();
				continue;
			}

			bool startsWord = current.Length > 0 && char.IsUpper(c)
				&& (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
					|| (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
			if (startsWord)
			{
				Flush();
			}

			current.Append(c);
		}

		Flush();
		return words;

		void Flush()
		{
			if (current.Length > 0)
			{
				words.Add(current.ToString());
				current.Clear();
			}
		}
	}
}
