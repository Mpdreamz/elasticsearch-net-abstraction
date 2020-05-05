// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System.IO;
using System.Linq;
using Elastic.Elasticsearch.Managed.ConsoleWriters;

namespace Elastic.Elasticsearch.Ephemeral.Tasks.BeforeStartNodeTasks
{
	public class PrintYamlContents : ClusterComposeTask
	{
		public override void Run(IEphemeralCluster<EphemeralClusterConfiguration> cluster)
		{
			var c = cluster.ClusterConfiguration;
			var v = c.Version;
			var fs = cluster.FileSystem;

			var files = Directory.GetFiles(fs.ConfigPath, "*.yml", SearchOption.AllDirectories);
			foreach (var file in files) DumpFile(cluster, file);
		}

		private static void DumpFile(IEphemeralCluster<EphemeralClusterConfiguration> cluster, string configFile)
		{
			if (!File.Exists(configFile))
			{
				cluster.Writer.WriteDiagnostic($"{{{nameof(PrintYamlContents)}}} skipped printing [{configFile}] as it does not exists");
				return;
			}

			var fileName = Path.GetFileName(configFile);
			cluster.Writer.WriteDiagnostic($"{{{nameof(PrintYamlContents)}}} printing [{configFile}]");
			var lines = File.ReadAllLines(configFile).ToList();
			foreach (var l in lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")))
			{
				cluster.Writer.WriteDiagnostic($"{{{nameof(PrintYamlContents)}}} [{fileName}] {l}");
			}
		}
	}
}
