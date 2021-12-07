using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using KdSoft.EtwLogging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionConfig
    {
        readonly HostBuilderContext _context;
        readonly ILogger<SessionConfig> _logger;
        readonly JsonFormatter _jsonFormatter;

        bool _stateAvailable;
        bool _sinkProfileAvailable;

        public SessionConfig(HostBuilderContext context, ILogger<SessionConfig> logger) {
            this._context = context;
            this._logger = logger;
            var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            _jsonFormatter = new JsonFormatter(jsonSettings);

            LoadSessionState();
            LoadSinkProfile();
        }

        public JsonFormatter JsonFormatter => _jsonFormatter;

        EventSessionState _sessionState = new();
        public EventSessionState State => _sessionState;
        public bool StateAvailable => _stateAvailable;

        EventSinkProfile? _sinkProfile;
        public EventSinkProfile? SinkProfile => _sinkProfile;
        public bool SinkProfileAvailable => _sinkProfileAvailable;

        #region Load and Save Options

        string EventSessionStatePath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");
        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSink.json");

        public bool LoadSessionState() {
            try {
                var sessionStateJson = File.ReadAllText(EventSessionStatePath);
                _sessionState = string.IsNullOrWhiteSpace(sessionStateJson)
                    ? new EventSessionState()
                    : EventSessionState.Parser.ParseJson(sessionStateJson);
                _stateAvailable = true;
                return true;
            }
            catch (Exception ex) {
                _sessionState = new EventSessionState();
                _stateAvailable = false;
                _logger.LogError(ex, "Error loading event session options.");
                return false;
            }
        }

        public bool SaveSessionState(EventSessionState state) {
            try {
                var json = _jsonFormatter.Format(state);
                File.WriteAllText(EventSessionStatePath, json);
                _sessionState = state;
                _stateAvailable = true;
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
                _sinkProfile = string.IsNullOrWhiteSpace(sinkOptionsJson)
                    ? new EventSinkProfile()
                    : EventSinkProfile.Parser.ParseJson(sinkOptionsJson);
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
                var json = _jsonFormatter.Format(profile);
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
            LoadSessionState();
            _sessionState.ProviderSettings.Clear();
            _sessionState.ProviderSettings.AddRange(providers);
            return SaveSessionState(_sessionState);
        }

        #endregion

        #region Processing State

        public bool SaveProcessingState(ProcessingState state, bool updateFilterSource) {
            LoadSessionState();
            _sessionState.ProcessingState.BatchSize = state.BatchSize;
            _sessionState.ProcessingState.MaxWriteDelayMSecs = state.MaxWriteDelayMSecs;
            if (updateFilterSource) {
                _sessionState.ProcessingState.FilterSource = state.FilterSource;
            }
            return SaveSessionState(_sessionState);
        }

        #endregion
    }
}
