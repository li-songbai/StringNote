using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Xianpu.Desktop;

public sealed class LibraryData
{
    public ObservableCollection<string> Categories { get; set; } = new() { "民谣", "练习" };
    public ObservableCollection<SongModel> Songs { get; set; } = [];
    public Dictionary<string, List<string>> CustomChordsByKey { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CustomChordDefinition> CustomChordShapes { get; set; } = [];
    public List<TheoryNoteItem> TheoryNotes { get; set; } = [];
}

public sealed class TheoryNoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "未命名乐理卡片";
    public string Tag { get; set; } = "CAGED";
    public string Content { get; set; } = "";
    public int TonicPitch { get; set; } = 0; // 0 = C
    public string ScaleId { get; set; } = "major";
    public string TuningId { get; set; } = "standard";
    public int Capo { get; set; } = 0;
    public bool ShowCaged { get; set; } = true;
    public string ActiveCagedFilter { get; set; } = "ALL"; // ALL / C / A / G / E / D
    public bool ShowCapsules { get; set; } = false;
    public string HighlightChord { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
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
    private string _artist = "";
    private string _originalKey = "";
    private string _selectedKey = "";
    private string _capo = "";
    private int _bpm;
    private string _category = "未分类";
    private bool _favorite;
    private string _chordDisplayMode = "name";
    private int _columnMode = 0; // 0 = auto (2 columns when Lines.Count > 14, else 1), 1 = 1 col, 2 = 2 cols
    private bool _showChordGuide = false;
    private bool _showTabSections = true;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Artist { get => _artist; set { if (Set(ref _artist, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public string OriginalKey { get => _originalKey; set => Set(ref _originalKey, value); }
    public string SelectedKey { get => _selectedKey; set { if (Set(ref _selectedKey, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public string Capo { get => _capo; set { if (Set(ref _capo, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public int Bpm { get => _bpm; set => Set(ref _bpm, value); }
    public string Category { get => _category; set { if (Set(ref _category, value)) OnPropertyChanged(nameof(ListSummary)); } }
    public bool Favorite { get => _favorite; set => Set(ref _favorite, value); }
    public string ChordDisplayMode { get => _chordDisplayMode; set => Set(ref _chordDisplayMode, value); }
    public int ColumnMode { get => _columnMode; set => Set(ref _columnMode, value); }
    public bool ShowChordGuide { get => _showChordGuide; set => Set(ref _showChordGuide, value); }
    public bool ShowTabSections { get => _showTabSections; set => Set(ref _showTabSections, value); }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<LyricLine> Lines { get; set; } = [];
    public ObservableCollection<TabSection> TabSections { get; set; } = [];
    public string ListSummary => string.Join(" · ", new[] { Artist, Category, SelectedKey, Capo }.Where(x => !string.IsNullOrWhiteSpace(x)));

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
    public List<string> AltLyrics { get; set; } = [];
    public string PartTitle { get; set; } = "";
    public int RepeatCount { get; set; } = 1;
    public bool IsReferenceLine { get; set; }
    public List<ChordPlacement> Chords { get; set; } = [];

    [JsonIgnore]
    public bool HasStackedLyrics => AltLyrics is { Count: > 0 };

    public LyricLine Clone() => new()
    {
        Text = Text,
        AltLyrics = AltLyrics is { Count: > 0 } ? [.. AltLyrics] : [],
        PartTitle = PartTitle,
        RepeatCount = RepeatCount,
        IsReferenceLine = IsReferenceLine,
        Chords = Chords.Select(c => new ChordPlacement
        {
            Position = c.Position,
            BeforeChar = c.BeforeChar,
            Name = c.Name,
            ShapeId = c.ShapeId
        }).ToList()
    };
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

public enum TabDuration
{
    Auto = 0,
    Whole = 1,
    Half = 2,
    Quarter = 4,
    Eighth = 8,
    Sixteenth = 16
}

public enum TabStrumKind
{
    None = 0,
    Down = 1,
    Up = 2,
    Mute = 3,
    Arpeggio = 4
}

public enum TabLinkKind
{
    None = 0,
    HammerOn = 1,
    PullOff = 2,
    Slide = 3,
    Tie = 4
}

public enum TabNoteTechnique
{
    None = 0,
    BendFull = 1,
    BendHalf = 2,
    BendRelease = 3,
    NaturalHarmonic = 4,
    ArtificialHarmonic = 5,
    Vibrato = 6,
    GhostNote = 7,
    SlideIn = 8,
    SlideOut = 9,
    BendHalfRelease = 10
}

public sealed class TabSection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TabSectionKind Kind { get; set; } = TabSectionKind.Intro;
    public string Title { get; set; } = "前奏";
    public int InsertAfterLineIndex { get; set; } = -1; // -1 = 前奏(歌词前), >= 0 = 插在对应歌词行后
    public string TimeSignature { get; set; } = "4/4";
    public int ColumnsPerBar { get; set; } = 8;
    public bool ShowRhythmStems { get; set; } = true;
    public List<TabBar> Bars { get; set; } = [];

    public TabSection Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Kind = Kind,
        Title = Title,
        InsertAfterLineIndex = InsertAfterLineIndex,
        TimeSignature = TimeSignature,
        ColumnsPerBar = ColumnsPerBar,
        ShowRhythmStems = ShowRhythmStems,
        Bars = Bars.Select(b => b.Clone()).ToList()
    };
}

public sealed class TabBar
{
    public int ColumnCount { get; set; } = 8;
    public bool RepeatStart { get; set; }
    public bool RepeatEnd { get; set; }
    public List<TabNote> Notes { get; set; } = [];
    public List<TabChordMark> ChordMarks { get; set; } = [];
    public List<TabColumnMeta> ColumnMetas { get; set; } = [];

    public TabBar Clone() => new()
    {
        ColumnCount = ColumnCount,
        RepeatStart = RepeatStart,
        RepeatEnd = RepeatEnd,
        Notes = Notes.Select(n => new TabNote
        {
            Column = n.Column,
            StringIndex = n.StringIndex,
            Fret = n.Fret,
            LinkToNext = n.LinkToNext,
            GraceFret = n.GraceFret,
            GraceLink = n.GraceLink,
            Technique = n.Technique
        }).ToList(),
        ChordMarks = ChordMarks.Select(m => new TabChordMark { Column = m.Column, Text = m.Text }).ToList(),
        ColumnMetas = ColumnMetas.Select(m => m.Clone()).ToList()
    };
}

public sealed class TabNote
{
    public int Column { get; set; }
    public int StringIndex { get; set; } // 0 = 1弦(e) .. 5 = 6弦(E)
    public string Fret { get; set; } = "";
    public TabLinkKind LinkToNext { get; set; } = TabLinkKind.None;
    public string GraceFret { get; set; } = "";
    public TabLinkKind GraceLink { get; set; } = TabLinkKind.None;
    public TabNoteTechnique Technique { get; set; } = TabNoteTechnique.None;
}

public sealed class TabColumnMeta
{
    public int Column { get; set; }
    public TabDuration Duration { get; set; } = TabDuration.Auto;
    public bool Dotted { get; set; }
    public bool IsRest { get; set; }
    public TabStrumKind Strum { get; set; } = TabStrumKind.None;
    public bool PalmMute { get; set; }

    public TabColumnMeta Clone() => new()
    {
        Column = Column,
        Duration = Duration,
        Dotted = Dotted,
        IsRest = IsRest,
        Strum = Strum,
        PalmMute = PalmMute
    };
}

public sealed class TabChordMark
{
    public int Column { get; set; }
    public string Text { get; set; } = "";
}
