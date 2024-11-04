using JetBrains.Application.BuildScript.Application.Zones;

namespace ReSharperPlugin.GitHighlighter
{
    [ZoneDefinition]
    // [ZoneDefinitionConfigurableFeature("Title", "Description", IsInProductSection: false)]
    public interface IGitHighlighterZone : IZone
    {
    }
}
