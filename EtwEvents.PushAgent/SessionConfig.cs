using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Collections;
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

        public SessionConfig(HostBuilderContext context, ILogger<SessionConfig> logger) {
            this._context = context;
            this._logger = logger;
            var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            _jsonFormatter = new JsonFormatter(jsonSettings);

            LoadSessionState();
            LoadSinkProfiles();
        }

        public JsonFormatter JsonFormatter => _jsonFormatter;

        #region SessionState

        string EventSessionStatePath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");

        EventSessionState _sessionState = new();
        public EventSessionState State => _sessionState;
        public bool StateAvailable => _stateAvailable;

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

        #endregion

        #region EventSinkProfile

        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSink.json");

        List<EventSinkProfile> _sinkProfiles = new List<EventSinkProfile>();
        public IReadOnlyList<EventSinkProfile> SinkProfiles => _sinkProfiles;

        public bool LoadSinkProfiles() {
            try {
                var sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                _sinkProfiles = string.IsNullOrWhiteSpace(sinkOptionsJson)
                    ? new List<EventSinkProfile>()
                    : new List<EventSinkProfile>(EventSinkProfiles.Parser.ParseJson(sinkOptionsJson).Profiles);
                return true;
            }
            catch (Exception ex) {
                _sinkProfiles = new List<EventSinkProfile>();
                _logger.LogError(ex, "Error loading event sink options.");
                return false;
            }
        }

        public bool SaveSinkProfiles(IList<EventSinkProfile> profiles) {
            try {
                var json = _jsonFormatter.Format(new EventSinkProfiles { Profiles = { profiles } });
                File.WriteAllText(EventSinkOptionsPath, json);
                _sinkProfiles = new List<EventSinkProfile>(profiles);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event sink options.");
                return false;
            }
        }

        public bool SaveSinkProfile(EventSinkProfile profile) {
            var profileIndex = _sinkProfiles.FindIndex(p => string.Equals(p.Name, profile.Name, StringComparison.CurrentCultureIgnoreCase));
            if (profileIndex >= 0) {
                _sinkProfiles[profileIndex] = profile;
            }
            else {
                _sinkProfiles.Add(profile);
            }
            return SaveSinkProfiles(_sinkProfiles);
        }

        public bool DeleteSinkProfile(string profileName) {
            var profileIndex = _sinkProfiles.FindIndex(p => string.Equals(p.Name, profileName, StringComparison.CurrentCultureIgnoreCase));
            if (profileIndex >= 0) {
                _sinkProfiles.RemoveAt(profileIndex);
            }
            return SaveSinkProfiles(_sinkProfiles);
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
            if (updateFilterSource) {
                _sessionState.ProcessingState.FilterSource = state.FilterSource;
            }
            return SaveSessionState(_sessionState);
        }

        #endregion
    }
}
