using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>Itemy modułu OW (400-416) i haki użycia (sadzenie, mączka kostna, kakao, warstwy śniegu, lilie, motyka).</summary>
public static partial class ItemRegistry
{
    static partial void ItemsOverworld()
    {
        const int Mats = 7, Food = 4, Misc = 3, Red = 2;
        Simple(I.Emerald, "emerald", "Szmaragd", Mats);
        var snow = Simple(I.Snowball, "snowball", "Śnieżka", Misc, 16);
        snow.UseInAir = (p, s) =>
        {
            var w = p.World;
            w.SpawnEntity(new SnowballEntity(w, p));
            w.PlaySound("random.bow", p.X, p.Y, p.Z, 0.5f, 0.4f / (w.Rand.NextFloat() * 0.4f + 0.8f));
            OwUse.Consume(p, s); p.SwingArm();
            return true;
        };

        Item FoodItem(int id, string key, string name, int hunger, float sat)
        { var f = Simple(id, key, name, Food); f.Hunger = hunger; f.SaturationMod = sat; return f; }
        FoodItem(I.Carrot, "carrot", "Marchewka", 3, 0.6f).UseOnBlock = OwUse.Planter(B.Carrots, B.Farmland);
        FoodItem(I.Potato, "potato", "Ziemniak", 1, 0.3f).UseOnBlock = OwUse.Planter(B.Potatoes, B.Farmland);
        FoodItem(I.BakedPotato, "baked_potato", "Pieczony ziemniak", 5, 0.6f);
        FoodItem(I.PoisonousPotato, "poisonous_potato", "Trujący ziemniak", 2, 0.3f).OnEaten = p =>
        {
            if (p.World.Rand.NextFloat() < 0.6f) p.AddEffect(Effect.Poison, 100, 0);
        };
        FoodItem(I.MelonSlice, "melon", "Arbuz", 2, 0.3f);
        FoodItem(I.PumpkinPie, "pumpkin_pie", "Ciasto dyniowe", 8, 0.3f);
        Simple(I.PumpkinSeeds, "pumpkin_seeds", "Nasiona dyni", Mats).UseOnBlock = OwUse.Planter(B.PumpkinStem, B.Farmland);
        Simple(I.MelonSeeds, "melon_seeds", "Nasiona arbuza", Mats).UseOnBlock = OwUse.Planter(B.MelonStem, B.Farmland);
        ById(I.PumpkinSeeds).Sprite = TextureGen.Layer("item_seeds_pumpkin");
        ById(I.MelonSeeds).Sprite = TextureGen.Layer("item_seeds_melon");
        ById(I.BakedPotato).Sprite = TextureGen.Layer("item_potato_baked");
        ById(I.PoisonousPotato).Sprite = TextureGen.Layer("item_potato_poisonous");

        // drzwi z nowych drewien (ItemDoor)
        (int item, byte door, string key, string name)[] doors =
        {
            (I.SpruceDoorItem, B.SpruceDoor, "spruce_door", "Świerkowe drzwi"), (I.BirchDoorItem, B.BirchDoor, "birch_door", "Brzozowe drzwi"),
            (I.JungleDoorItem, B.JungleDoor, "jungle_door", "Dżunglowe drzwi"), (I.AcaciaDoorItem, B.AcaciaDoor, "acacia_door", "Akacjowe drzwi"),
            (I.DarkOakDoorItem, B.DarkOakDoor, "dark_oak_door", "Drzwi z ciemnego dębu"),
        };
        foreach (var (item, door, key, name) in doors)
        {
            // item-blok drzwi ma ten sam klucz co item drzwi w MC; zmiana klucza, żeby /give spruce_door dawało item drzwi
            if (Item.ById[door] != null) Item.ById[door].Key = key + "_block";
            var d = Simple(item, key, name, Red);
            d.UseOnBlock = OwUse.DoorPlacer(door);
        }

        // barwniki (meta jak w MC; 4 lazuryt i 15 mączka kostna to osobne itemy)
        var dye = new Item(I.Dye, "dye", "Barwnik")
        {
            CreativeTab = Mats, Sprite = TextureGen.Layer("item_dye_0"),
            Variants = Enumerable.Range(0, 16).Where(m => m != 4 && m != 15).ToArray(),
            SpriteFn = s => TextureGen.Layer("item_dye_" + ((s?.Damage ?? 0) & 15)),
            NameFn = s => Dyes.DyeNames[s.Damage & 15],
        };
        dye.UseOnBlock = OwUse.CocoaPlacer;

        // mączka kostna: wszystko, co rośnie (IGrowable)
        ById(I.BoneMeal).UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) =>
        {
            var w = p.World;
            var b = Block.Get(w.GetBlock(x, y, z));
            if (OwFlora.ApplyBoneMeal(w, x, y, z)) { OwUse.Consume(p, s); return true; }
            return b is IBoneMealable || b.Id == B.Wheat;
        };

        // motyka: gruboziarnista ziemia -> ziemia, bielica bez zmian (ItemHoe 1.8)
        foreach (int hoe in new[] { I.WoodHoe, I.StoneHoe, I.IronHoe, I.GoldHoe, I.DiamondHoe })
            ById(hoe).UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) =>
            {
                var w = p.World;
                if (w.GetBlock(x, y, z) != B.Dirt || face == Dir.Down || w.GetBlock(x, y + 1, z) != B.Air) return false;
                int m = w.GetMeta(x, y, z);
                if (m == 2) return true;
                if (m != 1) return false;
                w.SetBlock(x, y, z, B.Dirt, 0);
                w.PlaySound("step.gravel", x + 0.5, y + 0.5, z + 0.5, 1f, 0.8f);
                p.DamageHeld(1);
                return true;
            };

        // itemy-bloki: ikony płaskie i kolory jak w MC
        void Flat(int block, Func<ItemStack, int> fn = null)
        {
            var it = Item.ById[block]; if (it == null) return;
            var b = Block.Get(block);
            it.Sprite = b.GetTexture(0, 0);
            if (fn != null) it.SpriteFn = fn;
        }
        Flat(B.Poppy, s => Block.Get(B.Poppy).GetTexture(0, s?.Damage ?? 0));
        Flat(B.TallGrass, s => Block.Get(B.TallGrass).GetTexture(0, s?.Damage ?? 1));
        Flat(B.DoublePlant, s => Block.Get(B.DoublePlant).GetTexture(0, 8 | (s?.Damage ?? 0)));
        Flat(B.GlassPane, _ => TextureGen.Layer("glass"));
        Flat(B.IronBars);
        Flat(B.StainedGlassPane, s => Block.Get(B.StainedGlassPane).GetTexture(0, s?.Damage ?? 0));
        Flat(B.Vine); Flat(B.LilyPad); Flat(B.Cobweb); Flat(B.BrownMushroom); Flat(B.RedMushroom); Flat(B.DeadBush);
        foreach (int sap in new[] { B.OakSapling, B.SpruceSapling, B.BirchSapling, B.JungleSapling, B.AcaciaSapling, B.DarkOakSapling }) Flat(sap);
        if (Item.ById[B.DoublePlant] is { } dp) dp.ColorFn = s => (s?.Damage ?? 0) is 2 or 3 ? 0xFF5AC079 : 0xFFFFFFFF;
        if (Item.ById[B.LilyPad] is { } lily)
        {
            lily.ColorFn = _ => 0xFF208030;
            lily.UseInAir = OwUse.PlaceLily;
            lily.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => OwUse.PlaceLily(p, s);
        }
        if (Item.ById[B.SpruceLeaves] is { } sl) sl.ColorFn = _ => 0xFF619961;
        if (Item.ById[B.BirchLeaves] is { } bl) bl.ColorFn = _ => 0xFF80A755;
        if (Item.ById[B.SnowLayer] is { } sn) { sn.UseOnBlock = OwUse.SnowLayerStack; sn.Variants = new[] { 0 }; }
        // nieużywane warianty w kreatywnym (MC pokazuje tylko jeden)
        if (Item.ById[B.Cocoa] is { } co) co.InCreative = false;
    }
}

/// <summary>Wspólne funkcje użycia itemów OW.</summary>
public static class OwUse
{
    public static void Consume(Player p, ItemStack s)
    {
        if (p.Creative) return;
        s.Count--;
        if (s.Count <= 0 && p.Inventory.Selected == s) p.Inventory.Selected = null;
        p.Inventory.Dirty = true;
    }

    /// <summary>Sadzenie (ItemSeeds / ItemSeedFood): na górnej ścianie podłoża, gdy nad nim powietrze.</summary>
    public static Func<Player, ItemStack, int, int, int, int, double, double, double, bool> Planter(byte crop, byte soil)
        => (p, s, x, y, z, face, hx, hy, hz) =>
        {
            var w = p.World;
            if (face != Dir.Up || w.GetBlock(x, y, z) != soil || w.GetBlock(x, y + 1, z) != B.Air) return false;
            w.SetBlock(x, y + 1, z, crop, 0);
            w.PlaySound("dig.grass", x + 0.5, y + 1.5, z + 0.5, 1f, 0.8f);
            Consume(p, s);
            return true;
        };

    /// <summary>ItemDoor.onItemUse: tylko na górnej ścianie.</summary>
    public static Func<Player, ItemStack, int, int, int, int, double, double, double, bool> DoorPlacer(byte door)
        => (p, s, x, y, z, face, hx, hy, hz) =>
        {
            if (face != Dir.Up) return false;
            var w = p.World;
            if (!Block.Get(w.GetBlock(x, y, z)).Replaceable) y++;
            if (!OwDoorBlock.Place(w, p, door, x, y, z)) return false;
            w.PlaySound("dig.wood", x + 0.5, y + 0.5, z + 0.5, 1f, 0.8f);
            Consume(p, s);
            return true;
        };

    /// <summary>Ziarna kakaowe na bocznej ścianie dżunglowego drewna (ItemDye, meta 3).</summary>
    public static bool CocoaPlacer(Player p, ItemStack s, int x, int y, int z, int face, double hx, double hy, double hz)
    {
        if ((s.Damage & 15) != 3) return false;
        var w = p.World;
        if (w.GetBlock(x, y, z) != B.JungleLog || face == Dir.Up || face == Dir.Down) return true;
        int tx = x + Dir.X[face], ty = y, tz = z + Dir.Z[face];
        if (w.GetBlock(tx, ty, tz) != B.Air) return true;
        int meta = -1;
        for (int m = 0; m < 4; m++) { var (dx, dz) = CocoaBlock.LogOffset(m); if (dx == -Dir.X[face] && dz == -Dir.Z[face]) meta = m; }
        if (meta < 0) return true;
        w.SetBlock(tx, ty, tz, B.Cocoa, meta);
        w.PlaySound("dig.wood", tx + 0.5, ty + 0.5, tz + 0.5, 1f, 0.8f);
        Consume(p, s);
        return true;
    }

    /// <summary>ItemLilyPad: stawianie na źródle wody, w które celuje gracz (raycast z płynami).</summary>
    public static bool PlaceLily(Player p, ItemStack s)
    {
        var w = p.World;
        var (lx, ly, lz) = p.Look();
        var h = w.RayCast(p.X, p.Y + p.EyeHeight, p.Z, lx, ly, lz, 5, true);
        if (!h.Hit) return false;
        int id = w.GetBlock(h.X, h.Y, h.Z);
        if (!(id == B.Water && w.GetMeta(h.X, h.Y, h.Z) == 0 || id == B.Ice) || w.GetBlock(h.X, h.Y + 1, h.Z) != B.Air) return false;
        w.SetBlock(h.X, h.Y + 1, h.Z, B.LilyPad, 0);
        w.PlaySound("dig.grass", h.X + 0.5, h.Y + 1.5, h.Z + 0.5, 1f, 0.8f);
        Consume(p, s); p.SwingArm();
        return true;
    }

    /// <summary>ItemSnow: dokładanie warstw śniegu (do 8).</summary>
    public static bool SnowLayerStack(Player p, ItemStack s, int x, int y, int z, int face, double hx, double hy, double hz)
    {
        var w = p.World;
        int id = w.GetBlock(x, y, z);
        bool replaceable = id == B.SnowLayer ? (w.GetMeta(x, y, z) & 7) == 0 : Block.Get(id).Replaceable;
        if ((face != Dir.Up || id != B.SnowLayer) && !replaceable) { x += Dir.X[face]; y += Dir.Y[face]; z += Dir.Z[face]; }
        if (w.GetBlock(x, y, z) != B.SnowLayer) return false;
        int l = w.GetMeta(x, y, z) & 7;
        if (l >= 7) return false;
        w.SetMeta(x, y, z, l + 1);
        w.PlaySound("step.snow", x + 0.5, y + 0.5, z + 0.5, 1f, 0.8f);
        Consume(p, s);
        return true;
    }
}

