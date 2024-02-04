using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KdSoft.EtwEvents.PushAgent
{
    public static class Utils
    {
        /// <summary>
        /// Retrieves client certificates matching the specified options.
        /// </summary>
        /// <param name="certOptions">Options to match.</param>
        /// <exception cref="ArgumentException">One of SubjectCN or SubjectRole must specified in the certOptions argument.</exception>
        public static List<X509Certificate2> GetClientCertificates(ClientCertOptions certOptions) {
            if (certOptions.SubjectCN.Length == 0 && certOptions.SubjectRole.Length == 0)
                throw new ArgumentException("Client certificate options must have one of SubjectCN or SubjectRole specified.");

            var result = new List<X509Certificate2>();
            if (certOptions.SubjectCN.Length > 0) {
                var clientCerts = CertUtils.GetCertificates(certOptions.Location, certOptions.SubjectCN, Oids.ClientAuthentication);
                result.AddRange(clientCerts);
            }
            if (certOptions.SubjectRole.Length > 0) {
                var clientCerts = CertUtils.GetCertificates(certOptions.Location, Oids.ClientAuthentication, crt => {
                    var roles = crt.GetSubjectRoles();
                    if (roles.Exists(certRole => certRole.Equals(certOptions.SubjectRole, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    return false;
                });
                result.AddRange(clientCerts);
            }

            // sort by descending NotBefore date
            result.Sort((x, y) => DateTime.Compare(y.NotBefore, x.NotBefore));
            return result;
        }

        public static SocketsHttpHandler CreateHttpHandler(params X509Certificate2[] clientCerts) {
            var httpHandler = new SocketsHttpHandler {
                //PooledConnectionLifetime = TimeSpan.FromHours(4),
                SslOptions = new SslClientAuthenticationOptions {
                    ClientCertificates = new X509Certificate2Collection(clientCerts),
                },
            };
            return httpHandler;
        }

        public static void SetControlOptions(JsonNode rootNode, ControlOptions opts) {
            var controlNode = rootNode["Control"] as JsonObject;
            if (controlNode is null) {
                rootNode["Control"] = controlNode = new JsonObject();
            }

            if (opts.Uri is not null) {
                controlNode["Uri"] = opts.Uri.ToString();
            }
            if (opts.InitialRetryDelay is not null) {
                controlNode["InitialRetryDelay"] = opts.InitialRetryDelay.ToString();
            }
            if (opts.MaxRetryDelay is not null) {
                controlNode["MaxRetryDelay"] = opts.MaxRetryDelay.ToString();
            }
            if (opts.BackoffResetThreshold is not null) {
                controlNode["BackoffResetThreshold"] = opts.BackoffResetThreshold.ToString();
            }
            if (opts.ClientCertificate is not null) {
                controlNode.TryAdd("ClientCertificate", new JsonObject());
                controlNode["ClientCertificate"]!.ReplaceWith(opts.ClientCertificate);
            }
        }

        public static string SetControlOptions(string json, ControlOptions opts) {
            var jsonNodeOpts = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
            var jsonDocOpts = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            var rootNode = JsonNode.Parse(json, jsonNodeOpts, jsonDocOpts)!;

            SetControlOptions(rootNode, opts);

            var jsonSerializerOpts = new JsonSerializerOptions {
                Converters = { new JsonStringEnumConverter() },
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),  // required
                PropertyNamingPolicy = null,  // write as is
                AllowTrailingCommas = true,
                WriteIndented = true,
            };

            return rootNode.ToJsonString(jsonSerializerOpts);
        }

        public static void SetControlOptions(Utf8JsonReader jsonReader, Utf8JsonWriter jsonWriter, ControlOptions opts) {
            var jsonNodeOpts = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
            var rootNode = JsonNode.Parse(ref jsonReader, jsonNodeOpts)!;

            SetControlOptions(rootNode, opts);

            var jsonSerializerOpts = new JsonSerializerOptions {
                Converters = { new JsonStringEnumConverter() },
                TypeInfoResolver = new DefaultJsonTypeInfoResolver(),  // required
                PropertyNamingPolicy = null,  // write as is
                AllowTrailingCommas = true,
                WriteIndented = true,
            };
            rootNode.WriteTo(jsonWriter, jsonSerializerOpts);
        }

        public static void SaveControlOptions(string optionsFile, ControlOptions opts) {
            var json = File.ReadAllText(optionsFile, Encoding.UTF8);
            var updatedJson = SetControlOptions(json, opts);
            File.WriteAllText(optionsFile, updatedJson, Encoding.UTF8);
        }
    }
}
