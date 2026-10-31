using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Cykl błysków i obszar rażenia EntityLightningBolt z MCP 1.8.9.</summary>
public sealed class LightningBolt : Entity
{
    public int State = 2;
    public long VertexSeed;
    public int RemainingFlashes;
    private bool _started;
    public bool Flashing => !Removed && State >= 0;
    public override string TypeKey => "lightning_bolt";
    public override int NetData => State;
    public override void SetNetData(int data) => State = data;

    public LightningBolt(World.World w) : base(w)
    {
        NoGravity = NoClip = true; Width = 6; Height = 128;
        VertexSeed = w.Rand.NextLong(); RemainingFlashes = 1 + w.Rand.Next(3);
    }

    public override void Tick()
    {
        Age++;
        // Klient ogląda stan hosta, nigdy sam nie losuje błysków ani nie razi encji.
        if (World.IsRemote) return;
        if (!_started)
        {
            _started = true;
            if (World.Difficulty >= Difficulty.Normal) Ignite(true);
            // Zasięg grzmotu 64 bloki; głośność ogranicza istniejący mikser.
            World.PlaySound("ambient.weather.thunder", X, Y, Z, 4, .8f + World.Rand.NextFloat() * .2f);
            World.PlaySound("random.explode", X, Y, Z, .35f, .5f + World.Rand.NextFloat() * .2f);
        }
        if (--State < 0)
        {
            if (RemainingFlashes == 0) { Removed = true; return; }
            if (State < -World.Rand.Next(10))
            {
                RemainingFlashes--; State = 1; VertexSeed = World.Rand.NextLong(); Ignite(false);
            }
        }
        if (!Flashing) return;
        var box = new AABB(X - 3, Y - 3, Z - 3, X + 3, Y + 9, Z + 3);
        foreach (var entity in World.GetEntitiesInBox(box, this))
            if (!entity.Dead && entity is not LightningBolt) entity.OnStruckByLightning(this);
    }

    private void Ignite(bool neighbors)
    {
        int x = MathUtil.FloorToInt(X), y = MathUtil.FloorToInt(Y), z = MathUtil.FloorToInt(Z);
        if (!World.Rules.DoFireTick || !World.IsAreaLoaded(x, z, 10)) return;
        void Fire(int fx, int fy, int fz)
        {
            if (World.GetBlock(fx, fy, fz) == B.Air && Block.Get(B.Fire).CanPlaceAt(World, fx, fy, fz))
                World.SetBlock(fx, fy, fz, B.Fire);
        }
        Fire(x, y, z);
        if (neighbors) for (int i = 0; i < 4; i++) Fire(x + World.Rand.Next(3) - 1, y + World.Rand.Next(3) - 1, z + World.Rand.Next(3) - 1);
    }

    public override void Save(BinaryWriter b)
    { base.Save(b); b.Write(State); b.Write(VertexSeed); b.Write(RemainingFlashes); b.Write(_started); }
    public override void Load(BinaryReader b)
    { base.Load(b); State = b.ReadInt32(); VertexSeed = b.ReadInt64(); RemainingFlashes = b.ReadInt32(); _started = b.ReadBoolean(); }
}
