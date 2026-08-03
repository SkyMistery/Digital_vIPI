namespace Vipi.AuroraBridge.Core;

/// <summary>Modificatori di una combinazione globale. I valori coincidono con quelli di <c>RegisterHotKey</c>
/// (MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN), così su Windows non serve nessuna traduzione.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// Combinazione di tasti come la scrive l'utente nelle impostazioni: «Ctrl+Alt+L».
/// Il parsing sta qui, in Core, perché è la parte che può sbagliare — la registrazione vera è tre righe di Win32.
/// </summary>
public sealed record HotkeySpec(HotkeyModifiers Modifiers, char Key, int VirtualKey)
{
    /// <summary>Combinazione predefinita: L come «livello». Ctrl+Alt evita le scorciatoie di Aurora, che usa
    /// soprattutto i tasti funzione.</summary>
    public static readonly HotkeySpec Default = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'L', 0x4C);

    /// <summary>Interpreta «Ctrl+Alt+L». Accetta gli alias comuni (CTRL/CONTROL, WIN/META/CMD) e ignora spazi
    /// e maiuscole. Null se la stringa non contiene esattamente un tasto A-Z o 0-9, o se manca un modificatore:
    /// una combinazione globale senza modificatori ruberebbe un tasto a TUTTO il sistema.</summary>
    public static HotkeySpec? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var modifiers = HotkeyModifiers.None;
        char? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0) continue;

            switch (token.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifiers |= HotkeyModifiers.Control; continue;
                case "ALT": modifiers |= HotkeyModifiers.Alt; continue;
                case "SHIFT": modifiers |= HotkeyModifiers.Shift; continue;
                case "WIN" or "META" or "CMD" or "SUPER": modifiers |= HotkeyModifiers.Win; continue;
            }

            if (token.Length != 1) return null;          // «F5», «Space»: non supportati, meglio dirlo che indovinare
            var c = char.ToUpperInvariant(token[0]);
            if (!char.IsAsciiLetterOrDigit(c)) return null;
            if (key is not null) return null;            // due tasti: combinazione ambigua

            key = c;
        }

        if (key is null || modifiers == HotkeyModifiers.None) return null;
        return new HotkeySpec(modifiers, key.Value, key.Value);   // VK dei tasti A-Z e 0-9 == il loro codice ASCII
    }

    /// <summary>Come si scrive, in forma canonica: utile per rimettere in chiaro ciò che è stato interpretato.</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
