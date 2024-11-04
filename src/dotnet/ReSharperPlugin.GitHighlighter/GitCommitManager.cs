using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Application.Settings;
using JetBrains.Application.Threading;
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
        private IList<CommitInfo> _recentCommits = new List<CommitInfo>();
        private readonly ILogger _logger;
        private FileSystemWatcher _gitWatcher;

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
                SetupGitWatcher();
                SubscribeToSettingsChanges();
                LoadRecentCommits();
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

        private async void LoadRecentCommits()
        {
            try
            {
                var commits = await Task.Run(() =>
                {
                    var settings = _settingsStore.BindToContextLive(
                        _lifetime, ContextRange.Smart(_solution.ToDataContext()));

                    var numberOfCommits = settings.GetValue((OptionsSettings s) => s.NumberOfCommits);
                    return GetRecentCommits(numberOfCommits);
                });

                _shellLocks.Queue(_lifetime, "UpdateRecentCommits", () =>
                {
                    _recentCommits = commits;
                    InvalidateDaemon();
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error loading recent commits.");
            }
        }

        private IList<CommitInfo> GetRecentCommits(int numberOfCommits)
        {
            var commitList = new List<CommitInfo>();

            var gitLogOutput = ExecuteGitCommand($"log -n {numberOfCommits} --pretty=format:%H|%s");
            var commits = gitLogOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var commit in commits)
            {
                var parts = commit.Split('|');
                if (parts.Length >= 2)
                {
                    var hash = parts[0];
                    var message = parts[1];

                    var filesChangedOutput = ExecuteGitCommand($"diff-tree --no-commit-id --name-only -r {hash}");
                    var filesChanged = filesChangedOutput
                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (filesChanged.Length == 0)
                    {
                        continue;
                    }

                    commitList.Add(new CommitInfo
                    {
                        CommitMessage = message,
                        FilesChanged = filesChanged
                    });

                    _logger.Info($"Commit: {message}, .cs Files: {string.Join(", ", filesChanged)}");
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
                    _logger.Error($"Git command failed: {error}");
                    throw new InvalidOperationException($"Git command failed: {error}");
                }

                return output;
            }
        }

        private void SetupGitWatcher()
        {
            var gitDirPath = Path.Combine(_gitRepositoryPath, ".git");

            _gitWatcher = new FileSystemWatcher(gitDirPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true
            };

            _gitWatcher.Changed += OnGitRepositoryChanged;
            _gitWatcher.Created += OnGitRepositoryChanged;
            _gitWatcher.Deleted += OnGitRepositoryChanged;
            _gitWatcher.Renamed += OnGitRepositoryChanged;

            _gitWatcher.EnableRaisingEvents = true;
        }

        private void OnGitRepositoryChanged(object sender, FileSystemEventArgs e)
        {
            // Reload recent commits when a change is detected
            LoadRecentCommits();
        }

        private void SubscribeToSettingsChanges()
        {
            var settingsStore = _settingsStore.BindToContextLive(
                _lifetime, ContextRange.Smart(_solution.ToDataContext()));

            settingsStore.Changed.Advise(_lifetime, _ =>
            {
                LoadRecentCommits();
            });
        }

        private void InvalidateDaemon()
        {
            _shellLocks.ExecuteOrQueue(_lifetime, "InvalidateDaemon", () =>
            {
                var daemon = _solution.GetComponent<IDaemon>();
                daemon.Invalidate();
            });
        }

        public IList<CommitInfo> RecentCommits
        {
            get
            {
                IList<CommitInfo> commits = null;
                _shellLocks.ExecuteWithReadLock(() =>
                {
                    commits = _recentCommits;
                });
                return commits;
            }
        }

        public FileSystemPath GitRepositoryPath => FileSystemPath.Parse(_gitRepositoryPath);

        public void Dispose()
        {
            if (_gitWatcher != null)
            {
                _gitWatcher.EnableRaisingEvents = false;
                _gitWatcher.Dispose();
                _gitWatcher = null;
            }
        }
    }

    public class CommitInfo
    {
        public string CommitMessage { get; set; }
        public IEnumerable<string> FilesChanged { get; set; }
    }
}
