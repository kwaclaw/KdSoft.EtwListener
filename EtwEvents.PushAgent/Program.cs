using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using KdSoft.EtwEvents.Server;
using KdSoft.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("EtwEvents.Tests")]

namespace KdSoft.EtwEvents.PushAgent
{
    public class Program
    {
        public static void Main(string[] args) {
            // Today you have to be Admin to turn on ETW events (anyone can write ETW events).   
            if (!(TraceEventSession.IsElevated() ?? false)) {
                Debug.WriteLine("To turn on ETW events you need to be Administrator, please run from an Admin process.");
                Debugger.Break();
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        const string EventSinksDirectory = "EventSinks";

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, cfgBuilder) => {
                    // we are overriding some of the settings that are already loaded
                    cfgBuilder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                    cfgBuilder.AddCommandLine(args);
                })
                .ConfigureLogging((hostContext, loggingBuilder) => {
                    loggingBuilder.ClearProviders();
#if DEBUG
                    loggingBuilder.AddConsole();
#endif
                    loggingBuilder.AddRollingFileSink(opts => {
                        // make sure opts.Directory is an absolute path
                        opts.Directory = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.Directory);
                    });
                })
                .UseWindowsService()
                .ConfigureServices((hostContext, services) => {
                    // this section will be monitored for changes, so we cannot use Bind()
                    services.Configure<ControlOptions>(hostContext.Configuration.GetSection("Control"));
                    services.Configure<EventQueueOptions>(opts => {
                        hostContext.Configuration.GetSection("EventQueue").Bind(opts);
                        // make sure opts.LogPath is an absolute path
                        opts.BaseDirectory = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.BaseDirectory);
                    });
                    services.AddSingleton<SessionConfig>();
                    services.AddSingleton(provider => {
                        var options = provider.GetRequiredService<IOptions<ControlOptions>>();
                        var clientCert = Utils.GetClientCertificate(options.Value.ClientCertificate);
                        if (clientCert == null)
                            throw new ArgumentException("Cannot find client certificate based on specified options.", nameof(options.Value.ClientCertificate));
                        return clientCert;
                    });
                    services.AddSingleton(provider => {
                        var clientCert = provider.GetRequiredService<X509Certificate2>();
                        var handler = Utils.CreateHttpHandler(clientCert);
                        return handler;
                    });
                    services.AddSingleton(provider => new TraceSessionManager(TimeSpan.FromMinutes(3)));
                    services.AddSingleton(provider => {
                        var eventSinksDirPath = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, EventSinksDirectory);
                        // make sure the directory exists, even if empty, to avoid IO exceptions
                        Directory.CreateDirectory(eventSinksDirPath);

                        var options = provider.GetRequiredService<IOptions<ControlOptions>>();
                        return new EventSinkService(
                            hostContext.HostingEnvironment.ContentRootPath,
                            EventSinksDirectory,
                            options,
                            provider.GetRequiredService<SocketsHttpHandler>(),
                            provider.GetRequiredService<ILogger<EventSinkService>>()
                        );
                    });
                    services.AddSingleton<ControlConnector>();
                    services.AddScoped<SessionWorker>();
                    services.AddHostedService<ControlWorker>();

                    var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), nameof(KdSoft.EtwEvents.PushAgent), "Keys");
                    var dpBuilder = services.AddDataProtection(opts => {
                        opts.ApplicationDiscriminator = nameof(KdSoft.EtwEvents.PushAgent);
                    }).PersistKeysToFileSystem(
                        new DirectoryInfo(keyDirectory)
                    );
                    var provider = services.BuildServiceProvider();
                    var clientCert = provider.GetRequiredService<X509Certificate2>();
                    dpBuilder.ProtectKeysWithCertificate(clientCert);
                });
    }
}
