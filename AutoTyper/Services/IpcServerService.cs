using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTyper.Services
{
    public class IpcServerService : IDisposable
    {
        public const string PipeName = "AutoTyper_GameBar_IPC";

        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly ConcurrentDictionary<int, StreamWriter> _activeClients = new();
        private int _clientCounter = 0;

        public Func<IpcCommand, Task<IpcResponse>>? OnCommandReceived { get; set; }
        public Action<bool>? OnClientConnectionChanged { get; set; }

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenForClientsAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                foreach (var kvp in _activeClients)
                {
                    try { kvp.Value.Dispose(); } catch { }
                }
                _activeClients.Clear();
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task ListenForClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var pipeServer = CreateSecuredPipeServer();
                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    int clientId = Interlocked.Increment(ref _clientCounter);
                    _ = Task.Run(() => HandleClientAsync(pipeServer, clientId, cancellationToken), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    try
                    {
                        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Auto Typer byGo", "startup_log.txt");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [IPC Server Error] {ex}\n");
                    }
                    catch { }
                    await Task.Delay(500, cancellationToken);
                }
            }
        }

        private NamedPipeServerStream CreateSecuredPipeServer()
        {
            var pipeSecurity = new PipeSecurity();

            // Allow current user full access
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            }

            // CRITICAL: Allow UWP AppContainer (ALL APPLICATION PACKAGES: S-1-15-2-1) to read & write
            var allAppPackagesSid = new SecurityIdentifier("S-1-15-2-1");
            pipeSecurity.AddAccessRule(new PipeAccessRule(allAppPackagesSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            // Allow Everyone (S-1-1-0) to read & write
            var everyoneSid = new SecurityIdentifier("S-1-1-0");
            pipeSecurity.AddAccessRule(new PipeAccessRule(everyoneSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 8192,
                outBufferSize: 8192,
                pipeSecurity: pipeSecurity
            );
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipeStream, int clientId, CancellationToken cancellationToken)
        {
            var utf8NoBom = new System.Text.UTF8Encoding(false);
            using (pipeStream)
            using (var reader = new StreamReader(pipeStream, utf8NoBom, leaveOpen: true))
            using (var writer = new StreamWriter(pipeStream, utf8NoBom, leaveOpen: true) { AutoFlush = true })
            {
                _activeClients[clientId] = writer;
                OnClientConnectionChanged?.Invoke(true);

                try
                {
                    while (!cancellationToken.IsCancellationRequested && pipeStream.IsConnected)
                    {
                        string? line = await reader.ReadLineAsync(cancellationToken);
                        if (line == null) break; // Client disconnected

                        line = line.Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        IpcResponse response;
                        try
                        {
                            var command = JsonSerializer.Deserialize<IpcCommand>(line);
                            if (command != null && OnCommandReceived != null)
                            {
                                response = await OnCommandReceived(command);
                            }
                            else
                            {
                                response = new IpcResponse
                                {
                                    Type = "ERROR",
                                    Success = false,
                                    Message = "Unknown command or handler not configured"
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            response = new IpcResponse
                            {
                                Type = "ERROR",
                                Success = false,
                                Message = $"Failed to parse command: {ex.Message}"
                            };
                        }

                        string responseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson.AsMemory(), cancellationToken);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IPC Server] Client {clientId} error: {ex.Message}");
                }
                finally
                {
                    _activeClients.TryRemove(clientId, out _);
                    OnClientConnectionChanged?.Invoke(_activeClients.Count > 0);
                }
            }
        }

        public async Task BroadcastAsync(IpcResponse response)
        {
            if (_activeClients.IsEmpty) return;

            string json = JsonSerializer.Serialize(response);
            var deadClients = new List<int>();

            foreach (var kvp in _activeClients)
            {
                try
                {
                    await kvp.Value.WriteLineAsync(json);
                }
                catch
                {
                    deadClients.Add(kvp.Key);
                }
            }

            foreach (int id in deadClients)
            {
                _activeClients.TryRemove(id, out _);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
