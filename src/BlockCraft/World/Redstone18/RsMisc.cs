using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.World
{
    /// <summary>Blok szlamu jak BlockSlime 1.8: odbija spadające encje, brak obrażeń od upadku (chyba że skradanie), spowalnia chodzenie.</summary>
    public sealed class SlimeBlockBlock : Block
    {
        public SlimeBlockBlock(byte id, string k, string n) : base(id, k, n)
        {
            T("slime"); Transparent(RenderLayer.Translucent, 0); Hardness = 0; BlastResistance = 0; Slipperiness = 0.8f; Sound = SoundType.Wool;
        }
        private static bool Sneaks(Entity e) => e is LivingEntity { Sneaking: true };
        public override void OnEntityLanded(World w, Entity e, double motionY)
        {
            if (!Sneaks(e) && motionY < 0) e.MotionY = -motionY;
        }
        public override float FallDamageMultiplier(World w, Entity e) => Sneaks(e) ? 1f : 0f;
        public override void OnEntityWalk(World w, int x, int y, int z, Entity e)
        {
            if (Math.Abs(e.MotionY) < 0.1 && !Sneaks(e))
            {
                double d = 0.4 + Math.Abs(e.MotionY) * 0.2;
                e.MotionX *= d; e.MotionZ *= d;
            }
        }
    }
}

namespace BlockCraft.Render
{
    public static partial class TextureGen
    {
        static partial void TexRedstone()
        {
            // blok szlamu: półprzezroczysta zieleń z jaśniejszym środkiem
            var s = New();
            var n = TileNoise(185, 4);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 15 || y == 15;
                    bool core = x >= 3 && x <= 12 && y >= 3 && y <= 12;
                    bool coreEdge = core && (x == 3 || x == 12 || y == 3 || y == 12);
                    float v = n[y * 16 + x];
                    uint c = edge ? Rgba(80, 160, 70, 230) : coreEdge ? Rgba(100, 185, 85, 220) : core ? Rgba(125, 205, 105, 200) : Rgba(110, 190, 95, 150);
                    s[y * 16 + x] = Mul(c, 0.9f + v * 0.2f);
                }
            Add("slime", s);
            TexRedstoneComponents();
        }
    }
}
