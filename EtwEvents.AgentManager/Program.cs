using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;

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
                    webBuilder.UseStartup<Startup>();
                    webBuilder.ConfigureKestrel((context, options) => {
                        options.Limits.MinRequestBodyDataRate = null;
                        options.ConfigureHttpsDefaults(opts => {
                            opts.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                            opts.ClientCertificateValidation = (cert, chain, errors) => {
                                if (chain != null) {
                                    var clientThumbprint = context.Configuration["ClientValidation:RootCertificateThumbprint"];
                                    foreach (var chainElement in chain.ChainElements) {
                                        if (chainElement.Certificate.Thumbprint.ToUpperInvariant() == clientThumbprint?.ToUpperInvariant())
                                            return true;
                                    }
                                    var agentThumbprint = context.Configuration["AgentValidation:RootCertificateThumbprint"];
                                    foreach (var chainElement in chain.ChainElements) {
                                        if (chainElement.Certificate.Thumbprint.ToUpperInvariant() == agentThumbprint?.ToUpperInvariant())
                                            return true;
                                    }
                                }
                                return false;
                            };
                        });
                    });
                });
    }
}
