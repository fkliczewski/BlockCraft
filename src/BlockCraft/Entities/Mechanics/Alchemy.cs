using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Entities;

public static class PlayerAlchemy
{
    public static void ExchangeOne(Player player, ItemStack used, ItemStack result)
    {
        if (--used.Count <= 0) player.Inventory.Selected = result;
        else if (!player.Inventory.Add(result)) player.DropStack(result, false);
        player.Inventory.Dirty = true;
    }
    public static bool Use(Player player, ItemStack stack)
    {
        var w = player.World;
        if (stack.Id == I.Potion && !Potions.Splash(stack.Damage)) { player.StartUsing(stack); return true; }
        if (stack.Id == I.ExpBottle || stack.Id == I.Potion)
        {
            w.SpawnEntity(new AlchemyProjectile(w, player, stack));
            w.PlaySound("random.bow", player.X, player.Y, player.Z, .5f, .4f / (w.Rand.NextFloat() * .4f + .8f));
            if (!player.Creative && --stack.Count <= 0) player.Inventory.Selected = null;
            player.SwingArm(); return true;
        }
        if (stack.Id == I.GlassBottle)
        {
            var (dx, dy, dz) = player.Look(); var hit = w.RayCast(player.X, player.Y + player.EyeHeight, player.Z, dx, dy, dz, 5, true);
            if (hit.Hit && w.GetBlock(hit.X, hit.Y, hit.Z) == B.Water) ExchangeOne(player, stack, new ItemStack(I.Potion));
            return true;
        }
        return false;
    }
    public static void Drink(Player player, ItemStack stack)
    {
        foreach (var effect in Potions.Effects(stack))
            if (EffectLogic.Instant(effect.Id)) EffectLogic.ApplyInstant(player, effect.Id, effect.Amplifier); else player.AddEffect(effect);
        player.World.PlaySound("random.drink", player.X, player.Y, player.Z);
        if (!player.Creative) ExchangeOne(player, stack, new ItemStack(I.GlassBottle));
    }
}

public sealed class AlchemyProjectile : Entity
{
    public ItemStack Stack;
    public LivingEntity Thrower;
    public AlchemyProjectile(World.World world) : base(world) { Width = Height = .25f; }
    public AlchemyProjectile(World.World world, LivingEntity thrower, ItemStack stack) : this(world)
    {
        Stack = stack.CopyWith(1); Thrower = thrower;
        SetPositionAndPrev(thrower.X, thrower.Y + thrower.EyeHeight - .1, thrower.Z);
        double yaw = thrower.Yaw * MathUtil.Deg2Rad, pitch = (thrower.Pitch - 20) * MathUtil.Deg2Rad;
        double speed = stack.Id == I.ExpBottle ? .7 : .5;
        MotionX = -Math.Sin(yaw) * Math.Cos(pitch) * speed; MotionY = -Math.Sin(pitch) * speed; MotionZ = Math.Cos(yaw) * Math.Cos(pitch) * speed;
    }
    public override string TypeKey => "me_potion";
    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        double length = Math.Sqrt(MotionX * MotionX + MotionY * MotionY + MotionZ * MotionZ);
        var hit = length > 0 ? World.RayCast(X, Y, Z, MotionX / length, MotionY / length, MotionZ / length, length) : default;
        double closest = hit.Hit ? hit.Distance : length; Entity target = null;
        if (length > 0) foreach (var e in World.GetEntitiesInBox(BoundingBox.Expand(MotionX, MotionY, MotionZ).Grow(1), this))
            if (e.CanBeAttacked && (e != Thrower || Age > 5) && e.BoundingBox.Grow(.3).RayIntersect(X, Y, Z, MotionX / length, MotionY / length, MotionZ / length, out double t, out _) && t <= closest) { target = e; closest = t; }
        if (target != null || hit.Hit)
        {
            if (length > 0) SetPosition(X + MotionX / length * closest, Y + MotionY / length * closest, Z + MotionZ / length * closest);
            Impact(target); return;
        }
        SetPosition(X + MotionX, Y + MotionY, Z + MotionZ);
        MotionX *= .99; MotionZ *= .99; MotionY = MotionY * .99 - .05;
        if (Y < -64 || Age > 1200) Kill();
    }
    public void Impact(Entity direct)
    {
        if (World.IsRemote) return;
        if (Stack?.Id == I.ExpBottle) World.SpawnXp(X, Y, Z, 3 + World.Rand.Next(5) + World.Rand.Next(5));
        else if (Stack != null)
        {
            foreach (var entity in World.GetEntitiesInBox(BoundingBox.Grow(4, 2, 4), this).OfType<LivingEntity>())
            {
                double distance = entity.DistanceSq(X, Y, Z); if (distance >= 16) continue;
                double strength = entity == direct ? 1 : 1 - Math.Sqrt(distance) / 4;
                foreach (var effect in Potions.Effects(Stack))
                {
                    if (EffectLogic.Instant(effect.Id)) EffectLogic.ApplyInstant(entity, effect.Id, effect.Amplifier, strength, Thrower);
                    else { int duration = (int)(strength * effect.Duration + .5); if (duration > 20) entity.AddEffect(effect.Id, duration, effect.Amplifier); }
                }
            }
        }
        World.SpawnParticles(ParticleKind.Spell, X, Y, Z, 40, Stack == null ? 0x385DC6 : EffectLogic.MixedColor(Potions.Effects(Stack)));
        World.PlaySound("game.potion.smash", X, Y, Z); Kill();
    }
    public override void Save(BinaryWriter writer) { base.Save(writer); writer.WriteStack(Stack); }
    public override void Load(BinaryReader reader) { base.Load(reader); Stack = reader.ReadStack(); }
}

public static partial class Registries
{
    static partial void EntitiesMechanics()
    {
        Tile(60, () => new BrewingStandTile()); Tile(61, () => new World.JukeboxTile()); Entity("me_potion", w => new AlchemyProjectile(w));
        EntityRenderer.Custom[typeof(AlchemyProjectile)] = (r, e, pt, pos, cam) =>
        {
            var p = (AlchemyProjectile)e; r.RenderBillboardSprite(p.Stack?.Item?.GetSprite(p.Stack) ?? TextureGen.Layer("item_potion"), pos, .25f, Vector4.One, cam);
        };
    }
}
