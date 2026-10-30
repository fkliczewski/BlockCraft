using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>Użycie itemów NE prawym przyciskiem: ładunek ognisty (ItemFireball) i oko Endu (ItemEnderEye) z MC 1.8.</summary>
public static partial class ItemRegistry
{
    private static void NetherEndItemUse()
    {
        Item.ById[I.FireCharge].UseOnBlock = (p, held, x, y, z, face, hx, hy, hz) =>
        {
            var w = p.World;
            if (w.IsRemote) return true;
            int fx = x + Dir.X[face], fy = y + Dir.Y[face], fz = z + Dir.Z[face];
            if (w.GetBlock(fx, fy, fz) == B.Air)
            {
                w.PlaySound("item.fireCharge.use", fx + .5, fy + .5, fz + .5, 1f, (w.Rand.NextFloat() - w.Rand.NextFloat()) * .2f + 1f);
                w.SetBlock(fx, fy, fz, B.Fire, 0);
            }
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            return true;
        };
        // Wkładanie oka do ramy obsługuje EndFrameBlock.OnActivated; tu rzut w stronę twierdzy.
        Item.ById[I.EyeOfEnder].UseInAir = (p, held) =>
        {
            var w = p.World;
            if (w.IsRemote) return true;
            var target = WorldHooks.FindStronghold?.Invoke(w, MathUtil.FloorToInt(p.X), MathUtil.FloorToInt(p.Z));
            if (target == null || w.Dimension != DimensionId.Overworld) return true;
            var eye = new EnderEyeEntity(w);
            eye.SetPositionAndPrev(p.X, p.Y + 1.62 - .2, p.Z);
            eye.MoveTowards(target.Value.x, target.Value.y, target.Value.z);
            w.SpawnEntity(eye);
            w.PlaySound("random.bow", p.X, p.Y, p.Z, .5f, .4f / (w.Rand.NextFloat() * .4f + .8f));
            p.SwingArm();
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        };
    }
}
