using KdSoft.EtwEvents.Client.Shared;
using KdSoft.EtwEvents.EventSinks;
using KdSoft.EtwEvents.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                .UseWindowsService()
                .ConfigureServices((hostContext, services) => {
                    services.Configure<ControlOptions>(opts => {
                        hostContext.Configuration.GetSection("Control").Bind(opts);
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
