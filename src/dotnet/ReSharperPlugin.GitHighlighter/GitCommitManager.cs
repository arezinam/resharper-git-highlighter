using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JetBrains.Application.Components;
using JetBrains.Application.Settings;
using JetBrains.Application.Threading;
using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ProjectModel.DataContext;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.Util;

namespace ReSharperPlugin.GitHighlighter
{
    [SolutionComponent]
    public class GitCommitManager : IDisposable
    {
        private readonly ISolution _solution;
        private readonly ISettingsStore _settingsStore;
        private readonly IShellLocks _shellLocks;
        private readonly Lifetime _lifetime;
        private readonly string _gitRepositoryPath;
        private FileSystemWatcher _gitWatcher;
        private IList<CommitInfo> _recentCommits;
        private readonly ILogger _logger;

        public GitCommitManager(
            ISolution solution,
            ISettingsStore settingsStore,
            IShellLocks shellLocks,
            Lifetime lifetime,
            ILogger logger)
        {
            _solution = solution;
            _settingsStore = settingsStore;
            _shellLocks = shellLocks;
            _lifetime = lifetime;
            _logger = logger;

            _gitRepositoryPath = FindGitRepositoryPath();
            if (_gitRepositoryPath != null)
            {
                LoadRecentCommits();
                SetupGitWatcher();
                SubscribeToSettingsChanges();
            }
        }

        private string FindGitRepositoryPath()
        {
            var dir = _solution.SolutionFilePath.Directory;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullPath, ".git")))
                {
                    return dir.FullPath;
                }
                dir = dir.Parent;
            }
            return null;
        }

        private void LoadRecentCommits()
        {
            var settings = _settingsStore.BindToContextLive(
                _lifetime, ContextRange.Smart(_solution.ToDataContext()));

            var numberOfCommits = settings.GetValue((OptionsSettings s) => s.NumberOfCommits);

            _recentCommits = GetRecentCommits(numberOfCommits);
        }

        private IList<CommitInfo> GetRecentCommits(int numberOfCommits)
        {
            var commitList = new List<CommitInfo>();

            var gitLogOutput = ExecuteGitCommand($"log -n {numberOfCommits} --pretty=format:%H|%s");
            var commits = gitLogOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var commit in commits)
            {
                var parts = commit.Split('|');
                if (parts.Length >= 2)
                {
                    var hash = parts[0];
                    var message = parts[1];

                    var filesChangedOutput = ExecuteGitCommand($"diff-tree --no-commit-id --name-only -r {hash}");
                    var filesChanged = filesChangedOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    commitList.Add(new CommitInfo
                    {
                        CommitMessage = message,
                        FilesChanged = filesChanged
                    });
                    
                    _logger.Info($"Commit: {message}, Files: {string.Join(", ", filesChanged)}");
                }
            }

            return commitList;
        }

        private string ExecuteGitCommand(string arguments)
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = _gitRepositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Git command failed: {error}");
                }

                return output;
            }
        }

        private void SetupGitWatcher()
        {
            _gitWatcher = new FileSystemWatcher(_gitRepositoryPath, ".git")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _gitWatcher.Changed += OnGitRepositoryChanged;
            _gitWatcher.Created += OnGitRepositoryChanged;
            _gitWatcher.Deleted += OnGitRepositoryChanged;
            _gitWatcher.Renamed += OnGitRepositoryChanged;

            _gitWatcher.EnableRaisingEvents = true;
        }

        private void OnGitRepositoryChanged(object sender, FileSystemEventArgs e)
        {
            _shellLocks.Queue(_lifetime, "ReloadCommits", () =>
            {
                LoadRecentCommits();
                InvalidateDaemon();
            });
        }

        private void SubscribeToSettingsChanges()
        {
            var settingsStore = _settingsStore.BindToContextLive(
                _lifetime, ContextRange.Smart(_solution.ToDataContext()));

            var entry = _settingsStore.Schema.GetScalarEntry((OptionsSettings s) => s.NumberOfCommits);

            settingsStore.Changed.Advise(_lifetime, entry =>
            {
                LoadRecentCommits();
                InvalidateDaemon();
            });
        }

        private void InvalidateDaemon()
        {
            var daemon = _solution.GetComponent<IDaemon>();
            daemon.Invalidate();
        }

        public IList<CommitInfo> RecentCommits => _recentCommits;

        public void Dispose()
        {
            _gitWatcher?.Dispose();
        }
    }

    public class CommitInfo
    {
        public string CommitMessage { get; set; }
        public IEnumerable<string> FilesChanged { get; set; }
    }
}
