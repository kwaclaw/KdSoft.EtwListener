using System;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace KdSoft.Logging {
    internal class RollingFileLoggerOptionsSetup: NamedConfigureFromConfigurationOptions<RollingFileLoggerOptions> {
        public RollingFileLoggerOptionsSetup(string optionsName, ILoggerProviderConfiguration<RollingFileLoggerProvider> providerConfiguration)
            : base(optionsName, providerConfiguration.Configuration) { }
    }

    [UnsupportedOSPlatform("browser")]
    public static class RollingFileLoggerExtensions {
        static ILoggingBuilder AddRollingFileSink(this ILoggingBuilder builder, string optionsName, Func<IServiceProvider, RollingFileLoggerProvider> providerFactory) {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, RollingFileLoggerProvider>(providerFactory));

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<RollingFileLoggerOptions>, RollingFileLoggerOptionsSetup>(
                sp => new RollingFileLoggerOptionsSetup(optionsName, sp.GetRequiredService<ILoggerProviderConfiguration<RollingFileLoggerProvider>>())
            ));

            return builder;
        }

        /// <summary>
        /// Adds a rolling file logger to the factory.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        public static ILoggingBuilder AddRollingFileSink(this ILoggingBuilder builder) {
            return builder.AddRollingFileSink(Options.DefaultName, sp => new RollingFileLoggerProvider(sp.GetRequiredService<IOptions<RollingFileLoggerOptions>>()));
        }

        /// <summary>
        /// Adds a console logger named 'Console' to the factory.
        /// </summary>
        /// <param name="builder">The <see cref="ILoggingBuilder"/> to use.</param>
        /// <param name="configure">A delegate to configure the <see cref="ConsoleLogger"/>.</param>
        public static ILoggingBuilder AddRollingFileSink(this ILoggingBuilder builder, Action<RollingFileLoggerOptions> configure) {
            if (configure == null) {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddRollingFileSink();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
