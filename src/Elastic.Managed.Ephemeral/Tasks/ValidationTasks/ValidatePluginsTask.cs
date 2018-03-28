using System;
using System.Linq;
using Elastic.Managed.Configuration;
using Elastic.Managed.ConsoleWriters;
using Elastic.Managed.FileSystem;
using Nest;

namespace Elastic.Managed.Ephemeral.Tasks.ValidationTasks
{
	public class ValidatePluginsTask : NodeValidationTaskBase
	{
		public override void Validate(EphemeralCluster cluster, INodeFileSystem fs)
		{
			var v = cluster.ClusterConfiguration.Version;
			if (v.Major == 2)
			{
				cluster.Writer?.WriteDiagnostic($"{{{nameof(ValidatePluginsTask)}}} skipping validate plugins on {{2.x}} version: [{v}]");
				return;
			}

			var supported = cluster.RequiredPlugins.Select(p => p.Moniker).ToList();
			if (!supported.Any()) return;

			var checkPlugins = cluster.Client.CatPlugins();

			if (!checkPlugins.IsValid)
				throw new Exception($"Failed to check plugins: {checkPlugins.DebugInformation}.");

			var missingPlugins = supported
				.Except((checkPlugins.Records ?? Enumerable.Empty<CatPluginsRecord>()).Select(r => r.Component))
				.ToList();
			if (!missingPlugins.Any()) return;

			var pluginsString = string.Join(", ", missingPlugins);
			throw new Exception($"Already running elasticsearch missed the following plugin(s): {pluginsString}.");
		}
	}
}
