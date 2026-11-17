using System.Security.Cryptography;
using SshAcessivel.App.Resources;
using SshAcessivel.Core.Persistence;

namespace SshAcessivel.App;

/// <summary>Cria ou edita uma conexão salva.</summary>
internal sealed class ConnectionEditForm : Form
{
    private readonly ConnectionProfile _profile;
    private readonly TextBox _txtName = new() { Width = 300 };
    private readonly TextBox _txtHost = new() { Width = 300 };
    private readonly NumericUpDown _numPort = new() { Minimum = 1, Maximum = 65535, Width = 100 };
    private readonly TextBox _txtUser = new() { Width = 300 };
    private readonly ComboBox _cmbAuth = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly TextBox _txtKey = new() { Width = 300 };
    private readonly Button _btnBrowse = new() { AutoSize = true, UseMnemonic = true };
    private readonly CheckBox _chkSave = new() { AutoSize = true };
    private readonly Label _lblSecret = new() { AutoSize = true, UseMnemonic = true, Anchor = AnchorStyles.Left };
    private readonly TextBox _txtSecret = new() { Width = 300, UseSystemPasswordChar = true };
    private readonly TextBox _txtGroup = new() { Width = 300 };

    /// <summary>A conexão com as alterações, depois de OK.</summary>
    public ConnectionProfile Profile => _profile;

    public ConnectionEditForm(ConnectionProfile profile, bool isNew)
    {
        // Trabalha numa cópia: Cancelar não altera a conexão original.
        _profile = profile.Clone();

        Text = isNew ? Strings.Edit_TitleNew : Strings.Edit_TitleEdit;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        _txtName.Text = _profile.Name;
        _txtHost.Text = _profile.Host;
        _numPort.Value = Math.Clamp(_profile.Port, 1, 65535);
        _txtUser.Text = _profile.Username;
        _cmbAuth.Items.AddRange([Strings.Auth_Password, Strings.Auth_Key]);
        _cmbAuth.SelectedIndex = _profile.Authentication == AuthenticationKind.PrivateKey ? 1 : 0;
        _txtKey.Text = _profile.PrivateKeyPath ?? "";
        _chkSave.Checked = _profile.HasSavedSecret;
        _txtGroup.Text = _profile.Group ?? "";
        _btnBrowse.Text = Strings.Edit_Browse;
        _btnBrowse.Click += OnBrowseClick;
        _cmbAuth.SelectedIndexChanged += (_, _) => UpdateAuthFields();
        _chkSave.CheckedChanged += (_, _) => UpdateAuthFields();

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, TabIndex = 0 };
        int tab = 0;
        AddRow(grid, NewLabel(Strings.Edit_NameLabel), Strings.Edit_NameName, _txtName, ref tab);
        AddRow(grid, NewLabel(Strings.Conn_HostLabel), Strings.Conn_HostName, _txtHost, ref tab);
        AddRow(grid, NewLabel(Strings.Conn_PortLabel), Strings.Conn_PortName, _numPort, ref tab);
        AddRow(grid, NewLabel(Strings.Conn_UserLabel), Strings.Conn_UserName, _txtUser, ref tab);
        AddRow(grid, NewLabel(Strings.Edit_AuthLabel), Strings.Edit_AuthName, _cmbAuth, ref tab);
        AddRow(grid, NewLabel(Strings.Edit_KeyLabel), Strings.Edit_KeyName, _txtKey, ref tab);
        AddUnlabeled(grid, _btnBrowse, ref tab);
        AddUnlabeled(grid, _chkSave, ref tab);
        AddRow(grid, _lblSecret, Strings.Edit_PasswordName, _txtSecret, ref tab);
        AddRow(grid, NewLabel(Strings.Edit_GroupLabel), Strings.Edit_GroupName, _txtGroup, ref tab);

        var btnOk = new Button { Text = Strings.Edit_Ok, AutoSize = true, TabIndex = 0 };
        btnOk.Click += OnOkClick;
        var btnCancel = new Button { Text = Strings.Conn_Cancel, AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, TabIndex = 1 };
        buttons.Controls.AddRange([btnOk, btnCancel]);

        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([grid, buttons]);
        Controls.Add(outer);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ActiveControl = _txtName;
        UpdateAuthFields();
    }

    private static Label NewLabel(string text) =>
        new() { Text = text, AutoSize = true, UseMnemonic = true, Anchor = AnchorStyles.Left };

    // O rótulo recebe o TabIndex logo antes do campo: Alt+letra do rótulo leva o foco ao campo.
    private static void AddRow(TableLayoutPanel grid, Label label, string accessibleName, Control field, ref int tab)
    {
        label.TabIndex = tab++;
        field.AccessibleName = accessibleName;
        field.TabIndex = tab++;
        grid.Controls.Add(label);
        grid.Controls.Add(field);
    }

    private static void AddUnlabeled(TableLayoutPanel grid, Control control, ref int tab)
    {
        grid.Controls.Add(new Label { AutoSize = true });
        control.TabIndex = tab++;
        grid.Controls.Add(control);
    }

    // Campos que não se aplicam ficam desabilitados. A senha/passphrase só é digitada aqui se for salva;
    // sem salvar, o app pergunta na hora de conectar.
    private void UpdateAuthFields()
    {
        bool key = _cmbAuth.SelectedIndex == 1;
        _txtKey.Enabled = key;
        _btnBrowse.Enabled = key;
        _lblSecret.Text = key ? Strings.Edit_PassphraseLabel : Strings.Edit_PasswordLabel;
        _txtSecret.AccessibleName = key ? Strings.Edit_PassphraseName : Strings.Edit_PasswordName;
        _chkSave.Text = key ? Strings.Edit_SavePassphrase : Strings.Edit_SavePassword;
        _lblSecret.Enabled = _chkSave.Checked;
        _txtSecret.Enabled = _chkSave.Checked;
    }

    private void OnBrowseClick(object? sender, EventArgs e)
    {
        var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        using var dialog = new OpenFileDialog
        {
            Title = Strings.Edit_BrowseTitle,
            Filter = Strings.Edit_KeyFilter,
            InitialDirectory = Directory.Exists(sshDir) ? sshDir : "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtKey.Text = dialog.FileName;
        }
        _txtKey.Focus();
    }

    private void OnOkClick(object? sender, EventArgs e)
    {
        var host = _txtHost.Text.Trim();
        var user = _txtUser.Text.Trim();
        var keyPath = _txtKey.Text.Trim();
        bool key = _cmbAuth.SelectedIndex == 1;
        if (host.Length == 0)
        {
            ShowValidation(Strings.Conn_HostRequired, _txtHost);
            return;
        }
        if (user.Length == 0)
        {
            ShowValidation(Strings.Conn_UserRequired, _txtUser);
            return;
        }
        if (key && keyPath.Length == 0)
        {
            ShowValidation(Strings.Edit_KeyRequired, _txtKey);
            return;
        }
        if (key && !File.Exists(keyPath))
        {
            ShowValidation(string.Format(Strings.Edit_KeyMissing, keyPath), _txtKey);
            return;
        }

        var auth = key ? AuthenticationKind.PrivateKey : AuthenticationKind.Password;
        // Campo vazio com "salvar" marcado mantém o segredo já salvo, se o tipo de autenticação não mudou.
        string? protectedSecret = _profile.ProtectedSecret;
        if (!_chkSave.Checked)
        {
            protectedSecret = null;
        }
        else if (_txtSecret.Text.Length > 0)
        {
            try
            {
                protectedSecret = SecretProtector.Protect(_txtSecret.Text);
            }
            catch (CryptographicException ex)
            {
                ShowValidation(string.Format(Strings.Edit_ProtectFailed, ex.Message), _txtSecret);
                return;
            }
        }
        else if (auth != _profile.Authentication)
        {
            protectedSecret = null; // a senha antiga não serve como passphrase, nem o contrário
        }

        var name = _txtName.Text.Trim();
        var group = _txtGroup.Text.Trim();
        _profile.Name = name.Length == 0 ? host : name;
        _profile.Host = host;
        _profile.Port = (int)_numPort.Value;
        _profile.Username = user;
        _profile.Authentication = auth;
        _profile.PrivateKeyPath = key ? keyPath : null;
        _profile.Group = group.Length == 0 ? null : group;
        _profile.ProtectedSecret = protectedSecret;
        DialogResult = DialogResult.OK;
    }

    private void ShowValidation(string message, Control field)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        field.Focus();
    }
}
