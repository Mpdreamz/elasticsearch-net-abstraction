// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using Elastic.Elasticsearch.Managed.ConsoleWriters;

namespace Elastic.Elasticsearch.Ephemeral.Tasks.BeforeStartNodeTasks
{
	public class CacheElasticsearchInstallation : ClusterComposeTask
	{
		public override void Run(IEphemeralCluster<EphemeralClusterConfiguration> cluster)
		{
			if (!cluster.ClusterConfiguration.CacheEsHomeInstallation) return;

			var fs = cluster.FileSystem;
			var cachedEsHomeFolder = Path.Combine(fs.LocalFolder, cluster.GetCacheFolderName());
			var cachedelasticsearchYaml = Path.Combine(cachedEsHomeFolder, "config", "elasticsearch.yml");
			if (File.Exists(cachedelasticsearchYaml))
			{
				cluster.Writer?.WriteDiagnostic(
					$"{{{nameof(CacheElasticsearchInstallation)}}} cached home already exists [{cachedEsHomeFolder}]");
				return;
			}

			var source = fs.ElasticsearchHome;
			var target = cachedEsHomeFolder;
			cluster.Writer?.WriteDiagnostic(
				$"{{{nameof(CacheElasticsearchInstallation)}}} caching {{{source}}} to [{target}]");
			CopyFolder(source, target, false);
		}
	}
}
