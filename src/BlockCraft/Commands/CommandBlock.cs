using BlockCraft.Core;
using BlockCraft.Cmd;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft.Cmd
{
    /// <summary>Wspólna logika bloku i wagonika z blokiem poleceń.</summary>
    public sealed class CommandBlockLogic : CommandSender
    {
        public string Command = "", LastOutput = "", CustomName = "@";
        public bool TrackOutput = true;
        public int SuccessCount;
        public readonly CommandStats Stats = new();
        public World.World BoundWorld;
        public Func<(double X, double Y, double Z)> Position;
        public Entity Owner;
        public override string Name => CustomName;
        public override World.World World => Owner?.World ?? BoundWorld;
        public override Entity Entity => Owner;
        public override (double X, double Y, double Z) Pos => Owner != null ? (Owner.X, Owner.Y, Owner.Z) : Position();
        public override (int X, int Y, int Z) BlockPos => Floor(Pos.X, Pos.Y, Pos.Z);
        public override int PermissionLevel => 2;
        public override bool CommandFeedback => Commands.Home(World).Rules.CommandBlockOutput;
        public override void Message(string msg) { if (TrackOutput) LastOutput = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg; }
        public override void SetStat(StatType t, int v) => Stats.Set(this, t, v);
        [ThreadStatic] private static int _depth;
        public void Trigger()
        {
            if (World == null || World.IsRemote) return;
            if (_depth >= 64) { SuccessCount = 0; Message("Przekroczono limit zagnieżdżenia komend"); return; }
            LastOutput = "";
            _depth++;
            try { SuccessCount = Commands.Run(this, Command); }
            finally { _depth--; }
        }
        public void Save(BinaryWriter w)
        { w.Write(Command); w.Write(CustomName); w.Write(SuccessCount); w.Write(TrackOutput); w.Write(LastOutput); Stats.Write(w); }
        public void Load(BinaryReader r)
        { Command = r.ReadString(); CustomName = r.ReadString(); SuccessCount = r.ReadInt32(); TrackOutput = r.ReadBoolean(); LastOutput = r.ReadString(); Stats.Read(r); }
        public void WriteNbt(Nbt n)
        {
            n["Command"] = Command; n["CustomName"] = CustomName; n["SuccessCount"] = SuccessCount; n["TrackOutput"] = TrackOutput ? 1 : 0;
            n["LastOutput"] = System.Text.Json.JsonSerializer.Serialize(new { text = LastOutput });
            var st = new Nbt();
            for (int i = 0; i < 5; i++) if (Stats.Selector[i] != null) { st[CommandStats.TypeNames[i] + "Name"] = Stats.Selector[i]; st[CommandStats.TypeNames[i] + "Objective"] = Stats.Objective[i]; }
            n["CommandStats"] = st;
        }
        public void ReadNbt(Nbt n)
        {
            Command = n.GetString("Command", Command); CustomName = n.GetString("CustomName", CustomName);
            SuccessCount = n.GetInt("SuccessCount", SuccessCount); TrackOutput = n.GetBool("TrackOutput", TrackOutput);
            if (n.GetString("LastOutput") is { } output)
            { try { LastOutput = TextComponent.Render(output, this, null); } catch (CommandException) { LastOutput = output; } }
            if (n.GetCompound("CommandStats") is { } st)
                for (int i = 0; i < 5; i++) { Stats.Selector[i] = st.GetString(CommandStats.TypeNames[i] + "Name"); Stats.Objective[i] = st.GetString(CommandStats.TypeNames[i] + "Objective"); }
        }
    }

    public static class CommandBlocks
    {
        public static bool CanEdit(Player p) => p != null && !p.Dead && p.Creative && new PlayerSender(p).PermissionLevel >= 2;
        public static CommandBlockLogic Resolve(Player p, int x, int y, int z, int entityId)
        {
            if (!CanEdit(p)) return null;
            if (entityId >= 0)
                return p.World.Entities.OfType<CommandMinecart>().FirstOrDefault(e => e.Id == entityId && e.DistanceSq(p) <= 64)?.Logic;
            if (p.DistanceSq(x + .5, y + .5, z + .5) > 64 || p.World.GetTileEntity(x, y, z) is not CommandBlockTile tile) return null;
            tile.Bind(p.World); return tile.Logic;
        }
        public static bool Open(Player p, int x, int y, int z, int entityId = -1)
        {
            var logic = Resolve(p, x, y, z, entityId); if (logic == null) return false;
            if (p.Handler is RemoteClient remote) remote.OpenCommands(x, y, z, entityId, logic);
            else if (p.Handler is Game game) game.SetScreen(new CommandBlockScreen(x, y, z, entityId, logic.Command, logic.TrackOutput, logic.LastOutput));
            return true;
        }
        public static bool Update(Player p, int x, int y, int z, int entityId, string command, bool track)
        {
            if (command == null || command.Length > 32767) return false;
            var logic = Resolve(p, x, y, z, entityId); if (logic == null) return false;
            logic.Command = command; logic.TrackOutput = track; if (!track) logic.LastOutput = "";
            if (entityId < 0) p.World.MarkDirty(x, y, z);
            return true;
        }
    }
}

namespace BlockCraft.World
{
    public sealed class CommandBlockTile : TileEntity
    {
        public override int TypeId => 89;
        public readonly Cmd.CommandBlockLogic Logic = new();
        public CommandBlockTile() { Logic.Position = () => (X + .5, Y + .5, Z + .5); }
        public void Bind(World w) => Logic.BoundWorld = w;
        public override void Tick(World w) => Bind(w);
        public override void Save(BinaryWriter w) => Logic.Save(w);
        public override void Load(BinaryReader r) => Logic.Load(r);
    }

    public sealed class CommandBlock : Block, IComparatorSource
    {
        public CommandBlock() : base(B.CommandBlock, "command_block", "Blok poleceń")
        { H(-1, 6000000); T("command_block"); HasTileEntity = true; }
        public override int DropId(int meta, Rng r) => 0;
        public override void OnAdded(World w, int x, int y, int z)
        { if (w.GetTileEntity(x, y, z) is not CommandBlockTile) w.SetTileEntity(x, y, z, new CommandBlockTile()); ((CommandBlockTile)w.GetTileEntity(x, y, z)).Bind(w); }
        public override bool OnActivated(World w, int x, int y, int z, Player p) => Cmd.CommandBlocks.Open(p, x, y, z);
        public override void OnNeighborChanged(World w, int x, int y, int z, int id)
        {
            if (w.IsRemote) return;
            bool power = Redstone.IsBlockPowered(w, x, y, z); int meta = w.GetMeta(x, y, z);
            if (power && (meta & 1) == 0) { w.SetMeta(x, y, z, meta | 1, false); w.ScheduleTick(x, y, z, Id, 1); }
            else if (!power && (meta & 1) != 0) w.SetMeta(x, y, z, meta & ~1, false);
        }
        public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
        {
            if (w.IsRemote || w.GetTileEntity(x, y, z) is not CommandBlockTile tile) return;
            tile.Bind(w); tile.Logic.Trigger(); w.MarkDirty(x, y, z); Redstone.NotifyNeighbors(w, x, y, z, Id);
        }
        public int GetComparatorOutput(World w, int x, int y, int z) => w.GetTileEntity(x, y, z) is CommandBlockTile tile ? tile.Logic.SuccessCount : 0;
    }

    public static partial class Blocks { static partial void BlocksCommands() { new CommandBlock(); } }
}

namespace BlockCraft.Entities
{
    public sealed class CommandMinecart : MinecartEntity
    {
        public readonly Cmd.CommandBlockLogic Logic = new();
        private long _lastRun = -4;
        public CommandMinecart(World.World w) : base(w) { Logic.Owner = this; }
        public override string TypeKey => "command_block_minecart";
        public override int DisplayBlock => B.CommandBlock;
        public override bool Interact(Player p) => Cmd.CommandBlocks.Open(p, 0, 0, 0, Id);
        protected override void OnActivatorRail(bool powered)
        { if (powered && !World.IsRemote && World.TotalTime - _lastRun >= 4) { _lastRun = World.TotalTime; Logic.Trigger(); } }
        public override void Save(BinaryWriter w) { base.Save(w); Logic.Save(w); }
        public override void Load(BinaryReader r) { base.Load(r); Logic.Load(r); }
    }
    public static partial class Registries
    {
        static partial void EntitiesCommands()
        {
            Tile(89, () => new World.CommandBlockTile()); Entity("command_block_minecart", w => new CommandMinecart(w));
            NetHooks.WorldState["scoreboard"] = (w => Scoreboard.Of(w).ToBytes(), (w, b) => { using var r = new BinaryReader(new MemoryStream(b)); Scoreboard.Client.Read(r); });
            NetHooks.WorldState["worldborder"] = (w => WorldBorder.Of(w).ToBytes(), (w, b) => { using var r = new BinaryReader(new MemoryStream(b)); WorldBorder.Client.Read(r); });
        }
    }
}

namespace BlockCraft.Render
{
    public static partial class TextureGen
    {
        static partial void TexCommands()
        {
            var px = new uint[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                bool rim = x is 0 or 15 || y is 0 or 15;
                bool dot = x % 5 is 2 or 3 && y % 5 is 2 or 3;
                px[y * 16 + x] = rim ? Rgba(107, 74, 48) : dot ? Rgba(165, 116, 87) : Rgba(211 + (x + y) % 7, 167, 119);
            }
            Add("command_block", px);
        }
    }
}
