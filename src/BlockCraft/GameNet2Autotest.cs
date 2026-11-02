using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>NET2: rzeczywisty przekaźnik, pęknięcia, wyposażenie, wytwarzanie i wejście gościa do łódki.</summary>
public sealed partial class Game
{
    private Func<NetInput, NetInput> _net2TestInput;

    private void SetupNet2HostTest()
    {
        Player Guest() => Server.Clients.Single(c => c.P != null).P;
        BoatEntity boat = null;
        int teleports = 0;
        Step(1, () =>
        {
            var p = Guest();
            p.CloseContainer(); p.SetGameMode(0);
            p.Inventory.Main[7] = null;
            p.Inventory.Armor[0] = new ItemStack(I.IronHelmet);
            Player.Inventory.Main[0] = new ItemStack(I.DiamondSword);
            Player.Inventory.SelectedSlot = 0;
            Player.Inventory.Armor[0] = new ItemStack(I.DiamondHelmet);
            Player.SetPositionAndPrev(11.5, 4, 9.5); Player.Yaw = 130; Player.Pitch = 20;
            World.SetBlock(8, 4, 6, B.Stone);
            NetTestSignal("net2-break");
        });
        WaitUntil(() => World.DestroyProgress.TryGetValue(Guest().Id, out var a) && a.Stage >= 1);
        Step(1, () => { Shot("net2_host_cracks"); NetTestSignal("net2-break-host-seen"); });
        WaitUntil(() => File.Exists(NetTestFile("net2-break-guest-seen")) && !World.DestroyProgress.ContainsKey(Guest().Id));
        Step(1, () =>
        {
            var p = Guest();
            World.SetBlock(10, 4, 8, B.CraftingTable);
            p.OpenBlockGui(GuiKind.Crafting, 10, 4, 8);
            p.Cursor = new ItemStack(B.OakLog);
        });
        WaitUntil(() => File.Exists(NetTestFile("net2-crafted")) && Guest().Inventory.CountOf(B.OakPlanks) == 4);
        Step(1, () =>
        {
            var p = Guest(); p.CloseContainer();
            for (int x = 2; x <= 26; x++) for (int z = 20; z <= 23; z++) World.SetBlock(x, 3, z, B.Water);
            boat = new BoatEntity(World); boat.SetPositionAndPrev(5.5, 3.6, 21.5); boat.Yaw = boat.PrevYaw = -90;
            World.AddEntityNow(boat);
            p.SetPositionAndPrev(5.5, 4, 18.5);
            Player.SetPositionAndPrev(10.5, 4, 17.5); Player.Yaw = 0; Player.Pitch = 20;
            NetTestSignal("net2-boat", boat.Id.ToString());
        });
        WaitUntil(() => Guest().RidingEntity == boat);
        Step(1, () => teleports = Server.Clients.Single(c => c.P != null).TeleportId);
        WaitUntil(() => boat.X > 7 && File.Exists(NetTestFile("net2-boat-riding")));
        Step(1, () =>
        {
            if (Server.Clients.Single(c => c.P != null).TeleportId != teleports)
                throw new InvalidOperationException("Teleport podczas jazdy łódką");
            Shot("net2_host_boat"); NetTestSignal("net2-boat-host-seen");
        });
        WaitUntil(() => Guest().RidingEntity == null && File.Exists(NetTestFile("net2-boat-done")));
        Step(1, () => { boat.Kill(); Log.Info("NET2 E2E: pęknięcia, wyposażenie, wytwarzanie, łódka i wysiadanie OK"); });
    }

    private void SetupNet2JoinTest()
    {
        BoatEntity boat = null;
        WaitUntil(() => File.Exists(NetTestFile("net2-break")) && Player.GameMode == 0 && Player.OpenContainer == null);
        Step(1, () =>
        {
            SetScreen(null);
            Player.SetPositionAndPrev(8.5, 4, 8.5); Player.Yaw = 180; Player.Pitch = 20;
            Player.Inventory.SelectedSlot = 7;
            _net2TestInput = i =>
            {
                i.LeftDown = true; i.HitKind = 1;
                i.HitX = 8; i.HitY = 4; i.HitZ = 6; i.HitFace = Dir.South;
                i.HitFx = i.HitFy = .5f; i.HitFz = 1;
                return i;
            };
        });
        WaitUntil(() => File.Exists(NetTestFile("net2-break-host-seen"))
            && World.Players.Any(p => p != Player && p.Inventory.Selected?.Id == I.DiamondSword && p.Inventory.Armor[0]?.Id == I.DiamondHelmet));
        Step(1, () =>
        {
            Shot("net2_guest_equipment"); _net2TestInput = null;
            NetTestSignal("net2-break-guest-seen");
        });
        WaitUntil(() => Player.OpenContainer is WorkbenchContainer && Player.Cursor?.Id == B.OakLog);
        Step(1, () => Player.OpenContainer.Click(1, 0, ClickType.Pickup, Player));
        WaitUntil(() => Player.OpenContainer?.Slots[0].Stack is { Id: B.OakPlanks, Count: 4 });
        Step(1, () => Player.OpenContainer.Click(0, 0, ClickType.QuickMove, Player));
        WaitUntil(() => Player.Inventory.CountOf(B.OakPlanks) == 4);
        Step(1, () => { Shot("net2_guest_crafting"); NetTestSignal("net2-crafted"); });
        WaitUntil(() => File.Exists(NetTestFile("net2-boat")) && Player.OpenContainer == null
            && Math.Abs(Player.X - 5.5) < .5 && Math.Abs(Player.Z - 18.5) < .5
            && World.EntityById.TryGetValue(int.Parse(File.ReadAllText(NetTestFile("net2-boat"))), out var e) && (boat = e as BoatEntity) != null);
        Step(1, () =>
        {
            SetScreen(null); Player.Yaw = -90; Player.Pitch = 15;
            bool click = true;
            _net2TestInput = i =>
            {
                i.HitKind = 2; i.HitEntity = boat.Id; i.RightPressed = click; click = false;
                i.Forward = 1; i.Yaw = -90;
                return i;
            };
        });
        WaitUntil(() => Player.RidingEntity == boat && boat.X > 7);
        Step(1, () => { Shot("net2_guest_boat"); NetTestSignal("net2-boat-riding"); });
        WaitUntil(() => File.Exists(NetTestFile("net2-boat-host-seen")));
        Step(1, () => _net2TestInput = i => { i.Sneaking = true; return i; });
        WaitUntil(() => Player.RidingEntity == null);
        Step(1, () => { _net2TestInput = null; NetTestSignal("net2-boat-done"); Log.Info("NET2 E2E: gość sterował łódką i wysiadł OK"); });
    }
}
