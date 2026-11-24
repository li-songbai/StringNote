namespace Xianpu.Desktop.Services;

/// <summary>
/// Abstraction over desktop dialogs and file pickers so ViewModels remain UI-independent and unit-testable.
/// </summary>
public interface IDialogService
{
    void ShowInfo(string title, string message, string? actionLabel = null, Action? actionCallback = null);
    void ShowError(string title, string message);
    bool Confirm(string title, string message, string confirmText = "确定", bool isDanger = false);
    string? PromptSingleLine(string title, string label, string initial = "");
    string? PromptLyrics(string title, string label, string initial = "", double height = 360);
    string? PickOpenFile(string title, string filter);
    string? PickSaveFile(string title, string filter, string defaultFileName);
}
