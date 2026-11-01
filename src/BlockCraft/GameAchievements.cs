using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft;

public sealed partial class Game
{
    public void DrawAchievementIcon(Achievement a,float x,float y)
    {
        if(Item.ById[a.Icon]!=null) DrawItem(new ItemStack(a.Icon),x,y,false);
        else if(AchievementList.IconTexture(a.Icon) is {} key) Sb.DrawLayer(Renderer.Terrain,Render.TextureGen.Layer(key),x,y,16,16,0xFFFFFFFF);
    }
    private Player _noticePlayer;
    private Achievement _notice;
    private long _noticeStart;
    private void DrawAchievementNotice()
    {
        if(Player==null) {_noticePlayer=null;_notice=null;return;}
        if(_noticePlayer!=Player){_noticePlayer=Player;_notice=null;}
        long now=Environment.TickCount64;
        if(_notice!=null&&now-_noticeStart>=3000)_notice=null;
        if(_notice==null&&Player.AchievementNotices.TryDequeue(out string id))
        {_notice=AchievementList.Find(id);_noticeStart=now;}
        if(_notice==null||HideGui)return;
        int x=GuiW-164; double age=(now-_noticeStart)/3000.0;
        int y=(int)(-36*Math.Pow(Math.Max(0,1-Math.Min(age,1-age)*8),2));
        Sb.Fill(x,y,160,34,0xFF606060);Sb.Fill(x+1,y+1,158,32,0xFF202020);
        DrawAchievementIcon(_notice,x+7,y+9);
        Sb.Text("Zdobyto osiągnięcie!",x+29,y+6,0xFF55FFFF);
        string title=_notice.Title;
        while(title.Length>0&&Sb.Measure(title)>126) title=title[..^1];
        Sb.Text(title,x+29,y+18,0xFFFFFFFF);
    }
}
