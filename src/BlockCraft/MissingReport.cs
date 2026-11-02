using System.Reflection;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>
/// Raport braków MC 1.8: stałe ID z B (bloki) i I (przedmioty), które nie mają jeszcze rejestracji w grze.
/// Uruchomienie: BlockCraft.exe --missing [plik]. Służy do planowania dalszych modułów.
/// </summary>
public static class MissingReport
{
    public static int Run(string[] args)
    {
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        var lines = new List<string>();
        int missB = 0, missI = 0;
        foreach (var f in typeof(B).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.IsLiteral) continue;
            int id = Convert.ToInt32(f.GetRawConstantValue());
            if (id == 0) continue;
            var b = Block.ById[id];
            if (b == null || b.Id != id) { lines.Add($"B {id} {f.Name}"); missB++; }
        }
        foreach (var f in typeof(I).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.IsLiteral) continue;
            int id = Convert.ToInt32(f.GetRawConstantValue());
            if (Item.ById[id] == null) { lines.Add($"I {id} {f.Name}"); missI++; }
        }
        lines.Add($"RAZEM brak bloków: {missB}, brak przedmiotów: {missI}");
        int o = Array.IndexOf(args, "--missing");
        string path = o >= 0 && o + 1 < args.Length && !args[o + 1].StartsWith("--") ? args[o + 1] : "missing.txt";
        File.WriteAllLines(path, lines);
        return 0;
    }
}
