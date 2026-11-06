using BlockCraft.Audio;

namespace BlockCraft;

/// <summary>Etap 5 paczek: sounds.json z paczek (replace / dopisywanie, kolejność paczek).</summary>
public static partial class SelfTest
{
    private static void TestPackSounds()
    {
        Console.WriteLine("[Paczki zasobów: dźwięki]");
        byte[] J(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        var low = new Dictionary<string, byte[]>
        {
            [SoundBank.PackSoundsJson] = J("{\"mob.cow.say\":{\"category\":\"neutral\",\"sounds\":[\"pack/low_cow\"]},\"custom.only.low\":{\"sounds\":[{\"name\":\"minecraft:pack/x\",\"weight\":3,\"stream\":true}]}}"),
        };
        var high = new Dictionary<string, byte[]>
        {
            [SoundBank.PackSoundsJson] = J("{\"mob.cow.say\":{\"replace\":true,\"sounds\":[\"pack/high_cow\"]},\"custom.only.low\":{\"sounds\":[\"pack/y\"]},\"zepsute\":[1,2]}"),
        };
        // Selection checks file availability; decoding is outside this fixture's scope.
        low["assets/minecraft/sounds/pack/low_cow.ogg"] = Array.Empty<byte>();
        low["assets/minecraft/sounds/pack/x.ogg"] = Array.Empty<byte>();
        high["assets/minecraft/sounds/pack/high_cow.ogg"] = Array.Empty<byte>();
        high["assets/minecraft/sounds/pack/y.ogg"] = Array.Empty<byte>();
        var bank = SoundBank.Load(new Func<string, byte[]>[] { p => high.GetValueOrDefault(p), p => low.GetValueOrDefault(p) });
        Check(bank.Events.TryGetValue("mob.cow.say", out var cow) && cow.Entries.Count == 1 && cow.Entries[0].Name == "pack/high_cow",
            "replace: wyższa paczka zastępuje dźwięki zdarzenia", string.Join(",", cow?.Entries.Select(e => e.Name) ?? Array.Empty<string>()));
        Check(bank.Events.TryGetValue("custom.only.low", out var c) && c.Entries.Count == 2 && c.TotalWeight == 4,
            "Bez replace: dźwięki dopisywane", c?.Entries.Count.ToString());
        Check(c != null && c.Entries.Count > 0 && c.Entries[0].Name == "pack/x" && c.Entries[0].Stream, "Prefiks minecraft: i opcje wpisu");
        Check(!bank.Events.ContainsKey("zepsute"), "Zły wpis w sounds.json pominięty");
    }
}
