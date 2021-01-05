using System;
using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.Server
{
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

        // fix up log file base name to become a template for date-based log files
        static string MakeLogFileTemplate(string logFilePath) {
            var logDir = Path.GetDirectoryName(logFilePath) ?? "";
            if (logDir == string.Empty || !Path.IsPathFullyQualified(logFilePath)) {
                logDir = Path.Combine(AppContext.BaseDirectory, logDir);
            }
            var baseFileName = Path.GetFileNameWithoutExtension(logFilePath);
            var ext = Path.GetExtension(logFilePath);
            var logFileNameTemplate = $"{baseFileName}_{{0:yyyy}}-{{0:MM}}-{{0:dd}}{ext}";
            var logFilePathTemplate = Path.Combine(logDir, logFileNameTemplate);
            return logFilePathTemplate;
        }


        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) => {
                    var env = hostingContext.HostingEnvironment;
                    var provider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, ".."));
                    config.AddJsonFile(provider, "appsettings.Local.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging((context, loggingBuilder) => {
                    var loggingSection = context.Configuration.GetSection("Logging");
                    // fix up log file path to become a template for date-based log files
                    var logFilePathTemplate = MakeLogFileTemplate(loggingSection["File:Path"]);
                    loggingSection["File:Path"] = logFilePathTemplate;

                    loggingBuilder.AddFile(loggingSection, loggerOpts => {
                        var startDate = DateTimeOffset.MinValue.Date;
                        var logFileName = string.Format(logFilePathTemplate, startDate);
                        loggerOpts.FormatLogFileName = logFileTemplate => {
                            var today = DateTimeOffset.Now.Date;
                            if (today != startDate) {
                                startDate = today;
                                logFileName = String.Format(logFileTemplate, today);
                            }
                            return logFileName;
                        };
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
