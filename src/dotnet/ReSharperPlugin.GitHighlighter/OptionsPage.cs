using JetBrains.Application.UI.Controls.FileSystem;
using JetBrains.Application.UI.Options;
using JetBrains.Application.UI.Options.OptionsDialog;
using JetBrains.IDE.UI;
using JetBrains.IDE.UI.Options;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Feature.Services.Daemon.OptionPages;
using JetBrains.ReSharper.UnitTestFramework.Resources;

namespace ReSharperPlugin.GitHighlighter;

[OptionsPage(PID, PageTitle, typeof(UnitTestingThemedIcons.Session),
    // Discover derived types of AEmptyOptionsPage
    ParentId = CodeInspectionPage.PID)]
public class OptionsPage : BeSimpleOptionsPage
{
    private const string PID = nameof(OptionsPage);
    private const string PageTitle = "Git Highlighter";
    
    private readonly Lifetime _lifetime;

    public OptionsPage(Lifetime lifetime,
        OptionsPageContext optionsPageContext,
        OptionsSettingsSmartContext optionsSettingsSmartContext,
        IconHostBase iconHost,
        ICommonFileDialogs dialogs)
        : base(lifetime, optionsPageContext, optionsSettingsSmartContext)
    {
        _lifetime = lifetime;

        AddHeader("General Settings");
        
        AddIntOption((OptionsSettings x) => x.NumberOfCommits, "Number of commits to highlight", minValue:1);
    }
}