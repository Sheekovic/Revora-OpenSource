using Revora.ClientEndpoint;

namespace Revora.ClientEndpoint.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new EndpointForm());
    }
}

internal sealed class EndpointForm : Form
{
    private readonly TextBox path = new() { Dock = DockStyle.Fill, AccessibleName = "Client executable" };
    private readonly TextBox address = new() { Text = "127.0.0.1", Dock = DockStyle.Fill, AccessibleName = "Account server IPv4" };
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 9958, Dock = DockStyle.Fill, AccessibleName = "Account server port" };
    private readonly TextBox output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = "Inspection and operation result" };

    public EndpointForm()
    {
        Text = $"Revora • Client Endpoint 6711 — {Application.ProductVersion.Split('+')[0]}";
        ClientSize = new Size(1000, 800); MinimumSize = new Size(900, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 3, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Wide(Control control, int row) { layout.Controls.Add(control, 0, row); layout.SetColumnSpan(control, 3); }
        Wide(new Label { Text = "Point your 6711 DX9 client to your own account server", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) }, 0);
        Wide(new Label { Text = "Use a copy of your client and close the game first. Only two reviewed DX9 builds are supported.\nThe tool keeps a backup and can restore the original bytes. No client is bundled.", AutoSize = true, Margin = new Padding(0, 0, 0, 16) }, 1);
        layout.Controls.Add(new Label { Text = "Client executable", AutoSize = true }, 0, 2); layout.Controls.Add(path, 1, 2);
        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "6711 executable|*.exe", CheckFileExists = true, Title = "Select Env_DX9\\Conquer.exe or Conquer-Revora.exe" };
            if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName;
        };
        layout.Controls.Add(browse, 2, 2);
        layout.Controls.Add(new Label { Text = "Account server IPv4", AutoSize = true }, 0, 3); layout.Controls.Add(address, 1, 3);
        layout.Controls.Add(new Label { Text = "Account server port", AutoSize = true }, 0, 4); layout.Controls.Add(port, 1, 4);
        Wide(new Label { Text = "9958 is the repository default. Local acceptance setups may use 19558.\nGame port, Server.dat, RSA keys and launcher signing are configured separately.", AutoSize = true, Margin = new Padding(0, 10, 0, 10) }, 5);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        foreach (var (text, action) in new (string, Func<string>)[]
        {
            ("Inspect", () => {
                var state = ClientFiles.Inspect(path.Text);
                return $"{state.Profile}\r\nSHA-256: {state.CurrentSha256}\r\n" +
                    (state.ServerListKeyChanged ? "Managed Server.dat signing key: use the Server.dat tab for editing and paired restore.\r\n" : "") +
                    (state.Patched ? $"Endpoint override: {state.Address}:{state.Port}\r\nOriginal SHA-256: {state.OriginalSha256}" : "No endpoint override: routing comes from the signed server list.");
            }),
            ("Apply endpoint", () => Describe(ClientFiles.Apply(path.Text, address.Text.Trim(), (int)port.Value))),
            ("Restore original", () => Describe(ClientFiles.Restore(path.Text)))
        })
        {
            var button = new Button { Text = text, AutoSize = true, Padding = new Padding(6) };
            // Operations are short, local and synchronous; controls are read on the UI thread.
            button.Click += (_, _) => { try { output.Text = action(); } catch (Exception error) { output.Text = "No operation completed: " + error.Message; } };
            buttons.Controls.Add(button);
        }
        Wide(buttons, 6); Wide(output, 7);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var endpointTab = new TabPage("Executable endpoint") { UseVisualStyleBackColor = true };
        endpointTab.Controls.Add(layout); tabs.TabPages.Add(endpointTab); tabs.TabPages.Add(new ServerDatTab());
        Controls.Add(tabs);
    }

    private static string Describe(FileChange change) => $"Completed.\r\nBackup: {change.Backup}\r\nBefore: {change.BeforeSha256}\r\nAfter: {change.AfterSha256}\r\n\r\nUse Inspect to verify. Start the client with your configured launcher after connecting the server components.";
}
