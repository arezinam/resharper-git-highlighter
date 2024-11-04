using System.Collections.Generic;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Feature.Services.Daemon;

namespace ReSharperPlugin.GitHighlighter
{
    [DaemonStage]
    public class GitHighlightDaemonStage : IDaemonStage
    {
        private readonly GitCommitManager _gitCommitManager;

        public GitHighlightDaemonStage(GitCommitManager gitCommitManager)
        {
            _gitCommitManager = gitCommitManager;
        }

        public IEnumerable<IDaemonStageProcess> CreateProcess(
            IDaemonProcess process,
            IContextBoundSettingsStore settings,
            DaemonProcessKind processKind)
        {
            if (!process.SourceFile.IsValid())
                return null;

            return new[]
            {
                new GitHighlightDaemonProcess(process, _gitCommitManager)
            };
        }
    }
}