using System.IO;
using KdSoft.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.AgentManager
{
    public static class Program
    {
        public static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder.ConfigureAppConfiguration((hostContext, cfgBuilder) => {
                        // we are overriding some of the settings that are already loaded
                        cfgBuilder.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
                        cfgBuilder.AddJsonFile("authorization.json", optional: true, reloadOnChange: true);
                        cfgBuilder.AddCommandLine(args);
                    });
                    webBuilder.ConfigureLogging((hostContext, loggingBuilder) => {
                        loggingBuilder.ClearProviders();
#if DEBUG
                        loggingBuilder.AddConsole();
#endif
                        loggingBuilder.AddRollingFileSink(opts => {
                            // make sure opts.Directory is an absolute path
                            opts.Directory = Path.Combine(hostContext.HostingEnvironment.ContentRootPath, opts.Directory);
                        });
                    });
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel((context, options) => {
                        options.Limits.MinRequestBodyDataRate = null;
                        options.ConfigureHttpsDefaults(opts => {
                            opts.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                            opts.CheckCertificateRevocation = false;
                            // This callback is part of the certificate negotiation and forces the browser
                            // to re-trigger the client certificate dialog when the certificate does not validate.
                            // If we rejected a certificate later (e.g. in the CertificateAuthenticationEvents), then
                            // the browser would still think that the certificate itself is valid, and it would not
                            // offer the choice to select another client certificate on page reload.
                            var authService = options.ApplicationServices.GetRequiredService<AuthorizationService>();
                            opts.ClientCertificateValidation = authService.ValidateClientCertificate;
                        });
                    });
                })
                .UseWindowsService();
    }
}
