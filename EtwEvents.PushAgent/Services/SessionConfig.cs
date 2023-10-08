using System.Buffers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
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
        readonly ArrayBufferWriter<byte> _bufferWriter;

        const string DataProtectionPurpose = "sink-credentials";
        const string localSettingsFile = "appsettings.Local.json";

        static readonly JsonFormatter _jsonFormatter = new(
            JsonFormatter.Settings.Default.WithFormatDefaultValues(true).WithFormatEnumsAsIntegers(true)
        );

        static readonly JsonSerializerOptions _jsonOptions = new() {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        static readonly JsonWriterOptions _writerOptions = new() {
            Indented = true,
            SkipValidation = true
        };

        static readonly JsonNodeOptions _nodeOptions = new() {
            PropertyNameCaseInsensitive = true,
        };

        static readonly JsonDocumentOptions _docOptions = new() {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

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
            _bufferWriter = new ArrayBufferWriter<byte>(2048);

            lock (_bufferWriter) {
                _dpProvider = InitializeDataProtection(_dataOptionsMonitor.CurrentValue.Certificate);
            }

            LoadSessionState();
            LoadSinkProfiles();
        }

        public JsonFormatter JsonFormatter => _jsonFormatter;

        void SaveNodeAtomic(string filePath, JsonNode node) {
            _bufferWriter.Clear();
            using (var utf8Writer = new Utf8JsonWriter(_bufferWriter, _writerOptions)) {
                node.WriteTo(utf8Writer, _jsonOptions);
                utf8Writer.Flush();
            }
            FileUtils.WriteFileAtomic(_bufferWriter.WrittenSpan, filePath);
        }

        void SaveUtf8FileAtomic(string filePath, string text) {
            _bufferWriter.Clear();
            var buffer = _bufferWriter.GetSpan(text.Length * 3);
            var status = Utf8.FromUtf16(text, buffer, out int _, out int bytesWritten, true, true);
            if (status == OperationStatus.Done) {
                _bufferWriter.Advance(bytesWritten);
                FileUtils.WriteFileAtomic(_bufferWriter.WrittenSpan, filePath);
            }
            else {
                if (status == OperationStatus.DestinationTooSmall)
                    throw new InvalidOperationException("UTF8 buffer too small.");
                else
                    throw new InvalidOperationException("Could not convert UTF16 to UTF8.");
            }
        }

        JsonNode? ReadNode(string filePath) {
            using var fs = FileUtils.OpenFileWithRetry(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            using var buffer = MemoryPool<byte>.Shared.Rent((int)fs.Length);
            var byteCount = fs.Read(buffer.Memory.Span);
            if (byteCount == 0) {
                return null;
            }
            try {
                return JsonObject.Parse(buffer.Memory.Span[..byteCount], _nodeOptions, _docOptions);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in {method}.", nameof(ReadNode));
                return null;
            }
        }

        #region DataProtectionOptions

        void SaveDataProtectionOptions(DataProtectionOptions dataProtectionOptions) {
            var jsonFile = Path.Combine(_context.HostingEnvironment.ContentRootPath, localSettingsFile);
            JsonObject? docObj;

            try {
                var docNode = ReadNode(jsonFile);
                docObj = docNode as JsonObject;
                docObj ??= new JsonObject(_nodeOptions);
            }
            catch (IOException) {
                docObj = new JsonObject();
            }
            catch (JsonException) {
                docObj = new JsonObject();
            }

            var dataProtectionNode = JsonSerializer.SerializeToNode(dataProtectionOptions, _jsonOptions);
            docObj["DataProtection"] = dataProtectionNode;

            SaveNodeAtomic(jsonFile, docObj);
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
            lock (_bufferWriter) {
                LoadSinkProfiles();
                SaveDataProtectionOptions(new DataProtectionOptions { Certificate = newCertOptions });
                _dpProvider = InitializeDataProtection(newCertOptions);
                SaveSinkProfilesInternal(_sinkProfiles);
            }
        }

        #endregion

        #region SessionState

        string EventSessionStatePath => Path.Combine(_context.HostingEnvironment.ContentRootPath, "eventSession.json");

        EventSessionState _sessionState = new();
        public EventSessionState State => _sessionState;
        public bool StateAvailable => _stateAvailable;

        bool LoadSessionState() {
            try {
                string sessionStateJson;
                sessionStateJson = File.ReadAllText(EventSessionStatePath);
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

        bool SaveSessionState(EventSessionState state) {
            try {
                //TODO add indentation to JSON once it is supported in Google.protobuf
                var json = _jsonFormatter.Format(state);
                SaveUtf8FileAtomic(EventSessionStatePath, json);
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

        Dictionary<string, EventSinkProfile> _sinkProfiles = new(StringComparer.CurrentCultureIgnoreCase);
        public IReadOnlyDictionary<string, EventSinkProfile> SinkProfiles => _sinkProfiles;

        bool LoadSinkProfiles() {
            try {
                string sinkOptionsJson;
                sinkOptionsJson = File.ReadAllText(EventSinkOptionsPath);
                var profiles = string.IsNullOrWhiteSpace(sinkOptionsJson)
                    ? new Dictionary<string, EventSinkProfile>(StringComparer.CurrentCultureIgnoreCase)
                    : new Dictionary<string, EventSinkProfile>(EventSinkProfiles.Parser.ParseJson(sinkOptionsJson).Profiles, StringComparer.CurrentCultureIgnoreCase);
                foreach (var profile in profiles.Values) {
                    if (profile.Credentials.StartsWith('*')) {
                        try {
                            var dataProtector = _dpProvider.CreateProtector(DataProtectionPurpose);
                            var rawCredentials = dataProtector.Unprotect(profile.Credentials[1..]);
                            profile.Credentials = rawCredentials;
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

        bool SaveSinkProfilesInternal(IDictionary<string, EventSinkProfile> profiles) {
            try {
                // clone profiles so we only modify the stored version
                var clonedProfiles = new Dictionary<string, EventSinkProfile>(profiles);
                foreach (var profileEntry in clonedProfiles) {
                    var clonedProfile = profileEntry.Value.Clone();
                    if (!clonedProfile.Credentials.StartsWith('*')) {
                        var dataProtector = _dpProvider.CreateProtector(DataProtectionPurpose);
                        var protectedCredentials = dataProtector.Protect(clonedProfile.Credentials);
                        clonedProfile.Credentials = $"*{protectedCredentials}";
                    }
                    clonedProfiles[profileEntry.Key] = clonedProfile;
                }
                //TODO add indentation to JSON once it is supported in Google.protobuf
                var json = _jsonFormatter.Format(new EventSinkProfiles { Profiles = { clonedProfiles } });
                SaveUtf8FileAtomic(EventSinkOptionsPath, json);
                _sinkProfiles = new Dictionary<string, EventSinkProfile>(profiles, StringComparer.CurrentCultureIgnoreCase);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error saving event sink options.");
                return false;
            }
        }

        public bool SaveSinkProfiles(IDictionary<string, EventSinkProfile> profiles) {
            lock (_bufferWriter) {
                return SaveSinkProfilesInternal(profiles);
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
            lock (_bufferWriter) {
                LoadSessionState();
                _sessionState.ProviderSettings.Clear();
                _sessionState.ProviderSettings.AddRange(providers);
                return SaveSessionState(_sessionState);
            }
        }

        #endregion

        #region Processing State

        public bool SaveProcessingState(ProcessingState state, bool updateFilterSource) {
            lock (_bufferWriter) {
                LoadSessionState();
                if (updateFilterSource) {
                    _sessionState.ProcessingState.FilterSource = state.FilterSource;
                }
                return SaveSessionState(_sessionState);
            }
        }

        #endregion

        #region Live View

        public bool SaveLiveViewOptions(LiveViewOptions liveViewOptions) {
            lock (_bufferWriter) {
                LoadSessionState();
                _sessionState.LiveViewOptions = liveViewOptions;
                return SaveSessionState(_sessionState);
            }
        }

        #endregion
    }
}
