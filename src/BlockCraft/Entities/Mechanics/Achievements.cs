using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public sealed record Achievement(string Id, string Title, string Description, int X, int Y, int Icon, string Parent = null, bool Special = false);

/// <summary>AchievementList (tl) z klienta 1.8: 34 węzły, te same zależności i współrzędne.</summary>
public static class AchievementList
{
    public static readonly Achievement[] All =
    {
        new("openInventory", "Ekwipunek", "Otwórz swój ekwipunek.", 0,0,I.Book),
        new("mineWood", "Zdobywanie drewna", "Podnieś blok drewna.",2,1,B.OakLog,"openInventory"),
        new("buildWorkBench", "Wytwarzanie", "Wytwórz stół rzemieślniczy.",4,-1,B.CraftingTable,"mineWood"),
        new("buildPickaxe", "Czas kopać!", "Wytwórz drewniany kilof.",4,2,I.WoodPickaxe,"buildWorkBench"),
        new("buildFurnace", "Gorący temat", "Wytwórz piec.",3,4,B.Furnace,"buildPickaxe"),
        new("acquireIron", "Żelazna sztuka", "Odbierz sztabkę żelaza z pieca.",1,4,I.IronIngot,"buildFurnace"),
        new("buildHoe", "Czas na uprawę!", "Wytwórz motykę.",2,-3,I.WoodHoe,"buildWorkBench"),
        new("makeBread", "Chleba naszego", "Wytwórz chleb.",-1,-3,I.Bread,"buildHoe"),
        new("bakeCake", "To nie kłamstwo", "Wytwórz ciasto.",0,-5,I.CakeItem,"buildHoe"),
        new("buildBetterPickaxe", "Ulepszenie", "Wytwórz lepszy kilof.",6,2,I.StonePickaxe,"buildPickaxe"),
        new("cookFish", "Pyszna rybka", "Odbierz upieczoną rybę z pieca.",2,6,I.CookedFish,"buildFurnace"),
        new("onARail", "Na torach", "Przejedź wagonikiem co najmniej kilometr od miejsca startu.",2,3,B.Rail,"acquireIron",true),
        new("buildSword", "Czas na atak!", "Wytwórz drewniany miecz.",6,-1,I.WoodSword,"buildWorkBench"),
        new("killEnemy", "Łowca potworów", "Zabij potwora.",8,-1,I.Bone,"buildSword"),
        new("killCow", "Krowi interes", "Podnieś skórę.",7,-3,I.Leather,"buildSword"),
        new("flyPig", "Gdy świnie latają", "Zjedź na świni z urwiska.",9,-3,I.Saddle,"killCow",true),
        new("snipeSkeleton", "Snajper", "Zabij szkieleta strzałą z co najmniej 50 metrów.",7,0,I.Bow,"killEnemy",true),
        new("diamonds", "Diamenty!", "Podnieś diament.",-1,5,I.Diamond,"acquireIron"),
        new("diamondsToYou", "Diamenty dla ciebie!", "Rzuć diament innemu graczowi.",-1,2,I.Diamond,"diamonds"),
        new("portal", "Musimy zejść głębiej", "Przejdź do Netheru.",-1,7,B.Obsidian,"diamonds"),
        new("ghast", "Zwrot do nadawcy", "Zabij ghasta jego kulą ognia.",-4,8,I.GhastTear,"portal",true),
        new("blazeRod", "W ogniu", "Podnieś płomienną różdżkę.",0,9,I.BlazeRod,"portal"),
        new("potion", "Lokalny browar", "Odbierz miksturę ze statywu alchemicznego.",2,8,I.Potion,"blazeRod"),
        new("theEnd", "Koniec?", "Wejdź do Endu.",3,10,I.EyeOfEnder,"blazeRod",true),
        new("theEnd2", "Koniec.", "Wróć z Endu przez portal wyjściowy.",4,13,B.DragonEgg,"theEnd",true),
        new("enchantments", "Zaklinacz", "Wytwórz stół do zaklinania.",-4,4,B.EnchantingTable,"diamonds"),
        new("overkill", "Pogromca", "Zadaj jednym ciosem co najmniej 18 obrażeń.",-4,1,I.DiamondSword,"enchantments",true),
        new("bookcase", "Bibliotekarz", "Wytwórz biblioteczkę.",-3,6,B.Bookshelf,"enchantments"),
        new("breedCow", "Rozmnażanie", "Rozmnóż dwie krowy pszenicą.",7,-5,I.Wheat,"killCow"),
        new("spawnWither", "Początek?", "Przywołaj Withera.",7,12,I.SkullItem,"theEnd2"),
        new("killWither", "Początek.", "Zabij Withera.",7,10,I.NetherStar,"spawnWither"),
        new("fullBeacon", "Latarnia nadziei", "Zbuduj pełną czteropoziomową piramidę pod magiczną latarnią.",7,8,B.Beacon,"killWither",true),
        new("exploreAllBiomes", "Czas przygody", "Odwiedź wszystkie wymagane biomy Overworldu.",4,8,I.DiamondBoots,"theEnd",true),
        new("overpowered", "Przepotężny", "Wytwórz zaklęte złote jabłko.",6,4,I.GoldenApple,"buildBetterPickaxe",true),
    };
    public static Achievement Find(string id) => All.FirstOrDefault(a => a.Id == id);
    public static string IconTexture(int id) => id switch
    {
        I.CakeItem => "achievement_cake", I.CookedFish => "achievement_fish", I.SkullItem => "achievement_skull", B.Beacon => "achievement_beacon", _=>null
    };
    public static void Crafted(Player p, ItemStack s)
    {
        if (s == null) return;
        if (s.Item?.Tool == ToolType.Pickaxe) p.AwardAchievement("buildPickaxe");
        if (s.Item?.Tool == ToolType.Sword) p.AwardAchievement("buildSword");
        string id = s.Id switch
        {
            B.CraftingTable => "buildWorkBench", B.Furnace => "buildFurnace", I.WoodPickaxe => "buildPickaxe",
            I.StonePickaxe or I.IronPickaxe or I.GoldPickaxe or I.DiamondPickaxe => "buildBetterPickaxe",
            I.WoodHoe or I.StoneHoe or I.IronHoe or I.GoldHoe or I.DiamondHoe => "buildHoe",
            I.Bread => "makeBread", I.CakeItem => "bakeCake", I.WoodSword => "buildSword",
            B.EnchantingTable => "enchantments", B.Bookshelf => "bookcase", I.GoldenApple when s.Damage == 1 => "overpowered", _ => null
        };
        if (id != null) p.AwardAchievement(id);
    }
    public static void PickedUp(Player p, ItemStack s)
    {
        string id = s?.Id switch { B.OakLog or B.BirchLog or B.SpruceLog or B.JungleLog or B.AcaciaLog or B.DarkOakLog => "mineWood", I.Leather => "killCow", I.Diamond => "diamonds", I.BlazeRod => "blazeRod", _ => null };
        if (id != null) p.AwardAchievement(id);
    }
    public static void Killed(LivingEntity victim, DamageSource src)
    {
        if (src.Attacker is not Player p || victim.World.IsRemote) return;
        if (victim is Monster) p.AwardAchievement("killEnemy");
        if (victim.TypeKey == "wither") p.AwardAchievement("killWither");
        if (victim.TypeKey == "ghast" && src.Type == "fireball") p.AwardAchievement("ghast");
        double dx = victim.X-p.X, dz = victim.Z-p.Z;
        if (victim.TypeKey == "skeleton" && src.Type == "arrow" && dx*dx+dz*dz >= 2500) p.AwardAchievement("snipeSkeleton");
    }
    public static bool FullBeacon(World.World w, int x, int y, int z)
    {
        if (w.GetBlock(x,y,z) != B.Beacon || y < 4) return false;
        for(int yy=y+1;yy<Chunk.Height;yy++) if(Block.Get(w.GetBlock(x,yy,z)).LightOpacity>0) return false;
        for(int layer=1;layer<=4;layer++)
            for(int dx=-layer;dx<=layer;dx++) for(int dz=-layer;dz<=layer;dz++)
                if(w.GetBlock(x+dx,y-layer,z+dz) is not (B.IronBlock or B.GoldBlock or B.DiamondBlock or B.EmeraldBlock)) return false;
        return true;
    }
}

public partial class Player
{
    public readonly Queue<string> AchievementNotices = new();
    public bool CanAwardAchievement(Achievement a) => a != null && (a.Parent == null || Achievements.Contains(a.Parent));
    public bool AwardAchievement(string id)
    {
        if (World.IsRemote || !CanAwardAchievement(AchievementList.Find(id)) || !Achievements.Add(id)) return false;
        AchievementNotices.Enqueue(id);
        return true;
    }
    public ulong AchievementBits => AchievementList.All.Select((a, i) => Achievements.Contains(a.Id) ? 1UL << i : 0).Aggregate(0UL, (a,b) => a|b);
    private bool _receivedAchievements;
    public void ReceiveAchievements(ulong bits)
    {
        for (int i = 0; i < AchievementList.All.Length; i++)
        {
            string id = AchievementList.All[i].Id;
            if ((bits & (1UL << i)) != 0) { if (Achievements.Add(id) && _receivedAchievements) AchievementNotices.Enqueue(id); }
            else Achievements.Remove(id);
        }
        _receivedAchievements = true;
    }
    private Entity _railVehicle;
    private double _railX, _railY, _railZ;
    public void TickAchievements()
    {
        if (World.IsRemote) return;
        if (RidingEntity?.TypeKey.StartsWith("minecart", StringComparison.Ordinal) == true)
        {
            if (_railVehicle != RidingEntity) { _railVehicle = RidingEntity; _railX=X; _railY=Y; _railZ=Z; }
            if ((X-_railX)*(X-_railX)+(Y-_railY)*(Y-_railY)+(Z-_railZ)*(Z-_railZ) >= 1000000) AwardAchievement("onARail");
        }
        else _railVehicle = null;
        if (Age % 20 != 0) return;
        if(Age%80==0&&!Achievements.Contains("fullBeacon")&&Achievements.Contains("killWither"))
        {
            int bx=Core.MathUtil.FloorToInt(X),by=Core.MathUtil.FloorToInt(Y),bz=Core.MathUtil.FloorToInt(Z);
            for(int x=bx-20;x<=bx+20;x++) for(int z=bz-20;z<=bz+20;z++) for(int y=Math.Max(4,by-20);y<=Math.Min(Chunk.Height-1,by+20);y++)
                if(World.GetBlock(x,y,z)==B.Beacon&&AchievementList.FullBeacon(World,x,y,z)) AwardAchievement("fullBeacon");
        }
        if(World.Dimension != DimensionId.Overworld) return;
        int biome = World.GetBiome(Core.MathUtil.FloorToInt(X), Core.MathUtil.FloorToInt(Z));
        Statistics["achievement.biome." + biome] = 1;
        // BiomeGenBase.explorationBiomesList: base biomes, excluding Hell, Sky and unused FrozenOcean/ExtremeHillsEdge.
        if (Enumerable.Range(0,40).Where(i => i is not (8 or 9 or 10 or 20)).All(i => Statistics.ContainsKey("achievement.biome."+i))) AwardAchievement("exploreAllBiomes");
    }
}
