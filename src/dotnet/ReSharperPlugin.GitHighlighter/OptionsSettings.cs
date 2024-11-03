using JetBrains.Application.Settings;
using JetBrains.Application.Settings.WellKnownRootKeys;

namespace ReSharperPlugin.GitHighlighter;


[SettingsKey(
    // Discover others through usages of SettingsKeyAttribute
    Parent: typeof(EnvironmentSettings),
    Description: "Github Highlighter settings")]
public class OptionsSettings
{
    [SettingsEntry(DefaultValue: 5, Description: "Number of commits to highlight")]
    public int NumberOfCommits;
    
    
}