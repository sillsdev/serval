﻿global using System.Globalization;
global using System.IdentityModel.Tokens.Jwt;
global using System.IO.Compression;
global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using CaseExtensions;
global using Grpc.Core;
global using Grpc.Net.Client.Configuration;
global using Hangfire;
global using Hangfire.Mongo;
global using Hangfire.Mongo.Migration.Strategies;
global using Hangfire.Mongo.Migration.Strategies.Backup;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using MongoDB.Driver;
global using Nito.AsyncEx;
global using Polly;
global using Polly.Retry;
global using Serval.Aqua.Shared.Configuration;
global using Serval.Aqua.Shared.Contracts;
global using Serval.Aqua.Shared.Models;
global using Serval.Aqua.Shared.Services;
global using SIL.AspNetCore.Services;
global using SIL.AspNetCore.Utils;
global using SIL.DataAccess;
global using SIL.IO;
global using SIL.Machine.Corpora;
global using SIL.Scripture;
global using SIL.WritingSystems;