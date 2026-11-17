using AcessivelSsh.App.Resources;

namespace AcessivelSsh.App;

/// <summary>Pede uma senha ou passphrase. O texto digitado não aparece na tela nem é lido pelo NVDA.</summary>
internal sealed class SecretPromptForm : Form
{
    private readonly TextBox _txtSecret;

    private SecretPromptForm(string title, string label)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var lbl = new Label { Text = label, AutoSize = true, UseMnemonic = true, TabIndex = 0, MaximumSize = new Size(420, 0) };
        _txtSecret = new TextBox
        {
            Width = 320,
            UseSystemPasswordChar = true,
            // O nome inclui usuário e servidor (ou o arquivo da chave), sem o & da tecla de acesso.
            AccessibleName = label.Replace("&", "").TrimEnd(':', ' '),
            TabIndex = 1,
        };
        var btnOk = new Button { Text = Strings.Edit_Ok, AutoSize = true, DialogResult = DialogResult.OK, TabIndex = 0 };
        var btnCancel = new Button { Text = Strings.Conn_Cancel, AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, TabIndex = 2 };
        buttons.Controls.AddRange([btnOk, btnCancel]);
        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([lbl, _txtSecret, buttons]);
        Controls.Add(outer);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ActiveControl = _txtSecret;
    }

    /// <summary>Mostra o diálogo. Retorna o texto digitado, ou null se o usuário cancelar.</summary>
    public static string? Ask(IWin32Window owner, string title, string label)
    {
        using var form = new SecretPromptForm(title, label);
        return form.ShowDialog(owner) == DialogResult.OK ? form._txtSecret.Text : null;
    }
}
