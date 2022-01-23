using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Server;
using KdSoft.Logging;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("EtwEvents.Tests")]

namespace KdSoft.EtwEvents.PushAgent
{
    public class Program
    {
        public static async Task Main(string[] args) {
            // Today you have to be Admin to turn on ETW events (anyone can write ETW events).   
            if (!(TraceEventSession.IsElevated() ?? false)) {
                Debug.WriteLine("To turn on ETW events you need to be Administrator, please run from an Admin process.");
                Debugger.Break();
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostContext, cfgBuilder) => {
                    var env = hostContext.HostingEnvironment;
                    var provider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, ".."));
                    // we are overriding some of the settings that are already loaded
                    cfgBuilder.AddJsonFile(provider, "appsettings.Local.json", optional: true, reloadOnChange: true);
                    cfgBuilder.AddCommandLine(args);
                })
                .ConfigureLogging((hostContext, loggingBuilder) => {
                    loggingBuilder.ClearProviders();
                    loggingBuilder.AddConsole();
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
                        var handler = Utils.CreateHttpHandler(options.Value.ClientCertificate);
                        return handler;
                    });
                    services.AddSingleton(provider => new TraceSessionManager(TimeSpan.FromMinutes(3)));
                    services.AddSingleton(provider => {
                        var options = provider.GetRequiredService<IOptions<ControlOptions>>();
                        return new EventSinkService(
                            hostContext.HostingEnvironment.ContentRootPath,
                            "EventSinks",
                            options,
                            provider.GetRequiredService<SocketsHttpHandler>(),
                            provider.GetRequiredService<ILogger<EventSinkService>>()
                        );
                    });
                    services.AddSingleton<ControlConnector>();
                    services.AddScoped<SessionWorker>();
                    services.AddHostedService<ControlWorker>();
                });
    }
}
