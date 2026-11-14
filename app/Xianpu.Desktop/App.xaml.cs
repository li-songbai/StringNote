using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Xianpu.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static string? StartupFilePath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        if (e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0]) && File.Exists(e.Args[0]))
        {
            StartupFilePath = e.Args[0];
        }
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"弦谱遇到未处理的异常：\n{e.Exception.Message}",
            "弦谱运行错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
