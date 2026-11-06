namespace BlockCraft.Core;

/// <summary>Akcje w grze przypisywane do przycisków pada (ekran „Kontroler...”). Ruch i kamera są zawsze na gałkach.</summary>
public enum PadAction
{
    Jump, Sneak, Sprint, Attack, Use, PickBlock, Inventory, Drop, HotbarPrev, HotbarNext, Pause, Chat, Command, Camera,
}

/// <summary>Przypisania akcji do przycisków; zapis w ustawieniach jako nazwy (np. "Jump": "A").</summary>
public sealed class PadBindings
{
    public static readonly IReadOnlyDictionary<PadAction, PadButton> Defaults = new Dictionary<PadAction, PadButton>
    {
        [PadAction.Jump] = PadButton.A, [PadAction.Sneak] = PadButton.B, [PadAction.Drop] = PadButton.X,
        [PadAction.Inventory] = PadButton.Y, [PadAction.HotbarPrev] = PadButton.LeftBumper, [PadAction.HotbarNext] = PadButton.RightBumper,
        [PadAction.Use] = PadButton.LeftTrigger, [PadAction.Attack] = PadButton.RightTrigger, [PadAction.Sprint] = PadButton.LeftStick,
        [PadAction.PickBlock] = PadButton.RightStick, [PadAction.Pause] = PadButton.Start, [PadAction.Chat] = PadButton.Back,
        [PadAction.Camera] = PadButton.DPadUp, [PadAction.Command] = PadButton.DPadDown,
    };

    public static readonly IReadOnlyDictionary<PadAction, string> ActionNames = new Dictionary<PadAction, string>
    {
        [PadAction.Jump] = "Skok / w górę", [PadAction.Sneak] = "Skradanie / w dół", [PadAction.Sprint] = "Sprint",
        [PadAction.Attack] = "Atak / niszczenie", [PadAction.Use] = "Użyj / postaw", [PadAction.PickBlock] = "Wybierz blok",
        [PadAction.Inventory] = "Ekwipunek", [PadAction.Drop] = "Wyrzuć przedmiot", [PadAction.HotbarPrev] = "Poprzedni slot",
        [PadAction.HotbarNext] = "Następny slot", [PadAction.Pause] = "Pauza", [PadAction.Chat] = "Czat",
        [PadAction.Command] = "Komenda", [PadAction.Camera] = "Perspektywa",
    };

    private readonly Dictionary<PadAction, PadButton> _map = new(Defaults);

    public PadButton this[PadAction a] => _map[a];

    /// <summary>Przypisuje przycisk; akcja, która go miała, dostaje poprzedni przycisk tej akcji (zamiana).</summary>
    public void Assign(PadAction action, PadButton button)
    {
        var old = _map[action];
        foreach (var (other, b) in _map.ToList()) if (other != action && b == button) _map[other] = old;
        _map[action] = button;
    }

    public void Reset() { foreach (var (a, b) in Defaults) _map[a] = b; }

    /// <summary>Z ustawień; nieznane nazwy i powtórzone przyciski są pomijane (zostają domyślne).</summary>
    public static PadBindings FromSettings(Dictionary<string, string> saved)
    {
        var b = new PadBindings();
        if (saved == null) return b;
        foreach (var (k, v) in saved)
            if (Enum.TryParse<PadAction>(k, out var a) && Enum.TryParse<PadButton>(v, out var btn)) b.Assign(a, btn);
        return b;
    }

    /// <summary>Do ustawień: tylko różnice względem domyślnych.</summary>
    public Dictionary<string, string> ToSettings() =>
        _map.Where(kv => Defaults[kv.Key] != kv.Value).ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.ToString());

    /// <summary>Krótka nazwa przycisku (ikony podpowiedzi); jedyne źródło skrótów. Na PlayStation przyciski A/B/X/Y
    /// mają ikony-kształty, a tu zastępcze skróty.</summary>
    public static string ShortName(PadButton b, PadLayout layout = PadLayout.Xbox) => layout == PadLayout.PlayStation
        ? b switch
        {
            PadButton.A => "X", PadButton.B => "O", PadButton.X => "Kw", PadButton.Y => "Tr",
            PadButton.LeftBumper => "L1", PadButton.RightBumper => "R1", PadButton.LeftTrigger => "L2", PadButton.RightTrigger => "R2",
            PadButton.LeftStick => "L3", PadButton.RightStick => "R3", PadButton.Back => "Share", PadButton.Start => "Options",
            PadButton.Guide => "PS", _ => ShortName(b),
        }
        : b switch
        {
            PadButton.LeftBumper => "LB", PadButton.RightBumper => "RB", PadButton.LeftTrigger => "LT", PadButton.RightTrigger => "RT",
            PadButton.LeftStick => "LS", PadButton.RightStick => "RS", PadButton.Back => "Back", PadButton.Start => "Start",
            PadButton.Guide => "Guide", PadButton.DPadUp => "+G", PadButton.DPadDown => "+D", PadButton.DPadLeft => "+L", PadButton.DPadRight => "+P",
            _ => b.ToString(),
        };

    /// <summary>Pełna nazwa przycisku (ekran przypisań): opis tam, gdzie skrót nie wystarcza.</summary>
    public static string ButtonName(PadButton b, PadLayout layout = PadLayout.Xbox) => b switch
    {
        PadButton.A when layout == PadLayout.PlayStation => "Krzyżyk",
        PadButton.B when layout == PadLayout.PlayStation => "Kółko",
        PadButton.X when layout == PadLayout.PlayStation => "Kwadrat",
        PadButton.Y when layout == PadLayout.PlayStation => "Trójkąt",
        PadButton.LeftStick or PadButton.RightStick => ShortName(b, layout) + " (wciśnięcie)",
        PadButton.Back => layout == PadLayout.PlayStation ? "Share / Create" : "Back / View",
        PadButton.Start => layout == PadLayout.PlayStation ? "Options" : "Start / Menu",
        PadButton.DPadUp => "Krzyżak w górę", PadButton.DPadDown => "Krzyżak w dół",
        PadButton.DPadLeft => "Krzyżak w lewo", PadButton.DPadRight => "Krzyżak w prawo", _ => ShortName(b, layout),
    };
}
