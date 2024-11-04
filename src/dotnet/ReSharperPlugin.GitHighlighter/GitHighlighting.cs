using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Daemon;

namespace ReSharperPlugin.GitHighlighter
{
    [StaticSeverityHighlighting(Severity.WARNING, typeof(HighlightingGroupIds.GutterMarks), OverlapResolve = OverlapResolveKind.NONE)]
    public class GitHighlighting : IHighlighting
    {
        private readonly string _toolTip;
        private readonly DocumentRange _range;

        public GitHighlighting(string toolTip, DocumentRange range)
        {
            _toolTip = toolTip;
            _range = range;
        }

        public bool IsValid() => true;

        public DocumentRange CalculateRange() => _range;

        public string ToolTip => _toolTip;

        public string ErrorStripeToolTip => _toolTip;
    }
}