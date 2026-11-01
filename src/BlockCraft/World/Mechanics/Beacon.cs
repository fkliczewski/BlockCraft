using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Net;
using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>TileEntityBeacon (bck) z klienta Java 1.8. Obliczenia i efekty wyłącznie na hoście.</summary>
public sealed class BeaconTile : TileEntity
{
    public override int TypeId => 65;
    public int Levels; public long VisualTick = -80;
    public Effect Primary, Secondary;
    public bool Active;
    public readonly SimpleInventory Payment = new(1);
    public readonly List<BeaconBeamSection> Beam = new();
    private static readonly (float r,float g,float b)[] GlassColors = {
        (1,1,1),(.85f,.5f,.2f),(.7f,.3f,.85f),(.4f,.6f,.85f),(.9f,.9f,.2f),(.5f,.8f,.1f),(.95f,.5f,.65f),(.3f,.3f,.3f),
        (.6f,.6f,.6f),(.3f,.5f,.6f),(.5f,.25f,.7f),(.2f,.3f,.7f),(.4f,.3f,.2f),(.4f,.5f,.2f),(.6f,.2f,.2f),(.1f,.1f,.1f)};
    public static bool Mineral(int id) => id is B.IronBlock or B.GoldBlock or B.EmeraldBlock or B.DiamondBlock;
    public static bool Currency(ItemStack s) => s?.Id is I.IronIngot or I.GoldIngot or I.Emerald or I.Diamond;
    public static readonly Effect[][] Powers = { new[]{Effect.Speed,Effect.Haste},new[]{Effect.Resistance,Effect.JumpBoost},new[]{Effect.Strength},new[]{Effect.Regeneration} };
    public static bool AllowedPrimary(Effect e,int levels) => Enumerable.Range(0,Math.Min(3,Math.Max(0,levels))).Any(i=>Powers[i].Contains(e));
    public override void Tick(World w) { if(w.TotalTime%80==0) Update(w); }
    public void Update(World w)
    {
        int previous=Levels; Rebuild(w);
        if(w.IsRemote)return;
        if(Levels==4&&previous<4)
        {
            var box=new AABB(X-10,Y-9,Z-10,X+10,Y+5,Z+10);
            foreach(var p in w.Players)if(box.Intersects(p.BoundingBox))p.AwardAchievement("fullBeacon");
        }
        if(!Active||Primary==Effect.None)return;
        double range=Levels*10+10;
        var area=new AABB(X-range,Y-range,Z-range,X+1+range,Y+1+range+Chunk.Height,Z+1+range);
        foreach(var p in w.Players)
        {
            if(!area.Intersects(p.BoundingBox))continue;
            p.AddEffect(new EffectInstance(Primary,180,Levels>=4&&Primary==Secondary?1:0,true));
            if(Levels>=4&&Secondary!=Effect.None&&Secondary!=Primary)p.AddEffect(new EffectInstance(Secondary,180,0,true));
        }
    }
    public void Rebuild(World w)
    {
        Levels=0;Active=true;Beam.Clear();
        var color=(r:1f,g:1f,b:1f);int start=Y;bool firstGlass=true;
        for(int y=Y+1;y<Chunk.Height;y++)
        {
            int id=w.GetBlock(X,y,Z);
            if(id is B.StainedGlass or B.StainedGlassPane)
            {
                var next=GlassColors[w.GetMeta(X,y,Z)&15];
                if(!firstGlass)next=((color.r+next.r)/2,(color.g+next.g)/2,(color.b+next.b)/2);
                if(next!=color){Beam.Add(new(start,y,color.r,color.g,color.b));start=y;color=next;}
                firstGlass=false;
            }
            else if(Block.Get(id).LightOpacity>=15){Active=false;Beam.Clear();return;}
        }
        Beam.Add(new(start,Chunk.Height,color.r,color.g,color.b));
        for(int layer=1;layer<=4&&Y-layer>=0;layer++)
        {
            bool valid=true;
            for(int dx=-layer;dx<=layer&&valid;dx++)for(int dz=-layer;dz<=layer;dz++)
                if(!Mineral(w.GetBlock(X+dx,Y-layer,Z+dz))){valid=false;break;}
            if(!valid)break;Levels=layer;
        }
        Active=Levels>0;
    }
    public override void Save(BinaryWriter bw){bw.Write(Levels);bw.Write((int)Primary);bw.Write((int)Secondary);}
    public override void Load(BinaryReader br)
    {
        Levels=Math.Clamp(br.ReadInt32(),0,4);Primary=Filter(br.ReadInt32());Secondary=Filter(br.ReadInt32());
    }
    private static Effect Filter(int id)=>Powers.Any(row=>row.Contains((Effect)id))?(Effect)id:Effect.None;
}
public readonly record struct BeaconBeamSection(int Bottom,int Top,float R,float G,float B);

public sealed class BeaconBlock : Block
{
    public BeaconBlock():base(B.Beacon,"beacon","Magiczna latarnia")
    {T("beacon");H(3);Tab(Blocks.TabMisc);Opaque=FullCube=false;Layer=RenderLayer.Cutout;LightOpacity=0;LightEmit=15;HasTileEntity=true;Render=RenderType.Model;}
    public override void GetModel(IBlockAccess w,int x,int y,int z,int meta,List<ModelBox> boxes)
    {
        boxes.Add(ModelBox.Px(0,0,0,16,16,16,TextureGenLayer("glass")));
        boxes.Add(ModelBox.Px(1,0,1,15,3,15,TextureGenLayer("obsidian")));
        boxes.Add(ModelBox.Px(3,3,3,13,13,13,Tex));
    }
    private static int[] TextureGenLayer(string key)=>Enumerable.Repeat(BlockCraft.Render.TextureGen.Layer(key),6).ToArray();
    public override void OnAdded(World w,int x,int y,int z)=>w.SetTileEntity(x,y,z,new BeaconTile());
    public override void OnRemoved(World w,int x,int y,int z,int meta)=>w.RemoveTileEntity(x,y,z);
    public override bool OnActivated(World w,int x,int y,int z,Player p){p.OpenBlockGui(GuiKind.Beacon,x,y,z);return true;}
}
