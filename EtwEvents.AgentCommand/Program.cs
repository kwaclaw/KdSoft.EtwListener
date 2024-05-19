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

byte[] entropy = [23, 89, 222, 96, 144, 253, 99, 35, 11, 169, 73, 31, 2, 29, 47, 122];
byte[] encryptedPwdBytes = [];
char[] commandSeparators = [' ', ',', ';'];

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

var commands = cfg["cmds"];
if (string.IsNullOrWhiteSpace(commands)) {
    ShowUsage("Missing commands.");
    Console.ReadLine();
    return;
}
var commandList = commands.Split(commandSeparators, StringSplitOptions.RemoveEmptyEntries);

try {
    if (!TimeSpan.TryParse(cfg["ConnectTimeout"], out var connectTimeout))
        connectTimeout = TimeSpan.FromSeconds(30);
    using var cts = new CancellationTokenSource(connectTimeout);
    using var namedPipeClient = await NamedMessagePipeClient.ConnectAsync(host, "KdSoft.EtwEvents.PushAgent", "default", cancelToken: cts.Token);
    var messageTask = ReadNamedPipeMessages(namedPipeClient, cts.Token);
    foreach (var command in commandList) {
        bool success;
        try {
            success = await ExecuteCommand(namedPipeClient, command);
            if (!success) {
                Console.WriteLine($"{host}: Could not send command: {command}.");
                continue;
            }
        }
        catch (OperationCanceledException) {
            Console.WriteLine($"{host}: Timeout communicating with host, could not send command {command}.");
            continue;
        }
        catch (Exception ex) {
            Console.WriteLine($"{host}: {ex}");
            continue;
        }
    }

    await messageTask;
}
catch (OperationCanceledException) {
    Console.WriteLine($"{host}: Timeout communicating with host.");
}
catch (Exception ex) {
    ShowUsage(ex.Message);
    Console.ReadLine();
    return;
}

#if DEBUG
Console.WriteLine("Press any key to exit.");
Console.ReadKey();
#endif


void ShowUsage(string message) {
    Console.WriteLine(message + Environment.NewLine);
    Console.WriteLine("Usage: KdSoft.EtwEvents.AgentCommand.exe [options] --cmds <command list> [commmand-options]");
    Console.WriteLine("\tOptions:");
    Console.WriteLine("\t\t--cfg <config file> (optional, defaults to appsettings.json)");
    Console.WriteLine("\t\t--host <host to connect to> (required)");
    Console.WriteLine("\tCommands: (<command list> separated by ',', ';' or space)");
    Console.WriteLine("\t\tstart");
    Console.WriteLine("\t\tstop");
    Console.WriteLine("\t\tnew-cert --site-name <site name> [--site-email <contact email>]");
    Console.WriteLine("\t\tset-control (uses \"Control\" section in config file)");
    Console.WriteLine("\t\tset-options --site-options <options file> (\"Exported as Command\" from agent manager)");
}

async Task<bool> ExecuteCommand(NamedMessagePipeClient pipeClient, string command) {
    switch (command.ToLower()) {
        case "start":
            await WriteNamedPipeMessage(pipeClient, "Start:");
            return true;

        case "stop":
            await WriteNamedPipeMessage(pipeClient, "Stop:");
            return true;

        case "new-cert":
            var siteName = cfg["site-name"];
            if (string.IsNullOrEmpty(siteName?.Trim())) {
                Console.WriteLine($"{host}: Missing site-name option.");
                return false;
            }
            var siteEmail = cfg["site-email"];
            string pemEncoded;
            try {
                using (var certFactory = new CertificateFactory(cfg, GetPassword)) {
                    using (var cert = certFactory.CreateClientCertificate(siteName.Trim(), siteEmail?.Trim())) {
                        pemEncoded = CertUtils.ExportToPEM(cert, true);
                    }
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"{host}: {ex}");
                return false;
            }
            await WriteNamedPipeMessage(pipeClient, $"InstallCert:{pemEncoded}");
            return true;

        case "set-control":
            string controlJson;
            try {
                var jsonDocOptions = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                var jsonDoc = JsonNode.Parse(File.ReadAllText(configFile), null, jsonDocOptions);
                var controlNode = jsonDoc?["Control"];
                if (controlNode is null) {
                    Console.WriteLine($"{host}: Missing Control options.");
                    return false;
                }

                var jsonOptions = new JsonSerializerOptions {
                    PropertyNamingPolicy = null,
                    AllowTrailingCommas = true,
                    WriteIndented = false
                };
                controlJson = controlNode.ToJsonString(jsonOptions);
            }
            catch (Exception ex) {
                Console.WriteLine($"{host}: {ex}");
                return false;
            }
            await WriteNamedPipeMessage(pipeClient, $"SetControlOptions:{controlJson}");
            return true;

        case "set-options":
            string optionsJson;
            try {
                var optionsFile = cfg["site-options"];
                if (string.IsNullOrEmpty(optionsFile)) {
                    Console.WriteLine($"{host}: Missing site-options.");
                    break;
                }
                optionsFile = Environment.ExpandEnvironmentVariables(optionsFile);
                if (!File.Exists(optionsFile)) {
                    Console.WriteLine($"{host}: File does not exist: {optionsFile}.");
                    return false;
                }
                optionsJson = File.ReadAllText(optionsFile);
            }
            catch (Exception ex) {
                Console.WriteLine($"{host}: {ex}");
                return false;
            }
            await WriteNamedPipeMessage(pipeClient, $"ApplyAgentOptions:{optionsJson}");
            return true;

        default:
            Console.WriteLine($"{host}: Invalid command found: {command}.");
            return false;
    }
    return true;
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
        Console.WriteLine($"{host}: {msg}");
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
