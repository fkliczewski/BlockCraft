using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void EntitiesNetherEnd()
    {
        WorldHooks.OnEntityInPortal = DimensionPortals.ContactPortal;
        Entity("eye_of_ender_signal", w => new EnderEyeEntity(w));
        Render.EntityRenderer.Custom[typeof(EnderEyeEntity)] = (r, e, pt, pos, cam) => r.RenderBillboardSprite(Render.TextureGen.Layer("item_ender_eye"), pos + new OpenTK.Mathematics.Vector3(0, .1f, 0), .25f, OpenTK.Mathematics.Vector4.One, cam);
    }
}
