using System;
using System.Globalization;
using System.IO;
using Elastic.Managed.FileSystem;

namespace Elastic.Managed.Configuration
{
	public interface IClusterConfiguration<out TFileSystem> where TFileSystem : INodeFileSystem
	{
		TFileSystem FileSystem { get; }

		string ClusterName { get; }
		NodeSettings DefaultNodeSettings { get; }
		ElasticsearchVersion Version { get; }
		int NumberOfNodes { get; }
		int StartingPortNumber { get; set; }
		bool ShowElasticsearchOutputAfterStarted { get; set; }
		bool CacheEsHomeInstallation { get; set; }

		string CreateNodeName(int? node);
	}

	public class ClusterConfiguration : ClusterConfiguration<NodeFileSystem>
	{
		public ClusterConfiguration(ElasticsearchVersion version, string esHome, int numberOfNodes = 1)
			: base(version, (v, s) => new NodeFileSystem(v, esHome), numberOfNodes , null) { }

		public ClusterConfiguration(ElasticsearchVersion version, Func<ElasticsearchVersion, string, NodeFileSystem> fileSystem = null, int numberOfNodes = 1, string clusterName = null)
			: base(version, fileSystem ?? ((v, s) => new NodeFileSystem(v, s)), numberOfNodes , clusterName) { }
	}

	public class ClusterConfiguration<TFileSystem> : IClusterConfiguration<TFileSystem>
		where TFileSystem : INodeFileSystem
	{
		/// <summary>
		/// Creates a new instance of a configuration for an Elasticsearch cluster.
		/// </summary>
		/// <param name="version">The version of Elasticsearch</param>
		/// <param name="fileSystem">A delegate to create the instance of <typeparamref name="TFileSystem"/>.
		/// Passed the Elasticsearch version and the Cluster name</param>
		/// <param name="numberOfNodes">The number of nodes in the cluster</param>
		/// <param name="clusterName">The name of the cluster</param>
		public ClusterConfiguration(ElasticsearchVersion version, Func<ElasticsearchVersion, string, TFileSystem> fileSystem, int numberOfNodes = 1, string clusterName = null)
		{
			if (fileSystem == null) throw new ArgumentException(nameof(fileSystem));

			this.ClusterName = clusterName;
			this.Version = version;
			this.FileSystem = fileSystem(this.Version, this.ClusterName);
			this.NumberOfNodes = numberOfNodes;

			var fs = this.FileSystem;
			this.Add("node.max_local_storage_nodes", numberOfNodes.ToString(CultureInfo.InvariantCulture));
			this.Add("discovery.zen.minimum_master_nodes", Quorum(numberOfNodes).ToString(CultureInfo.InvariantCulture));

			this.Add("cluster.name", clusterName);
			this.Add("path.repo", fs.RepositoryPath);
			this.Add("path.data", fs.DataPath);
			var logsPathDefault = Path.Combine(fs.ElasticsearchHome, "logs");
			if (logsPathDefault != fs.LogsPath) this.Add("path.logs", fs.LogsPath);
		}

		public string ClusterName { get; }
		public ElasticsearchVersion Version { get; }
		public TFileSystem FileSystem { get; }
		public int NumberOfNodes { get; }
		public int StartingPortNumber { get; set; } = 9200;

		/// <summary> Will print the contents of all the yaml files when starting the cluster up, great for debugging purposes</summary>
		public bool PrintYamlFilesInConfigFolder { get; set; }

		/// <summary>
		/// Whether <see cref="ElasticsearchNode" /> should continue to write output to console after it has started.
		/// <para>Defaults to <c>true</c></para>
		/// </summary>
		public bool ShowElasticsearchOutputAfterStarted { get; set; } = true;

		public bool CacheEsHomeInstallation { get; set; }

		/// <summary>The node settings to apply to each started node</summary>
		public NodeSettings DefaultNodeSettings { get; } = new NodeSettings();

		/// <summary>
		/// Creates a node name
		/// </summary>
		public virtual string CreateNodeName(int? node) => node.HasValue ? $"managed-elasticsearch-{node}" : " managed-elasticsearch";

		/// <summary>
		/// Calculates the quorum given the number of instances
		/// </summary>
		private static int Quorum(int instanceCount) => Math.Max(1, (int) Math.Floor((double) instanceCount / 2) + 1);

		/// <summary>
		/// Creates a node attribute for the version of Elasticsearch
		/// </summary>
		public string AttributeKey(string attribute)
		{
			var attr = this.Version.Major >= 5 ? "attr." : "";
			return $"node.{attr}{attribute}";
		}

		/// <summary>
		/// Adds a node setting to the default node settings
		/// </summary>
		protected void Add(string key, string value)
		{
			if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
			this.DefaultNodeSettings.Add(key,value);
		}

		/// <summary>
		/// Adds a node setting to the default node settings only if the Elasticsearch
		/// version is in the range.
		/// </summary>
		protected void Add(string key, string value, string range)
		{
			if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
			if (string.IsNullOrWhiteSpace(range) || this.Version.InRange(range))
				this.DefaultNodeSettings.Add(key, value, range);
		}
	}
}
