using System.Buffers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KdSoft.EtwEvents;
using KdSoft.EtwEvents.AgentCommand;
using KdSoft.NamedMessagePipe;
using Microsoft.Extensions.Configuration;

#pragma warning disable CA1416 // Validate platform compatibility

[assembly: InternalsVisibleTo("KdSoft.EtwEvents.Tests")]

var cmdCfgBuilder = new ConfigurationBuilder();
cmdCfgBuilder.AddCommandLine(args);
var cmdCfg = cmdCfgBuilder.Build();
var configFile = cmdCfg["cfg"] ?? "appsettings.json";

var cfgBuilder = new ConfigurationBuilder();
cfgBuilder.AddJsonFile(configFile, false);
cfgBuilder.AddCommandLine(args);
var cfg = cfgBuilder.Build();

byte[] entropy = [ 23, 89, 222, 96, 144, 253, 99, 35, 11, 169, 73, 31, 2, 29, 47, 122 ];
byte[] encryptedPwdBytes = [];

var pwdFile = cfg["IssuerCertificate:PasswordPath"];
if (!string.IsNullOrWhiteSpace(pwdFile) && File.Exists(pwdFile)) {
    var fileBytes = File.ReadAllBytes(pwdFile);
    if (fileBytes.Length > 0) {
        if (fileBytes[0] != (byte)0) {
            encryptedPwdBytes = ProtectedData.Protect(fileBytes, entropy, DataProtectionScope.LocalMachine);
            fileBytes = null;
            var encryptedFileBytes = new byte[encryptedPwdBytes.Length + 1];
            encryptedFileBytes[0] = (byte)0;
            encryptedPwdBytes.CopyTo(encryptedFileBytes, 1);
            File.WriteAllBytes(pwdFile, encryptedFileBytes);
        }
        else {
            encryptedPwdBytes = new byte[fileBytes.Length - 1];
            Array.Copy(fileBytes[1..], encryptedPwdBytes, encryptedPwdBytes.Length);
            fileBytes = null;
        }
    }
}

var host = cfg["host"];
if (string.IsNullOrWhiteSpace(host)) {
    ShowUsage("Missing host options.");
    Console.ReadLine();
    return;
}

var command = cfg["cmd"];
if (string.IsNullOrWhiteSpace(command)) {
    ShowUsage("Missing command.");
    Console.ReadLine();
    return;
}

try {
    if (!TimeSpan.TryParse(cfg["ConnectTimeout"], out var connectTimeout))
        connectTimeout = TimeSpan.FromSeconds(10);
    using var cts = new CancellationTokenSource(connectTimeout);
    using var namedPipeClient = await NamedMessagePipeClient.ConnectAsync(host, "KdSoft.EtwEvents.PushAgent", "default", cancelToken: cts.Token);
    var messageTask = ReadNamedPipeMessages(namedPipeClient, cts.Token);
    await ExecuteCommand(namedPipeClient, command);
    await messageTask;
}
catch (OperationCanceledException) {
    Console.WriteLine($"Timeout communicating with host '{host}'.");
}
catch (Exception ex) {
    ShowUsage(ex.Message);
    Console.ReadLine();
    return;
}

Console.ReadLine();


void ShowUsage(string message) {
    Console.WriteLine(message + Environment.NewLine);
    Console.WriteLine("Usage: KdSoft.EtwEvents.AgentCommand.exe [options] --cmd <command> [commmand-options]");
    Console.WriteLine("\tOptions:");
    Console.WriteLine("\t\t--cfg <config file> (optional, defaults to appsettings.json)");
    Console.WriteLine("\t\t--host <host to connect to> (required)");
    Console.WriteLine("\tCommands:");
    Console.WriteLine("\t\tstart");
    Console.WriteLine("\t\tstop");
    Console.WriteLine("\t\tnew-cert --site-name <site name> [--site-email <contact email>]");
    Console.WriteLine("\t\tset-control (uses \"Control\" section in config file)");
}

async Task ExecuteCommand(NamedMessagePipeClient pipeClient, string command) {
    switch (command.ToLower()) {
        case "start":
            await WriteNamedPipeMessage(pipeClient, "Start:");
            break;

        case "stop":
            await WriteNamedPipeMessage(pipeClient, "Stop:");
            break;

        case "new-cert":
            var siteName = cfg["site-name"];
            if (string.IsNullOrEmpty(siteName?.Trim())) {
                ShowUsage("Missing site-name option.");
                break;
            }
            var siteEmail = cfg["site-email"];
            try {
                string pemEncoded;
                using (var certFactory = new CertificateFactory(cfg, GetPassword)) {
                    using (var cert = certFactory.CreateClientCertificate(siteName.Trim(), siteEmail?.Trim())) {
                        pemEncoded = CertUtils.ExportToPEM(cert, true);
                    }
                }
                await WriteNamedPipeMessage(pipeClient, $"InstallCert:{pemEncoded}");
            }
            catch (Exception ex) {
                ShowUsage(ex.Message);
            }
            break;

        case "set-control":
            try {
                var jsonDocOptions = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                var jsonDoc = JsonNode.Parse(File.ReadAllText(configFile), null, jsonDocOptions);
                var controlNode = jsonDoc?["Control"];
                if (controlNode is null) {
                    ShowUsage("Missing Control options.");
                    break;
                }

                var jsonOptions = new JsonSerializerOptions {
                    PropertyNamingPolicy = null,
                    AllowTrailingCommas = true,
                    WriteIndented = false
                };
                var controlJson = controlNode.ToJsonString(jsonOptions);
                await WriteNamedPipeMessage(pipeClient, $"SetControlOptions:{controlJson}");
            }
            catch (Exception ex) {
                ShowUsage(ex.Message);
            }
            break;

        default:
            ShowUsage($"Invalid command found: {command}.");
            break;
    }
}

string? GetPassword() {
    if (encryptedPwdBytes.Length > 0) {
        var pwdBytes = ProtectedData.Unprotect(encryptedPwdBytes, entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(pwdBytes);
    }
    return null;
}

string GetPipeString(ReadOnlySequence<byte> sequence) {
    return Encoding.UTF8.GetString(sequence.ToArray());
}

async Task ReadNamedPipeMessages(NamedMessagePipeClient pipeClient, CancellationToken cancelToken) {
    await foreach (var msgSequence in pipeClient.Messages(cancelToken)) {
        var msg = GetPipeString(msgSequence);
        Console.WriteLine(msg);
    }
}

int WriteToBuffer(Memory<byte> memory, string msg) {
    var buffer = memory.Span;
    var count = Encoding.UTF8.GetBytes(msg, buffer);
    buffer[count++] = 0;
    return count;
}

async ValueTask WriteNamedPipeMessage(NamedMessagePipeClient client, string msg) {
    using var memOwner = MemoryPool<byte>.Shared.Rent(16384);
    var count = WriteToBuffer(memOwner.Memory, msg);
    await client.Stream.WriteAsync(memOwner.Memory.Slice(0, count));
}
