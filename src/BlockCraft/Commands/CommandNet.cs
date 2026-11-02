using BlockCraft.Cmd;

namespace BlockCraft.Net;

public sealed partial class RemoteClient
{
    public void SendTitle(int action, string text, int fi, int stay, int fo)
    {
        var w = Out.Begin(S2C.CommandUi); w.Write((byte)0); w.Write(action); w.Write(text); w.Write(fi); w.Write(stay); w.Write(fo); Out.End();
    }
    public void SendSound(string name, double x, double y, double z, float volume, float pitch)
    {
        var w = Out.Begin(S2C.Sound); w.Write(name); w.Write(x); w.Write(y); w.Write(z); w.Write(volume); w.Write(pitch); Out.End();
    }
    public void OpenCommands(int x, int y, int z, int id, CommandBlockLogic logic)
    {
        var w = Out.Begin(S2C.CommandUi); w.Write((byte)1); w.Write(x); w.Write(y); w.Write(z); w.Write(id);
        w.Write(logic.Command); w.Write(logic.TrackOutput); w.Write(logic.LastOutput); Out.End();
    }
}
