using SshAcessivel.App.Resources;
using SshAcessivel.Core.Ssh;

namespace SshAcessivel.App;

internal enum HostKeyDecision
{
    Cancel,
    AcceptOnce,
    AcceptAndSave,
}

/// <summary>Primeira conexão com um servidor: mostra a impressão digital SHA256 e pede a decisão do usuário.</summary>
internal sealed class HostKeyForm : Form
{
    public HostKeyDecision Decision { get; private set; } = HostKeyDecision.Cancel;

    public HostKeyForm(HostKeyInfo info)
    {
        Text = Strings.HostKey_Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        // O TextBox só quebra linha com \r\n; o texto do .resx usa \n.
        var message = string.Format(Strings.HostKey_Message, info.Host, info.Port, info.KeyType, info.DisplayFingerprint)
            .ReplaceLineEndings("\r\n");

        // Os detalhes ficam num campo somente leitura: dá para ler linha por linha com as setas.
        var lblDetails = new Label { Text = Strings.HostKey_DetailsLabel, AutoSize = true, UseMnemonic = true, TabIndex = 0 };
        var txtDetails = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Width = 520,
            Height = 170,
            Text = message,
            TabIndex = 1,
            AccessibleName = Strings.HostKey_DetailsName,
        };
        // Campo separado com só a impressão digital, para soletrar caractere por caractere.
        var lblFingerprint = new Label { Text = Strings.HostKey_FingerprintLabel, AutoSize = true, UseMnemonic = true, TabIndex = 2 };
        var txtFingerprint = new TextBox
        {
            ReadOnly = true,
            Width = 520,
            Text = info.DisplayFingerprint,
            TabIndex = 3,
            AccessibleName = Strings.HostKey_FingerprintName,
        };

        var btnOnce = new Button { Text = Strings.HostKey_AcceptOnce, AutoSize = true, UseMnemonic = true, TabIndex = 0 };
        btnOnce.Click += (_, _) => Decide(HostKeyDecision.AcceptOnce);
        var btnSave = new Button { Text = Strings.HostKey_AcceptSave, AutoSize = true, UseMnemonic = true, TabIndex = 1 };
        btnSave.Click += (_, _) => Decide(HostKeyDecision.AcceptAndSave);
        var btnCancel = new Button { Text = Strings.HostKey_Cancel, AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 2 };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, TabIndex = 4 };
        buttons.Controls.AddRange([btnOnce, btnSave, btnCancel]);

        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([lblDetails, txtDetails, lblFingerprint, txtFingerprint, buttons]);
        Controls.Add(outer);

        // Enter e Esc cancelam: um Enter sem querer nunca aceita uma chave desconhecida.
        AcceptButton = btnCancel;
        CancelButton = btnCancel;
        ActiveControl = txtDetails;
    }

    private void Decide(HostKeyDecision decision)
    {
        Decision = decision;
        DialogResult = DialogResult.OK;
    }
}
