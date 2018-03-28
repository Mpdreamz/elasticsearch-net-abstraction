﻿using Elastic.Managed.Configuration;

namespace Elastic.Managed.FileSystem
{
	public interface INodeFileSystem
	{
		string Binary { get; }
		string PluginBinary { get; }
		string ElasticsearchHome { get; }
		string ConfigPath { get; }
		string DataPath { get; }
		string LogsPath { get; }
		string RepositoryPath { get; }
		string LocalFolder { get; }
	}
}
