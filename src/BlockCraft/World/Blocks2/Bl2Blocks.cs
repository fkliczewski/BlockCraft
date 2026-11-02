using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World
{
    public static partial class Blocks
    {
        static partial void BlocksBl2()
        {
            new EnderChestBlock(); new FlowerPotBlock(); new BarrierBlock();
        }
    }

    /// <summary>Stałe pionka BL2: TypeId tile entity (zakres ME 60-89) i rodzaj GUI skrzyni Endu.</summary>
    public static class Bl2
    {
        public const int EnderChestTileId = 80, FlowerPotTileId = 81;
        public const GuiKind EnderChestGui = (GuiKind)70;
    }

    /// <summary>TileEntityEnderChest 1.8: liczba patrzących i kąt wieka (animacja jak w update()).</summary>
    public sealed class EnderChestTile : TileEntity
    {
        public override int TypeId => Bl2.EnderChestTileId;
        /// <summary>numPlayersUsing (host liczy, klient dostaje przez synchronizację TE).</summary>
        public int Viewers;
        public float Lid, PrevLid;
        private readonly List<(Player p, Container c)> _users = new();

        public void Open(Player p, Container c) { _users.RemoveAll(u => u.p == p); _users.Add((p, c)); Viewers = _users.Count; }
        public void Close(Player p) { _users.RemoveAll(u => u.p == p); Viewers = _users.Count; }

        /// <summary>InventoryEnderChest.isUseableByPlayer: TE na miejscu i gracz w promieniu 8 bloków.</summary>
        public bool CanBeUsed(Player p, World w) => w.GetTileEntity(X, Y, Z) == this && p.World == w && !p.Dead
            && (p.X - X - .5) * (p.X - X - .5) + (p.Y - Y - .5) * (p.Y - Y - .5) + (p.Z - Z - .5) * (p.Z - Z - .5) <= 64;

        public override void Tick(World w)
        {
            if (!w.IsRemote)
            {
                foreach (var u in _users.ToArray())
                {
                    if (u.p.OpenContainer != u.c) { Close(u.p); continue; }
                    if (!CanBeUsed(u.p, w)) { Close(u.p); u.p.CloseContainer(); }
                }
                Viewers = _users.Count;
            }
            PrevLid = Lid;
            if (Viewers > 0 && Lid == 0 && !w.IsRemote)
                w.PlaySound("random.chestopen", X + .5, Y + .5, Z + .5, .5f, w.Rand.NextFloat() * .1f + .9f);
            if (Viewers == 0 && Lid > 0 || Viewers > 0 && Lid < 1)
            {
                float before = Lid;
                Lid += Viewers > 0 ? .1f : -.1f;
                if (Lid > 1) Lid = 1;
                if (Lid < .5f && before >= .5f && !w.IsRemote)
                    w.PlaySound("random.chestclosed", X + .5, Y + .5, Z + .5, .5f, w.Rand.NextFloat() * .1f + .9f);
                if (Lid < 0) Lid = 0;
            }
        }

        /// <summary>Świat klienta nie tickuje TE (World.Tick), więc animację wieka u klienta sieciowego napędza ten tick.</summary>
        public static void TickRemote(World w)
        {
            if (w == null || !w.IsRemote) return;
            foreach (var c in w.Chunks.Values) foreach (var te in c.TileEntities.Values) if (te is EnderChestTile t) t.Tick(w);
        }

        /// <summary>Zamyka GUI wszystkim patrzącym (usunięcie bloku).</summary>
        public void CloseAll() { foreach (var u in _users.ToArray()) if (u.p.OpenContainer == u.c) u.p.CloseContainer(); _users.Clear(); Viewers = 0; }

        public override void Save(BinaryWriter bw) => bw.Write((byte)Math.Clamp(Viewers, 0, 255));
        public override void Load(BinaryReader br) { int v = br.ReadByte(); if (_users.Count == 0) Viewers = v; }

        /// <summary>Kąt wieka po interpolacji jak TileEntityEnderChestRenderer (1 - (1-f)^3) * 90 stopni.</summary>
        public float LidAngle(float pt) { float f = 1 - (PrevLid + (Lid - PrevLid) * pt); return 1 - f * f * f; }

        /// <summary>Kontener GUI skrzyni Endu: wspólny ekwipunek gracza (27 slotów), układ skrzyni.</summary>
        public static Container CreateContainer(World w, Player p, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is not EnderChestTile t) return null;
            var c = new RsContainer(p.Inventory, p.EnderInventory, "Skrzynia Endu", RsContainer.Layout.Chest27, null, w.IsRemote ? null : () => t.Close(p));
            if (!w.IsRemote) t.Open(p, c);
            return c;
        }
    }

    /// <summary>BlockEnderChest 1.8.</summary>
    public sealed class EnderChestBlock : Block
    {
        private readonly int _front, _side, _top, _lock;
        public EnderChestBlock() : base(B.EnderChest, "ender_chest", "Skrzynia Endu")
        {
            _front = TextureGen.Layer("ender_chest_front"); _side = TextureGen.Layer("ender_chest_side"); _top = TextureGen.Layer("ender_chest_top"); _lock = TextureGen.Layer("ender_chest_lock");
            for (int i = 0; i < 6; i++) Tex[i] = _side;
            H(22.5f, 600); Tool_(ToolType.Pickaxe, 0); Snd(SoundType.Stone); Tab(Blocks.TabDecoration);
            Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; LightEmit = 7; HasTileEntity = true;
        }
        public override int GetTexture(int face, int meta) => face <= 1 ? _top : face == Facing(meta) ? _front : _side;
        /// <summary>Meta = EnumFacing (2-5) jak w 1.8; inne wartości traktujemy jako południe.</summary>
        public static int Facing(int meta) => meta is >= 2 and <= 5 ? meta : Dir.South;
        public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
            => list.Add(new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 14 / 16.0, 15 / 16.0));
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
            => list.Add(new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 14 / 16.0, 15 / 16.0));
        /// <summary>W świecie skrzynię rysuje renderer TE (ruchome wieko); model statyczny tylko dla ikon i przedmiotu w ręce.</summary>
        public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        {
            if (w.GetBlock(x, y, z) == Id) return;
            foreach (var (box, _) in Parts(Facing(meta))) boxes.Add(box);
        }
        /// <summary>ModelChest: podstawa 14x10x14, wieko 14x5x14 (zawias z tyłu, na wysokości 9), zamek 2x4x1. Część wieka = true.</summary>
        public IEnumerable<(ModelBox box, bool lid)> Parts(int facing)
        {
            var t = new int[6]; for (int f = 0; f < 6; f++) t[f] = GetTexture(f, facing);
            yield return (ModelBox.Px(1, 0, 1, 15, 10, 15, t), false);
            yield return (ModelBox.Px(1, 9, 1, 15, 14, 15, t), true);
            var l = ModelBox.All(_lock);
            yield return (facing switch
            {
                Dir.North => ModelBox.Px(7, 7, 0, 9, 11, 1, l),
                Dir.West => ModelBox.Px(0, 7, 7, 1, 11, 9, l),
                Dir.East => ModelBox.Px(15, 7, 7, 16, 11, 9, l),
                _ => ModelBox.Px(7, 7, 15, 9, 11, 16, l),
            }, true);
        }
        public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
            => Dir.HorizToFace((Dir.HorizFromYaw(p.Yaw) + 2) & 3);
        public override int DropId(int meta, Rng r) => B.Obsidian;
        public override int DropCount(Rng r) => 8;
        public override ItemStack SilkTouchStack(int meta) => new(B.EnderChest);
        public override ItemStack PickStack(int meta) => new(B.EnderChest);
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.GetTileEntity(x, y, z) is not EnderChestTile) return true;
            if (Block.Get(w.GetBlock(x, y + 1, z)).IsOpaqueCube(w.GetMeta(x, y + 1, z))) return true;
            if (w.IsRemote) return true;
            p.OpenBlockGui(Bl2.EnderChestGui, x, y, z);
            if (p.OpenContainer != null) Bl2Stats.Add(p, "stat.enderchestOpened");
            return true;
        }
        public override void OnAdded(World w, int x, int y, int z) { if (w.GetTileEntity(x, y, z) is not EnderChestTile) w.SetTileEntity(x, y, z, new EnderChestTile()); }
        public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
        {
            if (w.GetTileEntity(x, y, z) is EnderChestTile t) { t.CloseAll(); w.RemoveTileEntity(x, y, z); }
        }

        /// <summary>BlockEnderChest.randomDisplayTick: 3 cząsteczki portalu wokół skrzyni.</summary>
        public static void DisplayTick(World w, int x, int y, int z, Rng r, Action<double, double, double, double, double, double> portal)
        {
            for (int i = 0; i < 3; i++)
            {
                int j = r.Next(2) * 2 - 1, k = r.Next(2) * 2 - 1;
                double px = x + .5 + .25 * j, py = y + r.NextFloat(), pz = z + .5 + .25 * k;
                portal(px, py, pz, r.NextFloat() * j, (r.NextFloat() - .5) * .125, r.NextFloat() * k);
            }
        }
    }

    /// <summary>TileEntityFlowerPot: przedmiot rośliny i jego meta.</summary>
    public sealed class FlowerPotTile : TileEntity
    {
        public override int TypeId => Bl2.FlowerPotTileId;
        public int ItemId, Data;
        public bool Empty => ItemId == 0;
        public override void Save(BinaryWriter bw) { bw.Write(ItemId); bw.Write(Data); }
        public override void Load(BinaryReader br) { ItemId = br.ReadInt32(); Data = br.ReadInt32(); }
    }

    /// <summary>BlockFlowerPot 1.8: stawiany przedmiotem doniczki, rośliny wkłada się PPM (wyjąć można tylko niszcząc doniczkę).</summary>
    public sealed class FlowerPotBlock : Block
    {
        public FlowerPotBlock() : base(B.FlowerPot, "flower_pot_block", "Doniczka")
        {
            T("flower_pot"); H(0); Snd(SoundType.Stone); NotCreative(); Tab(Blocks.TabDecoration);
            Opaque = false; Solid = true; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
            HasTileEntity = true; NeedsSupport = true; DropOverride = I.FlowerPotItem;
        }
        static readonly AABB Box = new(.3125, 0, .3125, .6875, .375, .6875);
        public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box);
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box);
        /// <summary>models/block/flower_pot.json: cztery ścianki 1x6 i ziemia na wysokości 4.</summary>
        public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        {
            var pot = ModelBox.All(Tex[0]);
            int dirt = TextureGen.Layer("dirt");
            boxes.Add(ModelBox.Px(5, 0, 5, 6, 6, 11, pot));
            boxes.Add(ModelBox.Px(10, 0, 5, 11, 6, 11, pot));
            boxes.Add(ModelBox.Px(6, 0, 5, 10, 6, 6, pot));
            boxes.Add(ModelBox.Px(6, 0, 10, 10, 6, 11, pot));
            boxes.Add(ModelBox.Px(6, 0, 6, 10, 4, 10, new[] { pot[0], dirt, pot[2], pot[3], pot[4], pot[5] }));
        }
        /// <summary>World.doesBlockHaveSolidTopSurface pod spodem.</summary>
        public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z);
        public override ItemStack PickStack(int meta) => new(I.FlowerPotItem);
        public override void OnAdded(World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is FlowerPotTile) return;
            var t = new FlowerPotTile();
            var legacy = FlowerPots.FromLegacy(w.GetMeta(x, y, z));
            if (legacy.HasValue) (t.ItemId, t.Data) = legacy.Value;
            w.SetTileEntity(x, y, z, t);
        }
        public override void OnRemoved(World w, int x, int y, int z, int oldMeta) => w.RemoveTileEntity(x, y, z);
        public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
        {
            drops.Add(new ItemStack(I.FlowerPotItem));
            if (w.GetTileEntity(x, y, z) is FlowerPotTile t && !t.Empty && Item.ById[t.ItemId] != null) drops.Add(new ItemStack(t.ItemId, 1, t.Data));
        }
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            var held = p.Inventory.Selected;
            if (held?.Item == null || !held.Item.IsBlock) return false;
            if (w.GetTileEntity(x, y, z) is not FlowerPotTile t || !t.Empty) return false;
            if (!FlowerPots.CanContain(held.Item.BlockId, held.Damage)) return false;
            if (w.IsRemote) return true;
            t.ItemId = held.Id; t.Data = held.Damage;
            var c = w.GetChunk(x >> 4, z >> 4); if (c != null) c.Modified = true;
            w.MarkDirty(x, y, z);
            Bl2Stats.Add(p, "stat.flowerPotted");
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        }
    }

    /// <summary>Zawartość doniczek (BlockFlowerPot.canNotContain, stare meta 1.7 z TileEntityFlowerPot).</summary>
    public static class FlowerPots
    {
        public static bool CanContain(int block, int meta) => block is B.Dandelion or B.Poppy or B.Cactus or B.BrownMushroom or B.RedMushroom
            or B.OakSapling or B.BirchSapling or B.SpruceSapling or B.JungleSapling or B.AcaciaSapling or B.DarkOakSapling or B.DeadBush
            || block == B.TallGrass && meta == 2;

        /// <summary>Meta bloku doniczki sprzed 1.8 (np. chatka czarownicy stawia 7 = czerwony grzyb).</summary>
        public static (int id, int data)? FromLegacy(int meta) => meta switch
        {
            1 => (B.Poppy, 0), 2 => (B.Dandelion, 0), 3 => (B.OakSapling, 0), 4 => (B.SpruceSapling, 0), 5 => (B.BirchSapling, 0),
            6 => (B.JungleSapling, 0), 7 => (B.RedMushroom, 0), 8 => (B.BrownMushroom, 0), 9 => (B.Cactus, 0), 10 => (B.DeadBush, 0),
            11 => (B.TallGrass, 2), 12 => (B.AcaciaSapling, 0), 13 => (B.DarkOakSapling, 0), _ => null
        };

        /// <summary>ItemReed (Items.flower_pot): stawia blok doniczki na klikniętej ścianie.</summary>
        public static bool Place(Player p, ItemStack held, int x, int y, int z, int face)
        {
            var w = p.World;
            var target = Block.Get(w.GetBlock(x, y, z));
            if (target.Id == B.SnowLayer && (w.GetMeta(x, y, z) & 7) < 1) face = Dir.Up;
            else if (!target.Replaceable) { x += Dir.X[face]; y += Dir.Y[face]; z += Dir.Z[face]; }
            if (y < 0 || y >= Chunk.Height) return false;
            var at = Block.Get(w.GetBlock(x, y, z));
            if (at.Id != 0 && !at.Replaceable) return false;
            var pot = Block.Get(B.FlowerPot);
            if (!pot.CanPlaceAt(w, x, y, z)) return false;
            if (w.IsRemote) return true;
            w.SetBlock(x, y, z, B.FlowerPot, 0);
            w.PlaySound("place_stone", x + .5, y + .5, z + .5, 1f, .8f);
            Bl2Stats.Add(p, "stat.useItem." + I.FlowerPotItem);
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        }
    }

    /// <summary>BlockBarrier 1.8: niewidzialny, niezniszczalny w survivalu, nic nie wypada; widać go tylko jako cząsteczkę.</summary>
    public sealed class BarrierBlock : Block
    {
        public BarrierBlock() : base(B.Barrier, "barrier", "Bariera")
        {
            T("barrier"); H(-1f, 3600000); NotCreative(); Snd(SoundType.Stone);
            Opaque = false; FullCube = false; Render = RenderType.None; Layer = RenderLayer.Cutout; LightOpacity = 0;
        }
        public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops) { }
        public override ItemStack SilkTouchStack(int meta) => null;
        public override ItemStack PickStack(int meta) => new(B.Barrier);
    }
}
