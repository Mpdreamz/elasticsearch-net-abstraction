﻿using System;
using System.Net;
using System.Net.Http;
using Elastic.Managed.Ephemeral;
using Elastic.Managed.Ephemeral.Plugins;
using Elastic.Stack.Artifacts;
using Elastic.Stack.Artifacts.Products;
using Elasticsearch.Net;
using Nest;
using static Elastic.Managed.Ephemeral.ClusterFeatures;
using HttpMethod = Elasticsearch.Net.HttpMethod;

namespace ScratchPad
{
	public static class Program
	{
		public static int Main()
		{
		
		    var range = new SemVer.Range(">=7.2.0-alpha2");
		    var v = ElasticVersion.From("7.4.0-SNAPSHOT");
		    var vLatest = ElasticVersion.From("latest-7");
		    var v3 = ElasticVersion.From("7.2.0");
		    var v5 = ElasticVersion.From("7.1.0");
		    var v4 = ElasticVersion.From("7.2.0-alpha1");
		    
		    
		    Console.WriteLine(v.InRange(range));
		    Console.WriteLine(vLatest.InRange(range));
		    Console.WriteLine(v3.InRange(range));
		    Console.WriteLine(v4.InRange(range));
		    Console.WriteLine(v5.InRange(range));
		    
		    
		    
		    
		
		
			//ResolveVersions();
			//ManualConfigRun();
			//ValidateCombinations.Run();
			return 0;
		}

		private static void ManualConfigRun()
		{
			ElasticVersion version = "latest";

			var plugins =
				new ElasticsearchPlugins(ElasticsearchPlugin.IngestGeoIp, ElasticsearchPlugin.AnalysisKuromoji);
			var features = Security | XPack | SSL;
			var config = new EphemeralClusterConfiguration(version, features, null, numberOfNodes: 1)
			{
				HttpFiddlerAware = true,
				ShowElasticsearchOutputAfterStarted = true,
				CacheEsHomeInstallation = true,
				TrialMode = XPackTrialMode.Trial,
				NoCleanupAfterNodeStopped = false,
			};

			using (var cluster = new EphemeralCluster(config))
			{
				cluster.Start();

				var nodes = cluster.NodesUris();
				var connectionPool = new StaticConnectionPool(nodes);
				var settings = new ConnectionSettings(connectionPool).EnableDebugMode();
				if (config.EnableSecurity)
					settings = settings.BasicAuthentication(ClusterAuthentication.Admin.Username,
						ClusterAuthentication.Admin.Password);
				if (config.EnableSsl)
					settings = settings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);

				var client = new ElasticClient(settings);

				Console.Write(client.XPackInfo().DebugInformation);
				Console.WriteLine("Press any key to exit");
				Console.ReadKey();
				Console.WriteLine("Exitting..");
			}

			Console.WriteLine("Done!");
		}

		private static void ResolveVersions()
		{
			var versions = new[]
			{
				"8.0.0-SNAPSHOT", "7.0.0-beta1", "6.6.1", "latest-7", "latest", "7.0.0", "7.4.0-SNAPSHOT",
				"957e3089:7.2.0", "latest-6"
			};
			//versions = new[] {"latest-6"};
			var products = new Product[]
			{
				Product.Elasticsearch,
				Product.Kibana,
				Product.ElasticsearchPlugin(ElasticsearchPlugin.AnalysisIcu)
			};

			foreach (var v in versions)
			{
				foreach (var p in products)
				{
					var r = ElasticVersion.From(v);
					var a = r.Artifact(p);
					Console.ForegroundColor = ConsoleColor.Green;
					Console.Write(v);
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.Write($"\t{p.Moniker}");
					Console.ForegroundColor = ConsoleColor.Cyan;
					Console.Write($"\t\t{r.ArtifactBuildState.GetStringValue()}");
					Console.ForegroundColor = ConsoleColor.White;
					Console.WriteLine($"\t{a?.BuildHash}");
					Console.ForegroundColor = ConsoleColor.Blue;
//                    Console.WriteLine($"\t{a.Archive}");
//                    Console.WriteLine($"\t{r.ArtifactBuildState}");
//                    Console.WriteLine($"\t{a.FolderInZip}");
//                    Console.WriteLine($"\tfolder: {a.LocalFolderName}");
					if (a == null) 
					{
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\tArtifact not resolved");
                        continue;
					}
					Console.WriteLine($"\t{a.DownloadUrl}");
					var found = false;
					try
					{
						found = HeadReturns200OnDownloadUrl(a.DownloadUrl);
					}
					catch
					{
						// ignored, best effort but does not take into account proxies or other bits that might prevent the check
					}

					if (found) continue;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("\tArtifact not found");
				}
			}
		}

		private static HttpClient HttpClient { get; } = new HttpClient() { };

		public static bool HeadReturns200OnDownloadUrl(string url)
		{
			var message = new HttpRequestMessage
			{
				Method = System.Net.Http.HttpMethod.Head,
				RequestUri = new Uri(url)
			};

			using (var response = HttpClient.SendAsync(message).GetAwaiter().GetResult())
				return response.StatusCode == HttpStatusCode.OK;
		}
	}
}
