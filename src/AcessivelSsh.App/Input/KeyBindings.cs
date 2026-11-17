using AcessivelSsh.App.Resources;

namespace AcessivelSsh.App.Input;

/// <summary>Ações da janela principal que têm atalho de teclado.</summary>
internal enum ShortcutAction
{
    ToggleFocus,
    HistoryPrevious,
    HistoryNext,
    SendInterrupt,
    SendTab,
    CancelSpeech,
    ToggleAutoRead,
    RereadResponse,
    CopyResponse,
    ClearOutput,
    ReadLine1,
    ReadLine2,
    ReadLine3,
    ReadLine4,
    ReadLine5,
    ReadLine6,
    ReadLine7,
    ReadLine8,
    ReadLine9,
    ConnectionManager,
    Reconnect,
}

/// <summary>Tabela tecla -> ação, configurável e salva em settings.json.</summary>
/// <remarks>
/// Keys é um enum de "flags": Keys.Control | Keys.Shift | Keys.A junta os bits numa só combinação,
/// como enum.Flag no Python. É esse valor combinado que o Windows Forms entrega em ProcessCmdKey.
/// </remarks>
internal sealed class KeyBindings
{
    private readonly Dictionary<ShortcutAction, Keys> _keys = new();
    private readonly Dictionary<Keys, ShortcutAction> _actions = new();

    public static IReadOnlyList<ShortcutAction> AllActions { get; } = Enum.GetValues<ShortcutAction>();

    public static KeyBindings CreateDefault()
    {
        var b = new KeyBindings();
        b.Set(ShortcutAction.ToggleFocus, Keys.F6);
        b.Set(ShortcutAction.HistoryPrevious, Keys.Up);
        b.Set(ShortcutAction.HistoryNext, Keys.Down);
        b.Set(ShortcutAction.SendInterrupt, Keys.Control | Keys.C);
        b.Set(ShortcutAction.SendTab, Keys.Tab);
        b.Set(ShortcutAction.CancelSpeech, Keys.Escape);
        b.Set(ShortcutAction.ToggleAutoRead, Keys.Control | Keys.Shift | Keys.A);
        b.Set(ShortcutAction.RereadResponse, Keys.Control | Keys.Shift | Keys.R);
        b.Set(ShortcutAction.CopyResponse, Keys.Control | Keys.Shift | Keys.C);
        b.Set(ShortcutAction.ClearOutput, Keys.Control | Keys.L);
        for (int i = 0; i < 9; i++)
        {
            b.Set(ShortcutAction.ReadLine1 + i, Keys.Alt | (Keys.D1 + i));
        }
        b.Set(ShortcutAction.ConnectionManager, Keys.Control | Keys.N);
        b.Set(ShortcutAction.Reconnect, Keys.Control | Keys.R);
        return b;
    }

    /// <summary>Padrões, trocados pelo que estiver salvo. Valores inválidos no arquivo são ignorados.</summary>
    public static KeyBindings FromSettings(IReadOnlyDictionary<string, string>? saved)
    {
        var bindings = CreateDefault();
        if (saved is null)
        {
            return bindings;
        }
        foreach (var action in AllActions)
        {
            if (saved.TryGetValue(action.ToString(), out var text)
                && TryParse(text, out var keys)
                && (keys == Keys.None || IsAllowed(keys)))
            {
                bindings.Set(action, keys);
            }
        }
        return bindings;
    }

    public Keys this[ShortcutAction action] => _keys.TryGetValue(action, out var keys) ? keys : Keys.None;

    public ShortcutAction? FindAction(Keys keys) =>
        keys != Keys.None && _actions.TryGetValue(keys, out var action) ? action : null;

    /// <summary>Define o atalho. Se outra ação usava a mesma tecla, ela fica sem atalho.</summary>
    public void Set(ShortcutAction action, Keys keys)
    {
        if (_keys.Remove(action, out var old))
        {
            _actions.Remove(old);
        }
        if (keys == Keys.None)
        {
            return;
        }
        if (_actions.Remove(keys, out var other))
        {
            _keys.Remove(other);
        }
        _keys[action] = keys;
        _actions[keys] = action;
    }

    public KeyBindings Clone()
    {
        var copy = new KeyBindings();
        foreach (var (action, keys) in _keys)
        {
            copy.Set(action, keys);
        }
        return copy;
    }

    public Dictionary<string, string> ToSettings() =>
        AllActions.ToDictionary(a => a.ToString(), a => Format(this[a]));

    public static string ActionName(ShortcutAction action) =>
        action is >= ShortcutAction.ReadLine1 and <= ShortcutAction.ReadLine9
            ? string.Format(Strings.Action_ReadLine, action - ShortcutAction.ReadLine1 + 1)
            : Strings.ResourceManager.GetString("Action_" + action, Strings.Culture) ?? action.ToString();

    /// <summary>
    /// Teclas aceitas como atalho: com Ctrl ou Alt, teclas de função, ou Esc, setas acima/abaixo e Tab sozinhas.
    /// Uma letra sozinha impediria digitar no campo de comando.
    /// </summary>
    public static bool IsAllowed(Keys keys)
    {
        var code = keys & Keys.KeyCode;
        var modifiers = keys & Keys.Modifiers;
        if (code is Keys.None or Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.Apps)
        {
            return false;
        }
        if (keys == (Keys.Alt | Keys.F4))
        {
            return false; // fecha a janela no Windows
        }
        if ((modifiers & (Keys.Control | Keys.Alt)) != 0)
        {
            return true;
        }
        if (code is >= Keys.F1 and <= Keys.F24)
        {
            return true;
        }
        return modifiers == Keys.None && code is Keys.Escape or Keys.Up or Keys.Down or Keys.Tab;
    }

    /// <summary>Formato salvo no arquivo, em inglês e estável: "Ctrl+Shift+A", "Alt+D1", "F6". Vazio = sem atalho.</summary>
    public static string Format(Keys keys)
    {
        var code = keys & Keys.KeyCode;
        if (code == Keys.None)
        {
            return "";
        }
        var parts = new List<string>();
        if (keys.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (keys.HasFlag(Keys.Shift)) parts.Add("Shift");
        if (keys.HasFlag(Keys.Alt)) parts.Add("Alt");
        parts.Add(code.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out Keys keys)
    {
        keys = Keys.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }
        var result = Keys.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl" or "control": result |= Keys.Control; break;
                case "shift": result |= Keys.Shift; break;
                case "alt": result |= Keys.Alt; break;
                default: return false;
            }
        }
        var last = parts[^1];
        if (last.Length == 1 && char.IsAsciiDigit(last[0]))
        {
            last = "D" + last; // "Alt+1" escrito à mão vira Alt+D1
        }
        // Números puros seriam aceitos pelo Enum.TryParse como valores internos (1 = botão do mouse).
        if (last.All(char.IsAsciiDigit) || !Enum.TryParse<Keys>(last, ignoreCase: true, out var code)
            || (code & Keys.Modifiers) != 0)
        {
            return false;
        }
        keys = result | code;
        return true;
    }

    /// <summary>Texto para mostrar e falar: "Ctrl+Shift+A", "Alt+1", "Seta acima".</summary>
    public static string Display(Keys keys)
    {
        var code = keys & Keys.KeyCode;
        if (code == Keys.None)
        {
            return Strings.Key_None;
        }
        var parts = new List<string>();
        if (keys.HasFlag(Keys.Control)) parts.Add(Strings.Key_Ctrl);
        if (keys.HasFlag(Keys.Shift)) parts.Add(Strings.Key_Shift);
        if (keys.HasFlag(Keys.Alt)) parts.Add(Strings.Key_Alt);
        parts.Add(code switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (code - Keys.D0))).ToString(),
            Keys.Up => Strings.Key_Up,
            Keys.Down => Strings.Key_Down,
            Keys.Left => Strings.Key_Left,
            Keys.Right => Strings.Key_Right,
            Keys.Escape => Strings.Key_Escape,
            Keys.Return => Strings.Key_Enter,
            Keys.Space => Strings.Key_Space,
            Keys.Delete => Strings.Key_Delete,
            _ => code.ToString(),
        });
        return string.Join("+", parts);
    }
}
