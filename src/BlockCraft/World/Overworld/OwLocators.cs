using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

public sealed partial class OwGenerator
{
    public (int x, int y, int z)? Locate(string name, int x, int z)
    {
        if (name.Equals("Stronghold", StringComparison.OrdinalIgnoreCase))
        {
            var p = StrongholdChunks().MinBy(p => Distance(p.x, p.z));
            return (p.x * 16 + 8, 64, p.z * 16 + 8);
        }
        string kind = new[] { "Village", "Temple", "Mineshaft", "Monument" }.FirstOrDefault(t => t.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (kind == null) return null;
        int chunkX = x >> 4, chunkZ = z >> 4;
        long best = long.MaxValue; (int x, int y, int z)? result = null;
        if (kind == "Mineshaft")
        {
            for (int ring = 0; ring <= 128; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++) for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                    int cx = chunkX + dx, cz = chunkZ + dz;
                    if (Distance(cx, cz) < best && IsMineshaftStart(cx, cz)) Accept(cx, cz, Mineshaft(cx, cz));
                }
                if (result != null && (long)Math.Max(0, ring * 16 - 16) * Math.Max(0, ring * 16 - 16) > best) break;
            }
        }
        else
        {
            for (int ring = 0; ring <= 64; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++) for (int dz = -ring; dz <= ring; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                    var p = kind == "Monument" ? GridCandidate((chunkX >> 5) + dx, (chunkZ >> 5) + dz, 32, 5, 10387313, true)
                        : GridCandidate((chunkX >> 5) + dx, (chunkZ >> 5) + dz, 32, 8, kind == "Village" ? 10387312 : 14357617);
                    if (Distance(p.x, p.z) >= best) continue;
                    Accept(p.x, p.z, kind switch { "Village" => Village(p.x, p.z), "Temple" => Temple(p.x, p.z), _ => Monument(p.x, p.z) });
                }
                long bound = Math.Max(0, (ring - 1) * 512);
                if (result != null && bound * bound > best) break;
            }
        }
        return result;
        long Distance(int cx, int cz) { long dx = (long)cx * 16 + 8 - x, dz = (long)cz * 16 + 8 - z; return dx * dx + dz * dz; }
        void Accept(int cx, int cz, OwStructurePlan plan)
        { if (plan != null) { best = Distance(cx, cz); result = (cx * 16 + 8, kind == "Mineshaft" ? plan.Box.Y0 : 64, cz * 16 + 8); } }
    }
}

public static class OwLocators
{
    public static void Register()
    {
        foreach (string type in new[] { "Village", "Temple", "Mineshaft", "Monument", "Stronghold" })
        {
            string name = type;
            WorldHooks.Locators[name] = (w, x, z) => w.Generator.Type == WorldType.Superflat ? null : w.Generator.Overworld.Locate(name, x, z);
        }
        WorldHooks.FindStronghold = WorldHooks.Locators["Stronghold"];
    }
    public static bool Command(World w, Player player, string[] args)
    {
        if (args.Length != 2) { player.SendMessage("§c/locate <" + string.Join('|', WorldHooks.Locators.Keys) + ">"); return false; }
        if (!WorldHooks.Locators.TryGetValue(args[1], out var locator)) { player.SendMessage("§cNieznana struktura: " + args[1]); return false; }
        var p = locator(w, MathUtil.FloorToInt(player.X), MathUtil.FloorToInt(player.Z));
        if (p == null) { player.SendMessage("Nie znaleziono struktury " + args[1] + "."); return true; }
        player.SendMessage($"Najbliższa struktura {args[1]}: {p.Value.x}, {p.Value.y}, {p.Value.z}"); return true;
    }
}
