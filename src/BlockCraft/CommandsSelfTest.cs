using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestCommandSystem()
    {
        Console.WriteLine("[CMD 1.8]");
        var w = MakeWorld(182, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false; w.AllowCheats = true;
        var p = SpawnPlayer(w, .5, 4, .5); p.Name = "Ala"; p.IsLocalOp = true;
        var q = SpawnPlayer(w, 8.5, 4, .5); q.Name = "Bob";
        var s = new ConsoleSender(w); var ps = new PlayerSender(p);
        bool Run(string line, CommandSender sender = null, int expect = 1)
        {
            int result = Commands.Run(sender ?? s, line);
            Check(result == expect, "CMD /" + line, result == expect ? "" : "wynik=" + result + " " + string.Join(" | ", s.Log.TakeLast(2)));
            return result == expect;
        }
        bool Fails(Action action) { try { action(); return false; } catch (CommandException) { return true; } }
        Check(Arg.Coord(2.25, "~-1.5", true) == .75 && Arg.Coord(2, "5", true) == 5.5 && Arg.Coord(2, "5.0", true) == 5, "CMD współrzędne względne i środek bloku");
        Check(Fails(() => Arg.Double("NaN")) && Fails(() => Arg.Int("2147483648")), "CMD odrzuca NaN i przepełnienie");
        var nbt = Mojangson.Parse("{Name:\"A B\",Health:10f,Long:5L,CanDestroy:[minecraft:stone],Nested:{ok:1b}}");
        Check(nbt.GetString("Name") == "A B" && nbt["Health"] is float && nbt["Long"] is long && nbt.GetList("CanDestroy").Count == 1, "CMD Mojangson typy i listy");
        Check(Fails(() => Mojangson.Parse("{a:[1,2}")) && Fails(() => Mojangson.Parse("{}x")), "CMD błędny Mojangson");
        Check(Mojangson.Matches(Mojangson.Parse("{Nested:{ok:1b}}"), nbt) && !Mojangson.Matches(Mojangson.Parse("{Nested:{ok:2b}}"), nbt), "CMD częściowe porównanie NBT");
        Check(TextComponent.Render("{\"text\":\"Witaj \",\"extra\":[{\"selector\":\"@a\"}]}", s, p).Contains("Ala") && TextComponent.Render("{\"score\":{\"name\":\"Ala\",\"objective\":\"brak\"}}", s, p) == "", "CMD JSON tekst i selektor");
        var pig = new Pig(w); pig.SetPositionAndPrev(3.5, 4, 3.5); w.AddEntityNow(pig);
        Check(Selector.Match(s, "@a").Count == 2 && Selector.Match(ps, "@p").Single() == p, "CMD @a i @p");
        Check(Selector.Match(s, "@e[type=Pig]").Single() == pig && Selector.Match(s, "@e[type=!Player]").Single() == pig, "CMD type i negacja");
        Check(Selector.Match(ps, "@a[name=!Ala]").Single() == q && Selector.Match(ps, "@a[c=-1]").Single() == q, "CMD name i ujemne c");
        Check(Selector.Match(ps, "@r").Count == 1 && Selector.Match(ps, "@a[c=-2147483648]").Count == 2, "CMD losowy gracz i skrajne c");
        q.XpLevel = 9; q.Yaw = 120; q.Pitch = 30;
        Check(Selector.Match(ps, "@a[lm=9,l=9,rym=100,ry=130,rxm=20,rx=40]").Single() == q, "CMD poziom i obrót");
        Check(Selector.Match(ps, "@a[x=0,y=4,z=0,dx=1,dy=0,dz=1]").Single() == p && Selector.Match(ps, "@a[r=3]").Single() == p, "CMD obszar i promień");
        Run("help"); Run("seed"); Run("list"); Run("time set night"); Check(w.DayTime == 13000, "CMD czas");
        Run("time add 30"); Run("time query daytime"); Run("weather rain 10"); Run("toggledownfall");
        Run("difficulty hard"); Check(w.Difficulty == Difficulty.Hard, "CMD trudność");
        Run("gamerule randomTickSpeed 0"); Check(w.Rules.RandomTickSpeed == 0, "CMD gamerule liczba");
        Run("gamerule commandBlockOutput false"); Run("gamerule randomTickSpeed");
        Run("setworldspawn 2 4 2"); Run("spawnpoint Ala 3 4 3"); Check(p.SpawnX == 3, "CMD spawnpoint");
        Run("defaultgamemode adventure"); Check(w.DefaultGameMode == 2, "CMD domyślny tryb");
        Run("gamemode adventure @a", expect: 2); Check(p.GameMode == 2 && q.GameMode == 2, "CMD selektor wykonuje komendę dla obu graczy");
        Run("gamemode creative Ala"); Run("tp Ala ~1 ~ ~-1"); Check(p.X == 1.5 && p.Z == -.5, "CMD tp względne");
        Run("give Ala diamond 7 0 {display:{Name:\"Dar testowy\"}}");
        Check(p.Inventory.Main.Any(st => st?.CustomName == "Dar testowy" && st.Count == 7), "CMD give zachowuje NBT");
        Run("clear Ala diamond -1 0"); Check(p.Inventory.CountOf(I.Diamond) == 7, "CMD clear 0 tylko liczy");
        Run("clear Ala diamond -1 2"); Check(p.Inventory.CountOf(I.Diamond) == 5, "CMD clear maksymalna ilość");
        Run("xp 3L Ala"); Check(p.XpLevel == 3, "CMD poziomy XP"); Run("xp -1L Ala"); Run("xp -1 Ala", expect: 0);
        Run("effect Ala speed 4 1 true"); Check(p.Effects[Effect.Speed].Duration == 80 && p.EffectLevel(Effect.Speed) == 1, "CMD effect czas i poziom"); Run("effect Ala clear");
        Run("replaceitem entity Ala slot.weapon diamond_sword"); Run("enchant Ala sharpness 3");
        Check(EnchantHelper.GetLevel(p.Inventory.Selected, Ench.Sharpness) == 3, "CMD enchant");
        Run("achievement give achievement.openInventory Ala"); Check(p.Achievements.Contains("openInventory"), "CMD achievement"); Run("achievement take achievement.openInventory Ala");
        Run("say witaj @a"); Run("me testuje", ps); Run("tell Bob wiadomość", ps); Run("tellraw Ala {\"text\":\"Test\",\"color\":\"gold\"}");
        Run("title Ala times 0 20 0"); Run("title Ala subtitle {\"text\":\"Podtytuł\"}"); Run("title Ala title {\"text\":\"Tytuł\"}");
        Check(p.Title.Title == "Tytuł" && p.Title.Subtitle == "Podtytuł" && p.Title.Ticks == 20, "CMD title stan");
        Run("title Ala reset"); Check(p.Title.Ticks == 0 && p.Title.Stay == 70, "CMD title reset");
        Run("particle flame 0 5 0 1 1 1 0 3"); Run("playsound random.click Ala 1 4 0");
        Run("setblock 0 5 0 stone"); Run("testforblock 0 5 0 stone");
        Run("setblock 0 5 0 dirt 0 keep", expect: 0); Check(w.GetBlock(0, 5, 0) == B.Stone, "CMD setblock keep");
        Run("fill -2 6 -2 2 8 2 glass 0 hollow"); Check(w.GetBlock(0, 7, 0) == 0 && w.GetBlock(2, 7, 0) == B.Glass, "CMD fill hollow");
        Run("fill -2 6 -2 2 8 2 stone 0 outline"); Check(w.GetBlock(0, 7, 0) == 0 && w.GetBlock(2, 7, 0) == B.Stone, "CMD fill outline");
        Run("fill 10 5 10 11 5 10 dirt 0 keep"); Run("fill 10 5 10 11 5 10 gold_block 0 replace dirt");
        Check(w.GetBlock(10, 5, 10) == B.GoldBlock, "CMD fill replace filtr");
        Run("clone 10 5 10 11 5 10 10 5 12"); Run("testforblocks 10 5 10 11 5 10 10 5 12");
        Run("clone 10 5 10 11 5 10 12 5 10 replace move"); Check(w.GetBlock(10, 5, 10) == 0 && w.GetBlock(12, 5, 10) == B.GoldBlock, "CMD clone move");
        Run("setblock 5 5 5 chest"); Run("replaceitem block 5 5 5 slot.container.0 diamond 4");
        Check(((ChestTileEntity)w.GetTileEntity(5, 5, 5)).Get(0)?.Count == 4, "CMD replaceitem block");
        Run("blockdata 5 5 5 {Items:[{Slot:0b,id:minecraft:gold_ingot,Count:2b,Damage:0s}]}");
        Check(((ChestTileEntity)w.GetTileEntity(5, 5, 5)).Get(0)?.Id == I.GoldIngot, "CMD blockdata Items");
        Run("summon Pig 4 4 4 {CustomName:Prosiak,Health:7f}"); w.FlushAdds();
        Check(w.Entities.OfType<Pig>().Any(e => e.CustomName == "Prosiak" && e.Health == 7), "CMD summon NBT");
        Run("entitydata @e[name=Prosiak] {HealF:5f}"); Run("testfor @e[name=Prosiak] {HealF:5f}");
        Run("entitydata Ala {Health:1f}", expect: 0);
        Run("execute @e[name=Prosiak] ~ ~ ~ setblock ~ ~1 ~ diamond_block"); Check(w.GetBlock(4, 5, 4) == B.DiamondBlock, "CMD execute pozycja");
        Run("execute Ala 0 5 0 detect ~ ~ ~ stone -1 time set day"); Check(w.DayTime == 1000, "CMD execute detect");
        Run("execute Ala 0 5 0 detect ~ ~ ~ dirt -1 time set night", expect: 0);
        Run("scoreboard objectives add punkty dummy Wyniki"); Run("scoreboard objectives setdisplay sidebar punkty");
        Run("scoreboard players set Ala punkty 7"); Run("scoreboard players add @a punkty 2", expect: 2);
        var sb = Scoreboard.Of(w); var objective = sb.GetObjective("punkty");
        Check(sb.GetScore("Ala", objective).Points == 9 && sb.GetScore("Bob", objective).Points == 2, "CMD scoreboard selektory");
        Run("scoreboard players operation Ala punkty *= Bob punkty"); Check(sb.GetScore("Ala", objective).Points == 18, "CMD scoreboard operacje");
        Run("scoreboard players test Ala punkty * 18"); Run("scoreboard players test Ala punkty 19 *", expect: 0);
        Run("scoreboard players add * punkty 1"); Check(sb.GetScore("Bob", objective).Points == 3, "CMD scoreboard wildcard");
        Check(Selector.Match(ps, "@a[score_punkty_min=10]").Single() == p, "CMD selektor scoreboard");
        Run("scoreboard teams add red"); Run("scoreboard teams option red color red"); Run("scoreboard teams join red Ala");
        Check(sb.TeamOf("Ala")?.Color == 'c' && Selector.Match(ps, "@a[team=red]").Single() == p && sb.FormatName("Ala").StartsWith("§c"), "CMD drużyna selektor i kolor");
        Run("scoreboard objectives add przycisk trigger"); Run("trigger przycisk set 8", ps, 0);
        Run("scoreboard players enable Ala przycisk"); Run("trigger przycisk add 8", ps); Run("trigger przycisk add 1", ps, 0);
        Check(sb.GetScore("Ala", sb.GetObjective("przycisk")).Points == 8, "CMD trigger jednokrotny");
        Run("scoreboard objectives add hp health"); w.Tick(); Check(sb.GetScore("Ala", sb.GetObjective("hp")).Points == (int)Math.Ceiling(p.Health), "CMD kryterium health");
        Run("scoreboard players set Ala hp 1", expect: 0);
        Run("scoreboard objectives add czas dummy"); Run("scoreboard players set Ala czas 0"); Run("stats entity Ala set QueryResult Ala czas");
        Run("time query daytime", ps); Check(sb.GetScore("Ala", sb.GetObjective("czas")).Points == (int)w.DayTime % 24000, "CMD stats QueryResult");
        var roundtrip = new Scoreboard(); using (var r = new BinaryReader(new MemoryStream(sb.ToBytes()))) roundtrip.Read(r);
        Check(roundtrip.GetScore("Ala", roundtrip.GetObjective("punkty")).Points == 19 && roundtrip.TeamOf("Ala").Color == 'c' && roundtrip.Display[1].Name == "punkty", "CMD scoreboard zapis/sieć");
        Run("worldborder center 0 0"); Run("worldborder set 40"); Run("worldborder set 20 2");
        var border = WorldBorder.Of(w); Check(border.Diameter(w.TotalTime + 20) == 30 && border.Diameter(w.TotalTime + 40) == 20, "CMD granica interpolacja");
        Run("worldborder warning distance 3"); Run("worldborder warning time 4"); Run("worldborder damage buffer 1"); Run("worldborder damage amount 2");
        Check(border.WarningDistance == 3 && border.WarningTime == 4 && border.DamageBuffer == 1 && border.DamageAmount == 2, "CMD granica opcje");
        Run("worldborder get"); Run("worldborder set 10"); Check(!border.Contains(w, 12, 0) && border.Contains(w, 0, 0), "CMD granica contains");
        p.SetGameMode(0); p.SetPositionAndPrev(4, 4, 0); p.MotionX = 0; p.MoveWithCollision(3, 0, 0); Check(p.X <= 5.71, "CMD granica kolizja ruchu", p.X.ToString());
        p.SetPositionAndPrev(12, 4, 0); p.HurtResistantTime = 0; float hp = p.Health; w.Tick(); Check(p.Health < hp, "CMD granica obrażenia poza buforem");
        Run("worldborder set 60000000"); p.SetPositionAndPrev(.5, 4, .5); p.SetGameMode(1);
        TestCommandBlock(w, p, s, sb);
        var tagged = new ItemStack(I.DiamondPickaxe) { Tag = Mojangson.Parse("{CanDestroy:[minecraft:stone],CanPlaceOn:[minecraft:grass]}") };
        Check(Adventure.Allows(tagged, "CanDestroy", B.Stone) && !Adventure.Allows(tagged, "CanDestroy", B.Dirt) && GuiDraw.ItemTooltip(tagged, false).Contains("Kamień"), "CMD tagi przygody i tooltip");
        p.SetGameMode(2); p.Inventory.Selected = null; w.SetBlock(1, 4, 0, B.Stone);
        var ctrl = new PlayerController(w, p) { Hit = new World.World.HitResult { Hit = true, X = 1, Y = 4, Z = 0, Face = Dir.Up } };
        for (int i = 0; i < 120; i++) ctrl.TickLeft(i == 0);
        Check(w.GetBlock(1, 4, 0) == B.Stone, "CMD przygoda blokada kopania");
        p.Inventory.Selected = tagged;
        for (int i = 0; i < 120; i++) ctrl.TickLeft(i == 0);
        Check(w.GetBlock(1, 4, 0) == 0, "CMD przygoda CanDestroy pozwala kopać");
        p.Inventory.Selected = new ItemStack(B.Stone, 2); w.SetBlock(1, 3, 0, B.Grass); ctrl.Hit.Y = 3;
        ctrl.TickRight(true); Check(w.GetBlock(1, 4, 0) == 0, "CMD przygoda blokada stawiania");
        p.Inventory.Selected.Tag = tagged.Tag.Copy(); ctrl.TickRight(true); Check(w.GetBlock(1, 4, 0) == B.Stone, "CMD przygoda CanPlaceOn pozwala stawiać");
        Run("spreadplayers 0 0 2 8 false Ala Bob"); Check(p.DistanceSq(q) >= 4, "CMD spreadplayers odstęp");
        Run("kill @e[name=Prosiak]"); Check(w.Entities.OfType<Pig>().Any(e => e.CustomName == "Prosiak" && e.Dead), "CMD kill encji");
        Run("scoreboard teams leave Ala"); Run("scoreboard objectives remove przycisk");
        Check(sb.TeamOf("Ala") == null && sb.GetObjective("przycisk") == null, "CMD usuwanie drużyny i celu");
    }

    private static void TestCommandBlock(World.World w, Player p, ConsoleSender s, Scoreboard sb)
    {
        w.SetBlock(5, 4, 0, B.CommandBlock); var tile = (CommandBlockTile)w.GetTileEntity(5, 4, 0);
        tile.Logic.Command = "scoreboard players add #impulsy punkty 1";
        w.SetBlock(6, 4, 0, B.RedstoneBlock); w.Tick();
        Check(tile.Logic.SuccessCount == 1 && sb.GetScore("#impulsy", sb.GetObjective("punkty")).Points == 1, "CMD blok zbocze narastające");
        Ticks(w, 3); Check(sb.GetScore("#impulsy", sb.GetObjective("punkty")).Points == 1, "CMD blok nie powtarza przy stałym zasilaniu");
        w.SetBlock(6, 4, 0, B.Air); w.SetBlock(6, 4, 0, B.RedstoneBlock); w.Tick();
        Check(sb.GetScore("#impulsy", sb.GetObjective("punkty")).Points == 2 && ComparatorRead.Read(w, 5, 4, 0) == 1, "CMD drugi impuls i komparator");
        Check(tile.Logic.LastOutput.Contains("#impulsy"), "CMD LastOutput niezależny od commandBlockOutput");
        Check(CommandBlocks.Update(p, 5, 4, 0, -1, "testfor @a", false) && !tile.Logic.TrackOutput && tile.Logic.LastOutput == "", "CMD operator creative edytuje blok");
        p.SetGameMode(0); Check(!CommandBlocks.Update(p, 5, 4, 0, -1, "kill @a", true), "CMD survival nie edytuje bloku"); p.SetGameMode(1);
        var restored = (CommandBlockTile)ChunkCodec.LoadTile(89, 5, 4, 0, ChunkCodec.SaveTile(tile));
        Check(restored.Logic.Command == "testfor @a" && !restored.Logic.TrackOutput && restored.Logic.SuccessCount == 1, "CMD zapis TE");
        var cart = new CommandMinecart(w); cart.SetPositionAndPrev(0, 4, 5); cart.Logic.Command = "time set night"; w.AddEntityNow(cart);
        w.SetBlock(0, 3, 5, B.Stone); w.SetBlock(0, 4, 5, B.ActivatorRail); w.SetBlock(1, 4, 5, B.RedstoneBlock); cart.Tick();
        Check(cart.Logic.SuccessCount == 1 && w.DayTime == 13000, "CMD wagonik tory aktywacyjne");
        Check(Commands.Run(s, "summon MinecartCommandBlock 2 4 5 {Command:\"time set day\"}") == 1, "CMD summon wagonika z komendą");
    }
}
