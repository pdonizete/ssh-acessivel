using System.Text.Json;
using SshAcessivel.App.Resources;
using SshAcessivel.Core.Persistence;

namespace SshAcessivel.App;

/// <summary>Lista de servidores salvos. Enter conecta, Delete remove (com confirmação).</summary>
internal sealed class ConnectionManagerForm : Form
{
    private readonly ConnectionStore _store;
    private readonly ListView _list;
    private readonly Button _btnConnect;
    private readonly Button _btnEdit;
    private readonly Button _btnDuplicate;
    private readonly Button _btnRemove;

    /// <summary>Conexão escolhida para conectar (quando o diálogo retorna OK).</summary>
    public ConnectionProfile? SelectedProfile { get; private set; }

    public ConnectionManagerForm(ConnectionStore store)
    {
        _store = store;

        Text = Strings.Manager_Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(8);

        var lblList = new Label { Text = Strings.Manager_ListLabel, AutoSize = true, UseMnemonic = true, TabIndex = 0 };
        // ListView nativa em modo Detalhes: o NVDA lê o nome e as outras colunas de cada item.
        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            Width = 580,
            Height = 260,
            TabIndex = 1,
            AccessibleName = Strings.Manager_ListName,
        };
        _list.Columns.Add(Strings.Manager_ColName, 170);
        _list.Columns.Add(Strings.Manager_ColHost, 200);
        _list.Columns.Add(Strings.Manager_ColUser, 100);
        _list.Columns.Add(Strings.Manager_ColGroup, 100);
        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.KeyDown += OnListKeyDown;
        _list.DoubleClick += OnConnectClick;

        _btnConnect = NewButton(Strings.Manager_Connect, OnConnectClick);
        var btnAdd = NewButton(Strings.Manager_Add, OnAddClick);
        _btnEdit = NewButton(Strings.Manager_Edit, OnEditClick);
        _btnDuplicate = NewButton(Strings.Manager_Duplicate, OnDuplicateClick);
        _btnRemove = NewButton(Strings.Manager_Remove, OnRemoveClick);
        var btnImport = NewButton(Strings.Manager_Import, OnImportClick);
        var btnExport = NewButton(Strings.Manager_Export, OnExportClick);
        var btnClose = NewButton(Strings.Manager_Close, null);
        btnClose.DialogResult = DialogResult.Cancel;

        Control[] allButtons = [_btnConnect, btnAdd, _btnEdit, _btnDuplicate, _btnRemove, btnImport, btnExport, btnClose];
        for (int i = 0; i < allButtons.Length; i++)
        {
            allButtons[i].TabIndex = i;
        }
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, MaximumSize = new Size(600, 0), TabIndex = 2 };
        buttons.Controls.AddRange(allButtons);

        var outer = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        outer.Controls.AddRange([lblList, _list, buttons]);
        Controls.Add(outer);

        // Enter na lista conecta; Esc fecha.
        AcceptButton = _btnConnect;
        CancelButton = btnClose;
        ActiveControl = _list;
        RefreshList(null);
    }

    private ConnectionProfile? Selected =>
        _list.SelectedItems.Count > 0 ? (ConnectionProfile)_list.SelectedItems[0].Tag! : null;

    private static Button NewButton(string text, EventHandler? onClick)
    {
        var button = new Button { Text = text, AutoSize = true, UseMnemonic = true };
        if (onClick is not null)
        {
            button.Click += onClick;
        }
        return button;
    }

    private void RefreshList(Guid? selectId)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        // OrderBy/ThenBy (LINQ) ordenam como sorted(lista, key=...) no Python: por grupo, depois por nome.
        var ordered = _store.Profiles
            .OrderBy(p => p.Group ?? "", StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var profile in ordered)
        {
            var item = new ListViewItem(profile.Name) { Tag = profile };
            item.SubItems.Add($"{profile.Host}:{profile.Port}");
            item.SubItems.Add(profile.Username);
            item.SubItems.Add(profile.Group ?? "");
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        var items = _list.Items.Cast<ListViewItem>().ToList();
        var target = items.FirstOrDefault(i => ((ConnectionProfile)i.Tag!).Id == selectId) ?? items.FirstOrDefault();
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
        _btnConnect.Enabled = hasSelection;
        _btnEdit.Enabled = hasSelection;
        _btnDuplicate.Enabled = hasSelection;
        _btnRemove.Enabled = hasSelection;
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && e.Modifiers == Keys.None)
        {
            e.Handled = true;
            OnRemoveClick(sender, e);
        }
    }

    private void OnConnectClick(object? sender, EventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }
        SelectedProfile = profile;
        DialogResult = DialogResult.OK;
    }

    private void OnAddClick(object? sender, EventArgs e) => EditAndSave(new ConnectionProfile(), isNew: true);

    private void OnEditClick(object? sender, EventArgs e)
    {
        if (Selected is { } profile)
        {
            EditAndSave(profile, isNew: false);
        }
    }

    private void OnDuplicateClick(object? sender, EventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }
        var copy = profile.Clone();
        copy.Id = Guid.NewGuid();
        copy.Name += Strings.Manager_CopySuffix;
        EditAndSave(copy, isNew: true);
    }

    private void EditAndSave(ConnectionProfile profile, bool isNew)
    {
        using (var form = new ConnectionEditForm(profile, isNew))
        {
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                _store.AddOrUpdate(form.Profile);
                TrySave();
                RefreshList(form.Profile.Id);
            }
        }
        _list.Focus();
    }

    private void OnRemoveClick(object? sender, EventArgs e)
    {
        if (Selected is not { } profile)
        {
            return;
        }
        // OK/Cancelar (não Sim/Não) para Esc funcionar. Cancelar é o padrão: Enter sem querer não remove.
        var answer = MessageBox.Show(this, string.Format(Strings.Manager_ConfirmRemove, profile.Name), Text,
                                     MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (answer == DialogResult.OK)
        {
            _store.Remove(profile.Id);
            TrySave();
            RefreshList(null);
        }
        _list.Focus();
    }

    private void OnImportClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = Strings.Manager_ImportTitle, Filter = Strings.Manager_JsonFilter };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            int added = _store.Import(dialog.FileName);
            TrySave();
            RefreshList(null);
            ShowInfo(string.Format(Strings.Manager_ImportDone, added));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ShowError(string.Format(Strings.Manager_ImportFailed, ex.Message));
        }
        _list.Focus();
    }

    private void OnExportClick(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Strings.Manager_ExportTitle,
            Filter = Strings.Manager_JsonFilter,
            FileName = Strings.Manager_ExportFileName,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            _store.Export(dialog.FileName);
            ShowInfo(string.Format(Strings.Manager_ExportDone, dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(string.Format(Strings.Manager_ExportFailed, ex.Message));
        }
        _list.Focus();
    }

    private void TrySave()
    {
        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(string.Format(Strings.Manager_SaveFailed, ex.Message));
        }
    }

    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ShowError(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
