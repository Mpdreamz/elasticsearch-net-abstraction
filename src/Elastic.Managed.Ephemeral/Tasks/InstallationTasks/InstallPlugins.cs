using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Elastic.Managed.Configuration;
using Elastic.Managed.ConsoleWriters;
using Elastic.Managed.Ephemeral.Plugins;
using Elastic.Managed.FileSystem;
using Elastic.Stack.Artifacts;
using Elastic.Stack.Artifacts.Products;
using ProcNet;
using ProcNet.Std;

namespace Elastic.Managed.Ephemeral.Tasks.InstallationTasks
{
	public class InstallPlugins : ClusterComposeTask
	{
		public override void Run(IEphemeralCluster<EphemeralClusterConfiguration> cluster)
		{
			if (cluster.CachingAndCachedHomeExists()) return;

			var v = cluster.ClusterConfiguration.Version;

			//on 2.x we do not support tests requiring plugins for 2.x since we can not reliably install them
			if (v.Major == 2)
			{
				cluster.Writer?.WriteDiagnostic($"{{{nameof(InstallPlugins)}}} skipping install plugins on {{2.x}} version: [{v}]");
				return;
			}
			if (v.Major < 7 && v.ArtifactBuildState == ArtifactBuildState.Snapshot)
			{
				cluster.Writer?.WriteDiagnostic($"{{{nameof(InstallPlugins)}}} skipping install SNAPSHOT plugins on < {{7.x}} version: [{v}]");
				return;
			}

			var fs = cluster.FileSystem;
			var requiredPlugins = cluster.ClusterConfiguration.Plugins;

			if (cluster.ClusterConfiguration.ValidatePluginsToInstall)
			{
				var invalidPlugins = requiredPlugins
					.Where(p => !p.IsValid(v))
					.Select(p => p.SubProductName).ToList();
				if (invalidPlugins.Any())
					throw new ElasticsearchCleanExitException(
						$"Can not install the following plugins for version {v}: {string.Join(", ", invalidPlugins)} ");
			}

			foreach (var plugin in requiredPlugins)
			{
				var includedByDefault = plugin.IsIncludedOutOfTheBox(v);
				if (includedByDefault)
				{
					cluster.Writer?.WriteDiagnostic($"{{{nameof(Run)}}} SKIP plugin [{plugin.SubProductName}] shipped OOTB as of: {{{plugin.ShippedByDefaultAsOf}}}");
					continue;
				}
				var validForCurrentVersion = plugin.IsValid(v);
				if (!validForCurrentVersion)
				{
					cluster.Writer?.WriteDiagnostic($"{{{nameof(Run)}}} SKIP plugin [{plugin.SubProductName}] not valid for version: {{{v}}}");
					continue;
				}
				var alreadyInstalled = AlreadyInstalled(fs, plugin.SubProductName);
				if (alreadyInstalled)
				{
					cluster.Writer?.WriteDiagnostic($"{{{nameof(Run)}}} SKIP plugin [{plugin.SubProductName}] already installed");
					continue;
				}

				cluster.Writer?.WriteDiagnostic($"{{{nameof(Run)}}} attempting install [{plugin.SubProductName}] as it's not OOTB: {{{plugin.ShippedByDefaultAsOf}}} and valid for {v}: {{{plugin.IsValid(v)}}}");
				//var installParameter = v.ReleaseState == ReleaseState.Released ? plugin.Moniker : UseHttpPluginLocation(cluster.Writer, fs, plugin, v);
				var installParameter = UseHttpPluginLocation(cluster.Writer, fs, plugin, v);
				if (!Directory.Exists(fs.ConfigPath)) Directory.CreateDirectory(fs.ConfigPath);
				ExecuteBinary(
					cluster.ClusterConfiguration,
					cluster.Writer,
					fs.PluginBinary + BinarySuffix,
					$"install elasticsearch plugin: {plugin.SubProductName}",
					"install --batch", installParameter);

				CopyConfigDirectoryToHomeCacheConfigDirectory(cluster, plugin);
			}


		}

		private static void CopyConfigDirectoryToHomeCacheConfigDirectory(IEphemeralCluster<EphemeralClusterConfiguration> cluster, ElasticsearchPlugin plugin)
		{
			if (plugin.SubProductName == "x-pack") return;
			if (!cluster.ClusterConfiguration.CacheEsHomeInstallation) return;
			var fs = cluster.FileSystem;
			var cachedEsHomeFolder = Path.Combine(fs.LocalFolder, cluster.GetCacheFolderName());
			var configTarget = Path.Combine(cachedEsHomeFolder, "config");

			var configPluginPath = Path.Combine(fs.ConfigPath, plugin.SubProductName);
			var configPluginPathCached = Path.Combine(configTarget, plugin.SubProductName);
			if (!Directory.Exists(configPluginPath) || Directory.Exists(configPluginPathCached)) return;

			Directory.CreateDirectory(configPluginPathCached);
			CopyFolder(configPluginPath, configPluginPathCached);
		}

		private static bool AlreadyInstalled(INodeFileSystem fileSystem, string folderName)
		{
			var pluginFolder = Path.Combine(fileSystem.ElasticsearchHome, "plugins", folderName);
			return Directory.Exists(pluginFolder);
		}

		private static string UseHttpPluginLocation(IConsoleLineHandler writer, INodeFileSystem fileSystem, ElasticsearchPlugin plugin, ElasticVersion v)
		{
			var downloadLocation = Path.Combine(fileSystem.LocalFolder, $"{plugin.SubProductName}-{v}.zip");
			DownloadPluginSnapshot(writer, downloadLocation, plugin, v);
			//transform downloadLocation to file uri and use that to install from
			return new Uri(new Uri("file://"), downloadLocation).AbsoluteUri;
		}

		private static void DownloadPluginSnapshot(IConsoleLineHandler writer, string downloadLocation, ElasticsearchPlugin plugin, ElasticVersion v)
		{
			if (File.Exists(downloadLocation)) return;
			var artifact = v.Artifact(Product.ElasticsearchPlugin(plugin));
			var downloadUrl = artifact.DownloadUrl;
			writer?.WriteDiagnostic($"{{{nameof(DownloadPluginSnapshot)}}} downloading [{plugin.SubProductName}] from {{{downloadUrl}}}");
			try
			{
				DownloadFile(downloadUrl, downloadLocation);
				writer?.WriteDiagnostic($"{{{nameof(DownloadPluginSnapshot)}}} downloaded [{plugin.SubProductName}] to {{{downloadLocation}}}");
			}
			catch (Exception)
			{
				writer?.WriteDiagnostic($"{{{nameof(DownloadPluginSnapshot)}}} download failed! [{plugin.SubProductName}] from {{{downloadUrl}}}");
				throw;
			}
		}
	}
}
