// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");
using System.Runtime.CompilerServices;
using KdSoft.EtwEvents.AgentCommand;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo("KdSoft.EtwEvents.Tests")]

var bootstrapBuilder = new ConfigurationBuilder();
bootstrapBuilder.AddCommandLine(args);
var bootstrap = bootstrapBuilder.Build();
var configFile = bootstrap["config"] ?? "appsettings.json";

var cfgBuilder = new ConfigurationBuilder();
cfgBuilder.AddJsonFile(configFile, false);
cfgBuilder.AddCommandLine(args);
var cfg = cfgBuilder.Build();

var certFactory = new CertificateFactory(cfg);
Console.WriteLine(cfg["host"]);
