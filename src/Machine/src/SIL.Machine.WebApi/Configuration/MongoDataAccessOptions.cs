﻿namespace SIL.Machine.WebApi.Configuration
{
	public class MongoDataAccessOptions
	{
		public string ConnectionString { get; set; } = "mongodb://localhost:27017";
		public string DatabaseName { get; set; } = "machine";
	}
}
