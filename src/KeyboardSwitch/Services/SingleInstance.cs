using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardSwitch.Services;

/// <summary>
/// Cross-process single-instance guard using a named Mutex + a named pipe server for signalling.
/// When a second instance starts, it sends "show" through the pipe and exits; the first instance
/// receives it and raises <see cref="ShowWindowRequested"/>.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Global\\KeyboardSwitch-SingleInstance-Mutex";
    private const string PipeName = "KeyboardSwitch-SingleInstance-Pipe";

    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event EventHandler? ShowWindowRequested;

    /// <summary>Returns true if this is the first (and only) instance.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            SignalExistingInstance();
            return false;
        }

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PipeServerLoop(_cts.Token));
        return true;
    }

    private void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new System.IO.StreamWriter(client);
            writer.WriteLine("show");
            writer.Flush();
        }
        catch
        {
            // Best effort — the other instance may be busy.
        }
    }

    private async Task PipeServerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(ct);
                using var reader = new System.IO.StreamReader(server);
                var line = await reader.ReadLineAsync();
                if (line == "show")
                    ShowWindowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore one-off pipe errors */ }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cts?.Cancel(); } catch { }
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
    }
}
