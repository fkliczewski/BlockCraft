using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Gui;

public sealed class BeaconScreen : ContainerScreen
{
    private readonly BeaconContainer _bc;
    private Effect _primary,_secondary;
    public BeaconScreen(Player p,BeaconContainer c):base(p,c){_bc=c;_primary=c.Beacon.Primary;_secondary=c.Beacon.Secondary;}
    private static string Label(Effect e)=>e switch {Effect.Speed=>"Szybkość",Effect.Haste=>"Pośpiech",Effect.Resistance=>"Odporność",Effect.JumpBoost=>"Skok",Effect.Strength=>"Siła",Effect.Regeneration=>"Regeneracja",_=>"Brak"};
    private IEnumerable<(int x,int y,Effect e,bool secondary)> Buttons()
    {
        for(int row=0;row<3;row++)for(int i=0;i<BeaconTile.Powers[row].Length;i++)yield return (10+i*57,26+row*24,BeaconTile.Powers[row][i],false);
        yield return (132,26,Effect.Regeneration,true);
        if(_primary!=Effect.None)yield return (132,50,_primary,true);
    }
    private bool Enabled(Effect e,bool secondary)=>secondary?_bc.Beacon.Levels>=4:BeaconTile.AllowedPrimary(e,_bc.Beacon.Levels);
    private bool CanConfirm=>BeaconTile.Currency(_bc.Beacon.Payment.Get(0))&&BeaconTile.AllowedPrimary(_primary,_bc.Beacon.Levels);
    private static bool Inside(int mx,int my,int x,int y,int width,int height)=>mx>=x&&mx<x+width&&my>=y&&my<y+height;
    protected override void DrawBackground(int mx,int my)
    {
        var sb=G.Sb;GuiDraw.Panel(G,Left,Top,C.Width,C.Height);
        sb.Text("Moc podstawowa",Left+8,Top+8,0xFF404040,false);sb.Text("Dodatkowa",Left+132,Top+8,0xFF404040,false);
        foreach(var b in Buttons())
        {
            bool enabled=Enabled(b.e,b.secondary),selected=(b.secondary?_secondary:_primary)==b.e;
            sb.Fill(Left+b.x,Top+b.y,b.secondary?87:55,20,enabled?selected?0xFF75B882:0xFFB0B0B0:0xFF777777);
            string text=Label(b.e)+(b.secondary&&b.e==_primary?" II":"");
            sb.Text(text,Left+b.x+2,Top+b.y+6,enabled?0xFF202020:0xFF505050,false);
        }
        sb.Text("Piramida: "+_bc.Beacon.Levels+"/4",Left+10,Top+116,0xFF404040,false);
        foreach(var s in C.Slots)GuiDraw.Slot(G,Left+s.X-1,Top+s.Y-1,18);
        sb.Fill(Left+155,Top+106,64,22,CanConfirm?0xFF75B882:0xFF888888);sb.Text("Gotowe",Left+161,Top+113,0xFF202020,false);
        sb.Text("Ekwipunek",Left+35,Top+127,0xFF404040,false);
        int[] coins={I.Emerald,I.Diamond,I.GoldIngot,I.IronIngot};
        for(int i=0;i<4;i++)G.DrawItem(new ItemStack(coins[i]),Left+10+i*20,Top+96,false);
    }
    public override void MouseDown(int mx,int my,int btn)
    {
        if(btn==0)
        {
            foreach(var b in Buttons())if(Inside(mx,my,Left+b.x,Top+b.y,b.secondary?87:55,20))
            {
                if(Enabled(b.e,b.secondary))
                {
                    if(b.secondary)_secondary=b.e;
                    else{bool upgraded=_primary==_secondary;_primary=b.e;if(upgraded)_secondary=b.e;}
                }
                return;
            }
            if(Inside(mx,my,Left+155,Top+106,64,22))
            {
                if(CanConfirm)
                {
                    if(P.World.IsRemote)G.Client?.SetBeacon(_primary,_secondary);else _bc.Confirm(P,_primary,_secondary);
                    G.SetScreen(null);
                }
                return;
            }
        }
        base.MouseDown(mx,my,btn);
    }
    public override void Tick(){if(!_bc.StillValid(P))G.SetScreen(null);}
}
