using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Podgląd paczek zasobów do ręcznego porównania: BlockCraft.exe --showcase --packs-preview [--resourcepack NAZWA].
/// Zrzuty (bez paczki i z paczką) do folderu showcase/: ściana wszystkich bloków, ekwipunek kreatywny, ekran paczek.</summary>
public sealed partial class Game
{
    private void SetupPacksPreview()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        var packs = _packOverride is { Count: > 0 } ? _packOverride.ToList() : new List<string> { ResourcePacks.BuiltinId };
        _packOverride = new List<string>();
        _reloadPacks = true;
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(10, () => SetScreen(new ResourcePackScreen(new TitleScreen(), packs)));
        Step(10, () => Snap("packs_screen"));
        Step(5, () => CreateWorld("Paczki " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int bx = 0, by = 0, bz = 0;
        Step(30, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000;
            Player.Invulnerable = true; Settings.ShowFps = false;
            (bx, by, bz) = Stage(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z), 0, 50, 20, 9);
            // ściana wszystkich bloków: rzędy po 16, co drugi blok, na trawie przed graczem (+Z)
            int n = 0;
            for (int id = 1; id < 256; id++)
            {
                var b = Block.ById[id];
                if (b == null || b.Id == 0 || b.IsLiquid) continue;
                int x = bx - 15 + n % 16 * 2, z = bz + 3 + n / 16 * 2;
                World.SetBlock(x, by, z, (byte)id);
                n++;
            }
            HideGui = true;
        });
        foreach (bool withPack in new[] { false, true })
        {
            string tag = withPack ? "pack" : "base";
            Step(5, () =>
            {
                if (withPack) { _packOverride = packs; _reloadPacks = true; }
                // z powrotem nad ścianę bloków (sceny mobów przenoszą gracza)
                World.DayTime = 6000; HideGui = true;
                _holdY = by + 9; Player.SetPositionAndPrev(bx + 0.5, by + 9, bz + 0.5);
                Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 50;
            });
            WaitForMeshes(200);
            Step(20, () => Snap("blocks_" + tag));
            Step(5, () => { Player.Pitch = Player.PrevPitch = 20; Player.Yaw = Player.PrevYaw = 0; });
            Step(20, () => Snap("blocks_low_" + tag));
            // zbliżenie: kamień, bruk, mniszek, ziemia (modele z paczek)
            Step(5, () =>
            {
                int zz = bz - 6;
                for (int dx = -4; dx <= 4; dx++) for (int dz = -2; dz <= 3; dz++) for (int dy = 0; dy < 4; dy++) World.SetBlock(bx + dx, by + dy, zz + dz, B.Air);
                World.SetBlock(bx - 3, by, zz + 2, B.Stone); World.SetBlock(bx - 1, by, zz + 2, B.Cobblestone);
                World.SetBlock(bx + 1, by, zz + 2, B.Dandelion); World.SetBlock(bx + 3, by, zz + 2, B.Dirt);
                _holdY = by + 1.2; Player.SetPositionAndPrev(bx + 0.5, by + 1.2, zz - 1.5);
                Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 25;
            });
            WaitForMeshes(120);
            Step(15, () => Snap("models_" + tag));
            Step(5, () => { _holdY = by + 9; Player.SetPositionAndPrev(bx + 0.5, by + 9, bz + 0.5); Player.Pitch = Player.PrevPitch = 50; });
            Step(5, () => { Player.Pitch = Player.PrevPitch = 50; HideGui = false; Cmds.Execute(Player, "/gamemode creative"); });
            foreach (int tab in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 10, 11 })
            {
                int t = tab;
                Step(8, () => { _chat.Clear(); SetScreen(new CreativeScreen(Player, t)); });
                Step(8, () => Snap($"creative_{t:D2}_{tag}"));
            }
            Step(5, () => { SetScreen(null); _chat.Clear(); Cmds.Execute(Player, "/gamemode survival"); Player.Health = 13; Player.FoodLevel = 11; });
            Step(10, () => { _chat.Clear(); Snap("hud_" + tag); });
            Step(5, () => SetScreen(new ContainerScreen(Player, Player.InventoryContainer)));
            Step(8, () => Snap("inventory_" + tag));
            Step(5, () =>
            {
                SetScreen(null);
                var wb = new WorkbenchContainer(Player.Inventory, bx, by, bz - 3);
                Player.OpenContainer = wb; SetScreen(new ContainerScreen(Player, wb));
            });
            Step(8, () => Snap("crafting_" + tag));
            Step(5, () =>
            {
                SetScreen(null);
                World.SetBlock(bx + 2, by, bz - 3, B.Furnace, 0);
                OpenBlockGui(GuiKind.Furnace, bx + 2, by, bz - 3);
                if (Player.OpenContainer is FurnaceContainer fc) { fc.Furnace.BurnTime = 800; fc.Furnace.CurrentItemBurnTime = 1600; fc.Furnace.CookTime = 120; }
            });
            Step(8, () => Snap("furnace_" + tag));
            Step(5, () =>
            {
                SetScreen(null);
                World.SetBlock(bx - 2, by, bz - 3, B.Chest, 0);
                OpenBlockGui(GuiKind.Chest, bx - 2, by, bz - 3);
            });
            Step(8, () => Snap("chest_" + tag));
            Step(5, () => { SetScreen(null); HideGui = true; Cmds.Execute(Player, "/weather clear 99999"); _chat.Clear(); World.DayTime = 6000; Player.Pitch = Player.PrevPitch = -89; });
            Step(20, () => Snap("sun_" + tag));
            Step(5, () => World.DayTime = 18000 + 24000 * 2); // trzecia faza księżyca
            Step(20, () => Snap("moon_" + tag));
            Step(5, () => { World.DayTime = 6000; Player.Pitch = Player.PrevPitch = 50; Player.Yaw = Player.PrevYaw = 0; });
            // moby (etap 4): po kilka w rzędzie przed graczem
            // rząd: moby, odstęp między nimi, odległość od gracza i wysokość nad ziemią (cały rząd w kadrze)
            var mobRows = new (string[] Mobs, double Spacing, double Dist, int Lift)[]
            {
                (new[] { "Zombie", "Skeleton", "Creeper", "Spider", "CaveSpider", "Enderman", "PigZombie", "Witch", "Villager", "Slime" }, 1.6, 6, 0),
                (new[] { "Cow", "MushroomCow", "Pig", "Sheep", "Chicken", "Wolf", "Ozelot", "Rabbit", "Squid", "Bat" }, 1.6, 6, 0),
                (new[] { "EntityHorse", "VillagerGolem", "SnowMan", "Blaze", "Guardian" }, 3, 9, 0),
                (new[] { "LavaSlime", "Silverfish", "Endermite", "Ghast" }, 3, 9, 0),
                (new[] { "WitherBoss", "EnderDragon" }, 16, 18, 4),
            };
            int bossRow = mobRows.Length - 1;
            for (int row = 0; row < mobRows.Length; row++)
            {
                var (mobs, spacing, dist, lift) = mobRows[row]; int rowIndex = row; bool bosses = row == bossRow;
                Step(5, () =>
                {
                    Cmds.Execute(Player, "/difficulty normal"); _chat.Clear();
                    World.DayTime = rowIndex == 0 ? 13500 : 6000; // potwory nie płoną (bez czerwonego błysku obrażeń)
                    var (sx, sy, sz) = Stage(bx + 60 + rowIndex * 50, bz, 0, bosses ? 5 : 18, 14, bosses ? 6 : 1.5);
                    for (int i = 0; i < mobs.Length; i++)
                        Summon(mobs[i], sx + 0.5 + (i - (mobs.Length - 1) / 2.0) * spacing, sy + lift, sz + 0.5 + dist);
                    if (bosses) foreach (var e in World.Entities.OfType<Entities.EnderDragon>()) e.NoAI = true;
                });
                Step(12, () => { _chat.Clear(); Snap($"mobs_{rowIndex}_{tag}"); });
            }
            Step(5, () =>
            {
                foreach (var e in World.Entities.ToList()) if (e is not Entities.Player) e.Kill();
                Cmds.Execute(Player, "/difficulty peaceful"); _chat.Clear();
                Player.Inventory.Armor[0] = new ItemStack(I.DiamondHelmet); Player.Inventory.Armor[1] = new ItemStack(I.IronChest);
                Player.Inventory.Armor[2] = new ItemStack(I.GoldLegs); Player.Inventory.Armor[3] = new ItemStack(I.LeatherBoots);
                Player.Pitch = Player.PrevPitch = 10; CameraMode = 2; HideGui = true;
            });
            Step(10, () => Snap("armor_" + tag));
            Step(5, () => { CameraMode = 0; for (int a = 0; a < 4; a++) Player.Inventory.Armor[a] = null; });
            Step(5, () => SetScreen(new OptionsScreen(new TitleScreen())));
            Step(8, () => Snap("options_" + tag));
            Step(5, () => { SetScreen(null); HideGui = true; });
        }
        Step(5, () =>
        {
            foreach (var s in new[] { new ItemStack(I.IronPickaxe), new ItemStack(B.Wool, 1, 14), new ItemStack(I.SpawnEgg, 1, 50), new ItemStack(I.Potion, 1, 16385), new ItemStack(B.Stone, 1, 2) })
                Log.Info("NAZWA [" + Lang.Code + "] " + s.Item.Key + ":" + s.Damage + " = " + s.Name);
        });
        // wyszukiwanie: pisanie w zakładce Budulce przełącza na „Szukaj”
        Step(5, () =>
        {
            if (Player == null) return;
            Cmds.Execute(Player, "/gamemode creative"); _chat.Clear(); HideGui = false;
            SetScreen(new CreativeScreen(Player, 0));
            foreach (char ch in "zelazny") _screen.CharTyped(ch);
        });
        Step(8, () => Snap("search"));
        Step(5, () => SetScreen(new LanguageScreen(new TitleScreen())));
        Step(10, () => Snap("language"));
        Step(5, () => { Log.Info("PACKS PREVIEW DONE"); Close(); });
    }
}
