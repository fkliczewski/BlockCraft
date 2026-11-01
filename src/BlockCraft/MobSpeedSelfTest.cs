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
    }
}
