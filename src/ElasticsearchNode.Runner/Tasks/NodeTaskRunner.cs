using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elastic.ManagedNode.Configuration;
using Elastic.Net.Abstractions.Plugins;
using Elastic.Net.Abstractions.Tasks.AfterNodeStoppedTasks;
using Elastic.Net.Abstractions.Tasks.BeforeStartNodeTasks;
using Elastic.Net.Abstractions.Tasks.InstallationTasks;
using Elastic.Net.Abstractions.Tasks.ValidationTasks;
using Nest;

namespace Elastic.Net.Abstractions.Tasks
{
	public class NodeTaskRunner : IDisposable
	{
		public NodeConfiguration NodeConfiguration { get; }
		private static readonly object Lock = new object();

		public NodeTaskRunner(NodeConfiguration nodeConfiguration)
		{
			this.NodeConfiguration = nodeConfiguration;
		}

		private static IEnumerable<InstallationTaskBase> InstallationTasks { get; } = new List<InstallationTaskBase>
		{
			new CreateLocalApplicationDirectory(),
			new EnsureJavaHomeEnvironmentVariableIsSet(),
			new DownloadCurrentElasticsearchDistribution(),
			new UnzipCurrentElasticsearchDistribution(),
			new CreateEasyRunBatFile(),
			new InstallPlugins(),
		};
		private static IEnumerable<BeforeStartNodeTaskBase> BeforeStart { get; } = new List<BeforeStartNodeTaskBase>
		{
			new CreateEasyRunClusterBatFile()
		};
		private static IEnumerable<AfterNodeStoppedTaskBase> NodeStoppedTasks { get; } = new List<AfterNodeStoppedTaskBase>
		{
			new CleanUpDirectoriesAfterNodeStopped()
		};

		private static IEnumerable<NodeValidationTaskBase> ValidationTasks { get; } = new List<NodeValidationTaskBase>
		{
			new ValidateRunningVersion(),
			new ValidateLicenseTask(),
			new ValidatePluginsTask(),
			new ValidateClusterStateTask()
		};

		public void Install(InstallationTaskBase[] additionalInstallationTasks, ElasticsearchPlugin[] requiredPlugins)=>
			Itterate(
				InstallationTasks.Concat(additionalInstallationTasks ?? Enumerable.Empty<InstallationTaskBase>()),
				(t, n,  fs) => t.Run(n, fs, requiredPlugins)
			);

		public void Dispose() =>
			Itterate(NodeStoppedTasks, (t, n,  fs) => t.Run(n, fs));

		public void OnBeforeStart() =>
			Itterate(BeforeStart, (t, n,  fs) => t.Run(n, fs), log: false);

		public void ValidateAfterStart(IElasticClient client, ElasticsearchPlugin[] requiredPlugins) =>
			Itterate(ValidationTasks, (t, n,  fs) => t.Validate(client, n, requiredPlugins), log: false);

		private IList<string> GetCurrentRunnerLog()
		{
			var log = this.NodeConfiguration.FileSystem.TaskRunnerFile;
			return !File.Exists(log) ? new List<string>() : File.ReadAllLines(log).ToList();
		}
		private void LogTasks(IList<string> logs)
		{
			var log = this.NodeConfiguration.FileSystem.TaskRunnerFile;
			File.WriteAllText(log, string.Join(Environment.NewLine, logs));
		}

		private void Itterate<T>(IEnumerable<T> collection, Action<T, NodeConfiguration, NodeFileSystem> act, bool log = true)
		{
			lock (NodeTaskRunner.Lock)
			{
				var taskLog = this.GetCurrentRunnerLog();
				foreach (var task in collection)
				{
					var name = task.GetType().Name;
					if (log && taskLog.Contains(name)) continue;
					act(task,this.NodeConfiguration, this.NodeConfiguration.FileSystem);
					if (log) taskLog.Add(name);
				}
				if (log) this.LogTasks(taskLog);
			}
		}



	}
}
