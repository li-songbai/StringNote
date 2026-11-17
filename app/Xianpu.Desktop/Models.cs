using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Xianpu.Desktop;

public sealed class LibraryData
{
    public ObservableCollection<string> Categories { get; set; } = new() { "民谣", "练习" };
    public ObservableCollection<SongModel> Songs { get; set; } = [];
    public Dictionary<string, List<string>> CustomChordsByKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CustomChordDefinition> CustomChordShapes { get; set; } = [];
}

public sealed class CustomChordDefinition
{
    public string Name { get; set; } = "";
    public int BaseFret { get; set; } = 1;
    public int?[] Frets { get; set; } = new int?[6];
    public int[] Fingers { get; set; } = new int[6];
    public string Label { get; set; } = "自定义和弦";
}

public sealed class SongModel : INotifyPropertyChanged
{
    private string _title = "未命名歌曲";
    private string _originalKey = "";
    private string _selectedKey = "";
    private string _capo = "";
    private string _category = "未分类";
    private bool _favorite;
    private string _chordDisplayMode = "name";
    private int _columnMode = 0; // 0 = auto (2 columns when Lines.Count > 14, else 1), 1 = 1 col, 2 = 2 cols
    private bool _showChordGuide = false;
    private bool _showTabSections = true;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => Set(ref _title, value); }
    public string OriginalKey { get => _originalKey; set => Set(ref _originalKey, value); }
    public string SelectedKey { get => _selectedKey; set { if (Set(ref _selectedKey, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public string Capo { get => _capo; set { if (Set(ref _capo, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public string Category { get => _category; set { if (Set(ref _category, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public bool Favorite { get => _favorite; set => Set(ref _favorite, value); }
    public string ChordDisplayMode { get => _chordDisplayMode; set => Set(ref _chordDisplayMode, value); }
    public int ColumnMode { get => _columnMode; set => Set(ref _columnMode, value); }
    public bool ShowChordGuide { get => _showChordGuide; set => Set(ref _showChordGuide, value); }
    public bool ShowTabSections { get => _showTabSections; set => Set(ref _showTabSections, value); }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<LyricLine> Lines { get; set; } = [];
    public ObservableCollection<TabSection> TabSections { get; set; } = [];
    public string ListSummary => string.Join(" · ", new[] { Category, SelectedKey, Capo }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class LyricLine
{
    public string Text { get; set; } = "";
    public List<ChordPlacement> Chords { get; set; } = [];
}

public sealed class ChordPlacement
{
    public int Position { get; set; }
    public bool BeforeChar { get; set; }
    public string Name { get; set; } = "";
    public string ShapeId { get; set; } = "";
}

public sealed record ChordAnchor(int LineIndex, int Position, bool BeforeChar = false);

public sealed record BarreShape(int Fret, int FromString, int EndString);

public sealed class ChordShape
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Type { get; init; } = "open";
    public int?[] Frets { get; init; } = new int?[6];
    public int[] Fingers { get; init; } = new int[6];
    public int BaseFret { get; init; } = 1;
    public BarreShape? Barre { get; init; }
    public string Label { get; init; } = "开放和弦";
}

public enum TabSectionKind
{
    Intro,
    Interlude,
    Outro
}

public sealed class TabSection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TabSectionKind Kind { get; set; } = TabSectionKind.Intro;
    public string Title { get; set; } = "前奏";
    public int InsertAfterLineIndex { get; set; } = -1; // -1 = 前奏(歌词前), >= 0 = 插在对应歌词行后
    public string TimeSignature { get; set; } = "4/4";
    public int ColumnsPerBar { get; set; } = 8;
    public List<TabBar> Bars { get; set; } = [];

    public TabSection Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Kind = Kind,
        Title = Title,
        InsertAfterLineIndex = InsertAfterLineIndex,
        TimeSignature = TimeSignature,
        ColumnsPerBar = ColumnsPerBar,
        Bars = Bars.Select(b => b.Clone()).ToList()
    };
}

public sealed class TabBar
{
    public int ColumnCount { get; set; } = 8;
    public List<TabNote> Notes { get; set; } = [];
    public List<TabChordMark> ChordMarks { get; set; } = [];

    public TabBar Clone() => new()
    {
        ColumnCount = ColumnCount,
        Notes = Notes.Select(n => new TabNote { Column = n.Column, StringIndex = n.StringIndex, Fret = n.Fret }).ToList(),
        ChordMarks = ChordMarks.Select(m => new TabChordMark { Column = m.Column, Text = m.Text }).ToList()
    };
}

public sealed class TabNote
{
    public int Column { get; set; }
    public int StringIndex { get; set; } // 0 = 1弦(e) .. 5 = 6弦(E)
    public string Fret { get; set; } = "";
}

public sealed class TabChordMark
{
    public int Column { get; set; }
    public string Text { get; set; } = "";
}
