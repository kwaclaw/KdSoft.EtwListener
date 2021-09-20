using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KdSoft.EtwEvents.AgentManager.Controllers;
using KdSoft.EtwEvents.AgentManager.Services;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrchardCore.Localization;

#pragma warning disable CA1822 // Mark members as static

namespace KdSoft.EtwEvents.AgentManager
{
    public class Startup
    {
        IWebHostEnvironment _env;

        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        static Regex _roleRegex = new Regex(@"OID\.2\.5\.4\.72\s*=\s*(?<role>[^,=]*)\s*(,|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        // CalimsIdentity.Name here is the certificate's Subject Common Name (CN)
                        if (identity != null && identity.Name != null) {
                            string? certRole = null;
                            var match = _roleRegex.Match(context.ClientCertificate.Subject);
                            if (match.Success) {
                                certRole = match.Groups["role"].Value;
                            }

                            if (certRole != null && certRole.Equals("etw-pushagent", System.StringComparison.OrdinalIgnoreCase)) {
                                identity.AddClaim(new Claim(ClaimTypes.Role, Role.Agent.ToString()));
                                context.Success();
                                return Task.CompletedTask;
                            }
                            else {
                                var roleService = context.HttpContext.RequestServices.GetService<RoleService>();
                                var roles = roleService!.GetRoles(identity.Name);
                                foreach (var role in roles) {
                                    identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
                                }
                                if (roles.Count > 0) {
                                    context.Success();
                                    return Task.CompletedTask;
                                }
                            }
                        }
                        context.Fail("Not authorized.");
                        return Task.CompletedTask;
                    }
                };
            });

            // add user role service
            //TODO make this react to a reload of the config file
            var authorizedAgents = Configuration.GetSection("AgentValidation:AuthorizedCommonNames").Get<HashSet<string>>();
            var authorizedUsers = Configuration.GetSection("ClientValidation:AuthorizedCommonNames").Get<HashSet<string>>();
            services.AddSingleton(new RoleService(authorizedAgents, authorizedUsers));

            services.Configure<CookiePolicyOptions>(options => {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
            });

            services.AddSingleton<AgentProxyManager>();

            services.AddPortableObjectLocalization(options => options.ResourcesPath = "Resources");
            services.Replace(ServiceDescriptor.Singleton<ILocalizationFileLocationProvider, LocalizationFileProvider>());
            // workaround for bug in OrchardCore
            //services.TryAddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
            services.Configure<RequestLocalizationOptions>(options => {
                var supportedCultures = new List<CultureInfo> {
                    new CultureInfo("en-US"),
                    new CultureInfo("fr"),
                    new CultureInfo("es"),
                    new CultureInfo("de")
                };

                options.DefaultRequestCulture = new RequestCulture("en-US");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });

            services.AddRazorPages();

            services.AddControllers()
                .AddJsonOptions(opts => {
                    opts.JsonSerializerOptions.Converters.Add(new TimeSpanISO8601JsonConverter());
                    opts.JsonSerializerOptions.Converters.Add(new NullableTimeSpanISO8601JsonConverter());
                })
                .ConfigureApplicationPartManager(manager => {
                    manager.FeatureProviders.Add(new AgentControllerFeatureProvider());
                    manager.FeatureProviders.Add(new ManagerControllerFeatureProvider());
                });


            //services.AddSingleton<AppSecretsHolder>(provider => {
            //    var secretsPath = Path.Combine(_env.ContentRootPath, "appsecrets.json");
            //    var localizerFactory = provider.GetService<IStringLocalizerFactory>();
            //    var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
            //    var result = new AppSecretsHolder(secretsPath, "KdSoft-EtwEvents-Secrets", dataProtectionProvider, localizerFactory);
            //    result.EnsureProtected();
            //    return result;
            //});

            services.AddSpaStaticFiles(configuration => {
                configuration.RootPath = ".";
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env) {
            Contract.Assert(app != null);
            Contract.Assert(env != null);

            this._env = env;

            if (env.IsDevelopment()) {
                app.UseExceptionHandler("/error-local-development");
            }
            else {
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            // depending on development or production mode, we have to initialize the secrets differently
            //var appSecretsPath = Path.Combine(env.ContentRootPath, "appsecrets.json");
            //PrepareSecrets(env, appSecretsPath);

            app.UseRequestLocalization();

            app.UseStaticFiles();
            if (env.IsDevelopment()) {
                app.UseSpaStaticFiles();
            }

            app.UseRouting();

            // app.UseCertificateForwarding();  // for IIS
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {
                endpoints.MapRazorPages();
                endpoints.MapControllers();
            });

            if (env.IsDevelopment()) {
                app.UseSpa(spa => {
                    spa.Options.SourcePath = ".";
                    //spa.Options.DevServerPort = 41000;
                    spa.UseReactDevelopmentServer(npmScript: "dev");
                });
            }
        }

        /// <summary>
        /// Prepare secrets files, depending on development or production mode.
        /// In development mode: if the secrets file is missing, it will be copied from the clearText file.
        /// In production mode: If the clearText file is present, it will be renamed to the secrets file, replacing it.
        /// On first run of the web application, the secrets file will be encrypted if it is in clear text.
        /// NOTE: only the clear text file may be published.
        /// </summary>
        //void PrepareSecrets(IWebHostEnvironment env, string secretsPath) {
        //    var clearTextPath = secretsPath + ".clearText";
        //    if (env.IsDevelopment()) {
        //        if (!File.Exists(secretsPath))
        //            File.Copy(clearTextPath, secretsPath);
        //    }
        //    else {
        //        if (File.Exists(clearTextPath)) {
        //            File.Delete(secretsPath);
        //            File.Move(clearTextPath, secretsPath);
        //        }
        //    }
        //}
    }

    class AgentControllerFeatureProvider: ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) {
            var isController = !typeInfo.IsAbstract && typeof(AgentController).IsAssignableFrom(typeInfo);
            return isController || base.IsController(typeInfo);
        }
    }

    class ManagerControllerFeatureProvider: ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) {
            var isController = !typeInfo.IsAbstract && typeof(ManagerController).IsAssignableFrom(typeInfo);
            return isController || base.IsController(typeInfo);
        }
    }
}
