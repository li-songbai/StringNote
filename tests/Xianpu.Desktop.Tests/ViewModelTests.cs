using Xianpu.Desktop;
using Xianpu.Desktop.Services;
using Xianpu.Desktop.ViewModels;
using Xunit;

namespace Xianpu.Desktop.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public void SongUndoManager_UndoRedoAndMaxCap_WorkDeterministically()
    {
        var undo = new SongUndoManager(maxSteps: 3);
        var song = new SongModel { Title = "V0", Bpm = 80 };
        undo.Reset(song);

        Assert.False(undo.CanUndo);
        Assert.False(undo.CanRedo);

        // Recording identical state (only UpdatedAt changed) should not push an undo step
        song.UpdatedAt = DateTime.Now.AddMinutes(1);
        Assert.False(undo.RecordIfChanged(song));
        Assert.False(undo.CanUndo);

        for (var i = 1; i <= 4; i++)
        {
            song.Title = $"V{i}";
            Assert.True(undo.RecordIfChanged(song));
        }

        // Max cap is 3
        Assert.Equal(3, undo.UndoCount);
        Assert.True(undo.TryPopUndo(out var snap3));
        Assert.Equal("V3", LibraryStore.DeserializeSongSnapshot(snap3!)?.Title);
        Assert.True(undo.CanRedo);

        Assert.True(undo.TryPopRedo(out var snap4));
        Assert.Equal("V4", LibraryStore.DeserializeSongSnapshot(snap4!)?.Title);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void MainViewModel_FilteringAndDuplicateAndCommands_WorkWithoutWindow()
    {
        var vm = new MainViewModel();
        var s1 = new SongModel
        {
            Title = "晴天",
            SelectedKey = "G",
            Category = "流行",
            Favorite = true,
            Bpm = 74,
            Lines = [new LyricLine { Text = "故事的小黄花" }]
        };
        var s2 = new SongModel
        {
            Title = "成都",
            SelectedKey = "C",
            Category = "民谣",
            Favorite = false,
            Bpm = 68,
            Lines = [new LyricLine { Text = "让我掉下眼泪的" }]
        };
        vm.Library.Songs.Add(s1);
        vm.Library.Songs.Add(s2);

        vm.SearchQuery = "小黄花";
        var byLyric = vm.ComputeFilteredSongs();
        Assert.Single(byLyric);
        Assert.Equal("晴天", byLyric[0].Title);

        vm.SearchQuery = "";
        vm.FavoritesOnly = true;
        var byFav = vm.ComputeFilteredSongs();
        Assert.Single(byFav);
        Assert.Equal("晴天", byFav[0].Title);

        var dup = MainViewModel.CreateDuplicateSong(s1);
        Assert.NotEqual(s1.Id, dup.Id);
        Assert.Equal(74, dup.Bpm);
        Assert.Equal("晴天 (副本)", dup.Title);

        // UndoCommand / RedoCommand CanExecute reactivity
        vm.UndoManager.Reset(s1);
        vm.RefreshUndoState();
        Assert.False(vm.UndoCommand.CanExecute(null));

        s1.Bpm = 80;
        vm.UndoManager.RecordIfChanged(s1);
        vm.RefreshUndoState();
        Assert.True(vm.UndoCommand.CanExecute(null));

        var undoInvoked = false;
        vm.UndoRequested += () => undoInvoked = true;
        vm.UndoCommand.Execute(null);
        Assert.True(undoInvoked);
    }

    [Fact]
    public void PracticeViewModel_CyclesMetersAndUpdatesBeatState()
    {
        var practice = new PracticeViewModel();
        Assert.Equal("4/4", practice.MetronomeMeterLabel);
        Assert.Equal(4, practice.CurrentBeatsPerBar);

        var next = practice.CycleMeter();
        Assert.Equal(("3/4", 3), next);
        Assert.Equal("3/4", practice.MetronomeMeterLabel);

        practice.SetMetronomeRunning(96);
        Assert.True(practice.IsMetronomeActive);
        Assert.Equal("■ 96", practice.MetronomeButtonText);

        practice.UpdateBeat(1, 3, 96);
        Assert.Equal("● 1/3", practice.MetronomeBeatText);
        Assert.True(practice.IsAccentBeat);

        practice.SetMetronomeStopped();
        Assert.False(practice.IsMetronomeActive);
        Assert.Equal("♩ 节拍器", practice.MetronomeButtonText);
        Assert.Equal("", practice.MetronomeBeatText);
    }
}
