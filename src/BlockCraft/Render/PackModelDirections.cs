using System.Numerics;
using BlockCraft.Core;

namespace BlockCraft.Render;

internal static class PackModelDirections
{
    internal static readonly string[] Names = { "down", "up", "north", "south", "west", "east" };
    /// <summary>Obrót wariantu wokół środka bloku: najpierw x, potem y (stopnie, wielokrotności 90), jak ModelRotation 1.8.</summary>
    public static Vector3 RotateBlock(Vector3 p, int rotX, int rotY)
    {
        var d = p - new Vector3(.5f);
        for (int i = 0; i < ((rotX / 90) % 4 + 4) % 4; i++) d = new Vector3(d.X, d.Z, -d.Y);
        for (int i = 0; i < ((rotY / 90) % 4 + 4) % 4; i++) d = new Vector3(-d.Z, d.Y, d.X);
        return d + new Vector3(.5f);
    }

    internal static Vector3 DirVec(int f) => new(Dir.X[f], Dir.Y[f], Dir.Z[f]);

    /// <summary>Kierunek (Dir) najbliższy wektorowi.</summary>
    public static int NearestFace(Vector3 n)
    {
        int best = 1; float bd = float.MinValue;
        for (int f = 0; f < 6; f++)
        {
            float d = Vector3.Dot(n, DirVec(f));
            if (d > bd) { bd = d; best = f; }
        }
        return best;
    }
}
