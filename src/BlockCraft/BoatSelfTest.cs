using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Łódka (EntityBoat 1.8): wypór, sterowanie, rozbicie.</summary>
public static partial class SelfTest
{
    private static void TestBoat()
    {
        var w = MakeWorld(13, WorldType.Superflat, 1);
        // basen 12x12, głębokość 3, z murkiem na wschodzie
        for (int x = 0; x < 14; x++) for (int z = 0; z < 14; z++) for (int y = 1; y <= 3; y++) w.SetBlock(x, y, z, y == 3 ? B.Air : B.Water);
        for (int z = 0; z < 14; z++) for (int y = 1; y <= 5; y++) w.SetBlock(14, y, z, B.Stone);
        var boat = new BoatEntity(w); boat.SetPositionAndPrev(4.5, 4, 7.5); w.AddEntityNow(boat);
        for (int i = 0; i < 120; i++) w.Tick();
        Check(!boat.Dead && boat.Y > 2 && boat.Y < 3.2 && Math.Abs(boat.MotionY) < .05, "Łódka: unosi się na wodzie", $"y={boat.Y:F2} vy={boat.MotionY:F3}");
        var p = new Player(w); p.SetPositionAndPrev(4.5, 4, 7.5); w.AddEntityNow(p);
        boat.Interact(p);
        Check(p.RidingEntity == boat, "Łódka: gracz wsiada");
        p.Yaw = -90; p.MoveForward = 1; // patrzy na wschód (+X)
        double x0 = boat.X;
        for (int i = 0; i < 25; i++) w.Tick();
        Check(boat.X - x0 > 1, "Łódka: płynie w kierunku patrzenia", $"{boat.X - x0:F2}");
        for (int i = 0; i < 200 && !boat.Dead; i++) w.Tick();
        Check(boat.Dead && w.Entities.OfType<ItemEntity>().Count(e => e.Stack.Id == B.OakPlanks) == 3 && w.Entities.OfType<ItemEntity>().Count(e => e.Stack.Id == I.Stick) == 2 && p.RidingEntity == null,
            "Łódka: rozbija się o ścianę przy pełnej prędkości (3 deski, 2 patyki)");
        foreach (var e in w.Entities.OfType<ItemEntity>().ToList()) e.Kill();
        var b2 = new BoatEntity(w); b2.SetPositionAndPrev(6.5, 4, 3.5); w.AddEntityNow(b2); w.Tick();
        b2.Attack(DamageSource.PlayerAttack(p), 2); b2.Attack(DamageSource.PlayerAttack(p), 2); b2.Attack(DamageSource.PlayerAttack(p), 2);
        w.Tick();
        Check(b2.Dead && w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.Boat), "Łódka: trzy uderzenia niszczą ją i zostaje przedmiot");
        var b3 = new BoatEntity(w); b3.SetPositionAndPrev(20.5, 12, 20.5); w.AddEntityNow(b3);
        for (int i = 0; i < 80 && !b3.Dead; i++) w.Tick();
        Check(b3.Dead, "Łódka: upadek na ląd z ponad 3 bloków ją rozbija");
    }
}
