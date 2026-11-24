using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Xianpu.Desktop;

public partial class MainWindow
{
    private void LibraryToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (LibraryDrawer is null || LibraryColumn is null || LibraryToggle is null) return;
        var open = LibraryToggle.IsChecked == true;
        if (!open && LibraryColumn.ActualWidth > 100)
            _savedLibraryWidth = Math.Clamp(LibraryColumn.ActualWidth, 180, 420);

        LibraryDrawer.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        LibraryColumn.MinWidth = open ? 170 : 0;
        LibraryColumn.Width = open ? new GridLength(_savedLibraryWidth) : new GridLength(0);
        if (SplitterColumn is not null)
            SplitterColumn.Width = open ? new GridLength(5) : new GridLength(0);
        if (WorkspaceSplitter is not null)
            WorkspaceSplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;

        LibraryToggle.Background = open
            ? new SolidColorBrush(Color.FromRgb(234, 243, 237))
            : Brushes.White;
        LibraryToggle.BorderBrush = open
            ? new SolidColorBrush(Color.FromRgb(148, 191, 164))
            : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryToggle.Foreground = open
            ? new SolidColorBrush(Color.FromRgb(36, 85, 56))
            : new SolidColorBrush(Color.FromRgb(51, 65, 85));

        UpdateViewportScale();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateViewportScale);
    }

    private void RefreshCategoryOptions()
    {
        if (LibraryCategoryFilter is null || SongCategoryBox is null) return;

        if (_library.Categories.Count == 0)
        {
            _library.Categories.Add("民谣");
            _library.Categories.Add("练习");
        }

        foreach (var song in _library.Songs)
        {
            var cat = song.Category?.Trim();
            if (!string.IsNullOrWhiteSpace(cat) && cat != "未分类" && !_library.Categories.Contains(cat))
                _library.Categories.Add(cat);
        }

        var prevFilter = LibraryCategoryFilter.SelectedItem as string;
        var filterItems = new List<string> { AllCategoriesLabel };
        filterItems.AddRange(_library.Categories);

        var prevInit = _initializing;
        _initializing = true;
        LibraryCategoryFilter.ItemsSource = filterItems;
        LibraryCategoryFilter.SelectedItem = prevFilter is not null && filterItems.Contains(prevFilter)
            ? prevFilter
            : AllCategoriesLabel;

        var songCats = new List<string> { "未分类" };
        foreach (var c in _library.Categories)
        {
            if (!songCats.Contains(c))
                songCats.Add(c);
        }
        SongCategoryBox.ItemsSource = songCats;
        if (_selectedSong is not null)
            SongCategoryBox.Text = string.IsNullOrWhiteSpace(_selectedSong.Category) ? "未分类" : _selectedSong.Category;
        _initializing = prevInit;
    }

    private void RefreshLibraryList()
    {
        if (LibrarySongList is null) return;

        var query = LibrarySearchBox?.Text?.Trim() ?? "";
        if (LibrarySearchPlaceholder is not null)
            LibrarySearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        _vm.SelectedCategoryFilter = LibraryCategoryFilter?.SelectedItem as string ?? AllCategoriesLabel;
        _vm.ComputeFilteredSongs();
        SyncLibraryListSelection();
    }

    private void SyncLibraryListSelection()
    {
        if (LibrarySongList is null) return;
        var prevInit = _initializing;
        _initializing = true;
        LibrarySongList.SelectedItem = _selectedSong is not null && _vm.FilteredSongs.Contains(_selectedSong) ? _selectedSong : null;
        _initializing = prevInit;
    }

    private void LibrarySongList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (LibrarySongList.SelectedItem is SongModel song && !ReferenceEquals(song, _selectedSong))
        {
            SelectSong(song);
            SetStatus($"已切换到曲谱《{song.Title}》。");
        }
    }

    private void LibraryFilter_Changed(object sender, EventArgs e)
    {
        if (_initializing) return;
        RefreshLibraryList();
    }

    private void LibraryAllFilter_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = false;
        UpdateLibraryFilterButtons();
        RefreshLibraryList();
    }

    private void LibraryFavFilter_Click(object sender, RoutedEventArgs e)
    {
        _favoritesOnly = true;
        UpdateLibraryFilterButtons();
        RefreshLibraryList();
    }

    private void UpdateLibraryFilterButtons()
    {
        if (LibraryAllFilterButton is null || LibraryFavFilterButton is null) return;

        LibraryAllFilterButton.Background = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(234, 243, 237)) : Brushes.White;
        LibraryAllFilterButton.BorderBrush = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(47, 107, 72)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryAllFilterButton.Foreground = !_favoritesOnly ? new SolidColorBrush(Color.FromRgb(36, 85, 56)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));

        LibraryFavFilterButton.Background = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(254, 243, 199)) : Brushes.White;
        LibraryFavFilterButton.BorderBrush = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(217, 119, 6)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        LibraryFavFilterButton.Foreground = _favoritesOnly ? new SolidColorBrush(Color.FromRgb(180, 83, 9)) : new SolidColorBrush(Color.FromRgb(51, 65, 85));
    }

    private void SaveToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();
        if (!_library.Songs.Contains(_selectedSong))
            _library.Songs.Add(_selectedSong);
        SaveLibraryQuietly();
        RefreshCategoryOptions();
        RefreshLibraryList();
        SetStatus("已保存到本地曲库。");
    }

    private void DuplicateSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        CommitFields();

        var clone = ViewModels.MainViewModel.CreateDuplicateSong(_selectedSong);

        _library.Songs.Add(clone);
        SelectSong(clone);
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已创建副本《{clone.Title}》。");
    }

    private void DeleteSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var title = string.IsNullOrWhiteSpace(_selectedSong.Title) ? "未命名歌曲" : _selectedSong.Title;
        if (!AppDialogs.Confirm(this, "删除曲谱", $"确定从本地曲库删除《{title}》吗？", "确定删除", isDanger: true))
            return;

        var toRemove = _selectedSong;
        _library.Songs.Remove(toRemove);

        var next = _library.Songs.OrderByDescending(x => x.UpdatedAt).FirstOrDefault();
        if (next is null)
        {
            next = new SongModel
            {
                Title = "未命名歌曲",
                OriginalKey = "C",
                SelectedKey = "C",
                Capo = "",
                ColumnMode = 0
            };
            _library.Songs.Add(next);
        }

        SelectSong(next);
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已从本地曲库删除《{title}》。");
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptSingleLine("新增曲库分类", "请输入分类名称（例如：指弹、弹唱、摇滚）：");
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();
        if (trimmed != "未分类" && !_library.Categories.Contains(trimmed))
            _library.Categories.Add(trimmed);

        if (_selectedSong is not null)
        {
            _selectedSong.Category = trimmed;
            _selectedSong.UpdatedAt = DateTime.Now;
        }

        RefreshCategoryOptions();
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已添加并应用分类「{trimmed}」。");
    }

    private void SongCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        if (SongCategoryBox.SelectedItem is string val)
            ApplySongCategory(val);
    }

    private void SongCategoryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null) return;
        ApplySongCategory(SongCategoryBox.Text);
    }

    private void SongCategoryBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _selectedSong is not null)
        {
            ApplySongCategory(SongCategoryBox.Text);
            ScoreSurface.Focus();
            e.Handled = true;
        }
    }

    private void ApplySongCategory(string? rawCategory)
    {
        if (_selectedSong is null) return;
        var cat = string.IsNullOrWhiteSpace(rawCategory) ? "未分类" : rawCategory.Trim();
        if (_selectedSong.Category == cat) return;

        if (cat != "未分类" && !_library.Categories.Contains(cat))
            _library.Categories.Add(cat);

        _selectedSong.Category = cat;
        _selectedSong.UpdatedAt = DateTime.Now;
        RefreshCategoryOptions();
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void SongFavoriteToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing || _selectedSong is null || SongFavoriteToggle is null) return;
        _selectedSong.Favorite = SongFavoriteToggle.IsChecked == true;
        UpdateFavoriteToggleVisual();
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
    }

    private void ToggleSelectedFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        _selectedSong.Favorite = !_selectedSong.Favorite;
        var prevInit = _initializing;
        _initializing = true;
        if (SongFavoriteToggle is not null)
            SongFavoriteToggle.IsChecked = _selectedSong.Favorite;
        _initializing = prevInit;
        UpdateFavoriteToggleVisual();
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus(_selectedSong.Favorite ? $"已收藏《{_selectedSong.Title}》。" : $"已取消收藏《{_selectedSong.Title}》。");
    }

    private void UpdateFavoriteToggleVisual()
    {
        if (SongFavoriteToggle is null) return;
        var fav = SongFavoriteToggle.IsChecked == true;
        SongFavoriteToggle.Content = fav ? "★ 已收藏" : "☆ 收藏";
        SongFavoriteToggle.Background = fav ? new SolidColorBrush(Color.FromRgb(254, 243, 199)) : Brushes.White;
        SongFavoriteToggle.BorderBrush = fav ? new SolidColorBrush(Color.FromRgb(217, 119, 6)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        SongFavoriteToggle.Foreground = fav ? new SolidColorBrush(Color.FromRgb(180, 83, 9)) : new SolidColorBrush(Color.FromRgb(71, 85, 105));
    }

    private void RenameSong_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSong is null) return;
        var updated = PromptSingleLine("重命名曲谱", "请输入新的歌曲名称：", _selectedSong.Title);
        if (string.IsNullOrWhiteSpace(updated)) return;
        var trimmed = updated.Trim();
        _initializing = true;
        TitleBox.Text = trimmed;
        _initializing = false;
        _selectedSong.Title = trimmed;
        _selectedSong.UpdatedAt = DateTime.Now;
        SaveLibraryQuietly();
        RefreshLibraryList();
        SetStatus($"已重命名为《{trimmed}》。");
    }
}
