using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy optymalizacji: profil GPU, kubełki kierunków ścian, VisGraph, tick dzielony na kawałki.</summary>
public static partial class SelfTest
{
    private static void TestPerf()
    {
        // profil GPU po GL_RENDERER
        Check(GpuProfile.Detect("NVIDIA GeForce RTX 5070/PCIe/SSE2") == GpuProfile.Kind.Dedicated, "Profil GPU: RTX = osobna karta");
        Check(GpuProfile.Detect("Intel(R) Iris(R) Xe Graphics") == GpuProfile.Kind.Integrated, "Profil GPU: Iris Xe = zintegrowana");
        Check(GpuProfile.Detect("Intel(R) UHD Graphics 620") == GpuProfile.Kind.Integrated, "Profil GPU: UHD 620 = zintegrowana");
        Check(GpuProfile.Detect("Intel(R) Arc(TM) A770 Graphics") == GpuProfile.Kind.Dedicated, "Profil GPU: Arc A770 = osobna karta");
        Check(GpuProfile.Detect("AMD Radeon(TM) Graphics") == GpuProfile.Kind.Integrated, "Profil GPU: Radeon Graphics (APU) = zintegrowana");
        Check(GpuProfile.Detect("AMD Radeon RX 7800 XT") == GpuProfile.Kind.Dedicated, "Profil GPU: RX 7800 XT = osobna karta");
        Check(GpuProfile.Detect("llvmpipe (LLVM 15.0.7, 256 bits)") == GpuProfile.Kind.Software, "Profil GPU: llvmpipe = programowa");

        // kubełki kierunków: ściana +X na x=1 i ściana -Y na y=0 (kolejność wierzchołków CCW z zewnątrz)
        var vb = new VertexBuffer();
        void Quad(float[] a, float[] b, float[] c, float[] d) { foreach (var p in new[] { a, b, c, d }) vb.V(p[0], p[1], p[2], 0, 0, 0, 0, 1, 15, 0, 0xFFFFFFFF); }
        Quad(new float[] { 1, 0, 1 }, new float[] { 1, 0, 0 }, new float[] { 1, 1, 0 }, new float[] { 1, 1, 1 }); // +X
        Quad(new float[] { 0, 0, 0 }, new float[] { 1, 0, 0 }, new float[] { 1, 0, 1 }, new float[] { 0, 0, 1 }); // -Y
        Quad(new float[] { 0, 0, 0 }, new float[] { 1, 1, 0 }, new float[] { 0.5f, 1, 1 }, new float[] { 0, 0.3f, 1 }); // nieosiowa
        var sorted = FacingSort.Sort(vb.Data, vb.Count);
        Check(sorted.Start[1] - sorted.Start[0] == 4, "Kubełki ścian: nieosiowa w kubełku 0");
        Check(sorted.Start[3] - sorted.Start[2] == 4 && sorted.Plane[1] == 0, "Kubełki ścian: -Y w swoim kubełku z płaszczyzną y=0");
        Check(sorted.Start[4] - sorted.Start[3] == 4 && sorted.Plane[2] == 1, "Kubełki ścian: +X w swoim kubełku z płaszczyzną x=1");
        int m = FacingSort.VisibleMask(sorted.Plane, 0.5f, 5f, 0.5f);
        Check((m & (1 << 3)) == 0 && (m & (1 << 2)) == 0 && (m & 1) != 0, "Kubełki ścian: z góry i z lewej +X oraz -Y są tyłem");
        Check((FacingSort.VisibleMask(sorted.Plane, 2f, -1f, 0.5f) & (1 << 3 | 1 << 2)) == (1 << 3 | 1 << 2), "Kubełki ścian: z prawej i spod spodu widać +X i -Y");

        // VisGraph: superpłaski świat, sekcja 0 = bedrock i ziemia pod powietrzem
        var w = MakeWorld(1234, WorldType.Superflat, 2);
        var mesher = new ChunkMesher();
        mesher.Build(w, 0, 0, 0);
        Check(!ChunkMesher.VisConnects(mesher.Vis, 0, 1) && ChunkMesher.VisConnects(mesher.Vis, 1, 2), "VisGraph: przez dno sekcji z ziemią nie widać, z góry widać boki");
        mesher.Build(w, 0, 5, 0);
        Check(mesher.Vis == ChunkMesher.VisAll, "VisGraph: pusta sekcja widoczna ze wszystkich stron");

        // tick dzielony na najmniejsze kawałki daje ten sam świat co tick w całości
        ulong Run(bool sliced)
        {
            var tw = MakeWorld(1234, WorldType.Superflat, 4);
            for (int x = 0; x < 4; x++) for (int y = 0; y < 3; y++) for (int z = 0; z < 4; z++)
                tw.SpawnEntity(new TntEntity(tw, x + 0.5, 4 + y, z + 0.5, 20 + x + z));
            for (int t = 0; t < 60; t++)
            {
                if (!sliced) { tw.Tick(); continue; }
                tw.TickBegin();
                while (!tw.TickSlice(0)) { }
            }
            return Bench.Checksum(tw, -20, -20, 44);
        }
        Check(Run(false) == Run(true), "Tick dzielony na kawałki: ten sam świat co tick w całości");
    }
}
