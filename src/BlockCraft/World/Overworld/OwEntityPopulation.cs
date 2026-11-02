using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public sealed partial class World
{
    /// <summary>Plany struktur powstają w generatorze, encje dopiero na wątku świata z gotowymi rejestrami.</summary>
    public void ApplyGeneratedEntities(Chunk c)
    {
        if (IsRemote || c.GeneratedEntityPlans == null) return;
        var plans = c.GeneratedEntityPlans; c.GeneratedEntityPlans = null;
        foreach (var plan in plans)
        {
            var key = plan.Elder && plan.Key == "guardian" ? "elder_guardian" : plan.Key;
            var entity = EntitySerializer.Create(key, this);
            if (entity == null) continue;
            entity.SetPositionAndPrev(plan.X, plan.Y, plan.Z);
            if (entity is Villager v && plan.Profession >= 0) v.Profession = Math.Clamp(plan.Profession, 0, 4);
            if (entity is Mob mob) mob.PersistenceRequired = true;
            if (entity is IInventory inventory && plan.Slots != null)
                for (int i = 0; i < Math.Min(inventory.Size, plan.Slots.Length); i++) inventory.Set(i, plan.Slots[i]?.Copy());
            AddEntityNow(entity);
        }
        c.Modified = true;
    }
}
