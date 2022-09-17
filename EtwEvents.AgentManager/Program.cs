using System.Globalization;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Google.Protobuf;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.AgentManager;
using KdSoft.Logging;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

#region Initial Configuration

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile("authorization.json", optional: true, reloadOnChange: true);
builder.Configuration.AddCommandLine(args);

builder.Logging.ClearProviders();
#if DEBUG
builder.Logging.AddConsole();
#endif
builder.Logging.AddRollingFileSink(opts => {
    // make sure opts.Directory is an absolute path
    opts.Directory = Path.Combine(builder.Environment.ContentRootPath, opts.Directory);
});

builder.WebHost.ConfigureKestrel((context, options) => {
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

builder.Host.UseWindowsService();

#endregion

#region Add services to the container.

// for IIS
//builder.Services.AddCertificateForwarding(options => {
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

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme).AddCertificate(options => {
    options.AllowedCertificateTypes = CertificateTypes.Chained;
    // my custom certificates can't be checked for revocation (online or offline), so they would not validate
    options.RevocationMode = X509RevocationMode.NoCheck;
    // my custom certificates may not have the enhanced key usage flags set
    //options.ValidateCertificateUse = false;
    options.Events = new CertificateAuthenticationEvents {
        //OnAuthenticationFailed = context => {
        //    return Task.CompletedTask;
        //},
        OnChallenge = (context) => {  // does not seem to be called! should be invoked for each client certificate?
            //var clientCert = context.HttpContext.Connection.ClientCertificate;
            return Task.CompletedTask;
        },
        OnCertificateValidated = context => {
            var authService = context.HttpContext.RequestServices.GetRequiredService<AuthorizationService>();
            var success = authService!.AuthorizePrincipal(context, Role.Agent, Role.Manager);

            if (success) {
                context.Success();
            }
            else
                context.Fail("Not authorized.");
            return Task.CompletedTask;
        }
    };
});

// this section will be monitored for changes, so we cannot use Bind()
builder.Services.Configure<AuthorizationOptions>(builder.Configuration.GetSection("AuthorizationOptions"));

builder.Services.AddSingleton<AuthorizationService>();

builder.Services.Configure<CookiePolicyOptions>(options => {
    // This lambda determines whether user consent for non-essential cookies is needed for a given request.
    options.CheckConsentNeeded = context => true;
});

builder.Services.AddSingleton<AgentProxyManager>();
builder.Services.AddSingleton(provider => new EventSinkProvider(
    builder.Environment.ContentRootPath,
    "EventSinks",
    "EventSinksCache"
));
builder.Services.AddSingleton(provider => {
    var jsonSettings = JsonFormatter.Settings.Default
        .WithFormatDefaultValues(true)
        .WithFormatEnumsAsIntegers(true);
    return new JsonFormatter(jsonSettings);
});

//builder.Services.AddSingleton<AppSecretsHolder>(provider => {
//    var secretsPath = Path.Combine(builder.Environment.ContentRootPath, "appsecrets.json");
//    var localizerFactory = provider.GetService<IStringLocalizerFactory>();
//    var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
//    var result = new AppSecretsHolder(secretsPath, "KdSoft-EtwEvents-Secrets", dataProtectionProvider, localizerFactory);
//    result.EnsureProtected();
//    return result;
//});

builder.Services.AddPortableObjectLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Replace(ServiceDescriptor.Singleton<ILocalizationFileLocationProvider, LocalizationFileProvider>());
builder.Services.Configure<RequestLocalizationOptions>(options => {
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

builder.Services.AddRazorPages();

builder.Services.AddControllers()
    .AddJsonOptions(opts => {
        opts.JsonSerializerOptions.Converters.Add(new TimeSpanISO8601JsonConverter());
        opts.JsonSerializerOptions.Converters.Add(new NullableTimeSpanISO8601JsonConverter());
    })
    .ConfigureApplicationPartManager(manager => {
        manager.FeatureProviders.Add(new AgentControllerFeatureProvider());
        manager.FeatureProviders.Add(new ManagerControllerFeatureProvider());
    });

builder.Services.Configure<ApiBehaviorOptions>(options => {
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddGrpc(opts => {
    opts.Interceptors.Add<AuthInterceptor>();
});

builder.Services.AddSingleton<CertificateFileService>(provider => {
    var certDir = Path.Combine(builder.Environment.ContentRootPath, "AgentCerts");
    var authOptsMonitor = provider.GetRequiredService<IOptionsMonitor<AuthorizationOptions>>();
    var logger = provider.GetRequiredService<ILogger<CertificateFileService>>();
    return new CertificateFileService(new DirectoryInfo(certDir), authOptsMonitor, logger);
});
builder.Services.AddHostedService<CertificateFileService>(provider => provider.GetRequiredService<CertificateFileService>());

builder.Services.AddSingleton<AgentCertificateWatcher>(provider => {
    var certDir = Path.Combine(builder.Environment.ContentRootPath, "AgentCerts");
    var logger = provider.GetRequiredService<ILogger<AgentCertificateWatcher>>();
    var proxyMgr = provider.GetRequiredService<AgentProxyManager>();
    return new AgentCertificateWatcher(new DirectoryInfo(certDir), proxyMgr, logger);
});
builder.Services.AddHostedService<AgentCertificateWatcher>(provider => provider.GetRequiredService<AgentCertificateWatcher>());

#endregion

#region Application

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/error-local-development");
}
else {
    app.UseExceptionHandler("/error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// depending on development or production mode, we have to initialize the secrets differently
//var appSecretsPath = Path.Combine(app.Environment.ContentRootPath, "appsecrets.json");
//PrepareSecrets(app.Environment, appSecretsPath);

app.UseRequestLocalization();

app.UseStaticFiles();

app.UseRouting();

// app.UseCertificateForwarding();  // for IIS
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints => {
    endpoints.MapRazorPages();
    endpoints.MapControllers();
    endpoints.MapGrpcService<LiveViewSinkService>();
});

app.Run();

#endregion

#region Private

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

#endregion