namespace Vipi.Sectorfile.Models;

/// <summary>
/// Type-erased view of a <see cref="LayerSignal{T}"/>, letting <see cref="SectorPackage"/> hold all
/// its signals in one list and cancel them uniformly without naming each generic argument.
/// </summary>
public interface ILayerSignal
{
    /// <summary>Cancels the signal if it has not already completed, cancelled, or faulted.</summary>
    void Cancel();
}

/// <summary>
/// A one-shot completion signal for a Phase 1 layer load. Exposes a <see cref="Task{T}"/>
/// that consumers (AirportLoader, render-time fix resolution) await; the GlobalLayerLoader
/// completes, cancels, or faults it. Continuations run asynchronously so loader threads are
/// never hijacked by awaiting consumers.
/// </summary>
public sealed class LayerSignal<T> : ILayerSignal
{
    private readonly TaskCompletionSource<T> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<T> Task => _tcs.Task;

    public void Complete(T value) => _tcs.TrySetResult(value);

    public void Cancel() => _tcs.TrySetCanceled();

    public void Fail(Exception exception) => _tcs.TrySetException(exception);
}
