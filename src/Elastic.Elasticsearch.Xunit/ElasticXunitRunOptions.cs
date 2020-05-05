﻿// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Elastic.Elasticsearch.Xunit.XunitPlumbing;
using Elastic.Stack.ArtifactsApi;

namespace Elastic.Elasticsearch.Xunit
{
	/// <summary>
	/// The Xunit test runner options
	/// </summary>
	public class ElasticXunitRunOptions
	{
		/// <summary>
		/// Informs the runner whether we expect to run integration tests. Defaults to <c>true</c>
		/// </summary>
		public bool RunIntegrationTests { get; set; } = true;

		/// <summary>
		/// Setting this to true will assume the cluster that is currently running was started for the purpose of these tests
		/// Defaults to <c>false</c>
		/// </summary>
		public bool IntegrationTestsMayUseAlreadyRunningNode { get; set; } = false;

		/// <summary>
		/// Informs the runner whether unit tests will be run. Defaults to <c>false</c>.
		/// If set to <c>true</c> and <see cref="RunIntegrationTests"/> is <c>false</c>, the runner will run all the
		/// tests in parallel with the maximum degree of parallelism
		/// </summary>
		public bool RunUnitTests { get; set; }

		/// <summary> A global test filter that can be used to only run certain tests.
		/// Accepts a comma separated list of filters
		/// </summary>
		public string TestFilter { get; set; }

		/// <summary>
		/// A global cluster filter that can be used to only run certain cluster's tests.
		/// Accepts a comma separated list of filters
		/// </summary>
		public string ClusterFilter { get; set; }

		/// <summary>
		/// Informs the runner what version of Elasticsearch is under test. Required for
		/// <see cref="SkipVersionAttribute"/> to kick in
		/// </summary>
		public ElasticVersion Version { get; set; }

		/// <summary>
		/// Called when the tests have finished running successfully
		/// </summary>
		/// <param name="runnerClusterTotals">Per cluster timings of the total test time, including starting Elasticsearch</param>
		/// <param name="runnerFailedCollections">All collection of failed cluster, failed tests tuples</param>
		public virtual void OnTestsFinished(Dictionary<string, Stopwatch> runnerClusterTotals, ConcurrentBag<Tuple<string, string>> runnerFailedCollections)
		{
		}

		/// <summary>
		/// Called before tests run. An ideal place to perform actions such as writing information to
		/// <see cref="Console"/>.
		/// </summary>
		public virtual void OnBeforeTestsRun()
		{
		}
	}
}
