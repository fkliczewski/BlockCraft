using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Fajerwerki 1.8 (moduł ME-h 39b).</summary>
public static partial class SelfTest
{
    private sealed class FwSoundLog : IWorldListener
    {
        public readonly List<string> All = new();
        public void PlaySound(string n, double x, double y, double z, float volume, float pitch) => All.Add(n);
        public void OnBlockChanged(int x, int y, int z, byte id, int meta) { }
        public void SpawnParticles(ParticleKind k, double x, double y, double z, int count, int data) { }
        public void OnEntityAdded(Entity e) { }
        public void OnEntityRemoved(Entity e) { }
    }

    private static ItemStack Craft(params ItemStack[] items)
    {
        var grid = new ItemStack[9];
        for (int i = 0; i < items.Length; i++) grid[i] = items[i];
        return Recipes.Find(grid, 3, 3);
    }

    private static void TestFireworks()
    {
        var red = Dyes.DyeStack(Dyes.Red); var blue = Dyes.DyeStack(Dyes.Blue); var white = Dyes.DyeStack(Dyes.White);
        var star = Craft(new ItemStack(I.Gunpowder), red, new ItemStack(I.GoldNugget), new ItemStack(I.Diamond), new ItemStack(I.GlowstoneDust));
        var ex = star?.Tag?.GetCompound("Explosion");
        Check(star?.Id == I.FireworkStar && ex != null && ex.GetInt("Type") == Fireworks.Star && ex["Colors"] is int[] c1 && c1.Length == 1 && c1[0] == 11743532
            && ex.GetBool("Trail") && ex.GetBool("Flicker"), "Fajerwerk: gwiazdka (proch + barwnik + samorodek + diament + jasnopył) = gwiazda ze smugą i migotaniem");
        Check(Craft(new ItemStack(I.Gunpowder), red, new ItemStack(I.Feather), new ItemStack(I.GoldNugget)) == null, "Fajerwerk: dwa kształty naraz nie pasują");
        var two = Craft(new ItemStack(I.Gunpowder), red, blue, new ItemStack(I.FireCharge));
        Check(two?.Tag?.GetCompound("Explosion") is { } e2 && e2.GetInt("Type") == Fireworks.Large && (e2["Colors"] as int[])?.Length == 2, "Fajerwerk: ładunek ognia = duża kula, dwa kolory");
        Check(Fireworks.StarColor(two) == (0xFF000000u | (uint)(((11743532 & 255) + (2437522 & 255)) / 2 << 16 | ((11743532 >> 8 & 255) + (2437522 >> 8 & 255)) / 2 << 8 | ((11743532 >> 16) + (2437522 >> 16)) / 2)),
            "Fajerwerk: kolor ikony gwiazdki = średnia kolorów");
        var faded = Craft(star, white, blue);
        Check(faded?.Tag?.GetCompound("Explosion")?["FadeColors"] is int[] fc && fc.Length == 2 && faded.GetHashCode() != star.GetHashCode(), "Fajerwerk: gwiazdka + barwniki = kolory zanikania");
        Check(Craft(star, white, new ItemStack(I.Diamond)) == null, "Fajerwerk: zanikanie tylko z samymi barwnikami");
        var rocket = Craft(new ItemStack(I.Paper), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder), star, faded);
        var fw = rocket?.Tag?.GetCompound("Fireworks");
        Check(rocket?.Id == I.FireworkRocket && fw != null && fw.GetInt("Flight") == 3 && fw.GetList("Explosions")?.Count == 2, "Fajerwerk: rakieta (papier + 3 prochy + 2 gwiazdki) = lot 3, dwa wybuchy");
        var plain = Craft(new ItemStack(I.Paper), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder));
        Check(plain?.Id == I.FireworkRocket && plain.Tag == null, "Fajerwerk: rakieta bez gwiazdek nie ma tagu (jak w 1.8)");
        Check(Craft(new ItemStack(I.Paper), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder), new ItemStack(I.Gunpowder)) == null, "Fajerwerk: najwyżej 3 prochy");

        var w = MakeWorld(23, WorldType.Superflat, 2);
        w.Rules.DoMobSpawning = false;
        var log = new FwSoundLog(); w.Listeners.Add(log);
        int minL = int.MaxValue, maxL = 0;
        for (int i = 0; i < 200; i++) { var t = new FireworkRocketEntity(w, 0, 10, 0, rocket); minL = Math.Min(minL, t.Lifetime); maxL = Math.Max(maxL, t.Lifetime); }
        Check(minL >= 40 && maxL <= 51 && maxL - minL >= 8, "Fajerwerk: czas lotu 10 * (1 + lot) + rand(6) + rand(7)", $"{minL}-{maxL}");
        var pl = new FireworkRocketEntity(w, 0, 10, 0, plain);
        Check(pl.Lifetime is >= 10 and <= 21, "Fajerwerk: rakieta bez tagu leci jak lot 0", pl.Lifetime.ToString());

        var p = SpawnPlayer(w, 0.5, 4, 0.5);
        var held = rocket.CopyWith(2); p.Inventory.Selected = held;
        Item.ById[I.FireworkRocket].UseOnBlock(p, held, 0, 3, 0, Dir.Up, .5, 1, .5);
        w.FlushAdds();
        var r = w.Entities.OfType<FireworkRocketEntity>().FirstOrDefault();
        Check(r != null && held.Count == 1 && Math.Abs(r.Y - 4) < 1e-6, "Fajerwerk: PPM na bloku wypuszcza rakietę w punkcie kliknięcia");
        if (r != null)
        {
            int before = FireworkFx.Explosions; double y0 = r.Y; int ticks = 0;
            while (!r.Exploded && ticks < 100) { w.Tick(); ticks++; }
            Check(r.Exploded && FireworkFx.Explosions == before + 1 && r.Y > y0 + 10 && ticks == r.Lifetime + 1,
                "Fajerwerk: rakieta wznosi się i wybucha po czasie lotu", $"t{ticks} L{r.Lifetime} dy {r.Y - y0:F1}");
            Check(log.All.Count(s => s == "fireworks.launch") == 1, "Fajerwerk: dźwięk startu raz");
            for (int i = 0; i < 12; i++) w.Tick();
            Check(r.Removed, "Fajerwerk: po wybuchu rakieta znika");
        }
        // dozownik
        var src = new DispenseSource { W = w, X = 10, Y = 4, Z = 10, Facing = Dir.Up };
        var rest = DispenserBehaviors.Get(new ItemStack(I.FireworkRocket))(src, rocket.CopyWith(3));
        w.FlushAdds();
        Check(rest.Count == 2 && w.Entities.OfType<FireworkRocketEntity>().Any(e => !e.Removed && Math.Abs(e.Y - 4.2) < 1e-6 && Math.Abs(e.X - 10.5) < 1e-6),
            "Fajerwerk: dozownik wypuszcza rakietę (y + 0.2)");
        // zapis / sieć
        var a = new FireworkRocketEntity(w, 1, 2, 3, rocket) { FlightAge = 7 };
        var ms = new MemoryStream(); a.Save(new BinaryWriter(ms)); ms.Position = 0;
        var bb = new FireworkRocketEntity(w); bb.Load(new BinaryReader(ms)); bb.SetNetData(1);
        Check(bb.Lifetime == a.Lifetime && bb.FlightAge == 7 && bb.Stack?.Tag?.GetCompound("Fireworks")?.GetList("Explosions")?.Count == 2 && bb.Exploded,
            "Fajerwerk: zapis rakiety i flaga wybuchu dla gości");
    }
}
