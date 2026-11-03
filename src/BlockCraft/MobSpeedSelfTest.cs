using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Prędkość chodu mobów jak w MC 1.8 (setAIMoveSpeed ustawia też moveForward, przyspieszenie = speed^2).</summary>
public static partial class SelfTest
{
    private static double WalkSpeedPerTick(World.World w, Mob m, float speed)
    {
        m.SetPositionAndPrev(0.5, 4, 0.5); m.MotionX = m.MotionZ = 0; m.OnGround = true;
        double z0 = 0;
        for (int i = 0; i < 60; i++)
        {
            if (i == 40) z0 = m.Z;
            m.Yaw = 0; m.SetMoveSpeed(speed);
            m.Travel(0, m.MoveForward * 0.98f);
        }
        return (m.Z - z0) / 20;
    }

    private static void TestMobSpeed()
    {
        var w = MakeWorld(13, WorldType.Superflat, 1);
        double cow = WalkSpeedPerTick(w, new Cow(w), 0.2f);
        Check(cow is > 0.075 and < 0.1, "Moby: krowa chodzi ok. 1,75 bloku/s jak w MC 1.8 (nie 8)", $"{cow * 20:F2} b/s");
        double zombie = WalkSpeedPerTick(w, new Zombie(w), 0.23f);
        Check(zombie is > 0.1 and < 0.13, "Moby: zombie ok. 2,3 bloku/s, wolniej niż chodzący gracz (4,3)", $"{zombie * 20:F2} b/s");
        var vil = new Villager(w, 0);
        double villager = WalkSpeedPerTick(w, vil, (float)(vil.MoveSpeed * .6));
        Check(Math.Abs(vil.MoveSpeed - .5) < 1e-9 && villager is > 0.17 and < 0.21, "Moby: wieśniak wędruje z prędkością 0,5 x 0,6 jak EntityAIWander w 1.8", $"{villager * 20:F2} b/s");

        // Pełna symulacja AI (najszybsze 20 ticków): królik tylko skacze, panika zwierząt z mnożnikami 1.8
        var rab = SurveyMob("rabbit");
        Check(rab.wander is > 0.5 and < 2.5 && rab.player is > 2 and < 5.5 && rab.panic < 5.5, "Moby: królik porusza się skokami (wędrówka ok. 1-2 b/s, ucieczka wolniej niż sprint gracza)", $"{rab.wander:F2}/{rab.player:F2}/{rab.panic:F2} b/s");
        var pig = SurveyMob("pig");
        Check(pig.panic is > 2.5 and < 4.8, "Moby: świnia w panice 1.25 x 0.25 (ok. 4 b/s, nie 6,5)", $"{pig.panic:F2} b/s");
        var oce = SurveyMob("ocelot");
        Check(oce.wander < 3.2 && oce.panic < 8, "Moby: ocelot wędruje z mnożnikiem 0.8 i ucieka z 1.33", $"{oce.wander:F2}/{oce.panic:F2} b/s");
    }
}
