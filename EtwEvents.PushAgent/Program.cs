using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.PushAgent;
using KdSoft.EtwEvents.Server;
using KdSoft.Logging;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Options;

[assembly: InternalsVisibleTo("EtwEvents.Tests")]

// Today you have to be Admin to turn on ETW events (anyone can write ETW events).   
if (!(TraceEventSession.IsElevated() ?? false)) {
    Debug.WriteLine("To turn on ETW events you need to be Administrator, please run from an Admin process.");
    Debugger.Break();
    return;
}

#region Initial Configuration

const string EventSinksDirectory = "EventSinks";

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((hostContext, cfgBuilder) => {
    // we are overriding some of the settings that are already loaded
    cfgBuilder.AddJsonFile("appsettings.Personal.json", optional: true, reloadOnChange: true);
    cfgBuilder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    cfgBuilder.AddCommandLine(args);
});

builder.ConfigureLogging((hostContext, loggingBuilder) => {
    loggingBuilder.ClearProviders();
#if DEBUG
    loggingBuilder.AddConsole();
#endif
    loggingBuilder.AddRollingFileSink(opts => {
        // make sure opts.Directory is an absolute path
        opts.Directory = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.Directory);
    });
});

#endregion

builder.UseWindowsService();

builder.ConfigureServices((hostContext, services) => {
    // this section will be monitored for changes, so we cannot use Bind()
    services.Configure<ControlOptions>(hostContext.Configuration.GetSection("Control"));
    services.Configure<DataProtectionOptions>(hostContext.Configuration.GetSection("DataProtection"));
    services.Configure<EventQueueOptions>(opts => {
        hostContext.Configuration.GetSection("EventQueue").Bind(opts);
        // make sure opts.LogPath is an absolute path
        opts.BaseDirectory = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.BaseDirectory);
    });
    services.AddSingleton<SessionConfig>();
    services.AddSingleton<SocketsHandlerCache>();
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
            provider.GetRequiredService<SocketsHandlerCache>(),
            provider.GetRequiredService<ILogger<EventSinkService>>()
        );
    });
    services.AddSingleton(Channel.CreateUnbounded<ControlEvent>(new UnboundedChannelOptions {
        AllowSynchronousContinuations = false,
        SingleReader = true,
        SingleWriter = false
    }));
    services.AddSingleton<Func<IRetryStrategy>>(() => new BackoffRetryStrategy(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromHours(2),
        forever: true
    ));
    services.AddSingleton<ControlConnector>();
    services.AddScoped<SessionWorker>();
    services.AddHostedService<ControlWorker>();
});

builder.Build().Run();
