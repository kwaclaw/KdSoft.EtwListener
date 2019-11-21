using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Hosting;

namespace EtwEvents.Server
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

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel((context, options) => {
                        options.Limits.MinRequestBodyDataRate = null;
                        //options.ConfigureHttpsDefaults(o => o.ClientCertificateMode = ClientCertificateMode.AllowCertificate);
                        options.ConfigureHttpsDefaults(opts => {
                            opts.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                            opts.ClientCertificateValidation = (cert, chain, errors) => {
                                var thumbprint = context.Configuration["ClientValidation:RootCertificateThumbprint"];
                                foreach (var chainElement in chain.ChainElements) {
                                    if (chainElement.Certificate.Thumbprint.ToLowerInvariant() == thumbprint.ToLowerInvariant())
                                        return true;
                                }
                                return false;
                            };
                        });
                    });
                });
    }
}
