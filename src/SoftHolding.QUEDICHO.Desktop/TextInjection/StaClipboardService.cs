using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;

namespace SoftHolding.QUEDICHO.Desktop.TextInjection;

internal sealed partial class StaClipboardService : IDisposable
{
    private readonly BlockingCollection<Action> _workItems = [];
    private readonly Thread _thread;
    private bool _disposed;

    public StaClipboardService()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "QUEDICHO Clipboard"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<ClipboardLease?> TrySetTextAsync(string text) => InvokeAsync(() =>
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                var previousData = Clipboard.GetDataObject();
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                return new ClipboardLease(previousData, GetClipboardSequenceNumber());
            }
            catch (Exception exception) when (exception is COMException or ExternalException)
            {
                Thread.Sleep(25);
            }
        }

        return null;
    });

    public Task<bool> RestoreIfUnchangedAsync(ClipboardLease lease) => InvokeAsync(() =>
    {
        if (GetClipboardSequenceNumber() != lease.SequenceNumber)
        {
            return false;
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                if (lease.PreviousData is null)
                {
                    Clipboard.Clear();
                }
                else
                {
                    Clipboard.SetDataObject(lease.PreviousData, true);
                }

                return true;
            }
            catch (Exception exception) when (exception is COMException or ExternalException)
            {
                Thread.Sleep(25);
            }
        }

        return false;
    });

    private Task<T> InvokeAsync<T>(Func<T> operation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _workItems.Add(() =>
        {
            try
            {
                completion.SetResult(operation());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    private void Run()
    {
        foreach (var workItem in _workItems.GetConsumingEnumerable())
        {
            workItem();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _workItems.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _workItems.Dispose();
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetClipboardSequenceNumber();
}

internal sealed record ClipboardLease(IDataObject? PreviousData, uint SequenceNumber);
