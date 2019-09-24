using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using Elastic.Stack.Artifacts.Platform;
using Elastic.Stack.Artifacts.Products;
using SemVer;
using Version = SemVer.Version;

namespace Elastic.Stack.Artifacts.Resolvers
{
	public static class SnapshotApiResolver
	{
		private const string ArtifactsApiUrl = "https://artifacts-api.elastic.co/v1/";

		public static readonly Lazy<IReadOnlyCollection<Version>> AvailableVersions = new Lazy<IReadOnlyCollection<Version>>(LoadVersions, LazyThreadSafetyMode.ExecutionAndPublication);

		private static Regex PackageProductRegex { get; } = new Regex(@"(.*?)-(\d+\.\d+\.\d+(?:-(?:SNAPSHOT|alpha\d+|beta\d+|rc\d+))?)");
		private static Regex BuildHashRegex { get; } = new Regex(@"https://snapshots.elastic.co/(\d+\.\d+\.\d+-([^/]+)?)");

		private static Version IncludeOsMoniker { get; } = new Version("7.0.0");

		public static bool TryResolve(Product product, Version version, OSPlatform os, string filters, out Artifact artifact)
		{
			artifact = null;
			var p = product.SubProduct?.SubProductName ?? product.ProductName;
			var query = p;
			if (product.PlatformDependent && version > product.PlatformSuffixAfter)
				query += $",{OsMonikers.From(os)}";
			else if (product.PlatformDependent)
				query += $",{OsMonikers.CurrentPlatformSearchFilter()}";
			if (!string.IsNullOrWhiteSpace(filters))
				query += $",{filters}";

			var json = FetchJson($"search/{version}/{query}");
			Dictionary<string, SearchPackage> packages = new Dictionary<string, SearchPackage>();
			try
			{
				// if packages is empty it turns into an array[] otherwise its a dictionary :/
				packages = JsonSerializer.Deserialize<ArtifactsSearchResponse>(json).Packages;
			}
			catch { }

			if (packages == null || packages.Count == 0) return false;
			var list = packages
				.OrderByDescending(k => k.Value.Classifier == null ? 1 : 0)
				.ToArray();

			var ext = OsMonikers.CurrentPlatformArchiveExtension();
			var shouldEndWith = $"{version}.{ext}";
			if (product.PlatformDependent && version > product.PlatformSuffixAfter)
				shouldEndWith = $"{version}-{OsMonikers.CurrentPlatformPackageSuffix()}.{ext}";
			foreach (var kv in list)
			{
				if (product.PlatformDependent && !kv.Key.EndsWith(shouldEndWith)) continue;


				var tokens = PackageProductRegex.Split(kv.Key).Where(s=>!string.IsNullOrWhiteSpace(s)).ToArray();
				if (tokens.Length < 2) continue;

				if (!tokens[0].Equals(p, StringComparison.CurrentCultureIgnoreCase)) continue;
				if (!tokens[1].Equals(version.ToString(), StringComparison.CurrentCultureIgnoreCase)) continue;
				// https://snapshots.elastic.co/7.4.0-677857dd/downloads/elasticsearch-plugins/analysis-icu/analysis-icu-7.4.0-SNAPSHOT.zip
				var buildHash = GetBuildHash(kv.Value.DownloadUrl);
				artifact = new Artifact(product, version, kv.Value, buildHash);
			}
			return false;
		}

		private static string GetBuildHash(string url)
		{
			var tokens = BuildHashRegex.Split(url).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
			if (tokens.Length < 2) return null;

			return tokens[1];
		}

		private static IReadOnlyCollection<Version> LoadVersions()
		{
			var json = FetchJson("versions");
			var versions = JsonSerializer.Deserialize<ArtifactsVersionsResponse>(json).Versions;

			return new List<Version>(versions.Select(v => new Version(v)));
		}

		public static Version LatestReleaseOrSnapshot => AvailableVersions.Value.OrderByDescending(v => v).First();

		public static Version LatestSnapshotForMajor(int major)
		{
			var range = new Range($"~{major}");
			return AvailableVersions.Value
				.Reverse()
				.FirstOrDefault(v => v.PreRelease == "SNAPSHOT" && range.IsSatisfied(v.ToString().Replace("-SNAPSHOT", "")));
		}

		public static Version LatestReleaseOrSnapshotForMajor(int major)
		{
			var range = new Range($"~{major}");
			return AvailableVersions.Value
				.Reverse()
				.FirstOrDefault(v => range.IsSatisfied(v.ToString().Replace("-SNAPSHOT", "")));
		}

		private static HttpClient HttpClient { get; } = new HttpClient(new HttpClientHandler
		{
			SslProtocols = SslProtocols.Tls12
		}) { BaseAddress = new Uri(ArtifactsApiUrl) };

		private static string FetchJson(string path)
		{
			using (var stream = HttpClient.GetStreamAsync(path).GetAwaiter().GetResult())
			using (var fileStream = new StreamReader(stream))
				return fileStream.ReadToEnd();
		}

		private class ArtifactsVersionsResponse
		{
			[JsonPropertyName("versions")]
			public List<string> Versions { get; set; }
		}
		private class ArtifactsSearchResponse
		{
			[JsonPropertyName("packages")]
			public Dictionary<string, SearchPackage> Packages { get; set; }
		}

		internal class SearchPackage
		{
			[JsonPropertyName("url")] public string DownloadUrl { get; set; }
			[JsonPropertyName("sha_url")] public string ShaUrl { get; set; }
			[JsonPropertyName("asc_url")] public string AscUrl { get; set; }
			[JsonPropertyName("type")] public string Type { get; set; }
			[JsonPropertyName("architecture")] public string Architecture { get; set; }
			[JsonPropertyName("os")] public string OperatingSystem { get; set; }
			[JsonPropertyName("classifier")] public string Classifier { get; set; }
		}

	}
}
