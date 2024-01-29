using System.Buffers;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Channels;
using KdSoft.NamedMessagePipe;

namespace KdSoft.EtwEvents.PushAgent
{
    class NamedPipeHandler
    {
        readonly Channel<ControlEvent> _controlChannel;
        readonly ILogger<NamedPipeHandler> _logger;

        int _pipeServerCreated = 0;

        public NamedPipeHandler(Channel<ControlEvent> controlChannel, ILogger<NamedPipeHandler> logger) {
            this._controlChannel = controlChannel;
            this._logger = logger;
        }

        NamedMessagePipeServer CreatePipeServer(CancellationToken shutdownToken) {
            var pipeName = this.GetType().Namespace!;

            void AddRule(PipeSecurity pipeSec, WellKnownSidType sidType, SecurityIdentifier? domainSid) {
                var id = new SecurityIdentifier(sidType, domainSid);
                pipeSec.SetAccessRule(new PipeAccessRule(id, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            }

            // Allow admin users read and write access to the pipe. 
            var pipeSecurity = new PipeSecurity();
            AddRule(pipeSecurity, WellKnownSidType.BuiltinAdministratorsSid, null);
            var domainSid = new SecurityIdentifier(WellKnownSidType.NullSid, null).AccountDomainSid;
            if (domainSid is not null) {
                AddRule(pipeSecurity, WellKnownSidType.AccountAdministratorSid, domainSid);
                AddRule(pipeSecurity, WellKnownSidType.AccountDomainAdminsSid, domainSid);
                AddRule(pipeSecurity, WellKnownSidType.AccountEnterpriseAdminsSid, domainSid);
            }

            return new NamedMessagePipeServer(pipeName, "default", shutdownToken, -1, pipeSecurity);
        }

        ValueTask WriteMessage(NamedMessagePipeServer server, string msg) {
            using var memOwner = MemoryPool<byte>.Shared.Rent(1024);
            var buffer = memOwner.Memory.Span;
            var count = Encoding.UTF8.GetBytes(msg, buffer);
            buffer[count++] = 0;
            return server.Stream.WriteAsync(memOwner.Memory);
        }

        public async ValueTask ProcessPipeMessages(CancellationToken shutdownToken) {
            var oldPipeServerCreated = Interlocked.CompareExchange(ref _pipeServerCreated, 99, 0);
            if (oldPipeServerCreated != 0) {
                throw new InvalidOperationException("Only one named pipe server allowed.");
            }
            try {
                using var pipeServer = CreatePipeServer(shutdownToken);
                await foreach (var msgSequence in pipeServer.Messages()) {
                    try {
                        var msg = Encoding.UTF8.GetString(msgSequence);
                        await ProcessPipeMessage(pipeServer, msg).ConfigureAwait(false);
                    }
                    catch (Exception ex) {
                        _logger.LogError(ex, "{method}", nameof(ProcessPipeMessages));
                    }
                }
            }
            finally {
                Volatile.Write(ref _pipeServerCreated, 0);
            }
        }

        // All messages must start with message type / event name terminated by a ':',
        // then followed by the rest of the message (which depends on the message type).
        async ValueTask ProcessPipeMessage(NamedMessagePipeServer pipeServer, string msg) {
            try {
                _logger.LogInformation("{method}: Received message '{message}'", nameof(ProcessPipeMessage), msg);
                var parts = msg.Split(':', 2, StringSplitOptions.TrimEntries);
                if (parts.Length < 2) {
                    _logger.LogInformation("{method}: Invalid message received '{message}'", nameof(ProcessPipeMessage), msg);
                    await WriteMessage(pipeServer, "Invalid message");
                    return;
                }
                var controlEvent = new ControlEvent { Event = parts[0], Id = "", Data = parts[1] };
                var couldWrite = _controlChannel.Writer.TryWrite(controlEvent);
                if (couldWrite) {
                    await WriteMessage(pipeServer, $"{parts[0]} message queued");
                }
                else {
                    _logger.LogError("Error in {method}. Could not write event {event} to control channel, event data:\n{data}",
                        nameof(ProcessPipeMessage), controlEvent.Event, controlEvent.Event == Constants.InstallCertEvent ? "" : controlEvent.Data);
                    await WriteMessage(pipeServer, $"Could not queue {parts[0]} message");
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "{method}", nameof(ProcessPipeMessage));
            }
        }
    }
}
