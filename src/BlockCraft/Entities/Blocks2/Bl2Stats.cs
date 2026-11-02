using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Rodzaj formatowania wartości statystyki (IStatType z 1.8).</summary>
public enum StatFormat { Simple, Time, Distance, Damage }

/// <summary>
/// Statystyki gracza 1.8 (StatList): ogólne, bloki (wykopane), przedmioty (wytworzone, użyte, zniszczone) i moby.
/// Liczy wyłącznie świat hosta (World.IsRemote = false); klient dostaje kopię pakietem S2C.Statistics.
/// Klucze jak w 1.8, z numerycznym id dla bloków i przedmiotów (stat.mineBlock.1) i kluczem encji dla mobów (stat.killEntity.zombie).
/// </summary>
public static class Bl2Stats
{
    public sealed record Def(string Key, string Name, StatFormat Format);

    /// <summary>StatList.generalStats w kolejności dodawania w 1.8.</summary>
    public static readonly Def[] General =
    {
        new("stat.leaveGame", "Wyjścia z gry", StatFormat.Simple),
        new("stat.playOneMinute", "Czas gry", StatFormat.Time),
        new("stat.timeSinceDeath", "Czas od ostatniej śmierci", StatFormat.Time),
        new("stat.walkOneCm", "Przebyta odległość pieszo", StatFormat.Distance),
        new("stat.crouchOneCm", "Przebyta odległość na ugiętych nogach", StatFormat.Distance),
        new("stat.sprintOneCm", "Przebyta odległość biegiem", StatFormat.Distance),
        new("stat.swimOneCm", "Przepłynięta odległość", StatFormat.Distance),
        new("stat.fallOneCm", "Odległość upadków", StatFormat.Distance),
        new("stat.climbOneCm", "Odległość wspinaczki", StatFormat.Distance),
        new("stat.flyOneCm", "Przeleciana odległość", StatFormat.Distance),
        new("stat.diveOneCm", "Odległość pod wodą", StatFormat.Distance),
        new("stat.minecartOneCm", "Odległość wagonikiem", StatFormat.Distance),
        new("stat.boatOneCm", "Odległość łódką", StatFormat.Distance),
        new("stat.pigOneCm", "Odległość na świni", StatFormat.Distance),
        new("stat.horseOneCm", "Odległość na koniu", StatFormat.Distance),
        new("stat.jump", "Skoki", StatFormat.Simple),
        new("stat.drop", "Wyrzucone przedmioty", StatFormat.Simple),
        new("stat.damageDealt", "Zadane obrażenia", StatFormat.Damage),
        new("stat.damageTaken", "Otrzymane obrażenia", StatFormat.Damage),
        new("stat.deaths", "Liczba śmierci", StatFormat.Simple),
        new("stat.mobKills", "Zabite stworzenia", StatFormat.Simple),
        new("stat.animalsBred", "Rozmnożone zwierzęta", StatFormat.Simple),
        new("stat.playerKills", "Zabici gracze", StatFormat.Simple),
        new("stat.fishCaught", "Złowione ryby", StatFormat.Simple),
        new("stat.junkFished", "Złowione śmieci", StatFormat.Simple),
        new("stat.treasureFished", "Złowione skarby", StatFormat.Simple),
        new("stat.talkedToVillager", "Rozmowy z osadnikami", StatFormat.Simple),
        new("stat.tradedWithVillager", "Transakcje z osadnikami", StatFormat.Simple),
        new("stat.cakeSlicesEaten", "Zjedzone kawałki ciasta", StatFormat.Simple),
        new("stat.cauldronFilled", "Napełnione kotły", StatFormat.Simple),
        new("stat.cauldronUsed", "Woda zaczerpnięta z kotła", StatFormat.Simple),
        new("stat.armorCleaned", "Wyczyszczone części zbroi", StatFormat.Simple),
        new("stat.bannerCleaned", "Wyczyszczone sztandary", StatFormat.Simple),
        new("stat.brewingstandInteraction", "Użycia statywu alchemicznego", StatFormat.Simple),
        new("stat.beaconInteraction", "Użycia magicznej latarni", StatFormat.Simple),
        new("stat.dropperInspected", "Przeszukane podajniki", StatFormat.Simple),
        new("stat.hopperInspected", "Przeszukane leje", StatFormat.Simple),
        new("stat.dispenserInspected", "Przeszukane dozowniki", StatFormat.Simple),
        new("stat.noteblockPlayed", "Zagrane bloki nutowe", StatFormat.Simple),
        new("stat.noteblockTuned", "Nastrojone bloki nutowe", StatFormat.Simple),
        new("stat.flowerPotted", "Posadzone rośliny", StatFormat.Simple),
        new("stat.trappedChestTriggered", "Uruchomione skrzynie-pułapki", StatFormat.Simple),
        new("stat.enderchestOpened", "Otwarte skrzynie Endu", StatFormat.Simple),
        new("stat.itemEnchanted", "Zaklęte przedmioty", StatFormat.Simple),
        new("stat.recordPlayed", "Odtworzone płyty", StatFormat.Simple),
        new("stat.furnaceInteraction", "Użycia pieca", StatFormat.Simple),
        new("stat.workbenchInteraction", "Użycia stołu rzemieślniczego", StatFormat.Simple),
        new("stat.chestOpened", "Otwarte skrzynie", StatFormat.Simple),
    };

    public const string Mine = "stat.mineBlock.", Craft = "stat.craftItem.", Use = "stat.useItem.", Break = "stat.breakItem.",
        KillEntity = "stat.killEntity.", KilledBy = "stat.entityKilledBy.";

    /// <summary>Encje mające statystyki zabójstw (EntityList.entityEggs: moby z jajkiem spawnu).</summary>
    public static bool HasEgg(string key) => key != null && SpawnEggs.All.Any(e => e.Key == key);

    public static int Get(Player p, string key) => p?.Statistics.GetValueOrDefault(key) ?? 0;

    /// <summary>StatFileWriter.increaseStat: tylko na hoście.</summary>
    public static void Add(Player p, string key, int n = 1)
    {
        if (p?.World == null || p.World.IsRemote || n <= 0 || key == null) return;
        long v = (long)p.Statistics.GetValueOrDefault(key) + n;
        p.Statistics[key] = (int)Math.Min(int.MaxValue, v);
    }

    // ---------- haki wołane z logiki gry ----------

    /// <summary>EntityPlayer.onUpdate: minutesPlayed co tick, timeSinceDeath gdy żyje.</summary>
    public static void TickPlayer(Player p)
    {
        if (p.World.IsRemote) return;
        Add(p, "stat.playOneMinute");
        if (!p.Dead && p.Health > 0) Add(p, "stat.timeSinceDeath");
    }

    /// <summary>EntityPlayer.addMovementStat i addMountedMovementStat (przesunięcie w jednym ticku).</summary>
    public static void Moved(Player p, double dx, double dy, double dz)
    {
        if (p.World.IsRemote || p.Dead) return;
        if (p.RidingEntity != null)
        {
            int m = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy + dz * dz) * 100);
            if (m <= 0) return;
            switch (p.RidingEntity)
            {
                case MinecartEntity: Add(p, "stat.minecartOneCm", m); break;
                case BoatEntity: Add(p, "stat.boatOneCm", m); break;
                case Pig: Add(p, "stat.pigOneCm", m); break;
                case Horse: Add(p, "stat.horseOneCm", m); break;
            }
            return;
        }
        int horiz = (int)Math.Round(Math.Sqrt(dx * dx + dz * dz) * 100);
        if (p.IsHeadInWater())
        {
            int d = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy + dz * dz) * 100);
            Add(p, "stat.diveOneCm", d);
        }
        else if (p.InWater) Add(p, "stat.swimOneCm", horiz);
        else if (p.Bl2OnLadder) { if (dy > 0) Add(p, "stat.climbOneCm", (int)Math.Round(dy * 100)); }
        else if (p.OnGround)
        {
            if (horiz <= 0) return;
            Add(p, "stat.walkOneCm", horiz);
            if (p.Sprinting) Add(p, "stat.sprintOneCm", horiz);
            else if (p.Sneaking) Add(p, "stat.crouchOneCm", horiz);
        }
        else if (horiz > 25) Add(p, "stat.flyOneCm", horiz);
    }

    /// <summary>EntityPlayer.fall: odległość od 2 bloków, gdy gracz nie może latać.</summary>
    public static void Fell(Player p, float distance)
    {
        if (distance >= 2) Add(p, "stat.fallOneCm", (int)Math.Round(distance * 100));
    }

    /// <summary>EntityLivingBase.onDeath: punkty zabójcy (mobKills/playerKills) i onKillEntity (stat.killEntity).</summary>
    public static void Killed(LivingEntity victim, DamageSource src)
    {
        if (victim.World == null || victim.World.IsRemote) return;
        var killer = src?.Attacker as Player;
        if (killer != null && killer != victim)
        {
            Add(killer, victim is Player ? "stat.playerKills" : "stat.mobKills");
            if (victim is not Player && HasEgg(victim.TypeKey)) Add(killer, KillEntity + victim.TypeKey);
        }
        if (victim is Player dead)
        {
            Add(dead, "stat.deaths");
            dead.Statistics.Remove("stat.timeSinceDeath");
            var by = src?.Attacker;
            if (by != null && by is not Player && HasEgg(by.TypeKey)) Add(dead, KilledBy + by.TypeKey);
        }
    }

    /// <summary>Statystyki otwarcia GUI bloków (onBlockActivated w 1.8).</summary>
    public static void OpenedGui(Player p, GuiKind kind, int x, int y, int z)
    {
        if (p?.World == null || p.World.IsRemote) return;
        string key = kind switch
        {
            GuiKind.Chest => "stat.chestOpened",
            GuiKind.Crafting => "stat.workbenchInteraction",
            GuiKind.Furnace => "stat.furnaceInteraction",
            GuiKind.Brewing => "stat.brewingstandInteraction",
            GuiKind.Beacon => "stat.beaconInteraction",
            GuiKind.Hopper => "stat.hopperInspected",
            GuiKind.TrappedChest => "stat.trappedChestTriggered",
            GuiKind.Dispenser => p.World.GetBlock(x, y, z) == B.Dropper ? "stat.dropperInspected" : "stat.dispenserInspected",
            _ => null
        };
        if (key != null) Add(p, key);
    }

    public static void Crafted(Player p, ItemStack s) { if (s != null) Add(p, Craft + s.Id, Math.Max(1, s.Count)); }
    public static void Used(Player p, int itemId) { if (itemId > 0) Add(p, Use + itemId); }

    /// <summary>EntityPlayer.damageEntity: obrażenia po pancerzu i absorpcji, w dziesiątych częściach.</summary>
    public static void Damaged(Player p, float amount) { if (amount > 0) Add(p, "stat.damageTaken", (int)Math.Round(amount * 10)); }
    public static void DealtDamage(Player p, float amount) { if (amount > 0) Add(p, "stat.damageDealt", (int)Math.Round(amount * 10)); }

    // ---------- formatowanie jak StatBase w 1.8 ----------
    static string Dec(double v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    public static string Format(StatFormat f, long v)
    {
        switch (f)
        {
            case StatFormat.Time:
                {
                    double s = v / 20.0, m = s / 60, h = m / 60, d = h / 24, y = d / 365;
                    return y > .5 ? Dec(y) + " r." : d > .5 ? Dec(d) + " d." : h > .5 ? Dec(h) + " godz." : m > .5 ? Dec(m) + " min" : Dec(s) + " s";
                }
            case StatFormat.Distance:
                {
                    double m = v / 100.0, km = m / 1000;
                    return km > .5 ? Dec(km) + " km" : m > .5 ? Dec(m) + " m" : v + " cm";
                }
            case StatFormat.Damage: return Dec(v * .1);
            default: return v.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    // ---------- sieć: kopia statystyk dla klienta ----------
    public static byte[] Serialize(Dictionary<string, int> stats)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(stats.Count);
            foreach (var (k, v) in stats) { bw.Write(k); bw.Write(v); }
        }
        return ms.ToArray();
    }

    public static void Deserialize(byte[] data, Dictionary<string, int> into)
    {
        using var br = new BinaryReader(new MemoryStream(data));
        int n = br.ReadInt32();
        if (n < 0 || n > 100000) throw new InvalidDataException("Za dużo statystyk");
        into.Clear();
        for (int i = 0; i < n; i++) { string k = br.ReadString(); into[k] = br.ReadInt32(); }
    }
}

public partial class Player
{
    /// <summary>isOnLadder dla statystyki wspinaczki.</summary>
    internal bool Bl2OnLadder => IsOnLadder();
}
