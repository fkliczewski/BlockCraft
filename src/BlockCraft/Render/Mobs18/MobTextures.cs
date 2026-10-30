namespace BlockCraft.Render;

public static partial class TextureGen
{
    static partial void TexMobs()
    {
        var cage = New();
        for (int y=0;y<16;y++) for(int x=0;x<16;x++)
            if (x%5==0 || y%5==0 || x==15 || y==15) cage[y*16+x]=Hex(((x+y)&1)==0?0x34424Bu:0x202932u);
        Add("mob_spawner",cage);
        var egg=New(); var spots=New();
        for(int y=2;y<15;y++) for(int x=2;x<14;x++)
            if ((x-7.5)*(x-7.5)/30+(y-8.5)*(y-8.5)/44<1) {
                egg[y*16+x]=Gray(.7f+.3f*(15-x-y)/20f);
                if ((x*7+y*11)%17<5) spots[y*16+x]=Gray(.9f);
            }
        Add("item_spawn_egg",egg); Add("item_spawn_egg_spots",spots);
        string[] keys={"carrot_on_a_stick","diamond_horse_armor","name_tag","saddle","iron_horse_armor","golden_horse_armor","prismarine_shard","prismarine_crystals","slime_ball","rabbit","cooked_rabbit","rabbit_stew","rabbit_hide","rabbit_foot","lead"};
        uint[] colors={0xD88020,0x56CED1,0xC6AD7A,0x99542A,0xBCC3C9,0xECCA37,0x63A99C,0xA3D7C7,0x7BBA54,0xD58A80,0x9D6546,0x9C793D,0xAD835E,0xB2977A,0x997747};
        for(int i=0;i<keys.Length;i++) {
            var p=New();
            for(int y=3;y<14;y++) for(int x=3;x<13;x++) {
                bool inside=i is 1 or 4 or 5 ? y>7 || x<6 || x>9 : i==14 ? (x==3||x==12||y==3||y==13) : ((x-7)*(x-7)+(y-8)*(y-8)<31);
                if(inside) p[y*16+x]=Mul(Hex(colors[i]),.75f+((x*3+y*7)%5)*.08f);
            }
            Add("item_"+keys[i],p);
        }
    }
}
