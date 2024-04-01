using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using KdSoft.EtwEvents.PushAgent;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;
using Kdp = KdSoft.EtwEvents.PushAgent;

namespace KdSoft.EtwEvents.Tests
{
    public class OtherTests
    {
        readonly ITestOutputHelper _output;

        public OtherTests(ITestOutputHelper output) {
            this._output = output;
        }

        [Fact]
        public void UpdateControlOptions() {
            var opts = new Kdp.ControlOptions {
                Uri = new Uri("https://test.example.com:4444"),
                BackoffResetThreshold = TimeSpan.FromSeconds(44),
                ClientCertificate = new Kdp.ClientCertOptions {
                    Location = StoreLocation.LocalMachine,
                    SubjectCN = "XXXX",
                    SubjectRole = "etw-agent"
                }
            };
            var filesPath = Path.Combine(TestUtils.ProjectDir!, "Files");
            using var instream = File.OpenRead(Path.Combine(filesPath, "pushagent.appsettings.json"));
            using var reader = new StreamReader(instream, Encoding.UTF8, true);
            var json = reader.ReadToEnd();

            var updatedJson = Kdp.Utils.SetControlOptions(json, opts);

            var modifiedFileName = Path.GetTempFileName();
            _output.WriteLine("Modified file name file: {0}", modifiedFileName);
            using (var outstream = File.OpenWrite(modifiedFileName)) {
                using var writer = new StreamWriter(outstream, Encoding.UTF8);
                writer.Write(updatedJson);
            }

            var cfgBuilder = new ConfigurationBuilder();
            cfgBuilder.AddJsonFile(modifiedFileName);
            var cfg = cfgBuilder.Build();
            var testOpts = new ControlOptions();
            cfg.GetSection("Control").Bind(testOpts);

            File.Delete(modifiedFileName);
            Assert.Equal(opts, testOpts);
        }

        [Fact]
        public void UpdateControlOptions2() {
            var opts = new Kdp.ControlOptions {
                Uri = new Uri("https://test.example.com:4444"),
                BackoffResetThreshold = TimeSpan.FromSeconds(44),
                ClientCertificate = new Kdp.ClientCertOptions {
                    Location = StoreLocation.LocalMachine,
                    SubjectCN = "XXXX",
                    SubjectRole = "etw-agent"
                }
            };
            var filesPath = Path.Combine(TestUtils.ProjectDir!, "Files");

            var jsonBytes = File.ReadAllBytes(Path.Combine(filesPath, "pushagent.appsettings.json"));
            var readerOpts = new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            var jsonReader = new Utf8JsonReader(jsonBytes, readerOpts);

            var modifiedFileName = Path.GetTempFileName();
            _output.WriteLine("Modified file name file: {0}", modifiedFileName);

            using (var outstream = File.OpenWrite(modifiedFileName)) {
                var writerOpts = new JsonWriterOptions { Indented = true, SkipValidation = true };
                using var jsonWriter = new Utf8JsonWriter(outstream, writerOpts);
                Kdp.Utils.SetControlOptions(jsonReader, jsonWriter, opts);
            }

            var cfgBuilder = new ConfigurationBuilder();
            cfgBuilder.AddJsonFile(modifiedFileName);
            var cfg = cfgBuilder.Build();
            var testOpts = new ControlOptions();
            cfg.GetSection("Control").Bind(testOpts);

            File.Delete(modifiedFileName);
            Assert.Equal(opts, testOpts);
        }
    }
}
