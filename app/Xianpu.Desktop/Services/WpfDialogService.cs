using Microsoft.Win32;
using System.Windows;

namespace Xianpu.Desktop.Services;

/// <summary>
/// WPF implementation of IDialogService backed by AppDialogs and native Win32 file dialogs.
/// </summary>
internal sealed class WpfDialogService : IDialogService
{
    private readonly Window _owner;

    public WpfDialogService(Window owner)
    {
        _owner = owner;
    }

    public void ShowInfo(string title, string message, string? actionLabel = null, Action? actionCallback = null) =>
        AppDialogs.ShowInfo(_owner, title, message, actionLabel, actionCallback);

    public void ShowError(string title, string message) =>
        AppDialogs.ShowError(_owner, title, message);

    public bool Confirm(string title, string message, string confirmText = "确定", bool isDanger = false) =>
        AppDialogs.Confirm(_owner, title, message, confirmText, isDanger);

    public string? PromptSingleLine(string title, string label, string initial = "") =>
        AppDialogs.PromptSingleLine(_owner, title, label, initial);

    public string? PromptLyrics(string title, string label, string initial = "", double height = 360) =>
        AppDialogs.PromptLyrics(_owner, title, label, initial, height);

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }
}
