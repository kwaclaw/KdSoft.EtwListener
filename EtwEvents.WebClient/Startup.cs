using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EtwEvents.WebClient.Models;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

#pragma warning disable CA1822 // Mark members as static

namespace EtwEvents.WebClient
{
    public class Startup
    {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            // for IIS
            //services.AddCertificateForwarding(options => {
            //    options.CertificateHeader = "X-ARR-ClientCert";
            //    options.HeaderConverter = (headerValue) => {
            //        X509Certificate2? clientCertificate = null;
            //        if (!string.IsNullOrWhiteSpace(headerValue)) {
            //            var bytes = Encoding.ASCII.GetBytes(headerValue);
            //            clientCertificate = new X509Certificate2(bytes);
            //        }

            //        return clientCertificate;
            //    };
            //});

            services.AddAuthorization();
            services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme).AddCertificate(options => {
                options.AllowedCertificateTypes = CertificateTypes.Chained;
                // my custom certificates can't be checked, so they would not be validated
                options.RevocationMode = X509RevocationMode.NoCheck;
                // my custom certificates may not have the enhanced key usage flags set
                options.ValidateCertificateUse = false;
                options.Events = new CertificateAuthenticationEvents {
                    //OnAuthenticationFailed = context => {
                    //    return Task.CompletedTask;
                    //},
                    OnCertificateValidated = context => {
                        var authService = context.HttpContext.RequestServices.GetService<AuthService>();
                        if (authService.IsAuthorized(context.Principal))
                            context.Success();
                        else
                            context.Fail("User not authorized.");
                        return Task.CompletedTask;
                    }
                };
            });

            var authorizedNames = Configuration.GetSection("ClientValidation:AuthorizedCommonNames").Get<HashSet<string>>();
            services.AddSingleton(new AuthService(authorizedNames));

            services.Configure<CookiePolicyOptions>(options => {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
            });

            services.Configure<EventSessionOptions>(Configuration.GetSection("EventSessionOptions"));
            services.Configure<ClientCertOptions>(Configuration.GetSection("ClientCertificate"));

            services.AddSingleton<TraceSessionManager>();

            services.AddRazorPages();

            services.AddControllers()
                .AddJsonOptions(opts => {
                    opts.JsonSerializerOptions.Converters.Add(new TimeSpanISO8601JsonConverter());
                    opts.JsonSerializerOptions.Converters.Add(new NullableTimeSpanISO8601JsonConverter());
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseExceptionHandler("/error-local-development");
            }
            else {
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseRequestLocalization();

            var webSocketOptions = new WebSocketOptions() {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);

            app.UseStaticFiles();

            app.UseCookiePolicy();

            app.UseRouting();

            // app.UseCertificateForwarding();  // for IIS
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });
        }
    }
}
