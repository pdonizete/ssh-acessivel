using SshAcessivel.App.Resources;
using SshAcessivel.Core.Persistence;

namespace SshAcessivel.App;

/// <summary>Ferramentas > Configurações. OK grava os valores no objeto recebido.</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly NumericUpDown _numDebounce;
    private readonly NumericUpDown _numSummary;
    private readonly NumericUpDown _numProgress;
    private readonly NumericUpDown _numMaxLines;
    private readonly NumericUpDown _numHistory;
    private readonly NumericUpDown _numTimeout;
    private readonly NumericUpDown _numKeepAlive;
    private readonly CheckBox _chkTab;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;

        Text = Strings.Settings_Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, TabIndex = 0 };
        int tab = 0;
        _numDebounce = AddNumber(grid, Strings.Settings_Debounce, 50, 2000, settings.SpeechDebounceMilliseconds, ref tab);
        _numSummary = AddNumber(grid, Strings.Settings_Summary, 5, 10000, settings.SummaryLineThreshold, ref tab);
        _numProgress = AddNumber(grid, Strings.Settings_Progress, 1, 120, settings.ProgressIntervalSeconds, ref tab);
        _numMaxLines = AddNumber(grid, Strings.Settings_MaxLines, 500, 100000, settings.MaxOutputLines, ref tab);
        _numHistory = AddNumber(grid, Strings.Settings_History, 10, 10000, settings.HistorySize, ref tab);
        _numTimeout = AddNumber(grid, Strings.Settings_Timeout, 3, 300, settings.ConnectTimeoutSeconds, ref tab);
        _numKeepAlive = AddNumber(grid, Strings.Settings_KeepAlive, 0, 3600, settings.KeepAliveSeconds, ref tab);
        _chkTab = new CheckBox { Text = Strings.Settings_TabToServer, AutoSize = true, UseMnemonic = true, Checked = settings.TabSendsToServer, TabIndex = tab++ };
        grid.Controls.Add(new Label { AutoSize = true });
        grid.Controls.Add(_chkTab);

        var lblNote = new Label { Text = Strings.Settings_Note, AutoSize = true, MaximumSize = new Size(520, 0), UseMnemonic = false, TabIndex = 1 };

        var btnOk = new Button { Text = Strings.Edit_Ok, AutoSize = true, TabIndex = 0 };
        btnOk.Click += OnOkClick;
        var btnCancel = new Button { Text = Strings.Conn_Cancel, AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, TabIndex = 2 };
        buttons.Controls.AddRange([btnOk, btnCancel]);

        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([grid, lblNote, buttons]);
        Controls.Add(outer);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ActiveControl = _numDebounce;
    }

    // O rótulo recebe o TabIndex logo antes do campo: Alt+letra do rótulo leva o foco ao campo.
    private static NumericUpDown AddNumber(TableLayoutPanel grid, string label, int min, int max, int value, ref int tab)
    {
        var lbl = new Label { Text = label, AutoSize = true, UseMnemonic = true, Anchor = AnchorStyles.Left, TabIndex = tab++ };
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Width = 100,
            TabIndex = tab++,
            AccessibleName = label.Replace("&", "").TrimEnd(':', ' '),
        };
        grid.Controls.Add(lbl);
        grid.Controls.Add(number);
        return number;
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        _settings.SpeechDebounceMilliseconds = (int)_numDebounce.Value;
        _settings.SummaryLineThreshold = (int)_numSummary.Value;
        _settings.ProgressIntervalSeconds = (int)_numProgress.Value;
        _settings.MaxOutputLines = (int)_numMaxLines.Value;
        _settings.HistorySize = (int)_numHistory.Value;
        _settings.ConnectTimeoutSeconds = (int)_numTimeout.Value;
        _settings.KeepAliveSeconds = (int)_numKeepAlive.Value;
        _settings.TabSendsToServer = _chkTab.Checked;
        DialogResult = DialogResult.OK;
    }
}
