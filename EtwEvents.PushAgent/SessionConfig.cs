using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionConfig
    {
        readonly HostBuilderContext _context;
        readonly ILogger<SessionConfig> _logger;
        readonly IDataProtector _dataProtector;
        readonly JsonFormatter _jsonFormatter;

        bool _stateAvailable;

        public SessionConfig(HostBuilderContext context, IDataProtectionProvider provider, ILogger<SessionConfig> logger) {
            this._context = context;
            this._logger = logger;
            this._dataProtector = provider.CreateProtector("sink-credentials");
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

        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSinks.json");

        Dictionary<string, EventSinkProfile> _sinkProfiles = new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase);
        public IReadOnlyDictionary<string, EventSinkProfile> SinkProfiles => _sinkProfiles;

        public bool LoadSinkProfiles() {
            try {
                var sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                var profiles = string.IsNullOrWhiteSpace(sinkOptionsJson)
                    ? new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase)
                    : new Dictionary<string, EventSinkProfile>(EventSinkProfiles.Parser.ParseJson(sinkOptionsJson).Profiles, StringComparer.CurrentCultureIgnoreCase);
                foreach (var profile in profiles.Values) {
                    if (profile.Credentials.StartsWith('*')) {
                        var rawCredentials = this._dataProtector.Unprotect(profile.Credentials.Substring(1));
                        profile.Credentials = rawCredentials;
                    }
                }
                _sinkProfiles = profiles;
                return true;
            }
            catch (Exception ex) {
                _sinkProfiles = new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase);
                _logger.LogError(ex, "Error loading event sink options.");
                return false;
            }
        }

        public bool SaveSinkProfiles(IDictionary<string, EventSinkProfile> profiles) {
            try {
                // clone profiles so we only modify the stored version
                var clonedProfiles = new Dictionary<string, EventSinkProfile>(profiles);
                foreach (var profileEntry in clonedProfiles) {
                    var clonedProfile = profileEntry.Value.Clone();
                    if (!clonedProfile.Credentials.StartsWith('*')) {
                        var protectedCredentials = this._dataProtector.Protect(clonedProfile.Credentials);
                        clonedProfile.Credentials = $"*{protectedCredentials}";
                    }
                    clonedProfiles[profileEntry.Key] = clonedProfile;
                }
                var json = _jsonFormatter.Format(new EventSinkProfiles { Profiles = { clonedProfiles } });
                File.WriteAllText(EventSinkOptionsPath, json);
                _sinkProfiles = new Dictionary<string, EventSinkProfile>(profiles, StringComparer.CurrentCultureIgnoreCase);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event sink options.");
                return false;
            }
        }

        public bool SaveSinkProfile(EventSinkProfile profile) {
            _sinkProfiles[profile.Name] = profile;
            return SaveSinkProfiles(_sinkProfiles);
        }

        public bool DeleteSinkProfile(string profileName) {
            if (_sinkProfiles.Remove(profileName))
                return SaveSinkProfiles(_sinkProfiles);
            else
                return false;
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

        #region Live View

        public bool SaveLiveViewOptions(LiveViewOptions liveViewOptions) {
            LoadSessionState();
            _sessionState.LiveViewOptions = liveViewOptions;
            return SaveSessionState(_sessionState);
        }

        #endregion
    }
}
