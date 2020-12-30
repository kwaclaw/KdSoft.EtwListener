using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CA1801 // Review unused parameters

namespace KdSoft.EtwEvents.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // fix up log file base name to become a template for date-based log files
        string MakeLogFileTemplate(string logFilePath) {
            var logDir = Path.GetDirectoryName(logFilePath);
            if (string.IsNullOrEmpty(logDir))
                logDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var baseFileName = Path.GetFileNameWithoutExtension(logFilePath);
            var ext = Path.GetExtension(logFilePath);
            var logFileNameTemplate = $"{baseFileName}_{{0:yyyy}}-{{0:MM}}-{{0:dd}}{ext}";
            var logFilePathTemplate = Path.Combine(logDir, logFileNameTemplate);
            return logFilePathTemplate;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services) {
            // NOTE: None of this seems to work with Grpc, OnCertificateValidated is never called
            //       and consequently we cannot use the standard Authorize attribute and authorization policies.
            //       Therefore we are using an interceptor instead, which is likely more efficient.

            //services.AddAuthorization();
            //services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme).AddCertificate(options => {
            //    options.AllowedCertificateTypes = CertificateTypes.Chained;
            //    options.ValidateCertificateUse = true;
            //    options.Events = new CertificateAuthenticationEvents {
            //        OnAuthenticationFailed = context => {
            //            return Task.CompletedTask;
            //        },
            //        OnCertificateValidated = context => {
            //            var claims = new[]
            //            {
            //                new Claim(
            //                    ClaimTypes.NameIdentifier,
            //                    context.ClientCertificate.Subject,
            //                    ClaimValueTypes.String,
            //                    context.Options.ClaimsIssuer),
            //                new Claim(ClaimTypes.Name,
            //                    context.ClientCertificate.Subject,
            //                    ClaimValueTypes.String,
            //                    context.Options.ClaimsIssuer)
            //            };

            //            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
            //            context.Success();

            //            return Task.CompletedTask;
            //        }
            //    };
            //});

            services.AddGrpc(opts => {
                var authorizedNames = Configuration.GetSection("ClientValidation:AuthorizedCommonNames").Get<HashSet<string>>();
                opts.Interceptors.Add<AuthInterceptor>(authorizedNames);
            });

            services.AddSingleton<TraceSessionManager>(provider => new TraceSessionManager(TimeSpan.FromMinutes(3)));

            services.AddLogging(loggingBuilder => {
                var loggingSection = Configuration.GetSection("Logging");
                // fix up log file path to become a template for date-based log files
                var logFilePathTemplate = MakeLogFileTemplate(loggingSection["File:Path"]);
                loggingSection["File:Path"] = logFilePathTemplate;

                loggingBuilder.AddFile(loggingSection, loggerOpts => {
                    var startDate = DateTimeOffset.MinValue.Date;
                    var logFileName = String.Format(logFilePathTemplate, startDate);
                    loggerOpts.FormatLogFileName = logFileTemplate => {
                        var today = DateTimeOffset.Now.Date;
                        if (today != startDate) {
                            startDate = today;
                            logFileName = String.Format(logFileTemplate, today);
                        }
                        return logFileName;
                    };
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            //if (env.IsDevelopment()) {
            //    app.UseDeveloperExceptionPage();
            //}

            app.UseRouting();

            //NOTE: We are using Grpc interceptors instead
            //app.UseAuthentication();
            //app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                // Communication with gRPC endpoints must be made through a gRPC client.
                // To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909
                endpoints.MapGrpcService<EtwListenerService>();
            });
        }
    }
}
