// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Net.NetworkInformation;

var workingDir = Path.GetFullPath("..\\..\\..\\..\\EtwEvents.AgentManager\\Spa");

var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

bool isAvailable = true;
foreach (var tcpi in tcpConnInfoArray) {
    if (tcpi.LocalEndPoint.Port == 41000) {
        isAvailable = false;
        break;
    }
}
if (!isAvailable) {
    return;
}

var startInfo = new ProcessStartInfo {
    UseShellExecute = true,
    CreateNoWindow = false,
    FileName = "npm",
    Arguments = "run dev",
    WorkingDirectory = workingDir
};
var process = Process.Start(startInfo);
