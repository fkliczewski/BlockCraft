using BlockCraft.Entities;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed partial class EntityRenderer
{
    public static void RenderMinecart(EntityRenderer r, Entity entity, float pt, Vector3 pos, Vector3 cam)
    {
        var cart = (MinecartEntity)entity;
        float yaw = cart.PrevYaw + Core.MathUtil.WrapDegrees(cart.Yaw - cart.PrevYaw) * pt;
        double dx = Math.Cos(yaw * Core.MathUtil.Deg2Rad) * 0.3, dz = Math.Sin(yaw * Core.MathUtil.Deg2Rad) * 0.3;
        var a = MinecartEntity.RailPoint(cart.World, cart.IX(pt) - dx, cart.IY(pt), cart.IZ(pt) - dz);
        var b = MinecartEntity.RailPoint(cart.World, cart.IX(pt) + dx, cart.IY(pt), cart.IZ(pt) + dz);
        float slope = a != null && b != null ? (float)Math.Atan2(b.Value.y - a.Value.y, 0.6) : 0;
        float roll = cart.RollingAmplitude - pt, damage = Math.Max(0, cart.DamageTaken - pt);
        float wobble = roll > 0 ? MathF.Sin(roll) * roll * damage / 10 * cart.RollingDirection * Core.MathUtil.Deg2Rad : 0;
        var m = Matrix4.CreateRotationX(wobble) * Matrix4.CreateRotationZ(slope) * Matrix4.CreateRotationY(-yaw * Core.MathUtil.Deg2Rad) * Matrix4.CreateTranslation(pos);
        int side = TextureGen.Layer("minecart_side"), inside = TextureGen.Layer("minecart_inside");
        int[] tex = { side, inside, side, side, side, side };
        r.EmitModelBox(new ModelBox(-0.625f, 0, -0.5f, 0.625f, 0.125f, 0.5f, tex), m);
        r.EmitModelBox(new ModelBox(-0.625f, 0.125f, -0.5f, 0.625f, 0.625f, -0.375f, tex), m);
        r.EmitModelBox(new ModelBox(-0.625f, 0.125f, 0.375f, 0.625f, 0.625f, 0.5f, tex), m);
        r.EmitModelBox(new ModelBox(-0.625f, 0.125f, -0.375f, -0.5f, 0.625f, 0.375f, tex), m);
        r.EmitModelBox(new ModelBox(0.5f, 0.125f, -0.375f, 0.625f, 0.625f, 0.375f, tex), m);
        if (cart.DisplayBlock != 0)
        {
            var color = r.Terrain.Color;
            if (cart is TntMinecartEntity { Fuse: >= 0 } tnt && tnt.Fuse / 5 % 2 == 0) r.Terrain.Color = new Vector4(1.5f, 1.5f, 1.5f, 1);
            float scale = 0.75f;
            if (cart is TntMinecartEntity { Fuse: >= 0 and < 10 } tt) scale *= 1 + MathF.Pow(1 - (tt.Fuse - pt + 1) / 10f, 4) * 0.3f;
            r.RenderBlockAt(cart.DisplayBlock, 0, Matrix4.CreateScale(scale) * Matrix4.CreateTranslation(0, cart.Kind == MinecartKind.Hopper ? 0.43f : 0.65f, 0) * m);
            r.Terrain.Color = color;
        }
    }
}
