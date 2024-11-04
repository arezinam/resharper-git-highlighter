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
            try
            {
                var recentCommits = _gitCommitManager.RecentCommits;
                if (recentCommits == null || !recentCommits.Any())
                {
                    return;
                }

                var sourceFile = _process.SourceFile;
                if (sourceFile == null || !sourceFile.IsValid())
                    return;

                var filePath = sourceFile.GetLocation();
                var document = _process.Document;
                if (document == null)
                    return;

                // Get the relative path of the file within the Git repository
                var relativePath = _process.SourceFile.GetLocation().MakeRelativeTo(
                    _process.SourceFile.GetSolution().SolutionDirectory).FullPath.Replace("\\", "/");

                // Check if the file was modified in recent commits
                var modifiedInRecentCommits = recentCommits.Any(ci =>
                    ci.FilesChanged.Any(f =>
                        string.Equals(f.Replace("\\", "/").TrimStart('/'), relativePath, StringComparison.OrdinalIgnoreCase)));

                if (!modifiedInRecentCommits)
                    return;

                // Get the first 5 non-whitespace characters to highlight
                var text = document.GetText();
                var startIndex = text.TakeWhile(char.IsWhiteSpace).Count();
                if (startIndex >= text.Length)
                    return;

                var endIndex = Math.Min(startIndex + 5, text.Length);
                var range = new DocumentRange(document, new TextRange(startIndex, endIndex));

                // Get the commit message for the tooltip
                var commitInfo = recentCommits.FirstOrDefault(ci =>
                    ci.FilesChanged.Any(f =>
                        string.Equals(f.Replace("\\", "/").TrimStart('/'), relativePath, StringComparison.OrdinalIgnoreCase)));
                
                

                var commitMessage = commitInfo?.CommitMessage ?? "Recent Change";

                // Create the highlighting
                var highlighting = new GitHighlighting(commitMessage, range);
                var highlightings = new List<HighlightingInfo> { new HighlightingInfo(range, highlighting) };

                // Commit the highlighting result
                committer(new DaemonStageResult(highlightings));
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                // _logger.Error(ex, "Error in GitHighlightDaemonProcess.Execute");
            }
        }

        public IDaemonProcess DaemonProcess => _process;
    }
}