using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

public partial class Game
{
    private void RsTransportAutotest()
    {
        Step(5, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ + 14;
            for (int dx = -2; dx <= 16; dx++) for (int dz = -2; dz <= 12; dz++)
            {
                World.SetBlock(x + dx, y - 1, z + dz, B.Stone);
                for (int dy = 0; dy < 5; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
            }
            // Pętla ze wszystkimi łukami.
            for (int dx = 1; dx <= 7; dx++) { World.SetBlock(x + dx, y, z, B.Rail); World.SetBlock(x + dx, y, z + 6, B.Rail); }
            for (int dz = 1; dz < 6; dz++) { World.SetBlock(x + 1, y, z + dz, B.Rail); World.SetBlock(x + 7, y, z + dz, B.Rail); }
            for (int dx = 0; dx <= 12; dx++) World.SetBlock(x + dx, y, z + 9, B.PoweredRail);
            World.SetBlock(x - 1, y, z + 9, B.RedstoneBlock);
            World.SetBlock(x + 9, y, z, B.Rail);
            for (int dx = 10; dx <= 12; dx++)
            {
                int rise = dx - 9;
                for (int h = 0; h < rise; h++) World.SetBlock(x + dx, y + h, z, B.Stone);
                World.SetBlock(x + dx, y + rise, z, B.Rail);
            }
            World.SetBlock(x + 10, y, z + 4, B.DetectorRail);
            World.SetBlock(x + 12, y, z + 4, B.ActivatorRail);
            World.SetBlock(x + 12, y - 1, z + 4, B.RedstoneBlock);
            World.DayTime = 6000;
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 6.5, y + 12, z - 7); Player.Yaw = 0; Player.Pitch = 53;
        });
        Step(3, () => Shot("rs_10_tory"));
    }
}
