// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Net.NetworkInformation;

var workingDir = Path.GetFullPath("..\\..\\..\\..\\EtwEvents.AgentManager\\Spa");

var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();

bool isAlreadyRunning = false;
foreach (var tcpListener in tcpListeners) {
    if (tcpListener.Port == 41000) {
        isAlreadyRunning = true;
        break;
    }
}
if (isAlreadyRunning) {
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
