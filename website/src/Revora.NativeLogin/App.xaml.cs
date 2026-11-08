using System.Windows;
using System.Windows.Threading;
namespace Revora.NativeLogin;
public partial class App : Application
{
    public App() => DispatcherUnhandledException += ReportUnhandled;

    private static void ReportUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Revora.NativeLogin.MainWindow.TryWriteReport("application-failed", "Unexpected");
        e.Handled = false;
    }
}
