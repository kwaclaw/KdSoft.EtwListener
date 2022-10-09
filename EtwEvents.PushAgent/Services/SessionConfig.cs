using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Google.Protobuf;
using KdSoft.EtwLogging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace KdSoft.EtwEvents.PushAgent
{
    class SessionConfig
    {
        readonly HostBuilderContext _context;
        readonly SocketsHandlerCache _httpHandlerCache;
        readonly ILogger<SessionConfig> _logger;
        readonly IOptions<ControlOptions> _options;
        readonly IOptionsMonitor<DataProtectionOptions> _dataOptionsMonitor;
        readonly JsonFormatter _jsonFormatter;
        readonly JsonSerializerOptions _jsonOptions;
        readonly object dpSync = new object();

        const string DataProtectionPurpose = "sink-credentials";
        const string localSettingsFile = "appsettings.Local.json";

        IDataProtectionProvider _dpProvider;
        bool _stateAvailable;

        public SessionConfig(
            HostBuilderContext context,
            SocketsHandlerCache httpHandlerCache,
            IOptions<ControlOptions> options,
            IOptionsMonitor<DataProtectionOptions> dataOptionsMonitor,
            ILogger<SessionConfig> logger
        ) {
            this._context = context;
            this._httpHandlerCache = httpHandlerCache;
            this._logger = logger;
            this._options = options;
            this._dataOptionsMonitor = dataOptionsMonitor;

            var jsonSettings = JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true);
            _jsonFormatter = new JsonFormatter(jsonSettings);

            _jsonOptions = new JsonSerializerOptions {
                Converters = { new JsonStringEnumConverter() }
            };

            _dpProvider = InitializeDataProtection(_dataOptionsMonitor.CurrentValue.Certificate);

            LoadSessionState();
            LoadSinkProfiles();
        }

        public JsonFormatter JsonFormatter => _jsonFormatter;

        #region DataProtectionOptions

        void SaveDataProtectionOptions(DataProtectionOptions dataProtectionOptions) {
            var jsonFile = Path.Combine(_context.HostingEnvironment.ContentRootPath, localSettingsFile);
            JsonObject doc;
            try {
                var json = File.ReadAllText(jsonFile) ?? "{}";
                doc = JsonObject.Parse(json)?.AsObject() ?? new JsonObject();
            }
            catch (IOException) {
                doc = new JsonObject();
            }
            catch (JsonException) {
                doc = new JsonObject();
            }
            var dataProtectionNode = JsonSerializer.SerializeToNode(dataProtectionOptions, _jsonOptions);
            doc["DataProtection"] = dataProtectionNode;
            File.WriteAllText(jsonFile, doc.ToJsonString());
        }

        // https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/non-di-scenarios?view=aspnetcore-6.0
        // https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-6.0
        // When appsettings.Local.json is missing, there is no data protection thumbprint stored in it, then load
        // the first client certificate and store its thumbprint so it can be used in future startup processing.
        // On startup, compare the stored thumprint to the first client certificate's thumbprint, and if different,
        // re-encode with the new certificate and store the new thumbprint.
        // Do the same at runtime when the client certs change.
        IDataProtectionProvider InitializeDataProtection(DataCertOptions certOptions) {
            var dataCertificate = CertUtils.GetCertificate(certOptions.Location, certOptions.Thumbprint, "");
            var clientCertificate = ((X509Certificate2Collection)_httpHandlerCache.Handler.SslOptions.ClientCertificates!).First();
            if (dataCertificate is null || (clientCertificate.Thumbprint.ToLower() != certOptions.Thumbprint.ToLower())) {
                var clientCertOptions = new DataCertOptions {
                    Thumbprint = clientCertificate.Thumbprint,
                    Location = certOptions.Location,
                };
                SaveDataProtectionOptions(new DataProtectionOptions { Certificate = clientCertOptions });
                dataCertificate = clientCertificate;
            }
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // need to change the key directory whenever the certificate changes (we won't change the data protection certificate at runtime)
            var keyDirectory = Path.Combine(appDataPath, nameof(PushAgent), $"Keys-{dataCertificate.Thumbprint}");
            var dataProtectionProvider = DataProtectionProvider.Create(
                new DirectoryInfo(keyDirectory),
                dpBuilder => {
                    dpBuilder.SetApplicationName("KdSoft.EtwEvents.PushAgent");
                    //AuthenticatedEncryptorConfiguration cfg = new AuthenticatedEncryptorConfiguration {
                    //    EncryptionAlgorithm = EncryptionAlgorithm.AES_256_GCM,
                    //    ValidationAlgorithm = ValidationAlgorithm.HMACSHA512
                    //};
                    //dpBuilder.UseCryptographicAlgorithms(cfg);
                },
                dataCertificate
            );

            return dataProtectionProvider;
        }

        /// <summary>
        /// Update data protection when the certificate should change.
        /// </summary>
        public void UpdateDataProtection(DataCertOptions newCertOptions) {
            lock (dpSync) {
                LoadSinkProfiles();
                SaveDataProtectionOptions(new DataProtectionOptions { Certificate = newCertOptions });
                _dpProvider = InitializeDataProtection(newCertOptions);
                SaveSinkProfiles(_sinkProfiles);
            }
        }

        #endregion

        #region SessionState

        readonly object _sessionStateSync=new object();

        string EventSessionStatePath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");

        EventSessionState _sessionState = new();
        public EventSessionState State => _sessionState;
        public bool StateAvailable => _stateAvailable;

        public bool LoadSessionState() {
            try {
                string sessionStateJson;
                lock (_sessionStateSync) {
                    sessionStateJson = File.ReadAllText(EventSessionStatePath);
                }
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
                lock (_sessionStateSync) {
                    File.WriteAllText(EventSessionStatePath, json);
                }
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

        readonly object _sinkProfileSync = new object();

        string EventSinkOptionsPath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSinks.json");

        Dictionary<string, EventSinkProfile> _sinkProfiles = new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase);
        public IReadOnlyDictionary<string, EventSinkProfile> SinkProfiles => _sinkProfiles;

        public bool LoadSinkProfiles() {
            try {
                string sinkOptionsJson;
                lock (_sinkProfileSync) {
                    sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                }
                var profiles = string.IsNullOrWhiteSpace(sinkOptionsJson)
                    ? new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase)
                    : new Dictionary<string, EventSinkProfile>(EventSinkProfiles.Parser.ParseJson(sinkOptionsJson).Profiles, StringComparer.CurrentCultureIgnoreCase);
                foreach (var profile in profiles.Values) {
                    if (profile.Credentials.StartsWith('*')) {
                        try {
                            lock (dpSync) {
                                var dataProtector = _dpProvider.CreateProtector(DataProtectionPurpose);
                                var rawCredentials = dataProtector.Unprotect(profile.Credentials.Substring(1));
                                profile.Credentials = rawCredentials;
                            }
                        }
                        catch (Exception ex) {
                            profile.Credentials = "{}";
                            _logger.LogError(ex, "Error unprotecting event sink credentials.");
                        }
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
                        lock (dpSync) {
                            var dataProtector = _dpProvider.CreateProtector(DataProtectionPurpose);
                            var protectedCredentials = dataProtector.Protect(clonedProfile.Credentials);
                            clonedProfile.Credentials = $"*{protectedCredentials}";
                        }
                    }
                    clonedProfiles[profileEntry.Key] = clonedProfile;
                }
                var json = _jsonFormatter.Format(new EventSinkProfiles { Profiles = { clonedProfiles } });
                lock (_sinkProfileSync) {
                    File.WriteAllText(EventSinkOptionsPath, json);
                }
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
