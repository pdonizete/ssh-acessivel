using SshAcessivel.App.Input;
using SshAcessivel.App.Resources;

namespace SshAcessivel.App;

/// <summary>Ajuda > Atalhos de teclado: lista todos os atalhos e permite trocá-los.</summary>
internal sealed class ShortcutsForm : Form
{
    private readonly ListView _list;
    private readonly Button _btnChange;
    private readonly Button _btnClear;

    /// <summary>Atalhos com as alterações (válidos quando o diálogo retorna OK).</summary>
    public KeyBindings Bindings { get; private set; }

    public ShortcutsForm(KeyBindings current)
    {
        // Trabalha numa cópia: Cancelar descarta as alterações.
        Bindings = current.Clone();

        Text = Strings.Shortcuts_Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var lblHelp = new Label { Text = Strings.Shortcuts_Help, AutoSize = true, MaximumSize = new Size(600, 0), UseMnemonic = false, TabIndex = 0 };
        var lblList = new Label { Text = Strings.Shortcuts_ListLabel, AutoSize = true, UseMnemonic = true, TabIndex = 1 };
        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Width = 600,
            Height = 340,
            TabIndex = 2,
            AccessibleName = Strings.Shortcuts_ListName,
        };
        _list.Columns.Add(Strings.Shortcuts_ColAction, 420);
        _list.Columns.Add(Strings.Shortcuts_ColKeys, 160);
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();

        _btnChange = NewButton(Strings.Shortcuts_Change, OnChangeClick);
        _btnClear = NewButton(Strings.Shortcuts_Clear, OnClearClick);
        var btnReset = NewButton(Strings.Shortcuts_Reset, OnResetClick);
        var btnOk = NewButton(Strings.Edit_Ok, null);
        btnOk.DialogResult = DialogResult.OK;
        var btnCancel = NewButton(Strings.Conn_Cancel, null);
        btnCancel.DialogResult = DialogResult.Cancel;
        Control[] all = [_btnChange, _btnClear, btnReset, btnOk, btnCancel];
        for (int i = 0; i < all.Length; i++)
        {
            all[i].TabIndex = i;
        }
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, TabIndex = 3 };
        buttons.Controls.AddRange(all);

        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([lblHelp, lblList, _list, buttons]);
        Controls.Add(outer);

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        ActiveControl = _list;
        RefreshList(null);
    }

    private ShortcutAction? Selected =>
        _list.SelectedItems.Count > 0 ? (ShortcutAction)_list.SelectedItems[0].Tag! : null;

    private static Button NewButton(string text, EventHandler? onClick)
    {
        var button = new Button { Text = text, AutoSize = true, UseMnemonic = true };
        if (onClick is not null)
        {
            button.Click += onClick;
        }
        return button;
    }

    private void RefreshList(ShortcutAction? select)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var action in KeyBindings.AllActions)
        {
            var item = new ListViewItem(KeyBindings.ActionName(action)) { Tag = action };
            item.SubItems.Add(KeyBindings.Display(Bindings[action]));
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        var target = _list.Items.Cast<ListViewItem>().FirstOrDefault(i => (ShortcutAction)i.Tag! == select)
                     ?? (_list.Items.Count > 0 ? _list.Items[0] : null);
        if (target is not null)
        {
            target.Selected = true;
            target.Focused = true;
            target.EnsureVisible();
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = Selected is not null;
        _btnChange.Enabled = hasSelection;
        _btnClear.Enabled = hasSelection;
    }

    private void OnChangeClick(object? sender, EventArgs e)
    {
        if (Selected is not { } action)
        {
            return;
        }
        Keys keys;
        using (var capture = new KeyCaptureForm(KeyBindings.ActionName(action)))
        {
            if (capture.ShowDialog(this) != DialogResult.OK)
            {
                _list.Focus();
                return;
            }
            keys = capture.CapturedKeys;
        }
        if (!KeyBindings.IsAllowed(keys))
        {
            MessageBox.Show(this, string.Format(Strings.Shortcuts_Invalid, KeyBindings.Display(keys)), Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _list.Focus();
            return;
        }
        if (Bindings.FindAction(keys) is { } other && other != action)
        {
            var message = string.Format(Strings.Shortcuts_InUse, KeyBindings.Display(keys),
                                        KeyBindings.ActionName(other), KeyBindings.ActionName(action));
            // Cancelar é o padrão: Enter sem querer não tira o atalho de outra ação.
            if (MessageBox.Show(this, message, Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                                MessageBoxDefaultButton.Button2) != DialogResult.OK)
            {
                _list.Focus();
                return;
            }
        }
        Bindings.Set(action, keys);
        RefreshList(action);
        _list.Focus();
    }

    private void OnClearClick(object? sender, EventArgs e)
    {
        if (Selected is { } action)
        {
            Bindings.Set(action, Keys.None);
            RefreshList(action);
        }
        _list.Focus();
    }

    private void OnResetClick(object? sender, EventArgs e)
    {
        var selected = Selected;
        Bindings = KeyBindings.CreateDefault();
        RefreshList(selected);
        _list.Focus();
    }
}

/// <summary>Espera a próxima combinação de teclas. Esc cancela.</summary>
internal sealed class KeyCaptureForm : Form
{
    public Keys CapturedKeys { get; private set; }

    public KeyCaptureForm(string actionName)
    {
        Text = Strings.Shortcuts_CaptureTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var prompt = string.Format(Strings.Shortcuts_CapturePrompt, actionName);
        // O foco fica num campo somente leitura cujo nome é a instrução: o NVDA lê ao abrir.
        var txtPrompt = new TextBox
        {
            ReadOnly = true,
            ShortcutsEnabled = false,
            Width = 460,
            Text = prompt,
            AccessibleName = prompt,
            TabIndex = 0,
        };
        var btnCancel = new Button { Text = Strings.Conn_Cancel, AutoSize = true, DialogResult = DialogResult.Cancel, TabIndex = 1 };
        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([txtPrompt, btnCancel]);
        Controls.Add(outer);
        CancelButton = btnCancel;
        ActiveControl = txtPrompt;
    }

    // ProcessCmdKey recebe a tecla antes do campo e do diálogo, inclusive Tab, Enter e Alt+tecla.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            return true;
        }
        var code = keyData & Keys.KeyCode;
        if (code is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None)
        {
            return base.ProcessCmdKey(ref msg, keyData); // só o modificador: espera a tecla principal
        }
        CapturedKeys = keyData;
        DialogResult = DialogResult.OK;
        return true;
    }
}
