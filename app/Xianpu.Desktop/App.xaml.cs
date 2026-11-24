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
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0]) && File.Exists(e.Args[0]))
        {
            StartupFilePath = e.Args[0];
        }
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LibraryStore.AppendCrashLog("DispatcherUnhandledException", e.Exception);
        MessageBox.Show(
            $"弦笺遇到未处理的异常：\n{e.Exception.Message}\n\n详细日志已记录到：\n{LibraryStore.CrashLogPath}",
            "弦笺运行错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LibraryStore.AppendCrashLog("AppDomain.UnhandledException", ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LibraryStore.AppendCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
