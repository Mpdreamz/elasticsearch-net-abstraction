using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Elastic.Managed.Configuration;

namespace Elastic.Managed.Ephemeral
{
	public class EphemeralCluster : EphemeralCluster<EphemeralClusterConfiguration>
	{
		public EphemeralCluster(ElasticsearchVersion version, int numberOfNodes = 1)
			: base(new EphemeralClusterConfiguration(version, ClusterFeatures.None, numberOfNodes: numberOfNodes)) { }

		public EphemeralCluster(EphemeralClusterConfiguration clusterConfiguration) : base(clusterConfiguration) { }
	}

	public abstract class EphemeralCluster<TConfiguration> : ClusterBase<TConfiguration>, IEphemeralCluster<TConfiguration>
		where TConfiguration : EphemeralClusterConfiguration
	{
		protected EphemeralCluster(TConfiguration clusterConfiguration) : base(clusterConfiguration)
		{
			this.Composer = new EphemeralClusterComposer<TConfiguration>(this);
		}

		protected EphemeralClusterComposer<TConfiguration> Composer { get; }

		protected override void OnBeforeStart()
		{
			this.Composer.Install();
			this.Composer.OnBeforeStart();
		}

		protected override void OnDispose() => this.Composer.OnStop();

		protected override void OnAfterStarted() => this.Composer.OnAfterStart();

		public ICollection<Uri> NodesUris(string hostName = "localhost")
		{
			var ssl = this.ClusterConfiguration.EnableSsl ? "s" : "";
			return this.Nodes
				.Select(n=>$"http{ssl}://{hostName}:{n.Port ?? 9200}")
				.Distinct()
				.Select(n => new Uri(n))
				.ToList();
		}

		protected override string SeeLogsMessage(string message)
		{
			var log = Path.Combine(this.FileSystem.LogsPath, $"{this.ClusterConfiguration.ClusterName}.log");
			if (!File.Exists(log)) return message;
			using (var fileStream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (var textReader = new StreamReader(fileStream))
			{
				var logContents = textReader.ReadToEnd();
				return message + $" contents of {log}:{Environment.NewLine}" + logContents;
			}
		}

		public bool CachingAndCachedHomeExists()
		{
			if (!this.ClusterConfiguration.CacheEsHomeInstallation) return false;
			var cachedEsHomeFolder = Path.Combine(this.FileSystem.LocalFolder, this.GetCacheFolderName());
			return Directory.Exists(cachedEsHomeFolder);
		}

		public virtual string GetCacheFolderName()
		{
			var config = this.ClusterConfiguration;

			var sb = new StringBuilder();
			sb.Append(EphemeralClusterComposerBase.InstallationTasks.Count());
			sb.Append("-");
			if (config.XPackInstalled) sb.Append("x");
			if (config.EnableSecurity) sb.Append("sec");
			if (config.EnableSsl) sb.Append("ssl");
			if (config.Plugins != null && config.Plugins.Count > 0)
			{
				sb.Append("-");
				foreach (var p in config.Plugins.OrderBy(p=>p.Moniker))
					sb.Append(p.Moniker.ToLowerInvariant());
			}
			return sb.ToString();
		}

	}
}
