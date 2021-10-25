using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KdSoft.EtwEvents.Client;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionConfig {
        readonly HostBuilderContext _context;
        readonly IOptions<EventQueueOptions> _eventQueueOptions;
        readonly ILogger<SessionConfig> _logger;
        readonly JsonSerializerOptions _jsonOptions;

        bool _optionsAvailable;
        bool _sinkProfileAvailable;

        public SessionConfig(HostBuilderContext context, IOptions<EventQueueOptions> eventQueueOptions, ILogger<SessionConfig> logger) {
            this._context = context;
            this._eventQueueOptions = eventQueueOptions;
            this._logger = logger;
            _jsonOptions = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
                WriteIndented = true
            };

            LoadSessionOptions();
            LoadSinkProfile();
        }

        public JsonSerializerOptions JsonOptions => _jsonOptions;

        EventSessionOptions _sessionOptions = new EventSessionOptions();
        public EventSessionOptions Options => _sessionOptions;
        public bool OptionsAvailable => _optionsAvailable;

        EventSinkProfile? _sinkProfile;
        public EventSinkProfile? SinkProfile => _sinkProfile;
        public bool SinkProfileAvailable => _sinkProfileAvailable;

        #region Load and Save Options

        string EventSessionOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");
        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSink.json");

        public bool LoadSessionOptions() {
            try {
                var sessionOptionsJson = File.ReadAllText(EventSessionOptionsPath);
                _sessionOptions = JsonSerializer.Deserialize<EventSessionOptions>(sessionOptionsJson, _jsonOptions) ?? new EventSessionOptions();
                _optionsAvailable = true;
                return true;
            }
            catch (Exception ex) {
                _sessionOptions = new EventSessionOptions();
                _optionsAvailable = false;
                _logger.LogError(ex, "Error loading event session options.");
                return false;
            }
        }

        public bool SaveSessionOptions(EventSessionOptions options) {
            try {
                var json = JsonSerializer.Serialize(options, _jsonOptions);
                File.WriteAllText(EventSessionOptionsPath, json);
                _sessionOptions = options;
                _optionsAvailable = true;
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event session options.");
                return false;
            }
        }

        public bool LoadSinkProfile() {
            try {
                var sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                _sinkProfile = JsonSerializer.Deserialize<EventSinkProfile>(sinkOptionsJson, _jsonOptions) ?? new EventSinkProfile();
                _sinkProfileAvailable = true;
                return true;
            }
            catch (Exception ex) {
                _sinkProfile = new EventSinkProfile();
                _logger.LogError(ex, "Error loading event sink options.");
                _sinkProfileAvailable = false;
                return false;
            }
        }

        public bool SaveSinkProfile(EventSinkProfile profile) {
            try {
                var json = JsonSerializer.Serialize(profile, _jsonOptions);
                File.WriteAllText(EventSinkOptionsPath, json);
                _sinkProfile = profile;
                _sinkProfileAvailable = true;
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event sink options.");
                return false;
            }
        }

        #endregion

        #region Provider Settings

        public bool SaveProviderSettings(IEnumerable<ProviderSetting> providers) {
            if (LoadSessionOptions()) {
                _sessionOptions.Providers = providers.Select(p => new ProviderOptions {
                    Name = p.Name,
                    Level = (Microsoft.Diagnostics.Tracing.TraceEventLevel)p.Level,
                    MatchKeywords = p.MatchKeywords
                }).ToList();
                return SaveSessionOptions(_sessionOptions);
            }
            return false;
        }

        #endregion

        #region Processing Options

        public const string NoFilter = "#no-filter#";

        public bool SaveProcessingOptions(int batchSize, int maxWriteDelayMSecs, string filter = NoFilter) {
            if (LoadSessionOptions()) {
                if (!Object.ReferenceEquals(filter, NoFilter))
                    _sessionOptions.Filter = filter;
                _sessionOptions.BatchSize = batchSize;
                _sessionOptions.MaxWriteDelayMSecs = maxWriteDelayMSecs;
                return SaveSessionOptions(_sessionOptions);
            }
            return false;
        }

        #endregion
    }
}
