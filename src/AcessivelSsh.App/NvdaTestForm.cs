using AcessivelSsh.App.Resources;
using AcessivelSsh.Core.Speech;

namespace AcessivelSsh.App;

/// <summary>
/// Janela de teste da fase 1: fala ou mostra em braille um texto digitado.
/// Sem arquivo .Designer.cs: os controles são criados em código, para ficar legível sem Visual Studio.
/// </summary>
internal sealed class NvdaTestForm : Form
{
    private readonly IScreenReader _reader;
    private readonly TextBox _txtTexto;
    private readonly CheckBox _chkInterromper;
    private readonly ToolStripStatusLabel _lblStatus;

    public NvdaTestForm(IScreenReader reader, string initialStatus)
    {
        _reader = reader;

        Text = Strings.TestForm_Title;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MaximizeBox = false;
        Padding = new Padding(8);

        // Ordem de Tab: rótulo (0) -> campo (1) -> caixa de seleção (2) -> botões (3).
        // O rótulo vem logo antes do campo para que Alt+T (tecla de acesso do rótulo) leve o foco ao campo.
        var lblTexto = new Label
        {
            Text = Strings.TestForm_TextLabel,
            AutoSize = true,
            UseMnemonic = true,
            TabIndex = 0,
        };
        _txtTexto = new TextBox
        {
            Width = 420,
            TabIndex = 1,
            // O NVDA anuncia este nome ao focar o campo.
            AccessibleName = Strings.TestForm_TextName,
        };
        _chkInterromper = new CheckBox
        {
            Text = Strings.TestForm_Interrupt,
            AutoSize = true,
            Checked = true,
            TabIndex = 2,
        };

        var btnFalar = NewButton(Strings.TestForm_Speak, 0, OnSpeakClick);
        var btnBraille = NewButton(Strings.TestForm_Braille, 1, OnBrailleClick);
        var btnCancelar = NewButton(Strings.TestForm_Cancel, 2, (_, _) => _reader.Cancel());
        var btnFechar = NewButton(Strings.TestForm_Close, 3, (_, _) => Close());

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            TabIndex = 3,
        };
        buttons.Controls.AddRange([btnFalar, btnBraille, btnCancelar, btnFechar]);

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Dock = DockStyle.Fill,
            WrapContents = false,
            TabIndex = 0,
        };
        layout.Controls.AddRange([lblTexto, _txtTexto, _chkInterromper, buttons]);

        _lblStatus = new ToolStripStatusLabel
        {
            Text = initialStatus,
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var statusStrip = new StatusStrip
        {
            SizingGrip = false,
            AccessibleName = Strings.StatusBar_Name,
        };
        statusStrip.Items.Add(_lblStatus);

        Controls.Add(layout);
        Controls.Add(statusStrip);

        // Enter em qualquer campo aciona "Falar"; Esc aciona "Fechar".
        AcceptButton = btnFalar;
        CancelButton = btnFechar;
        ActiveControl = _txtTexto;
    }

    // "EventHandler" é o tipo de um callback de evento: uma função (sender, e) -> None.
    // "+=" inscreve o callback no evento, como adicionar à lista de observadores no Python.
    private static Button NewButton(string text, int tabIndex, EventHandler onClick)
    {
        var button = new Button { Text = text, AutoSize = true, TabIndex = tabIndex, UseMnemonic = true };
        button.Click += onClick;
        return button;
    }

    private void OnSpeakClick(object? sender, EventArgs e)
    {
        var text = _txtTexto.Text;
        if (text.Length == 0)
        {
            SetStatus(Strings.Status_EmptyText);
            return;
        }
        SetStatus(_reader.Speak(text, _chkInterromper.Checked) ? Strings.Status_Spoken : Strings.Status_SpeakFailed);
    }

    private void OnBrailleClick(object? sender, EventArgs e)
    {
        var text = _txtTexto.Text;
        if (text.Length == 0)
        {
            SetStatus(Strings.Status_EmptyText);
            return;
        }
        SetStatus(_reader.Braille(text) ? Strings.Status_BrailleSent : Strings.Status_BrailleFailed);
    }

    private void SetStatus(string message) => _lblStatus.Text = message;
}
