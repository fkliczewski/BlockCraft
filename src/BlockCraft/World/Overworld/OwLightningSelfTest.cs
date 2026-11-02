using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private sealed class LightningSounds : IWorldListener
    {
        public readonly List<string> Sounds = new();
        public void PlaySound(string n, double x, double y, double z, float volume, float pitch) => Sounds.Add(n);
        public void OnBlockChanged(int x, int y, int z, byte id, int meta) { }
        public void SpawnParticles(ParticleKind k, double x, double y, double z, int count, int data) { }
        public void OnEntityAdded(Entity e) { }
        public void OnEntityRemoved(Entity e) { }
    }

    private static void TestOwLightning()
    {
        var w = MakeWorld(55, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false; w.Rules.DoFireTick = false;
        var sounds = new LightningSounds(); w.Listeners.Add(sounds);
        T Spawn<T>(T e, double x, double z) where T : Entity { e.SetPositionAndPrev(x, 4, z); w.AddEntityNow(e); return e; }
        var pig = Spawn(new Pig(w), 8, 8); pig.CustomName = "Pig";
        var villager = Spawn(new Villager(w), 9, 8); villager.CustomName = "Villager";
        var creeper = Spawn(new Creeper(w) { PersistenceRequired = true }, 8, 9);
        var cow = Spawn(new Cow(w), 7, 8);
        var farCow = Spawn(new Cow(w), 15, 15);
        var bolt = Spawn(new LightningBolt(w) { RemainingFlashes = 0 }, 8, 8);
        w.Tick();
        Check(pig.Removed && w.Entities.Any(e => e.TypeKey == "zombie_pigman" && e is Mob l && l.CustomName == "Pig"),
            "Piorun zamienia świnię w zombie pigmana i zachowuje nazwę");
        Check(villager.Removed && w.Entities.OfType<Witch>().Any(e => e.CustomName == "Villager"), "Piorun zamienia wieśniaka w wiedźmę");
        Check(creeper.Charged && cow.Health == cow.MaxHealth - 5 && farCow.Health == farCow.MaxHealth,
            "Piorun ładuje creepera, zadaje 5 obrażeń i ma ograniczony zasięg", $"charged={creeper.Charged} cow={cow.Health}/{cow.MaxHealth} far={farCow.Health}/{farCow.MaxHealth}");
        Check(cow.FireTicks > 0 && w.GetBlock(8, 4, 8) == B.Air, "doFireTick=false blokuje ogień na blokach, ale nie podpalenie encji");
        Ticks(w, 4);
        Check(!w.Entities.Contains(bolt) && sounds.Sounds.Count(n => n == "ambient.weather.thunder") == 1
            && sounds.Sounds.Count(n => n == "random.explode") == 1, "Piorun znika, grzmot i trzask grają tylko raz");
        var serialized = new LightningBolt(w) { State = -3, RemainingFlashes = 2, VertexSeed = 987654321 };
        using var ms = new MemoryStream(); using var bw = new BinaryWriter(ms); serialized.Save(bw); ms.Position = 0;
        var remote = new World.World(55, WorldType.Superflat, true); var copy = new LightningBolt(remote);
        copy.Load(new BinaryReader(ms)); copy.Tick();
        Check(copy.State == -3 && copy.RemainingFlashes == 2 && copy.VertexSeed == 987654321,
            "Sieć odtwarza stan i geometrię pioruna, klient nie losuje kolejnych błysków");
        Check(EntitySerializer.Create("lightning_bolt", w) is LightningBolt && MobSpawner.Create("LightningBolt", w) is LightningBolt,
            "Rejestracja pioruna i alias komendy summon z MC 1.8");
        var fireWorld = MakeWorld(56, WorldType.Superflat, 1); fireWorld.Rules.DoMobSpawning = false;
        var fireBolt = new LightningBolt(fireWorld) { RemainingFlashes = 0 }; fireBolt.SetPositionAndPrev(8, 4, 8); fireWorld.AddEntityNow(fireBolt);
        fireWorld.Tick();
        Check(fireWorld.GetBlock(8, 4, 8) == B.Fire, "Uderzenie pioruna zapala podłoże przy włączonym doFireTick");
        var nature = MakeWorld(57, WorldType.Superflat, 1); nature.Rules.DoMobSpawning = false;
        nature.RestoreWeather(new WeatherData { Raining = true, Thundering = true, RainTime = 10000, ThunderTime = 10000 });
        Check(nature.TryNaturalLightning(8, 8), "Burza umożliwia naturalne uderzenie pioruna");
        nature.GetChunk(0, 0).Biomes[8 * 16 + 8] = (byte)Biome.Desert;
        Check(!nature.TryNaturalLightning(8, 8), "Naturalne pioruny nie uderzają na pustyni");
        nature.SetWeather("clear", 1000); Ticks(nature, 101);
        Check(!nature.TryNaturalLightning(8, 8), "Brak naturalnych piorunów przy dobrej pogodzie");
    }
}
