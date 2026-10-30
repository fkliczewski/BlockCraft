using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestIntegratedDimensions()
    {
        var home = MakeWorld(31, WorldType.Superflat, 0);
        home.SpawnX = home.SpawnZ = 8; home.SpawnY = 5;
        using var dimensions = new DimensionSet(home);
        var p = new Player(home); home.AddEntityNow(p);
        dimensions.Transfer(p, DimensionId.Nether, 8, 110, 8);
        p.Dead = true; p.Respawn();
        Check(p.World == home && !p.Dead && home.Players.Contains(p), "Odrodzenie z Netheru przenosi do Overworldu");

        var horse = new Horse(home); horse.SetPosition(8, 5, 8);
        horse.Tame(p); p.Inventory.Selected = new ItemStack(I.Saddle);
        Check(horse.Interact(p) && horse.Saddled && p.Inventory.Selected == null, "Oswojony koń przyjmuje siodło");
        Check(horse.Interact(p) && horse.Rider == p && p.RidingMob == horse, "Wsiadanie na konia");
        horse.Dismount();
        Check(horse.Rider == null && p.RidingMob == null, "Zsiadanie z konia");
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) horse.Save(writer);
        bytes.Position = 0;
        var copy = new Horse(home);
        using (var reader = new BinaryReader(bytes, System.Text.Encoding.UTF8, true)) copy.Load(reader);
        Check(copy.Tamed && copy.Saddled && copy.Owner == p.Name && copy.JumpStrength == horse.JumpStrength
            && copy.MaxHealth == horse.MaxHealth && copy.MoveSpeed == horse.MoveSpeed, "Zapis konia zachowuje oswojenie i atrybuty");
    }
}
