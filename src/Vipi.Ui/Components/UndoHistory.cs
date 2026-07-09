using System.Text.Json;

namespace Vipi.Ui.Components;

// Cronologia undo/redo puramente locale (in memoria del circuito Blazor): nessuna persistenza server.
// Coerente col modello autosave: lo stato "corrente" riflette ciò che è già stato salvato; qui teniamo
// solo la pila degli stati precedenti/successivi per poterli ripristinare e ri-salvare.
// T è tipicamente uno snapshot immutabile-per-uso (deep-clone via Clone()) dello stato editabile completo.
public sealed class UndoHistory<T>
{
    private readonly List<T> _undo = new();
    private readonly List<T> _redo = new();
    private readonly int _cap;
    private T _current = default!;
    private bool _has;

    public UndoHistory(int cap = 50) => _cap = Math.Max(1, cap);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // (Ri)inizializza con lo stato caricato: azzera la cronologia.
    public void Reset(T state)
    {
        _undo.Clear();
        _redo.Clear();
        _current = state;
        _has = true;
    }

    // Registra una nuova mutazione utente: lo stato corrente scende nella pila undo, il nuovo diventa corrente,
    // la pila redo viene invalidata (nuovo ramo).
    public void Push(T newState)
    {
        if (!_has) { _current = newState; _has = true; return; }
        _undo.Add(_current);
        if (_undo.Count > _cap) _undo.RemoveAt(0);
        _current = newState;
        _redo.Clear();
    }

    public bool Undo(out T state)
    {
        if (_undo.Count == 0) { state = default!; return false; }
        _redo.Add(_current);
        _current = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        state = _current;
        return true;
    }

    public bool Redo(out T state)
    {
        if (_redo.Count == 0) { state = default!; return false; }
        _undo.Add(_current);
        _current = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        state = _current;
        return true;
    }

    // Deep-clone via round-trip JSON: isola gli snapshot dallo stato mutabile in-memory dell'editor
    // (liste/record annidati), evitando aliasing tra pila e stato corrente.
    public static T Clone(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
