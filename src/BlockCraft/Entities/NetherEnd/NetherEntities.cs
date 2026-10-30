using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void EntitiesNetherEnd()
    {
        WorldHooks.OnEntityInPortal = DimensionPortals.ContactPortal;
        NetherFortress.Init();
        Mob("zombie_pigman", w => new ZombiePigman(w));
        Mob("ghast", w => new Ghast(w));
        Mob("blaze", w => new Blaze(w));
        Mob("magma_cube", w => new MagmaCube(w));
        Mob("wither_skeleton", w => new WitherSkeleton(w));
        Mob("endermite", w => new Endermite(w));
        Entity("fireball", w => new LargeFireballEntity(w));
        Entity("small_fireball", w => new SmallFireballEntity(w));
        Mob("ender_dragon", w => new EnderDragon(w));
        Entity("ender_crystal", w => new EnderCrystal(w));
        Mob("wither", w => new Wither(w));
        Entity("wither_skull", w => new WitherSkullEntity(w));
        Render.EntityRenderer.RegisterNetherRenderers();
        Render.EntityRenderer.RegisterBossRenderers();
        Entity("eye_of_ender_signal", w => new EnderEyeEntity(w));
        Render.EntityRenderer.Custom[typeof(EnderEyeEntity)] = (r, e, pt, pos, cam) => r.RenderBillboardSprite(Render.TextureGen.Layer("item_ender_eye"), pos + new OpenTK.Mathematics.Vector3(0, .1f, 0), .25f, OpenTK.Mathematics.Vector4.One, cam);
    }
}
