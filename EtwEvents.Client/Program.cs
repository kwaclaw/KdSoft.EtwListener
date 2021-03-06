using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using KdSoft.EtwLogging;

namespace KdSoft.EtwEvents.Client
{
    public class Program
    {
        const string filterBodyBase = @"
var appDomain = evt.PayloadStringByName(""AppDomain"");
var result = appDomain == ""MobilityServicesHost.exe"" || appDomain == ""MobilityPlatformClient.exe"";
";
        const string filterBody = filterBodyBase + @"
return result || (evt.ProviderName != ""Microsoft-Windows-Application Server-Applications"");
";
        const string filterBody2 = filterBodyBase + @"
var hostref = evt.PayloadStringByName(""HostReference"") ?? string.Empty;
result = result 
        || hostref.StartsWith(""MobilityServices"")
        || (evt.ProviderName != ""Microsoft-Windows-Application Server-Applications"");

var duration = evt.PayloadByName(""Duration"");
result = result || (duration != null && (long)duration > 0);

// result = result && evt.Level <= TraceEventLevel.Informational;

return result;
";
        static async Task Main(string[] args) {
            // Include port of the gRPC server as an application argument
            var port = args.Length > 0 ? args[0] : "50051";

            var channel = GrpcChannel.ForAddress("localhost:" + port, new GrpcChannelOptions {
                Credentials = ChannelCredentials.Insecure
            });
            var client = new EtwListener.EtwListenerClient(channel);

            var serverAppSetting = new ProviderSetting {
                Level = TraceEventLevel.Warning,
                MatchKeywords = 2305843009213825068,
                Name = "Microsoft-Windows-Application Server-Applications"
            };
            var palabraServicesSetting = new ProviderSetting {
                Level = TraceEventLevel.Informational,
                MatchKeywords = 0,
                Name = "SmartClinic-Services-Mobility"
            };
            var mqLinkSetting = new ProviderSetting {
                Level = TraceEventLevel.Informational,
                MatchKeywords = 0,
                Name = "SmartClinic-Services-Interop",
            };
            var runtimeSetting = new ProviderSetting {
                Level = TraceEventLevel.Informational,
                MatchKeywords = 0,
                Name = "Microsoft-Windows-DotNETRuntime",
            };

            var openEtwSession = new OpenEtwSession {
                Name = "TestSession",
                LifeTime = Duration.FromTimeSpan(TimeSpan.FromMinutes(10.0))
            };
            openEtwSession.ProviderSettings.Add(serverAppSetting);
            openEtwSession.ProviderSettings.Add(palabraServicesSetting);
            openEtwSession.ProviderSettings.Add(mqLinkSetting);
            openEtwSession.ProviderSettings.Add(runtimeSetting);

            var reply = await client.OpenSessionAsync(openEtwSession);
            Console.WriteLine("Session: ");
            foreach (var res in reply.Results) {
                Console.WriteLine($"\t{res.Name} - {res.Restarted}");
            }

            //var filterRequest = new SetFilterRequest {
            //    SessionName = "TestSession",
            //    CsharpFilter = "return true;" //filterBody
            //};
            //client.SetCSharpFilter(filterRequest);

            //var filterRequest2 = new SetFilterRequest {
            //    SessionName = "TestSession",
            //    CsharpFilter = filterBody2
            //};
            //var delayedFilter = Task.Delay(2000).ContinueWith((t) => client.SetCSharpFilter(filterRequest2));

            var request = new EtwEventRequest {
                SessionName = "TestSession"
            };
            using (var streamer = client.GetEvents(request)) {
                while (await streamer.ResponseStream.MoveNext()) {
                    var evtBatch = streamer.ResponseStream.Current;
                    string message;
                    string methodName;
                    foreach (var evt in evtBatch.Events) {
                        evt.Payload.TryGetValue("HostReference", out var hostRef);
                        evt.Payload.TryGetValue("AppDomain", out var appDomain);
                        evt.Payload.TryGetValue("Duration", out var duration);
                        bool gotMessage = evt.Payload.TryGetValue("Message", out message) || evt.Payload.TryGetValue("message", out message);
                        bool gotName = evt.Payload.TryGetValue("MethodName", out methodName) || evt.Payload.TryGetValue("name", out methodName);
                        string src = appDomain ?? hostRef;
                        Console.WriteLine($"{evt.ProviderName} :: {src}\n\t{evt.TimeStamp}-{evt.Level}:{duration}ms\n\t{evt.TaskName}-{methodName}\n\t{message}");
                    }
                }
            }

            await channel.ShutdownAsync();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
