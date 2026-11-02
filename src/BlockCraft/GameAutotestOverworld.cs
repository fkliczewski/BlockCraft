using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Game
{
    private void SetupAutotestOverworld()
    {
        int ticks = 0, failures = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++ticks > 900);
        Step(1, () => CreateWorld("AutoTestOW " + DateTime.Now.ToString("HHmmss"), 12345, 3, false, WorldType.Default, true));
        ticks = 0;
        WaitUntil(() => _sessionStarted || ++ticks > 1800);
        Step(1, () =>
        {
            World.Rules.DoMobSpawning = false; World.Rules.DoDaylightCycle = false; World.DayTime = 6000;
            Renderer.RenderDistance = 4; World.ChunkManager.Radius = 4;
            Player.Flying = true;
        });
        foreach (string structure in new[] { "Village", "Temple", "Monument" })
        {
            var name = structure; OwStructurePlan plan = null; int wait = 0;
            Step(1, () =>
            {
                var gen = World.Generator.Overworld;
                var location = gen.Locate(name, 0, 0);
                if (location == null) { failures++; Log.Error("OW-TEST brak " + name); return; }
                int cx = location.Value.x >> 4, cz = location.Value.z >> 4;
                plan = name switch { "Village" => gen.Village(cx, cz), "Temple" => gen.Temple(cx, cz), _ => gen.Monument(cx, cz) };
                if (plan == null) { failures++; return; }
                var box = plan.Box;
                double radius = Math.Max(box.SizeX, box.SizeZ) * .55 + 12;
                Player.SetPositionAndPrev((box.X0 + box.X1) / 2.0 + radius, Math.Max(70, box.Y1) + radius * .9, (box.Z0 + box.Z1) / 2.0 + radius);
                Player.Yaw = Player.PrevYaw = 135; Player.Pitch = Player.PrevPitch = 32;
                if (name == "Monument")
                {
                    // Kamera blisko szczytu budowli, pod powierzchnią oceanu.
                    // Box obejmuje również fundamenty; jego Y1 nie jest wysokością dachu.
                    Renderer.RenderDistance = 5; World.ChunkManager.Radius = 5;
                    Player.SetPositionAndPrev((box.X0 + box.X1) / 2.0 + 12, 60, (box.Z0 + box.Z1) / 2.0 + 12);
                    Player.Pitch = Player.PrevPitch = 30;
                }
                Log.Info($"OW-TEST {name} chunk={cx},{cz} bbox={box.X0},{box.Y0},{box.Z0}/{box.X1},{box.Y1},{box.Z1}");
            });
            WaitUntil(() => plan == null || ++wait > 1800 || wait > 60 && Renderer.PendingMeshes < 3
                && OwChunksReady(plan.Box));
            Step(20, () =>
            {
                if (plan == null) return;
                if (!OwChunksReady(plan.Box)) { failures++; Log.Error("OW-TEST nie wczytano " + name); }
                else if (name == "Monument")
                {
                    int prismarine = 0, lanterns = 0, gold = 0;
                    var b = plan.Box;
                    for (int x = b.X0; x <= b.X1; x++)
                        for (int z = b.Z0; z <= b.Z1; z++)
                            for (int y = 39; y <= 61; y++)
                            {
                                int id = World.GetBlock(x, y, z);
                                if (id == B.Prismarine) prismarine++;
                                if (id == B.SeaLantern) lanterns++;
                                if (id == B.GoldBlock) gold++;
                            }
                    bool geometry = prismarine > 1000 && lanterns > 0 && gold == 8;
                    if (!geometry) failures++;
                    Log.Info($"OW-TEST Monument geometria={(geometry ? "OK" : "FAIL")} prismarine={prismarine} lanterns={lanterns} gold={gold}");
                }
                Shot("ow_" + name.ToLowerInvariant());
            });
        }
        Step(1, () => CreateWorld("AutoTestWeather " + DateTime.Now.ToString("HHmmss"), 12345, 3, false, WorldType.Superflat, true));
        ticks = 0;
        WaitUntil(() => _sessionStarted || ++ticks > 1800);
        Step(1, () =>
        {
            World.Rules.DoMobSpawning = false; World.Rules.DoDaylightCycle = false; World.DayTime = 6000;
            Renderer.RenderDistance = 3; World.ChunkManager.Radius = 3;
            Player.SetPositionAndPrev(8, 5, 8); Player.Flying = true;
            Player.Yaw = Player.PrevYaw = 30; Player.Pitch = Player.PrevPitch = 5;
        });
        int weatherWait = 0;
        WaitUntil(() => ++weatherWait > 1200 || OwChunksReady(new OwBox(-16, 0, -16, 31, 10, 31)) && Renderer.PendingMeshes < 3);
        foreach (var test in new[] { ("rain", Biome.Plains), ("snow", Biome.IcePlains), ("desert", Biome.Desert), ("clear", Biome.Plains) })
        {
            var fixture = test; int wait = 0;
            Step(1, () =>
            {
                // Kontrolowana scena renderu, bez podróży do losowego biomu.
                foreach (var c in World.Chunks.Values) Array.Fill(c.Biomes, (byte)fixture.Item2);
                Cmds.Execute(Player, "/weather " + (fixture.Item1 == "clear" ? "clear" : "rain") + " 600");
            });
            WaitUntil(() => ++wait > 900 || (fixture.Item1 == "clear" ? World.RainStrength == 0 : World.RainStrength == 1));
            Step(100, () =>
            {
                bool ok = fixture.Item1 switch
                {
                    "rain" => Renderer.RainColumns > 0 && Renderer.SnowColumns == 0,
                    "snow" => Renderer.RainColumns == 0 && Renderer.SnowColumns > 0,
                    _ => Renderer.RainColumns == 0 && Renderer.SnowColumns == 0
                };
                ok &= fixture.Item1 == "rain" ? Sound.RainVoices <= 1 : Sound.RainVoices == 0;
                if (!ok) failures++;
                Log.Info($"OW-TEST pogoda {fixture.Item1}: {(ok ? "OK" : "FAIL")} rain={Renderer.RainColumns} snow={Renderer.SnowColumns} rainVoices={Sound.RainVoices} audio={Sound.Enabled} native={Renderer.WeatherNativeTextures} tex={Renderer.WeatherTextureSize}");
                Shot("ow_weather_" + fixture.Item1);
            });
        }
        Step(1, () =>
        {
            Player.SetPositionAndPrev(8, 28, 65); Player.Yaw = Player.PrevYaw = 180; Player.Pitch = Player.PrevPitch = -35;
            Renderer.RenderDistance = 6; World.ChunkManager.Radius = 6;
        });
        int lightningWait = 0;
        WaitUntil(() => ++lightningWait > 1800 || World.GetChunk(0, 0)?.Merged == true && Renderer.PendingMeshes < 3);
        BlockCraft.Entities.LightningBolt visualBolt = null;
        Step(1, () =>
        {
            visualBolt = new BlockCraft.Entities.LightningBolt(World) { RemainingFlashes = 0, VertexSeed = 1809 };
            visualBolt.SetPositionAndPrev(8, 4, 8); World.AddEntityNow(visualBolt);
        });
        Step(1, () =>
        {
            if (!visualBolt.Flashing) { failures++; Log.Error("OW-TEST piorun niewidoczny"); }
            Shot("ow_lightning");
        });
        Step(20, () =>
        {
            if (World.Entities.Contains(visualBolt)) { failures++; Log.Error("OW-TEST piorun nie zniknął"); }
            Log.Info($"OW-TEST KONIEC: {(failures == 0 ? "OK" : failures + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close();
        });
    }

    private bool OwChunksReady(OwBox box)
    {
        for (int cx = box.X0 >> 4; cx <= box.X1 >> 4; cx++)
            for (int cz = box.Z0 >> 4; cz <= box.Z1 >> 4; cz++)
                if (World.GetChunk(cx, cz)?.Merged != true) return false;
        return true;
    }
}
