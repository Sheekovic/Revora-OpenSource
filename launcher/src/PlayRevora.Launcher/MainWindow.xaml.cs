using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using PlayRevora.Launcher.ViewModels;

namespace PlayRevora.Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainWindowViewModel.Create(AppContext.BaseDirectory);
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else DragMove();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        TitleBar_MouseLeftButtonDown(sender, e);

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        Point point = e.GetPosition(this);
        double x = ((point.X / Math.Max(1, ActualWidth)) - 0.5) * -10;
        double y = ((point.Y / Math.Max(1, ActualHeight)) - 0.5) * -6;
        if (HeroParallax.RenderTransform is System.Windows.Media.TranslateTransform transform)
        {
            transform.X = x;
            transform.Y = y;
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (HeroParallax.RenderTransform is System.Windows.Media.TranslateTransform transform)
            (transform.X, transform.Y) = (0, 0);
    }

    private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        IntPtr handle = new WindowInteropHelper(this).Handle;
        ReleaseCapture();
        SendMessage(handle, 0x00A1, new IntPtr(17), IntPtr.Zero);
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(
        IntPtr window, uint message, IntPtr parameter, IntPtr data);
}
