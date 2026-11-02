using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft
{
    public sealed partial class Commands
    {
        private static void RegisterBlocks()
        {
            Reg("setblock", 2, "/setblock <x> <y> <z> <blok> [dane] [tryb] [tag danych]", SetBlock,
                tab: (s, a) => a.Length == 4 ? BlockKeys : a.Length == 6 ? new[] { "replace", "destroy", "keep" } : Array.Empty<string>());
            Reg("fill", 2, "/fill <x1> <y1> <z1> <x2> <y2> <z2> <blok> [dane] [tryb] [tag danych lub blok do zastąpienia]", Fill,
                tab: (s, a) => a.Length == 7 ? BlockKeys : a.Length == 9 ? new[] { "replace", "destroy", "keep", "hollow", "outline" } : a.Length == 10 && a[8] == "replace" ? BlockKeys : Array.Empty<string>());
            Reg("clone", 2, "/clone <x1> <y1> <z1> <x2> <y2> <z2> <x> <y> <z> [tryb maski] [tryb klonowania]", Clone,
                tab: (s, a) => a.Length == 10 ? new[] { "replace", "masked", "filtered" } : a.Length == 11 ? new[] { "normal", "force", "move" } : a.Length == 12 && a[9] == "filtered" ? BlockKeys : Array.Empty<string>());
            Reg("blockdata", 2, "/blockdata <x> <y> <z> <tag danych>", BlockData);
            Reg("testforblock", 2, "/testforblock <x> <y> <z> <blok> [dane] [tag danych]", TestForBlock, tab: (s, a) => a.Length == 4 ? BlockKeys : Array.Empty<string>());
            Reg("testforblocks", 2, "/testforblocks <x1> <y1> <z1> <x2> <y2> <z2> <x> <y> <z> [tryb]", TestForBlocks, tab: (s, a) => a.Length == 10 ? new[] { "masked", "all" } : Array.Empty<string>());
        }

        private static bool HasTile(int id) => id == B.CommandBlock || TileProbe(id);
        private static bool TileProbe(int id)
        {
            // blok ma tile entity, jeśli postawienie go w świecie testowym tworzy TE; tańsza heurystyka: znane bloki-pojemniki i bloki z TE
            return id is B.Chest or B.TrappedChest or B.Furnace or B.FurnaceLit or B.MobSpawner or B.StandingSign or B.WallSign or B.Dispenser or B.Dropper
                or B.Hopper or B.NoteBlock or B.Jukebox or B.Beacon or B.BrewingStand or B.Skull or B.StandingBanner or B.WallBanner or B.CommandBlock;
        }

        private static void ClearTile(World.World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is IInventory inv) { for (int i = 0; i < inv.Size; i++) inv.Set(i, null); }
        }

        private static void SetBlock(CommandSender s, string[] a)
        {
            if (a.Length < 4) throw new UsageException("/setblock <x> <y> <z> <blok> [dane] [tryb] [tag danych]");
            s.SetStat(StatType.AffectedBlocks, 0);
            var p = Arg.BlockPos(s, a, 0);
            int id = Arg.Block(a[3]);
            int meta = a.Length >= 5 ? Arg.Int(a[4], 0, 15) : 0;
            var w = s.World;
            if (!w.IsLoadedAt(p.X, p.Z) || p.Y < 0 || p.Y >= Chunk.Height) throw new CommandException("Nie można postawić bloku poza światem");
            Nbt tag = null;
            if (a.Length >= 7 && HasTile(id)) tag = ParseTag(ChatArg(s, a, 6, true));
            if (a.Length >= 6)
            {
                if (a[5] == "destroy")
                {
                    w.BreakBlock(p.X, p.Y, p.Z, true);
                    if (id == 0) { Notify(s, "Postawiono blok"); return; }
                }
                else if (a[5] == "keep" && w.GetBlock(p.X, p.Y, p.Z) != 0) throw new CommandException("Nie można było postawić bloku");
            }
            if (w.GetTileEntity(p.X, p.Y, p.Z) != null) { ClearTile(w, p.X, p.Y, p.Z); w.SetBlock(p.X, p.Y, p.Z, 0, 0, false); }
            if (!w.SetBlock(p.X, p.Y, p.Z, (byte)id, meta, false)) throw new CommandException("Nie można było postawić bloku");
            if (tag != null && w.GetTileEntity(p.X, p.Y, p.Z) is { } te) TileNbt.Read(te, w, tag);
            w.NotifyNeighbors(p.X, p.Y, p.Z, id);
            s.SetStat(StatType.AffectedBlocks, 1);
            Notify(s, "Postawiono blok");
        }

        private static void Fill(CommandSender s, string[] a)
        {
            if (a.Length < 7) throw new UsageException("/fill <x1> <y1> <z1> <x2> <y2> <z2> <blok> [dane] [tryb] [tag danych lub blok do zastąpienia]");
            s.SetStat(StatType.AffectedBlocks, 0);
            var p1 = Arg.BlockPos(s, a, 0); var p2 = Arg.BlockPos(s, a, 3);
            int id = Arg.Block(a[6]);
            int meta = a.Length >= 8 ? Arg.Int(a[7], 0, 15) : 0;
            int x0 = Math.Min(p1.X, p2.X), y0 = Math.Min(p1.Y, p2.Y), z0 = Math.Min(p1.Z, p2.Z), x1 = Math.Max(p1.X, p2.X), y1 = Math.Max(p1.Y, p2.Y), z1 = Math.Max(p1.Z, p2.Z);
            long vol = (long)(x1 - x0 + 1) * (y1 - y0 + 1) * (z1 - z0 + 1);
            if (vol > 32768) throw new CommandException($"Za dużo bloków w podanym obszarze ({vol} > 32768)");
            if (y0 < 0 || y1 >= 256) throw new CommandException("Nie można postawić bloków poza światem");
            var w = s.World;
            for (int z = z0; z < z1 + 16; z += 16) for (int x = x0; x < x1 + 16; x += 16) if (!w.IsLoadedAt(Math.Min(x, x1), Math.Min(z, z1))) throw new CommandException("Nie można postawić bloków poza światem");
            Nbt tag = null;
            if (a.Length >= 10 && HasTile(id)) tag = ParseTag(ChatArg(s, a, 9, true));
            string mode = a.Length >= 9 ? a[8] : null;
            int? repId = null; int repMeta = -1;
            if (mode == "replace" && !HasTile(id))
            {
                if (a.Length > 9) repId = Arg.Block(a[9]);
                if (a.Length > 10) repMeta = Arg.Int(a[10]);
            }
            var changed = new List<(int, int, int)>();
            int n = 0;
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (mode != null)
                        {
                            if (mode is not ("outline" or "hollow"))
                            {
                                if (mode == "destroy") w.BreakBlock(x, y, z, true);
                                else if (mode == "keep") { if (w.GetBlock(x, y, z) != 0) continue; }
                                else if (mode == "replace" && !HasTile(id))
                                {
                                    if (repId != null && w.GetBlock(x, y, z) != repId) continue;
                                    if (repMeta >= 0 && w.GetMeta(x, y, z) != repMeta) continue;
                                }
                            }
                            else if (x != x0 && x != x1 && y != y0 && y != y1 && z != z0 && z != z1)
                            {
                                if (mode == "hollow") { if (w.GetTileEntity(x, y, z) != null) ClearTile(w, x, y, z); if (w.SetBlock(x, y, z, 0, 0, false)) changed.Add((x, y, z)); }
                                continue;
                            }
                        }
                        if (w.GetTileEntity(x, y, z) != null) { ClearTile(w, x, y, z); w.SetBlock(x, y, z, 0, 0, false); }
                        if (w.SetBlock(x, y, z, (byte)id, meta, false))
                        {
                            changed.Add((x, y, z)); n++;
                            if (tag != null && w.GetTileEntity(x, y, z) is { } te) TileNbt.Read(te, w, tag);
                        }
                    }
            foreach (var (x, y, z) in changed) w.NotifyNeighbors(x, y, z, w.GetBlock(x, y, z));
            if (n <= 0) throw new CommandException("Nie wypełniono żadnych bloków");
            s.SetStat(StatType.AffectedBlocks, n);
            Notify(s, $"Wypełniono {n} bloków");
        }

        private static void Clone(CommandSender s, string[] a)
        {
            if (a.Length < 9) throw new UsageException("/clone <x1> <y1> <z1> <x2> <y2> <z2> <x> <y> <z> [tryb maski] [tryb klonowania]");
            s.SetStat(StatType.AffectedBlocks, 0);
            var b1 = Arg.BlockPos(s, a, 0); var b2 = Arg.BlockPos(s, a, 3); var b3 = Arg.BlockPos(s, a, 6);
            int sx0 = Math.Min(b1.X, b2.X), sy0 = Math.Min(b1.Y, b2.Y), sz0 = Math.Min(b1.Z, b2.Z), sx1 = Math.Max(b1.X, b2.X), sy1 = Math.Max(b1.Y, b2.Y), sz1 = Math.Max(b1.Z, b2.Z);
            int dx0 = b3.X, dy0 = b3.Y, dz0 = b3.Z, dx1 = b3.X + sx1 - sx0, dy1 = b3.Y + sy1 - sy0, dz1 = b3.Z + sz1 - sz0;
            long vol = (long)(sx1 - sx0 + 1) * (sy1 - sy0 + 1) * (sz1 - sz0 + 1);
            if (vol > 32768) throw new CommandException($"Za dużo bloków w podanym obszarze ({vol} > 32768)");
            bool overlap = sx1 >= dx0 && sx0 <= dx1 && sy1 >= dy0 && sy0 <= dy1 && sz1 >= dz0 && sz0 <= dz1;
            if ((a.Length < 11 || (a[10] != "force" && a[10] != "move")) && overlap) throw new CommandException("Źródło i cel nie mogą się nakładać");
            bool move = a.Length >= 11 && a[10] == "move";
            if (sy0 < 0 || sy1 >= 256 || dy0 < 0 || dy1 >= 256) throw new CommandException("Brak dostępu do bloków poza światem");
            var w = s.World;
            bool Loaded(int x0, int z0, int x1, int z1) { for (int x = x0; x <= x1 + 15; x += 16) for (int z = z0; z <= z1 + 15; z += 16) if (!w.IsLoadedAt(Math.Min(x, x1), Math.Min(z, z1))) return false; return true; }
            if (!Loaded(sx0, sz0, sx1, sz1) || !Loaded(dx0, dz0, dx1, dz1)) throw new CommandException("Brak dostępu do bloków poza światem");
            bool masked = false; int? filter = null; int filterMeta = -1;
            if (a.Length >= 10)
            {
                if (a[9] == "masked") masked = true;
                else if (a[9] == "filtered")
                {
                    if (a.Length < 12) throw new UsageException("/clone <x1> <y1> <z1> <x2> <y2> <z2> <x> <y> <z> [tryb maski] [tryb klonowania]");
                    filter = Arg.Block(a[11]);
                    if (a.Length >= 13) filterMeta = Arg.Int(a[12], 0, 15);
                }
            }
            int ox = dx0 - sx0, oy = dy0 - sy0, oz = dz0 - sz0;
            var full = new List<CloneData>(); var tiles = new List<CloneData>(); var other = new List<CloneData>();
            var sources = new LinkedList<(int, int, int)>();
            for (int z = sz0; z <= sz1; z++)
                for (int y = sy0; y <= sy1; y++)
                    for (int x = sx0; x <= sx1; x++)
                    {
                        int id = w.GetBlock(x, y, z), meta = w.GetMeta(x, y, z);
                        if (masked && id == 0) continue;
                        if (filter != null && (id != filter || (filterMeta >= 0 && meta != filterMeta))) continue;
                        var te = w.GetTileEntity(x, y, z);
                        var d = new CloneData(x + ox, y + oy, z + oz, id, meta, te != null ? (te.TypeId, ChunkCodec.SaveTile(te)) : null);
                        if (te != null) { tiles.Add(d); sources.AddLast((x, y, z)); }
                        else if (!Block.Get(id).Opaque) { other.Add(d); sources.AddFirst((x, y, z)); }
                        else { full.Add(d); sources.AddLast((x, y, z)); }
                    }
            if (move)
            {
                foreach (var (x, y, z) in sources) { ClearTile(w, x, y, z); w.SetBlock(x, y, z, 0, 0, false); }
                foreach (var (x, y, z) in sources) w.NotifyNeighbors(x, y, z, 0);
            }
            var all = full.Concat(tiles).Concat(other).ToList();
            var rev = Enumerable.Reverse(all).ToList();
            foreach (var d in rev) { ClearTile(w, d.X, d.Y, d.Z); w.SetBlock(d.X, d.Y, d.Z, 0, 0, false); }
            int n = 0;
            foreach (var d in all) if (w.SetBlock(d.X, d.Y, d.Z, (byte)d.Id, d.Meta, false) || d.Id == 0) n += d.Id != 0 || true ? 1 : 0;
            n = 0;
            foreach (var d in all) if (w.GetBlock(d.X, d.Y, d.Z) == d.Id && w.GetMeta(d.X, d.Y, d.Z) == d.Meta) n++;
            foreach (var d in tiles)
            {
                if (d.Tile is not { } t) continue;
                var te = ChunkCodec.LoadTile(t.type, d.X, d.Y, d.Z, t.data);
                if (te != null) w.SetTileEntity(d.X, d.Y, d.Z, te);
            }
            foreach (var d in rev) w.NotifyNeighbors(d.X, d.Y, d.Z, d.Id);
            if (n <= 0) throw new CommandException("Nie sklonowano żadnych bloków");
            s.SetStat(StatType.AffectedBlocks, n);
            Notify(s, $"Sklonowano {n} bloków");
        }

        private sealed record CloneData(int X, int Y, int Z, int Id, int Meta, (int type, byte[] data)? Tile);

        private static void BlockData(CommandSender s, string[] a)
        {
            if (a.Length < 4) throw new UsageException("/blockdata <x> <y> <z> <tag danych>");
            s.SetStat(StatType.AffectedBlocks, 0);
            var p = Arg.BlockPos(s, a, 0);
            var w = s.World;
            if (!w.IsLoadedAt(p.X, p.Z)) throw new CommandException("Nie można zmienić bloku poza światem");
            var te = w.GetTileEntity(p.X, p.Y, p.Z) ?? throw new CommandException("Wskazany blok nie przechowuje danych");
            var nbt = TileNbt.Write(te);
            var copy = nbt.Copy();
            var tag = ParseTag(ChatArg(s, a, 3, true));
            Mojangson.Merge(nbt, tag);
            nbt["x"] = p.X; nbt["y"] = p.Y; nbt["z"] = p.Z;
            if (Nbt.Same(nbt, copy)) throw new CommandException("Tag danych nie zmienił się: " + Mojangson.ToText(nbt));
            TileNbt.Read(te, w, nbt);
            w.MarkDirty(p.X, p.Y, p.Z);
            var c = w.GetChunk(p.X >> 4, p.Z >> 4); if (c != null) c.Modified = true;
            s.SetStat(StatType.AffectedBlocks, 1);
            Notify(s, "Dane bloku zaktualizowane na: " + Mojangson.ToText(nbt));
        }

        private static void TestForBlock(CommandSender s, string[] a)
        {
            if (a.Length < 4) throw new UsageException("/testforblock <x> <y> <z> <blok> [dane] [tag danych]");
            s.SetStat(StatType.AffectedBlocks, 0);
            var p = Arg.BlockPos(s, a, 0);
            int id;
            try { id = Arg.Block(a[3]); } catch (CommandException) { throw new CommandException($"Nie ma bloku o ID/nazwie {a[3]}"); }
            int meta = a.Length >= 5 ? Arg.Int(a[4], -1, 15) : -1;
            var w = s.World;
            if (!w.IsLoadedAt(p.X, p.Z)) throw new CommandException("Nie można sprawdzić bloku poza światem");
            Nbt tag = null;
            if (a.Length >= 6 && HasTile(id)) tag = ParseTag(ChatArg(s, a, 5, true));
            int cur = w.GetBlock(p.X, p.Y, p.Z);
            if (cur != id) throw new CommandException($"Blok na {p.X},{p.Y},{p.Z} to {Block.Get(cur).Name} (oczekiwano: {Block.Get(id).Name}).");
            if (meta > -1 && w.GetMeta(p.X, p.Y, p.Z) != meta) throw new CommandException($"Blok na {p.X},{p.Y},{p.Z} miał wartość danych {w.GetMeta(p.X, p.Y, p.Z)} (oczekiwano: {meta}).");
            if (tag != null)
            {
                var te = w.GetTileEntity(p.X, p.Y, p.Z) ?? throw new CommandException($"Blok na {p.X},{p.Y},{p.Z} nie ma danych i nie obsługuje porównania tagów.");
                if (!Mojangson.Matches(tag, TileNbt.Write(te), true)) throw new CommandException($"Blok na {p.X},{p.Y},{p.Z} nie ma wymaganych kluczy NBT.");
            }
            s.SetStat(StatType.AffectedBlocks, 1);
            Notify(s, $"Znaleziono blok na {p.X},{p.Y},{p.Z}.");
        }

        private static void TestForBlocks(CommandSender s, string[] a)
        {
            if (a.Length < 9) throw new UsageException("/testforblocks <x1> <y1> <z1> <x2> <y2> <z2> <x> <y> <z> [tryb]");
            s.SetStat(StatType.AffectedBlocks, 0);
            var b1 = Arg.BlockPos(s, a, 0); var b2 = Arg.BlockPos(s, a, 3); var b3 = Arg.BlockPos(s, a, 6);
            int sx0 = Math.Min(b1.X, b2.X), sy0 = Math.Min(b1.Y, b2.Y), sz0 = Math.Min(b1.Z, b2.Z), sx1 = Math.Max(b1.X, b2.X), sy1 = Math.Max(b1.Y, b2.Y), sz1 = Math.Max(b1.Z, b2.Z);
            long vol = (long)(sx1 - sx0 + 1) * (sy1 - sy0 + 1) * (sz1 - sz0 + 1);
            if (vol > 524288) throw new CommandException($"Za dużo bloków w podanym obszarze ({vol} > 524288)");
            int dy1 = b3.Y + sy1 - sy0;
            if (sy0 < 0 || sy1 >= 256 || b3.Y < 0 || dy1 >= 256) throw new CommandException("Brak dostępu do bloków poza światem");
            var w = s.World;
            bool masked = a.Length > 9 && a[9] == "masked";
            int ox = b3.X - sx0, oy = b3.Y - sy0, oz = b3.Z - sz0, n = 0;
            for (int z = sz0; z <= sz1; z++)
                for (int y = sy0; y <= sy1; y++)
                    for (int x = sx0; x <= sx1; x++)
                    {
                        if (!w.IsLoadedAt(x, z) || !w.IsLoadedAt(x + ox, z + oz)) throw new CommandException("Brak dostępu do bloków poza światem");
                        int id = w.GetBlock(x, y, z);
                        if (masked && id == 0) continue;
                        if (id != w.GetBlock(x + ox, y + oy, z + oz) || w.GetMeta(x, y, z) != w.GetMeta(x + ox, y + oy, z + oz)) throw new CommandException("Źródło i cel nie są identyczne");
                        var t1 = w.GetTileEntity(x, y, z); var t2 = w.GetTileEntity(x + ox, y + oy, z + oz);
                        if (t1 != null && t2 != null)
                        {
                            var n1 = TileNbt.Write(t1); var n2 = TileNbt.Write(t2);
                            foreach (var k in new[] { "x", "y", "z" }) { n1.Remove(k); n2.Remove(k); }
                            if (!Nbt.Same(n1, n2) || !ChunkCodec.SaveTile(t1).AsSpan().SequenceEqual(ChunkCodec.SaveTile(t2))) throw new CommandException("Źródło i cel nie są identyczne");
                        }
                        else if (t1 != null) throw new CommandException("Źródło i cel nie są identyczne");
                        n++;
                    }
            s.SetStat(StatType.AffectedBlocks, n);
            Notify(s, $"Porównano {n} bloków");
        }
    }
}

namespace BlockCraft.Cmd
{
    /// <summary>TileEntity.writeToNBT/readFromNBT dla komend: x,y,z,id, Items (pojemniki), Text1-4 (tabliczki), dane bloku poleceń,
    /// EntityId spawnera i pola typów mapowane przez refleksję.</summary>
    public static class TileNbt
    {
        private static readonly Dictionary<string, string[]> Extra = new()
        {
            ["Note"] = new[] { "Note", "Pitch" }, ["Primary"] = new[] { "Primary" }, ["Secondary"] = new[] { "Secondary" }, ["Levels"] = new[] { "Levels" },
            ["BurnTime"] = new[] { "BurnTime" }, ["CookTime"] = new[] { "CookTime" }, ["SkullType"] = new[] { "SkullType", "Type" }, ["Rot"] = new[] { "Rotation", "Rot" },
            ["Base"] = new[] { "Base", "BaseColor" }, ["OutputSignal"] = new[] { "OutputSignal", "Output" }, ["BrewTime"] = new[] { "BrewTime" },
            ["TransferCooldown"] = new[] { "TransferCooldown", "Cooldown" }, ["Delay"] = new[] { "Delay" }, ["Record"] = new[] { "Record" },
        };

        public static string IdOf(TileEntity te) => te switch
        {
            CommandBlockTile => "Control", FurnaceTileEntity => "Furnace", ChestTileEntity => "Chest", SignTile => "Sign",
            _ => te.GetType().Name.Replace("TileEntity", "").Replace("Tile", "")
        };

        public static Nbt Write(TileEntity te)
        {
            var n = new Nbt { ["x"] = te.X, ["y"] = te.Y, ["z"] = te.Z, ["id"] = IdOf(te) };
            if (te is IInventory inv)
            {
                var items = new List<object>();
                for (int i = 0; i < inv.Size; i++) if (inv.Get(i) is { } st) { var x = EntityNbt.ItemToNbt(st); x["Slot"] = i; items.Add(x); }
                n["Items"] = items;
            }
            switch (te)
            {
                case SignTile sg: for (int i = 0; i < 4; i++) n["Text" + (i + 1)] = "{\"text\":\"" + (sg.Lines[i] ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}"; break;
                case CommandBlockTile cb: cb.Logic.WriteNbt(n); break;
            }
            foreach (var kv in Extra)
            {
                var m = EntityNbt.Find(te, kv.Value);
                if (m == null) continue;
                object v = EntityNbt.Get(m, te);
                if (v is bool b) n[kv.Key] = b ? 1 : 0;
                else if (v is int or float or double or long or string) n[kv.Key] = v;
                else if (v is Enum e) n[kv.Key] = Convert.ToInt32(e);
            }
            return n;
        }

        public static void Read(TileEntity te, World.World w, Nbt n)
        {
            if (te is IInventory inv && n.GetList("Items") is { } items)
            {
                for (int i = 0; i < inv.Size; i++) inv.Set(i, null);
                foreach (var o in items.OfType<Nbt>()) { int slot = o.GetInt("Slot"); if (slot >= 0 && slot < inv.Size) inv.Set(slot, EntityNbt.ItemFromNbt(o)); }
                inv.Changed();
            }
            switch (te)
            {
                case SignTile sg:
                    for (int i = 0; i < 4; i++)
                        if (n.GetString("Text" + (i + 1)) is { } t)
                        {
                            string line;
                            try { line = TextComponent.Render(t, new ConsoleSender(w), null); } catch (CommandException) { line = t; }
                            sg.Lines[i] = line;
                        }
                    break;
                case CommandBlockTile cb: cb.Logic.ReadNbt(n); break;
            }
            if (n.GetString("EntityId") is { } eid) te.SetData("EntityId", EntityNames.KeyOf(eid) ?? eid);
            foreach (var kv in Extra)
            {
                if (!n.TryGetValue(kv.Key, out var v)) continue;
                var m = EntityNbt.Find(te, kv.Value);
                if (m == null) continue;
                try { EntityNbt.Set(m, te, v); } catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException) { }
            }
        }
    }
}
