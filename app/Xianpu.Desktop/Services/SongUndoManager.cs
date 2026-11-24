namespace Xianpu.Desktop.Services;

/// <summary>
/// Pure, UI-independent song snapshot undo/redo stack (up to 50 steps).
/// </summary>
public sealed class SongUndoManager
{
    public const int DefaultMaxSteps = 50;

    private readonly int _maxSteps;
    private readonly List<string> _undoStack = [];
    private readonly List<string> _redoStack = [];
    private string? _currentSnapshot;

    public SongUndoManager(int maxSteps = DefaultMaxSteps)
    {
        _maxSteps = Math.Max(1, maxSteps);
    }

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? CurrentSnapshot => _currentSnapshot;

    public void Reset(SongModel? song)
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentSnapshot = song is null ? null : LibraryStore.SerializeSongSnapshot(song);
    }

    public bool RecordIfChanged(SongModel? song)
    {
        if (song is null)
            return false;

        var nextSnapshot = LibraryStore.SerializeSongSnapshot(song);
        if (_currentSnapshot is not null &&
            !string.Equals(nextSnapshot, _currentSnapshot, StringComparison.Ordinal))
        {
            _undoStack.Add(_currentSnapshot);
            if (_undoStack.Count > _maxSteps)
                _undoStack.RemoveAt(0);
            _redoStack.Clear();
            _currentSnapshot = nextSnapshot;
            return true;
        }

        _currentSnapshot = nextSnapshot;
        return false;
    }

    public bool TryPopUndo(out string? targetSnapshot)
    {
        if (_undoStack.Count == 0)
        {
            targetSnapshot = null;
            return false;
        }

        targetSnapshot = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        if (_currentSnapshot is not null)
            _redoStack.Add(_currentSnapshot);

        _currentSnapshot = targetSnapshot;
        return true;
    }

    public bool TryPopRedo(out string? targetSnapshot)
    {
        if (_redoStack.Count == 0)
        {
            targetSnapshot = null;
            return false;
        }

        targetSnapshot = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        if (_currentSnapshot is not null)
            _undoStack.Add(_currentSnapshot);

        _currentSnapshot = targetSnapshot;
        return true;
    }
}
