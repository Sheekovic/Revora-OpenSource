using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Revora.ClientDoctor.Core;
namespace Revora.ClientDoctor;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += HandleDispatcherException;
        base.OnStartup(e);
    }

    private void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is OutOfMemoryException) return;
        try
        {
            string directory = Revora.ClientDoctor.MainWindow.ReportDirectory();
            Directory.CreateDirectory(directory);
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            File.WriteAllText(Path.Combine(directory, $"doctor-crash-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.json"), SanitizedReport.CreateUnhandled(version));
        }
        catch (Exception reportError) when (reportError is IOException or UnauthorizedAccessException) { }
        e.Handled = true;
        Shutdown(2);
    }
}
