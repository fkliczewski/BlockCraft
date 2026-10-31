using BlockCraft.Items;
using System.Runtime.CompilerServices;
using BlockCraft.Core;

namespace BlockCraft.World;

public sealed class Chunk
{
    public const int Size = 16, Height = 256, Sections = Height / 16, Volume = Size * Size * Height;
    public readonly int Cx, Cz;
    public readonly byte[] Blocks = new byte[Volume];
    public readonly byte[] Meta = new byte[Volume];
    public readonly byte[] Light = new byte[Volume]; // górne 4 bity = niebo, dolne = blok
    public readonly short[] HeightMap = new short[Size * Size]; // najwyższy y blokujący światło + 1
    public readonly byte[] Biomes = new byte[Size * Size];
    public readonly Dictionary<int, TileEntity> TileEntities = new();
    public readonly int[] SectionCounts = new int[Sections]; // liczba niepustych bloków w sekcji

    public bool Lit;          // światło policzone (lokalnie)
    public bool Merged;       // światło połączone z sąsiadami
    public bool Modified;     // do zapisu
    public bool Populated;    // zwierzęta wygenerowane
    public bool Unloaded;
    public int DirtySections; // maska bitowa sekcji do przebudowy mesha
    public long LastSeen;
    public byte[] PendingEntityData; // wczytane z dysku, encje tworzone przy integracji
    public bool FromDisk;

    public Chunk(int cx, int cz) { Cx = cx; Cz = cz; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index(int x, int y, int z) => (y << 8) | (z << 4) | x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetBlock(int x, int y, int z) => Blocks[(y << 8) | (z << 4) | x];
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetMeta(int x, int y, int z) => Meta[(y << 8) | (z << 4) | x];

    public void SetBlockRaw(int x, int y, int z, byte id, int meta)
    {
        int i = Index(x, y, z);
        byte old = Blocks[i];
        if ((old != 0) != (id != 0)) SectionCounts[y >> 4] += id != 0 ? 1 : -1;
        Blocks[i] = id; Meta[i] = (byte)meta;
    }

    public void RecountSections()
    {
        Array.Clear(SectionCounts);
        for (int i = 0; i < Volume; i++) if (Blocks[i] != 0) SectionCounts[i >> 12]++;
    }

    public int GetSky(int x, int y, int z) => Light[Index(x, y, z)] >> 4;
    public int GetBlockLight(int x, int y, int z) => Light[Index(x, y, z)] & 15;

    public void RecalcHeightMap()
    {
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                RecalcHeight(x, z);
    }
    public void RecalcHeight(int x, int z)
    {
        int y = Height - 1;
        while (y >= 0 && Block.ById[Blocks[Index(x, y, z)]].LightOpacity == 0) y--;
        HeightMap[(z << 4) | x] = (short)(y + 1);
    }
    public int GetHeight(int x, int z) => HeightMap[(z << 4) | x];

    public int TopSolidY(int x, int z)
    {
        for (int y = Height - 1; y >= 0; y--)
        {
            var b = Block.ById[Blocks[Index(x, y, z)]];
            if (b.Solid || b.IsLiquid) return y;
        }
        return -1;
    }

    public void MarkAllDirty() => DirtySections = (1 << Sections) - 1;
}

public abstract class TileEntity
{
    public int X, Y, Z;
    public virtual void Tick(World w) { }
    /// <summary>Ogólne ustawienie danych bez zależności kompilacyjnej między modułami, np. spawner: SetData("EntityId", "zombie"),
    /// skrzynia ze skarbem: SetData("LootTable", "dungeon"). Typ ignoruje nieznane klucze.</summary>
    public virtual void SetData(string key, string value) { }
    public abstract void Save(BinaryWriter bw);
    public abstract void Load(BinaryReader br);
    public abstract int TypeId { get; }

    /// <summary>Rejestr typów tile entity (TypeId -> fabryka). Moduły rejestrują własne w Registries.cs.</summary>
    public static readonly Dictionary<int, Func<TileEntity>> Types = new()
    {
        [1] = () => new FurnaceTileEntity(),
        [2] = () => new ChestTileEntity(),
    };

    public static TileEntity Create(int type) => Types.TryGetValue(type, out var f) ? f() : null;
}
