using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Efekty graficzne (docs/GRAFIKA_PLAN.md): presety, rozpoznawanie Własnej, profil słabego GPU.</summary>
public static partial class SelfTest
{
    private static void TestGraphicsPresets()
    {
        Console.WriteLine("[Efekty graficzne]");
        var s = new Settings();
        Check(s.GraphicsPreset == GraphicsPreset.Cacy && GraphicsPresets.PostFxOn(s), "Grafika: domyślnie Cacy z efektami");
        GraphicsPresets.Apply(s, GraphicsPreset.Classic);
        Check(!s.Tonemapping && !s.Fxaa && !s.VividColors && !s.WavingPlants && !GraphicsPresets.PostFxOn(s), "Grafika: Klasyczna wyłącza wszystkie efekty (dotychczasowy render)");
        GraphicsPresets.Apply(s, GraphicsPreset.Nice);
        Check(GraphicsPresets.Match(s) == GraphicsPreset.Nice, "Grafika: preset rozpoznany po swoich efektach");
        s.Fxaa = false;
        Check(GraphicsPresets.Match(s) == GraphicsPreset.Custom, "Grafika: zmiana jednego efektu = Własna");
        s.Fxaa = true; s.GraphicsPreset = GraphicsPreset.Custom;
        Check(GraphicsPresets.Match(s) != GraphicsPreset.Custom, "Grafika: powrót do efektów presetu = znów preset");
        GraphicsPresets.Apply(s, GraphicsPreset.Ultra);
        Check(GraphicsPresets.Match(s) == GraphicsPreset.Ultra, "Grafika: wybrany preset zostaje przy tych samych efektach");
        GraphicsPresets.Apply(s, GraphicsPreset.Nice); s.WavingPlants = false;
        Check(GraphicsPresets.Match(s) == GraphicsPreset.Custom, "Grafika: wyłączone falowanie = Własna");
        TestWaveFlags();
        TestGpuSwitch();
        var lap = new Settings();
        GpuProfile.ApplyLaptop(lap, GpuProfile.Kind.Software);
        Check(lap.GraphicsPreset == GraphicsPreset.Classic && !GraphicsPresets.PostFxOn(lap), "Grafika: karta programowa = Klasyczna");
        GpuProfile.ApplyLaptop(lap, GpuProfile.Kind.Integrated);
        Check(lap.GraphicsPreset == GraphicsPreset.Nice, "Grafika: zintegrowana karta = Ładna");
    }
}

public static partial class SelfTest
{
    /// <summary>Flagi falowania w wierzchołkach: rodzaje bloków i nieruchomy spód roślin.</summary>
    private static void TestWaveFlags()
    {
        Items.ItemRegistry.Init(); // --gfx-only: bez TestRegistry bloki nie są jeszcze zarejestrowane
        Check(ChunkMesher.WaveKindOf(B.OakLeaves) == ChunkMesher.WaveLeaves && ChunkMesher.WaveKindOf(B.Vine) == ChunkMesher.WaveVine, "Falowanie: liście i pnącza");
        Check(ChunkMesher.WaveKindOf(B.TallGrass) == ChunkMesher.WavePlant && ChunkMesher.WaveKindOf(B.Wheat) == ChunkMesher.WavePlant
            && ChunkMesher.WaveKindOf(B.DoublePlant) == ChunkMesher.WavePlant, "Falowanie: trawa, zboże, podwójne rośliny");
        Check(ChunkMesher.WaveKindOf(B.Water) == ChunkMesher.WaveWater, "Falowanie: woda");
        Check(ChunkMesher.WaveKindOf(B.Stone) == 0 && ChunkMesher.WaveKindOf(B.Cobweb) == 0 && ChunkMesher.WaveKindOf(B.SugarCane) == 0
            && ChunkMesher.WaveKindOf(B.Fire) == 0 && ChunkMesher.WaveKindOf(B.Lava) == 0, "Falowanie: kamień, pajęczyna, trzcina, ogień i lawa stoją");
        var vb = new VertexBuffer { Wave = ChunkMesher.WavePlant, WaveBaseY = 5 };
        vb.V(0, 5, 0, 0, 0, 0, 1, 1, 15, 0, 0xFFFFFFFF);
        vb.V(0, 6, 0, 0, 0, 0, 1, 1, 15, 0, 0xFFFFFFFF);
        Check(vb.Data[18] == 1 && vb.Data[VertexBuffer.Stride + 18] == (1 | ChunkMesher.WavePlant << 2), "Falowanie: spód rośliny stoi, wierzch faluje, flaga maski zostaje",
            $"{vb.Data[18]},{vb.Data[VertexBuffer.Stride + 18]}");
    }
}

public static partial class SelfTest
{
    /// <summary>Wybór karty graficznej: preferencja Windows, dopasowanie nazwy z GL_RENDERER, mocniejsza karta.</summary>
    private static void TestGpuSwitch()
    {
        Check(Core.GpuSwitch.ParsePreference("GpuPreference=2;") == 2 && Core.GpuSwitch.ParsePreference("SwapEffectUpgradeEnable=1;GpuPreference=1;") == 1
            && Core.GpuSwitch.ParsePreference(null) == 0, "GPU: odczyt preferencji Windows");
        Check(Core.GpuSwitch.SameCard("NVIDIA GeForce RTX 5070 Ti Laptop GPU", "NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2")
            && Core.GpuSwitch.SameCard("AMD Radeon(TM) 610M", "AMD Radeon(TM) 610M") && !Core.GpuSwitch.SameCard("AMD Radeon(TM) 610M", "NVIDIA GeForce RTX 5070 Ti Laptop GPU"),
            "GPU: ta sama karta po nazwie z GL_RENDERER");
        var list = new List<Core.GpuSwitch.Adapter>
        {
            new("NVIDIA GeForce RTX 5070 Ti Laptop GPU", 0x10DE, 12UL << 30),
            new("AMD Radeon(TM) 610M", 0x1002, 512UL << 20),
        };
        Check(Core.GpuSwitch.BetterThan("AMD Radeon(TM) 610M", list)?.VendorId == 0x10DE, "GPU: na zintegrowanej proponuje RTX");
        Check(Core.GpuSwitch.BetterThan("NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2", list) == null, "GPU: na RTX nic nie proponuje");
        Check(Core.GpuSwitch.IntegratedOther("NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2", list)?.VendorId == 0x1002, "GPU: z RTX można wrócić na zintegrowaną");
        Check(Core.GpuSwitch.BetterThan("AMD Radeon(TM) 610M", list.Take(0).ToList()) == null && !new Core.GpuSwitch.Adapter("Radeon 780M", 0x1002, 512UL << 20).Discrete,
            "GPU: bez drugiej karty nie pyta; Radeon z małą pamięcią to zintegrowana");
        Check(Gui.GpuChoiceScreen.Short("NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2") == "RTX 5070 Ti" && Gui.GpuChoiceScreen.Short("AMD Radeon(TM) 610M") == "Radeon 610M",
            "GPU: krótka nazwa na przycisk");
        var s = new Settings { RenderScale = 75, RenderDistance = 16, FancyGraphics = false };
        GraphicsPresets.Apply(s, GraphicsPreset.Nice);
        Render.GpuProfile.ApplyDesktop(s);
        Check(s.RenderScale == 100 && s.RenderDistance == 16 && s.FancyGraphics && s.GraphicsPreset == GraphicsPreset.Cacy, "GPU: mocna karta przywraca pełną jakość, zasięg gracza zostaje");
        var screens = new List<string> { "AMD Radeon(TM) 610M" };
        Check(Core.GpuSwitch.CrossAdapter("NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2", screens) && !Core.GpuSwitch.CrossAdapter("AMD Radeon(TM) 610M", screens)
            && !Core.GpuSwitch.CrossAdapter("NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2", new List<string> { "NVIDIA GeForce RTX 5070 Ti Laptop GPU" }),
            "GPU: VSync przez DWM tylko gdy ekran jest na innej karcie niż renderująca");
        foreach (var a in Core.GpuSwitch.Adapters()) Console.WriteLine($"  (karta w tym komputerze: {a.Name}, {a.DedicatedMemory >> 20} MB{(a.Discrete ? ", osobna" : "")})");
        Console.WriteLine("  (ekrany na: " + string.Join(", ", Core.GpuSwitch.DisplayAdapters()) + ")");
    }
}
