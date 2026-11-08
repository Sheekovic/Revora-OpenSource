using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Revora.NativeLogin;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows() || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) throw new InvalidOperationException("A real Windows STA is required.");
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var window = new MainWindow();
        Assert(window.Title.Contains("Owner Test 3", StringComparison.Ordinal), "Testing identity is not visible.");
        Assert(window.FindName("Username") is TextBox username && username.MaxLength == 16, "Username is not bounded.");
        Assert(window.FindName("Password") is PasswordBox password && password.MaxLength == 16, "Password is not masked and bounded.");
        Assert(window.FindName("StopButton") is Button, "Emergency Stop is missing.");
        Assert(window.FindName("StartButton") is Button start && !start.IsEnabled, "Launch was enabled before signed preparation.");
        Assert(!AllText(window).Any(value => value.Contains("0.0.0.0", StringComparison.Ordinal) || value.Contains("playrevora.com", StringComparison.Ordinal)), "The UI exposes an editable/public target.");
        Console.WriteLine("PASS Native Login WPF starts on a real Windows STA");
        Console.WriteLine("PASS credentials are masked/bounded and launch is gated");
        return 0;
    }

    private static IEnumerable<string> AllText(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox box) yield return box.Text;
            foreach (string nested in AllText(child)) yield return nested;
        }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
