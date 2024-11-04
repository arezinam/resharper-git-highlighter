using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.Util;

namespace ReSharperPlugin.GitHighlighter
{
    public class GitHighlightDaemonProcess : IDaemonStageProcess
    {
        private readonly IDaemonProcess _process;
        private readonly GitCommitManager _gitCommitManager;

        public GitHighlightDaemonProcess(
            IDaemonProcess process,
            GitCommitManager gitCommitManager)
        {
            _process = process;
            _gitCommitManager = gitCommitManager;
        }

        public void Execute(Action<DaemonStageResult> committer)
        {
            var recentCommits = _gitCommitManager.RecentCommits;
            if (recentCommits == null || !Enumerable.Any(recentCommits)) return;

            var filePath = _process.SourceFile.GetLocation().FullPath;
            var relativePath = _process.SourceFile.GetLocation().MakeRelativeTo(
                _process.SourceFile.GetSolution().SolutionDirectory).FullPath.Replace("\\", "/");

            var modifiedInRecentCommits = recentCommits.Any(ci =>
                ci.FilesChanged.Any(f => f.Equals(relativePath, StringComparison.OrdinalIgnoreCase)));

            if (!modifiedInRecentCommits) return;

            var document = _process.Document;
            if (document == null) return;

            var text = document.GetText();
            var startIndex = text.TakeWhile(char.IsWhiteSpace).Count();
            var endIndex = Math.Min(startIndex + 5, text.Length);

            var range = new DocumentRange(document, new TextRange(startIndex, endIndex));

            var commitMessage = recentCommits.First(ci =>
                ci.FilesChanged.Any(f => f.Equals(relativePath, StringComparison.OrdinalIgnoreCase))).CommitMessage;

            var highlighting = new GitHighlighting(commitMessage, range);
            var highlightings = new List<HighlightingInfo> { new HighlightingInfo(range, highlighting) };

            committer(new DaemonStageResult(highlightings));
        }

        public IDaemonProcess DaemonProcess => _process;
    }
}
