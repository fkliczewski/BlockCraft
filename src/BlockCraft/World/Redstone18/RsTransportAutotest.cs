using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public partial class Game
{
    private MinecartEntity _rsRideCart;
    private ChestMinecartEntity _rsCargoCart;
    private HopperMinecartEntity _rsHopperCart;
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
        Step(3, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ + 14;
            for (int dx = -2; dx <= 16; dx++) for (int dz = 13; dz <= 17; dz++)
            {
                World.SetBlock(x + dx, y - 1, z + dz, B.Stone);
                for (int dy = 0; dy < 5; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
            }
            foreach (MinecartKind kind in Enum.GetValues<MinecartKind>())
            {
                int xx = x + 1 + (int)kind * 3;
                World.SetBlock(xx, y, z + 14, kind == MinecartKind.Chest ? B.DetectorRail : B.Rail);
                World.SetMeta(xx, y, z + 14, 1);
                var c = MinecartEntity.Create(World, kind); c.SetPositionAndPrev(xx + 0.5, y + 0.0625, z + 14.5);
                if (c is FurnaceMinecartEntity f) f.Fuel = 3600;
                if (c is ChestMinecartEntity cc) { _rsCargoCart = cc; cc.Set(0, new ItemStack(I.Diamond, 64)); }
                if (c is HopperMinecartEntity hc) { _rsHopperCart = hc; World.DropItem(xx + 0.5, y + 0.3, z + 14.5, new ItemStack(B.Cobblestone, 12), false); }
                World.SpawnEntity(c);
            }
            _rsRideCart = new MinecartEntity(World); _rsRideCart.SetPositionAndPrev(x + 2.5, y + 0.0625, z + 0.5); _rsRideCart.MotionX = 0.3; World.SpawnEntity(_rsRideCart);
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 7, y + 8, z + 8); Player.Yaw = 0; Player.Pitch = 48;
        });
        Step(15, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ + 14;
            Log.Info($"AUTOTEST RS wagonik_ruch={(_rsRideCart.X > x + 3 ? "OK" : "FAIL")} detektor={((World.GetMeta(x + 4, y, z + 14) & 8) != 0 ? "OK" : "FAIL")} wagonik_lej={(_rsHopperCart.Get(0)?.Count == 12 ? "OK" : "FAIL")}");
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 7, y + 8, z + 8); Player.Yaw = 0; Player.Pitch = 48;
        });
        Step(3, () => Shot("rs_11_wagoniki"));
        Step(3, () => _rsCargoCart.Interact(Player));
        Step(3, () => Shot("rs_12_gui_wagonika_skrzyni"));
        Step(3, () => { Player.CloseContainer(); SetScreen(null); _rsHopperCart.Interact(Player); });
        Step(3, () => Shot("rs_13_gui_wagonika_leja"));
        Step(3, () =>
        {
            Player.CloseContainer(); SetScreen(null); Player.Flying = false; _rsRideCart.Interact(Player);
            _rsRideCart.MotionZ = 0.2; Player.Yaw = 0; Player.Pitch = 20;
        });
        Step(8, () => { Log.Info($"AUTOTEST RS pasazer={(Player.RidingEntity == _rsRideCart ? "OK" : "FAIL")}"); });
        Step(3, () => Shot("rs_14_jazda"));
        Step(3, () => { Player.Sneaking = true; Player.Tick(); Player.Sneaking = false; Log.Info($"AUTOTEST RS wysiadanie={(Player.RidingEntity == null ? "OK" : "FAIL")}"); });
        Step(3, () => SetScreen(new Gui.CreativeScreen(Player, Blocks.TabTransport)));
        Step(3, () => Shot("rs_15_transport"));
        Step(3, () => SetScreen(null));
    }
}
