using System.ComponentModel.Composition;
using CodeStraight.Core.Configuration;

namespace CodeStraight.Vsix;

/// <summary>Single configuration cache shared by the save handler and the guideline adornments.</summary>
[Export(typeof(ConfigurationService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public sealed class ConfigurationService
{
	public ConfigurationLoader Loader { get; } = new();
}
