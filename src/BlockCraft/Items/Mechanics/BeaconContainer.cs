using BlockCraft.Entities;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft.Items;

public sealed class BeaconContainer : Container
{
    public readonly BeaconTile Beacon;
    public readonly World.World World;
    public BeaconContainer(Player p,World.World w,BeaconTile tile)
    {
        World=w;Beacon=tile;Title="Magiczna latarnia";Width=230;Height=219;
        Slots.Add(new FilterSlot(tile.Payment,0,107,110,BeaconTile.Currency,1));
        AddPlayerSlots(p.Inventory,137,195);
        foreach(var slot in Slots.Skip(1))slot.X+=27;
    }
    public bool StillValid(Player p)=>World==p.World&&World.GetTileEntity(Beacon.X,Beacon.Y,Beacon.Z)==Beacon
        &&World.GetBlock(Beacon.X,Beacon.Y,Beacon.Z)==B.Beacon&&p.DistanceSq(Beacon.X+.5,Beacon.Y+.5,Beacon.Z+.5)<=64;
    public bool Confirm(Player p,Effect primary,Effect secondary)
    {
        if(World.IsRemote||p.Dead||!StillValid(p))return false;
        Beacon.Rebuild(World);
        if(!BeaconTile.Currency(Beacon.Payment.Get(0))||!BeaconTile.AllowedPrimary(primary,Beacon.Levels)
            ||secondary!=Effect.None&&(Beacon.Levels<4||secondary!=Effect.Regeneration&&secondary!=primary))return false;
        Beacon.Primary=primary;Beacon.Secondary=secondary;
        Beacon.Payment.Set(0,null);Beacon.Payment.Changed();return true;
    }
    protected override bool TransferFromPlayer(ItemStack s)=>BeaconTile.Currency(s)&&MergeStack(s,0,1,false);
    public override void OnClose(Player p)
    {
        base.OnClose(p);
        if(!World.IsRemote&&Beacon.Payment.Get(0) is {} s){Beacon.Payment.Set(0,null);p.DropStack(s,false);}
    }
}
