using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestOwEntityPopulation()
    {
        var w = MakeWorld(52, WorldType.Superflat, 1);
        var c = w.GetChunk(-1, 0);
        var loot = new ItemStack(I.Diamond, 3);
        c.GeneratedEntityPlans = new()
        {
            new("villager", -.5, 4, 8.5) { Profession = 3 },
            new("guardian", -2.5, 4, 8.5) { Elder = true },
            new("witch", -3.5, 4, 8.5),
            new("chest_minecart", -4.5, 4, 8.5, new[] { loot })
        };
        w.ApplyGeneratedEntities(c);
        Check(w.Entities.OfType<Villager>().Single().Profession == 3 && w.Entities.OfType<Villager>().Single().PersistenceRequired,
            "Populacja struktur: profesja i trwałość wieśniaka");
        Check(w.Entities.OfType<Guardian>().Single() is { Elder: true, MaxHealth: 80 } && w.Entities.OfType<Witch>().Count() == 1,
            "Populacja struktur: starszy strażnik i wiedźma");
        var cart = w.Entities.OfType<ChestMinecartEntity>().Single(); cart.Get(0).Count = 1;
        Check(loot.Count == 3 && cart.Get(0).Id == I.Diamond, "Łup wagonika nie współdzieli stosu z cache generatora");
        w.ApplyGeneratedEntities(c);
        Check(w.Entities.Count == 4 && c.GeneratedEntityPlans == null, "Ponowna integracja nie powiela encji struktury");
        var copy = MakeWorld(52, WorldType.Superflat, 1);
        EntitySerializer.LoadEntities(copy, EntitySerializer.SaveEntities(w.Entities)); copy.Tick();
        Check(copy.Entities.OfType<Villager>().Single().Profession == 3 && copy.Entities.OfType<Guardian>().Single().Elder
            && copy.Entities.OfType<ChestMinecartEntity>().Single().Get(0)?.Count == 1,
            "Zapis odtwarza mieszkańców struktury i ładunek wagonika");
    }
}
