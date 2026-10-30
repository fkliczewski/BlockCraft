using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

public static class SpawnEggs
{
    public sealed record Egg(int Id, string Key, string Name, uint Base, uint Spots);
    public static readonly Egg[] All = {
        new(50,"creeper","Creeper",0x0DA70B,0x000000), new(51,"skeleton","Szkielet",0xC1C1C1,0x494949),
        new(52,"spider","Pająk",0x342D27,0xA80E0E), new(54,"zombie","Zombie",0x00AFAF,0x799C65),
        new(55,"slime","Szlam",0x51A03E,0x7EBF6E), new(58,"enderman","Enderman",0x161616,0x000000),
        new(59,"cave_spider","Pająk jaskiniowy",0x0C424E,0xA80E0E), new(60,"silverfish","Rybik cukrowy",0x6E6E6E,0x303030),
        new(65,"bat","Nietoperz",0x4C3E30,0x0F0F0F), new(66,"witch","Wiedźma",0x340000,0x51A03E),
        new(68,"guardian","Strażnik",0x5A8272,0xF17D30), new(90,"pig","Świnia",0xF0A5A2,0xDB635F),
        new(91,"sheep","Owca",0xE7E7E7,0xFFB5B5), new(92,"cow","Krowa",0x443626,0xA1A1A1),
        new(93,"chicken","Kurczak",0xA1A1A1,0xFF0000), new(94,"squid","Kałamarnica",0x223B4D,0x708899),
        new(95,"wolf","Wilk",0xD7D3D3,0xCEAF96), new(96,"mooshroom","Grzybowa krowa",0xA00F10,0xB7B7B7),
        new(98,"ocelot","Ocelot",0xEFDE7D,0x564434), new(100,"horse","Koń",0xC09E7D,0xEEE500),
        new(101,"rabbit","Królik",0x995F40,0x734831), new(120,"villager","Wieśniak",0x563C33,0xBD8B72)
    };
    public static Egg Find(int id) => All.FirstOrDefault(e => e.Id == id);
    public static Mob Spawn(World.World w, ItemStack egg, double x, double y, double z)
    {
        var def = Find(egg.Damage);
        if (def == null || MobSpawner.Create(def.Key, w) is not Mob mob) return null;
        mob.SetPositionAndPrev(x, y, z); mob.Yaw = mob.PrevYaw = w.Rand.NextFloat() * 360;
        mob.CustomName = egg.CustomName;
        w.SpawnEntity(mob);
        return mob;
    }
}

public static partial class ItemRegistry
{
    static partial void ItemsMobs()
    {
        Simple(I.CarrotOnAStick,"carrot_on_a_stick","Marchewka na patyku",5,1).MaxDamage = 25;
        Simple(I.DiamondHorseArmor,"diamond_horse_armor","Diamentowa zbroja dla konia",6,1);
        Simple(I.IronHorseArmor,"iron_horse_armor","Żelazna zbroja dla konia",6,1);
        Simple(I.GoldHorseArmor,"golden_horse_armor","Złota zbroja dla konia",6,1);
        Simple(I.NameTag,"name_tag","Plakietka z nazwą",5);
        Simple(I.Saddle,"saddle","Siodło",5,1);
        Simple(I.PrismarineShard,"prismarine_shard","Odłamek pryzmarynu",7);
        Simple(I.PrismarineCrystals,"prismarine_crystals","Kryształy pryzmarynu",7);
        Simple(I.Slimeball,"slime_ball","Kula szlamu",7);
        var raw = Simple(I.RawRabbit,"rabbit","Surowy królik",4); raw.Hunger = 3; raw.SaturationMod = .3f;
        var cooked = Simple(I.CookedRabbit,"cooked_rabbit","Pieczony królik",4); cooked.Hunger = 5; cooked.SaturationMod = .6f;
        var stew = Simple(I.RabbitStew,"rabbit_stew","Gulasz z królika",4,1); stew.Hunger = 10; stew.SaturationMod = .6f;
        Simple(I.RabbitHide,"rabbit_hide","Skóra królika",7); Simple(I.RabbitFoot,"rabbit_foot","Królicza łapka",7);
        Simple(I.Lead,"lead","Smycz",5);
        var egg = Simple(I.SpawnEgg,"spawn_egg","Jajko przyzywające",3);
        egg.Variants = SpawnEggs.All.Select(e => e.Id).ToArray();
        egg.NameFn = s => "Jajko: " + (SpawnEggs.Find(s.Damage)?.Name ?? "?");
        egg.ColorFn = s => 0xFF000000 | (SpawnEggs.Find(s.Damage)?.Base ?? 0xFFFFFF);
        egg.OverlayFn = s => (TextureGen.Layer("item_spawn_egg_spots"), 0xFF000000 | (SpawnEggs.Find(s.Damage)?.Spots ?? 0));
    }
}

public static partial class Recipes
{
    static partial void RecipesMobs()
    {
        Shaped(new ItemStack(I.CarrotOnAStick), "R "," C", 'R',I.FishingRod,'C',I.Carrot);
        Shaped(new ItemStack(I.Lead,2),"SS ","SB ","  S",'S',I.String,'B',I.Slimeball);
        Shaped(new ItemStack(I.Leather),"HH","HH",'H',I.RabbitHide);
        foreach (int mushroom in new[] { (int)B.BrownMushroom, B.RedMushroom })
            Shaped(new ItemStack(I.RabbitStew)," R ","CPM"," B ",'R',I.CookedRabbit,'C',I.Carrot,'P',I.BakedPotato,'M',mushroom,'B',I.Bowl);
        Smelting.Table[I.RawRabbit] = (new ItemStack(I.CookedRabbit),.35f);
    }
}
