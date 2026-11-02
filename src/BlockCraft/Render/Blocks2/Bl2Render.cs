using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render
{
    /// <summary>Renderery BL2 w przebiegu encji: skrzynia Endu z ruchomym wiekiem (TileEntityEnderChestRenderer) i rośliny w doniczkach.</summary>
    public static class Bl2Render
    {
        public static void DrawTiles(EntityRenderer r, World.World w, float pt, Vector3d cam)
        {
            int cx = MathUtil.FloorToInt(cam.X) >> 4, cz = MathUtil.FloorToInt(cam.Z) >> 4;
            for (int x = cx - 4; x <= cx + 4; x++)
                for (int z = cz - 4; z <= cz + 4; z++)
                {
                    var chunk = w.GetChunk(x, z); if (chunk == null) continue;
                    foreach (var te in chunk.TileEntities.Values)
                    {
                        if (te is not EnderChestTile and not FlowerPotTile) continue;
                        var rel = new Vector3((float)(te.X - cam.X), (float)(te.Y - cam.Y), (float)(te.Z - cam.Z));
                        if (rel.LengthSquared > 64 * 64) continue;
                        r.Terrain.Sky = w.GetSkyLight(te.X, te.Y, te.Z); r.Terrain.Blk = w.GetBlockLight(te.X, te.Y, te.Z);
                        r.Terrain.Color = Vector4.One; r.Terrain.Overlay = Vector4.Zero;
                        if (te is EnderChestTile ec && w.GetBlock(te.X, te.Y, te.Z) == B.EnderChest)
                        {
                            r.Terrain.Blk = Math.Max(r.Terrain.Blk, 7);
                            DrawEnderChest(r, ec, EnderChestBlock.Facing(w.GetMeta(te.X, te.Y, te.Z)), ec.LidAngle(pt), rel);
                        }
                        else if (te is FlowerPotTile pot && !pot.Empty && w.GetBlock(te.X, te.Y, te.Z) == B.FlowerPot)
                            DrawPlant(r, w, pot, rel);
                    }
                }
        }

        /// <summary>Kąt obrotu modelu skierowanego na południe do danej ściany (jak w rendererze skrzyni).</summary>
        public static float FacingAngle(int facing) => facing switch { Dir.North => 180f, Dir.East => 90f, Dir.West => -90f, _ => 0f };

        public static void DrawEnderChest(EntityRenderer r, EnderChestTile t, int facing, float lid, Vector3 rel)
        {
            var block = (EnderChestBlock)Block.Get(B.EnderChest);
            var place = Matrix4.CreateTranslation(-.5f, 0, -.5f) * Matrix4.CreateRotationY(FacingAngle(facing) * MathUtil.Deg2Rad)
                * Matrix4.CreateTranslation(rel + new Vector3(.5f, 0, .5f));
            // zawias wieka: tylna krawędź podstawy (z = 1/16) na wysokości 9/16
            var hinge = new Vector3(0, 9 / 16f, 1 / 16f);
            var open = Matrix4.CreateTranslation(-hinge) * Matrix4.CreateRotationX(-lid * MathF.PI / 2f) * Matrix4.CreateTranslation(hinge) * place;
            foreach (var (box, isLid) in block.Parts(Dir.South)) r.EmitModelBox(box, isLid ? open : place);
        }

        /// <summary>models/block/flower_pot_cross.json (dwie płaszczyzny 45 stopni, y 4-16, rescale) i flower_pot_cactus.json.</summary>
        public static void DrawPlant(EntityRenderer r, World.World w, FlowerPotTile pot, Vector3 rel)
        {
            var b = Block.ById[pot.ItemId];
            if (b == null || b.Id == 0) return;
            var at = Matrix4.CreateTranslation(rel);
            if (b.Id == B.Cactus)
            {
                var t = new int[6]; for (int f = 0; f < 6; f++) t[f] = b.GetTexture(f, 0);
                r.EmitModelBox(ModelBox.Px(6, 4, 6, 10, 16, 10, t), at);
                return;
            }
            if (b.TintAt(pot.Data) == TintType.Grass)
            {
                uint c = Biome.GrassColor(w.GetBiome(pot.X, pot.Z));
                r.Terrain.Color = new Vector4((c >> 16 & 255) / 255f, (c >> 8 & 255) / 255f, (c & 255) / 255f, 1);
            }
            int tex = b.GetTexture(Dir.North, pot.Data);
            float half = 5.4f / 16f * MathF.Sqrt(2f);
            var plane = new ModelBox(.5f - half, 4 / 16f, .5f, .5f + half, 1f, .5f, ModelBox.All(tex)) { Uv = FullUv };
            foreach (float a in new[] { 45f, -45f })
            {
                var m = Matrix4.CreateTranslation(-.5f, 0, -.5f) * Matrix4.CreateRotationY(a * MathUtil.Deg2Rad) * Matrix4.CreateTranslation(.5f, 0, .5f) * at;
                r.EmitModelBox(plane, m);
            }
            r.Terrain.Color = Vector4.One;
        }
        private static readonly float[] FullUv = Enumerable.Range(0, 6).SelectMany(_ => new[] { 0f, 0f, 1f, 1f }).ToArray();
    }

    public static partial class TextureGen
    {
        static partial void TexBl2()
        {
            EnderChestTextures();
            Add("flower_pot", Client("blocks/flower_pot.png") ?? FlowerPotTile());
            var barrier = Client("items/barrier.png") ?? BarrierIcon();
            Add("item_barrier", barrier); Add("barrier", barrier);
            Add("item_flower_pot", Client("items/flower_pot.png") ?? FlowerPotIcon());
            Add("item_writable_book", Client("items/book_writable.png") ?? BookIcon(true));
            Add("item_written_book", Client("items/book_written.png") ?? BookIcon(false));
        }

        /// <summary>Tekstura 16x16 z client.jar 1.8 (gdy pobrana do cache), inaczej null.</summary>
        private static uint[] Client(string path)
        {
            var src = ClientAssets.TryReadTexture(path, out int w, out int h);
            if (src == null || w <= 0 || h <= 0) return null;
            var px = New();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) px[y * 16 + x] = src[y * h / 16 * w + x * w / 16];
            return px;
        }

        /// <summary>Kafle skrzyni Endu ułożone tak, by mapowanie UV po pozycji odtworzyło entity/chest/ender.png (wieko y 9-14, podstawa y 0-10).</summary>
        private static void EnderChestTextures()
        {
            var src = ClientAssets.TryReadTexture("entity/chest/ender.png", out int sw, out int sh);
            bool ok = src != null && sw >= 64 && sh >= 64;
            int k = ok ? sw / 64 : 1;
            uint S(int u, int v) => src[(v * k) * sw + u * k];
            var rng = new Rng(211);
            uint body(int x, int y) => Mul(Hex(0x14211F), .85f + rng.NextFloat() * .3f);
            uint edge = Hex(0x2D4C47), seam = Hex(0x081010);
            uint[] front = New(), side = New(), top = New(), lck = New();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool border = x == 1 || x == 14;
                    // bok i przód: wiersze 2-6 wieko, 7-15 podstawa
                    uint lidPx = y == 2 || border ? edge : y == 6 ? seam : body(x, y);
                    uint basePx = y == 15 || border ? edge : y == 7 ? seam : body(x, y);
                    if (ok)
                    {
                        int cx = Math.Clamp(x - 1, 0, 13);
                        lidPx = y <= 6 ? S(cx, 14 + Math.Clamp(y - 2, 0, 4)) : 0;
                        basePx = S(cx, 33 + Math.Clamp(y - 6, 0, 9));
                        uint lidF = y <= 6 ? S(14 + cx, 14 + Math.Clamp(y - 2, 0, 4)) : 0, baseF = S(14 + cx, 33 + Math.Clamp(y - 6, 0, 9));
                        front[y * 16 + x] = y <= 6 ? lidF : baseF;
                        side[y * 16 + x] = y <= 6 ? lidPx : basePx;
                        top[y * 16 + x] = S(14 + Math.Clamp(x - 1, 0, 13), Math.Clamp(y - 1, 0, 13));
                        continue;
                    }
                    side[y * 16 + x] = y <= 6 ? lidPx : basePx;
                    front[y * 16 + x] = y <= 6 ? lidPx : basePx;
                    top[y * 16 + x] = x == 1 || x == 14 || y == 1 || y == 14 ? edge : body(x, y);
                }
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    lck[y * 16 + x] = ok ? S(1 + Math.Clamp(x * 2 / 16, 0, 1), 1 + Math.Clamp(y * 4 / 16, 0, 3))
                        : (x >= 5 && x <= 10 && y >= 4 && y <= 11) ? Hex(x >= 7 && x <= 8 && y >= 6 && y <= 9 ? 0x1B6E5Au : 0x3EC9A4u) : Hex(0x2A2A2A);
            Add("ender_chest_front", front); Add("ender_chest_side", side); Add("ender_chest_top", top); Add("ender_chest_lock", lck);
        }

        private static uint[] FlowerPotTile()
        {
            var px = New(); var r = new Rng(218);
            for (int i = 0; i < 256; i++) px[i] = Mul(Hex(0x7C4536), .85f + r.NextFloat() * .25f);
            for (int x = 0; x < 16; x++) { px[x] = Hex(0x93533E); px[15 * 16 + x] = Hex(0x5E3328); }
            return px;
        }

        private static uint[] FlowerPotIcon()
        {
            var px = New();
            for (int y = 8; y < 15; y++) for (int x = 5; x < 11; x++) px[y * 16 + x] = Hex(y == 8 ? 0x93533Eu : x == 5 || x == 10 ? 0x5E3328u : 0x7C4536u);
            for (int x = 6; x < 10; x++) px[8 * 16 + x] = Hex(0x3B2A1E);
            return px;
        }

        /// <summary>items/barrier.png: czerwony okrąg z ukośną kreską.</summary>
        private static uint[] BarrierIcon()
        {
            var px = New();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    double dx = x - 7.5, dy = y - 7.5, d = Math.Sqrt(dx * dx + dy * dy);
                    bool ring = d <= 7.2 && d >= 5.2, slash = Math.Abs(dx + dy) <= 1.2 && d < 6;
                    if (ring || slash) px[y * 16 + x] = Hex(ring && d > 6.6 ? 0x8E0000u : 0xE21E1Eu);
                }
            return px;
        }

        private static uint[] BookIcon(bool writable)
        {
            var px = New();
            for (int y = 3; y < 15; y++)
                for (int x = 2; x < 12; x++)
                    px[y * 16 + x] = Hex(x == 2 ? 0x4A2A10u : x <= 3 ? 0x6B3A14u : y == 3 || y == 14 ? 0x5A3312u : x >= 10 ? 0xE6DFC8u : 0x8A4E1Cu);
            if (writable)
            {
                for (int i = 0; i < 9; i++) { int x = 6 + i, y = 13 - i; if (x < 16 && y >= 0) { px[y * 16 + x] = Hex(0xF0F0F0); if (x + 1 < 16) px[y * 16 + x + 1] = Hex(0xBFBFBF); } }
                px[14 * 16 + 5] = Hex(0x202020);
            }
            else for (int y = 6; y <= 11; y += 2) for (int x = 5; x < 9; x++) px[y * 16 + x] = Hex(0xD9C9A0);
            return px;
        }
    }
}

namespace BlockCraft
{
    public sealed partial class Game
    {
        private readonly Random _bl2Rand = new();

        /// <summary>
        /// WorldClient.doVoidFogParticles 1.8: co tick 1000 losowych bloków w promieniu 16 wokół gracza dostaje randomDisplayTick
        /// (cząsteczki portalu skrzyni Endu), a w kreatywnym z barierą w ręce widoczne bariery pokazują cząsteczkę BARRIER.
        /// </summary>
        private void Bl2ClientTick()
        {
            var w = World; var p = Player;
            if (w == null || p == null) return;
            EnderChestTile.TickRemote(w);
            bool barrier = p.GameMode == 1 && p.Inventory.Selected?.Id == B.Barrier;
            int px = MathUtil.FloorToInt(p.X), py = MathUtil.FloorToInt(p.Y), pz = MathUtil.FloorToInt(p.Z);
            var r = _bl2Rand;
            for (int i = 0; i < 1000; i++)
            {
                int x = px + r.Next(16) - r.Next(16), y = py + r.Next(16) - r.Next(16), z = pz + r.Next(16) - r.Next(16);
                if ((uint)y >= Chunk.Height) continue;
                int id = w.GetBlock(x, y, z);
                if (id == B.EnderChest) EnderChestBlock.DisplayTick(w, x, y, z, w.Rand, PortalParticle);
                else if (barrier && id == B.Barrier) BarrierParticle(x + .5f, y + .5f, z + .5f);
            }
        }

        /// <summary>EntityPortalFX: start w pozycja + ruch (+1 w górę), dociąga do pozycji w 40-49 tickach.</summary>
        internal void PortalParticle(double x, double y, double z, double mx, double my, double mz)
        {
            int life = _bl2Rand.Next(10) + 40;
            var start = new Vector3((float)(x + mx), (float)(y + my + 1), (float)(z + mz));
            var end = new Vector3((float)x, (float)y, (float)z);
            float f = (float)_bl2Rand.NextDouble() * .6f + .4f;
            Renderer.AddParticle(start, (end - start) / life, Render.TextureGen.Layer("particle_portal"), .1f * ((float)_bl2Rand.NextDouble() * .2f + .5f) * 2f, life, 0,
                new Vector4(f * .9f, f * .3f, f, 1), false, true);
        }

        /// <summary>EntityBarrierFX: ikona bariery, nieruchoma, 80 ticków.</summary>
        internal void BarrierParticle(float x, float y, float z)
            => Renderer.AddParticle(new Vector3(x, y, z), Vector3.Zero, Render.TextureGen.Layer("item_barrier"), .5f, 80, 0, Vector4.One, false, false);
    }
}
