using Revora.ClientEndpoint;

namespace Revora.ClientEndpoint.Gui;

internal sealed class ServerDatTab : TabPage
{
    private readonly TextBox executable = new() { Dock = DockStyle.Fill, AccessibleName = "Server-list client executable" };
    private readonly TextBox dat = new() { Dock = DockStyle.Fill, AccessibleName = "Matching signed server-list file" };
    private readonly ComboBox servers = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Server row to edit" };
    private readonly TextBox name = new() { Dock = DockStyle.Fill, MaxLength = 50, AccessibleName = "Server name" };
    private readonly TextBox address = new() { Dock = DockStyle.Fill, AccessibleName = "Server-list account IPv4" };
    private readonly NumericUpDown port = new() { Dock = DockStyle.Fill, Minimum = 1, Maximum = 65535, Value = 9958, AccessibleName = "Server-list account port" };
    private readonly TextBox output = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "Server-list operation result" };
    private readonly Button save = new() { Text = "Save signed pair", AutoSize = true, Enabled = false };
    private ServerListState? loaded;

    internal ServerDatTab()
    {
        Text = "Server.dat editor"; UseVisualStyleBackColor = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 3, RowCount = 11 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        for (int i = 0; i < 10; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Wide(Control c, int row) { layout.Controls.Add(c, 0, row); layout.SetColumnSpan(c, 3); }
        Wide(new Label { Text = "Edit your signed 6711 server list", AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold), Margin = new Padding(0, 0, 0, 12) }, 0);
        Wide(new Label { Text = "Use a private client copy and close the game/launcher. Select its matching Server.dat or RevoraServer.dat.\nSaving signs the list with your local key, updates the executable's public key and removes its endpoint override.\nBoth files are backed up together. RSA validation stays enabled. No client or signing key is distributed.", AutoSize = true, Margin = new Padding(0, 0, 0, 14) }, 1);
        void Field(string label, Control c, int row)
        { layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row); layout.Controls.Add(c, 1, row); }
        Field("Client executable", executable, 2); Field("Signed server list", dat, 3);
        foreach (var (row, box, filter) in new[] { (2, executable, "Client executable|*.exe"), (3, dat, "Signed server list|*.dat") })
        {
            var browse = new Button { Text = "Browse…", AutoSize = true };
            browse.Click += (_, _) => Run(() =>
            {
                using var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                box.Text = dialog.FileName;
                if (box == executable && string.IsNullOrWhiteSpace(dat.Text))
                {
                    ClientState state = ClientFiles.Inspect(executable.Text);
                    string filename = state.OriginalSha256 == EndpointPatch.RevoraHash ? "RevoraServer.dat" : "Server.dat";
                    string directory = Path.GetDirectoryName(executable.Text)!;
                    foreach (string candidate in new[] { Path.Combine(directory, filename), Path.Combine(Path.GetDirectoryName(directory) ?? directory, filename) })
                        if (File.Exists(candidate)) { dat.Text = candidate; break; }
                }
            });
            layout.Controls.Add(browse, 2, row);
        }
        var load = new Button { Text = "Load and verify pair", AutoSize = true, Padding = new Padding(6) };
        load.Click += (_, _) => Run(() => { LoadPair(); output.Text = $"Verified signature and XML. {loaded!.Servers.Count} rows loaded; group/category rows are preserved.\r\nChoose a server below and edit its name, account IPv4 and account port."; });
        Wide(load, 4);
        Field("Server to edit", servers, 5); Field("Server name", name, 6); Field("Account server IPv4", address, 7); Field("Account server port", port, 8);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        save.Click += (_, _) => Run(() =>
        {
            if (loaded is null || servers.SelectedItem is not ServerEntry entry) throw new InvalidOperationException("Load the matching pair and choose a server first.");
            PairChange change = ServerDatFiles.Apply(executable.Text, dat.Text, entry.Id, name.Text.Trim(), address.Text.Trim(), (int)port.Value, loaded);
            LoadPair(); output.Text = Describe(change) + "\r\nKeep the private signing key secret and back up the entire .revora-endpoint folder privately.\r\nRe-hash and re-sign your launcher profile after changing these files.";
        });
        var restore = new Button { Text = "Restore pair", AutoSize = true };
        restore.Click += (_, _) => Run(() => { PairChange change = ServerDatFiles.Restore(executable.Text, dat.Text); LoadPair(); output.Text = Describe(change) + "\r\nThe previous executable and matching list were restored byte for byte. Your local signing key was kept."; });
        buttons.Controls.Add(save); buttons.Controls.Add(restore); Wide(buttons, 9); Wide(output, 10);
        servers.SelectedIndexChanged += (_, _) =>
        {
            if (servers.SelectedItem is not ServerEntry entry) return;
            name.Text = entry.Name; address.Text = entry.Address;
            port.Value = Math.Clamp(entry.Port, 1, 65535);
        };
        executable.TextChanged += (_, _) => InvalidateLoaded(); dat.TextChanged += (_, _) => InvalidateLoaded();
        Controls.Add(layout);
    }

    private void InvalidateLoaded() { loaded = null; save.Enabled = false; servers.DataSource = null; }
    private void LoadPair()
    {
        uint? selectedId = (servers.SelectedItem as ServerEntry)?.Id;
        loaded = ServerDatFiles.Inspect(executable.Text, dat.Text);
        servers.DataSource = loaded.Servers.Where(e => !e.Group).ToArray(); save.Enabled = servers.Items.Count > 0;
        if (selectedId is not null)
            foreach (ServerEntry entry in servers.Items) if (entry.Id == selectedId) { servers.SelectedItem = entry; break; }
    }
    private void Run(Action action) { try { action(); } catch (Exception error) { output.Text = "Operation stopped: " + error.Message; } }
    private static string Describe(PairChange change) => $"Completed.\r\nExecutable SHA-256: {change.ExecutableSha256}\r\nServer-list SHA-256: {change.DatSha256}\r\nRestore journal: {change.Journal}\r\n" + (change.KeyFile.Length > 0 ? $"Private key (local only): {change.KeyFile}\r\n" : "");
}
