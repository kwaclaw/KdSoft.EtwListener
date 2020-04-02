using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;

namespace EtwEvents.WebClient
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
                                var thumbprint = context.Configuration["ClientValidation:RootCertificateThumbprint"];
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
