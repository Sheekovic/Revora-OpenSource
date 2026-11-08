using System.Runtime.InteropServices;
using System.Windows;
using Revora.Launcher.Core;

namespace Revora.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (OperatingSystem.IsWindows()) _ = SetCurrentProcessExplicitAppUserModelID(LauncherPolicy.AppUserModelId);
        try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, ".launcher-ready"), LauncherPolicy.ProductVersion); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
