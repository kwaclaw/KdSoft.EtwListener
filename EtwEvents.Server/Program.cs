using System.Diagnostics;
using System.IO;
using KdSoft.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KdSoft.EtwEvents.Server {
    public static class Program
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

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) => {
                    var env = hostingContext.HostingEnvironment;
                    var provider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, ".."));
                    config.AddJsonFile(provider, "appsettings.Local.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging((context, loggingBuilder) => {
                    loggingBuilder.AddRollingFileSink(opts => {
                        // make sure opts.Directory is an absolute path
                        opts.Directory = Path.Combine(context.HostingEnvironment.ContentRootPath, opts.Directory);
                    });
                })
                .UseWindowsService()
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel((context, options) => {
                        options.Limits.MinRequestBodyDataRate = null;
                        //options.ConfigureHttpsDefaults(o => o.ClientCertificateMode = ClientCertificateMode.AllowCertificate);
                        options.ConfigureHttpsDefaults(opts => {
                            opts.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                            opts.ClientCertificateValidation = (cert, chain, errors) => {
                                var thumbprint = context.Configuration["ClientValidation:RootCertificateThumbprint"];
                                if (string.IsNullOrEmpty(thumbprint))
                                    return true;
                                foreach (var chainElement in chain.ChainElements) {
                                    if (chainElement.Certificate.Thumbprint.ToUpperInvariant() == thumbprint.ToUpperInvariant())
                                        return true;
                                }
                                return false;
                            };
                        });
                    });
                });
    }
}
