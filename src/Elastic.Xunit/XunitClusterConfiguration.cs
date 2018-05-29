﻿using System;
using Elastic.Managed.Configuration;
using Elastic.Managed.Ephemeral;
using Elastic.Managed.Ephemeral.Plugins;

namespace Elastic.Xunit
{
	public class XunitClusterConfiguration : EphemeralClusterConfiguration
	{
		public XunitClusterConfiguration(
			ElasticsearchVersion version,
			ClusterFeatures features = ClusterFeatures.None,
			ElasticsearchPlugins plugins = null,
			int numberOfNodes = 1)
			: base(version, features, plugins, numberOfNodes)
		{
			this.AdditionalAfterStartedTasks.Add(new PrintXunitAfterStartedTask());
		}

		/// <inheritdoc />
		protected override string NodePrefix => "xunit";

		/// <summary>
		/// The maximum number of tests that can run concurrently against a cluster using this configuration.
		/// </summary>
		public int MaxConcurrency { get; set; }

		/// <summary>
		/// The maximum amount of time a cluster can run using this configuration.
		/// </summary>
		public TimeSpan? Timeout { get; set; }
	}
}
