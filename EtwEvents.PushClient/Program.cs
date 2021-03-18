using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwEvents.Server;
using KdSoft.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushClient
{
    public class Program
    {
        public static async Task Main(string[] args) {
            var host = CreateHostBuilder(args).Build();
            var cts = new CancellationTokenSource();
            var runTask = host.RunAsync(cts.Token);

            string? line;
            while ((line = Console.ReadLine()) != "quit") {
                Console.WriteLine($">> {line}");
            }

            cts.Cancel();
            await runTask.ConfigureAwait(false);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                //.ConfigureAppConfiguration((hostContext, cfgBuilder) => {
                //    cfgBuilder.AddJsonFile(provider, "appsettings.Local.json", optional: true, reloadOnChange: true);
                //})
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
                    services.Configure<ControlOptions>(opts => {
                        hostContext.Configuration.GetSection("Control").Bind(opts);
                    });
                    services.Configure<EventQueueOptions>(opts => {
                        hostContext.Configuration.GetSection("EventQueue").Bind(opts);
                        // make sure opts.LogPath is an absolute path
                        opts.LogPath = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.LogPath);
                    });
                    services.Configure<EventSessionOptions>(opts => {
                        hostContext.Configuration.GetSection("EventSession").Bind(opts);
                    });
                    services.Configure<EventSinkOptions>(opts => {
                        hostContext.Configuration.GetSection("EventSink").Bind(opts);
                    });
                    services.AddSingleton(provider => new TraceSessionManager(TimeSpan.FromMinutes(3)));
                    services.AddSingleton<IEventSinkFactory>(provider => new ElasticSinkFactory());
                    services.AddHostedService<Worker>();
                });
    }
}
