using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Gui;

public sealed class AchievementsScreen : Screen
{
    private readonly Screen _parent;
    private float _panX, _panY, _zoom = 1;
    private bool _drag;
    private int _mx, _my;
    public AchievementsScreen(Screen parent) { _parent = parent; }
    protected override void Init()
    {
        Add(new Button(W/2-50,H-26,100,20,"Gotowe",()=>G.SetScreen(_parent)));
    }
    protected override void Escape() => G.SetScreen(_parent);
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb; var p = G.Player;
        sb.TextCentered($"Osiągnięcia ({p?.Achievements.Count ?? 0}/34)", W/2, 10, 0xFFFFFFFF);
        sb.Fill(8,28,W-16,H-62,0xFF303030);
        (int x,int y) Pos(Achievement a) => ((int)(W/2 + a.X*24*_zoom+_panX), (int)(80+a.Y*24*_zoom+_panY));
        int size=(int)(24*_zoom), center=size/2;
        bool Inside(int x,int y) => x>=12 && x+size<W-12 && y>=32 && y+size<H-38;
        foreach (var a in AchievementList.All)
        {
            var parent = AchievementList.Find(a.Parent); if(parent==null) continue;
            var (x,y)=Pos(a); var (px,py)=Pos(parent);
            if(!Inside(x,y)||!Inside(px,py)) continue;
            uint line=p?.Achievements.Contains(a.Id)==true?0xFF60A060:0xFF707070;
            sb.Fill(Math.Min(x,px)+center,y+center,Math.Abs(px-x)+1,1,line);
            sb.Fill(px+center,Math.Min(y,py)+center,1,Math.Abs(py-y)+1,line);
        }
        Achievement hover=null;
        foreach(var a in AchievementList.All)
        {
            var (x,y)=Pos(a); if(!Inside(x,y)) continue;
            bool got=p?.Achievements.Contains(a.Id)==true, ready=p?.CanAwardAchievement(a)==true;
            sb.PushTransform(x,y,0,_zoom);
            sb.Fill(-1,-1,26,26,a.Special?0xFFA050C0:0xFF909090);
            sb.Fill(0,0,24,24,got?0xFF309050:ready?0xFF606060:0xFF282828);
            G.DrawAchievementIcon(a,4,4);
            if(!got&&!ready) sb.Fill(3,3,18,18,0xA0000000);
            sb.PopTransform();
            if(mx>=x&&mx<x+size&&my>=y&&my<y+size) hover=a;
        }
        base.Render(mx,my,pt);
        if(hover!=null)
        {
            int x=12,y=H-82;
            sb.Fill(x,y,W-24,44,0xF0201020);
            sb.Text(hover.Title,x+5,y+4,p?.Achievements.Contains(hover.Id)==true?0xFF55FF55:0xFFFFFFFF);
            string desc=p?.CanAwardAchievement(hover)==true?hover.Description:"Wymagane: "+AchievementList.Find(hover.Parent)?.Title;
            string line=""; int dy=0;
            foreach(string word in desc.Split(' '))
            {
                if(sb.Measure(line+word)>W-40) {sb.Text(line,x+5,y+16+dy,0xFFE0E0E0);line="";dy+=10;}
                line+=word+" ";
            }
            sb.Text(line,x+5,y+16+dy,0xFFE0E0E0);
        }
    }
    public override void MouseDown(int mx,int my,int btn)
    {
        base.MouseDown(mx,my,btn);
        if(btn==0&&my>=28&&my<H-34){_drag=true;_mx=mx;_my=my;}
    }
    public override void MouseUp(int mx,int my,int btn){base.MouseUp(mx,my,btn);_drag=false;}
    public override void MouseMove(int mx,int my)
    {
        if(_drag){_panX=Math.Clamp(_panX+mx-_mx,-800,800);_panY=Math.Clamp(_panY+my-_my,-800,300);_mx=mx;_my=my;}
        base.MouseMove(mx,my);
    }
    public override void Scroll(int mx,int my,float delta) => _zoom=Math.Clamp(_zoom+delta*.1f,.5f,1.5f);
}
