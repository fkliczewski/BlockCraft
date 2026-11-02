using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World
{
    /// <summary>Pęknięcie bloku kopanego przez innego gracza (RenderGlobal.damagedBlocks z MC 1.8).</summary>
    public readonly record struct BreakAnim(int X, int Y, int Z, int Stage, long Tick);

    public sealed partial class World
    {
        /// <summary>Pęknięcia bloków kopanych przez innych graczy: id kopiącego -> blok i etap 0-9.
        /// Host wpisuje tu postęp zdalnych graczy, klient postęp hosta i pozostałych gości (S2C.BlockBreakAnim).</summary>
        public readonly Dictionary<int, BreakAnim> DestroyProgress = new();
    }
}

namespace BlockCraft.Net
{
    /// <summary>Powiązania encji przesyłane w S2C.EntityLinks: pojazd, wierzchowiec i smycz.</summary>
    public readonly record struct EntityLinkIds(int Riding, int Mount, int Leash)
    {
        public static readonly EntityLinkIds None = new(-1, -1, -1);
        public bool IsNone => Riding < 0 && Mount < 0 && Leash < 0;
        public bool Rides => Riding >= 0 || Mount >= 0;
    }

    /// <summary>Jazda (łódka, wagonik, koń, świnia), dżokeje i smycze w sieci oraz identyfikatory części smoka.</summary>
    public static class NetLinks
    {
        public static EntityLinkIds Of(Entity e) => new(
            e.RidingEntity is { Removed: false } r ? r.Id : -1,
            e is Player p ? p.RidingMob is { Removed: false } m ? m.Id : -1 : e is Mob mob && mob.Vehicle is { Removed: false } v ? v.Id : -1,
            e is Mob lm && lm.LeashHolder is { Removed: false } h ? h.Id : -1);

        /// <summary>Klient: ustawia powiązania encji bez logiki gry (wysiadanie i miejsce obok pojazdu liczy host).
        /// Zwraca false, gdy któraś encja docelowa jeszcze nie dotarła.</summary>
        public static bool Apply(World.World w, Entity e, EntityLinkIds l)
        {
            bool ok = true;
            Entity Find(int id) { if (id < 0) return null; if (w.EntityById.TryGetValue(id, out var t) && !t.Removed) return t; ok = false; return null; }
            var riding = Find(l.Riding);
            if (riding == e) riding = null;
            if (e.RidingEntity != riding)
            {
                if (e.RidingEntity?.Passenger == e) e.RidingEntity.Passenger = null;
                e.RidingEntity = riding;
                if (riding != null)
                {
                    var old = riding.Passenger;
                    if (old != null && old != e && old.RidingEntity == riding) old.RidingEntity = null;
                    riding.Passenger = e;
                }
            }
            var mount = Find(l.Mount);
            if (e is Player p)
            {
                var m = mount as Mob;
                if (p.RidingMob != m)
                {
                    if (p.RidingMob?.Rider == p) p.RidingMob.Rider = null;
                    p.RidingMob = m;
                    if (m != null) { if (m.Rider != null && m.Rider != p) m.Rider.RidingMob = null; m.Rider = p; }
                }
            }
            else if (e is Mob mob) mob.Vehicle = mount == e ? null : mount;
            var leash = Find(l.Leash);
            if (e is Mob lm) lm.LeashHolder = leash == e ? null : leash;
            if (riding != null || e.RidingEntity == null && e is Player { RidingMob: not null }) { e.MotionX = e.MotionY = e.MotionZ = 0; e.FallDistance = 0; }
            return ok;
        }

        /// <summary>Część smoka po stronie klienta ma ujemne ID wyliczone z ID smoka u hosta (MC: id smoka + 1..7),
        /// więc nie zderza się z ID encji hosta, a host odtwarza z niego swoją część.</summary>
        public static int DragonPartId(int dragonId, int index) => -(dragonId * 8 + index + 1);

        public static bool DecodeDragonPart(int id, out int dragonId, out int index)
        {
            dragonId = index = -1;
            if (id >= 0) return false;
            long v = -(long)id - 1;
            dragonId = (int)(v / 8); index = (int)(v % 8);
            return true;
        }

        /// <summary>Kwadrat odległości punktu od prostopadłościanu encji (sprawdzanie zasięgu ataku dużych encji).</summary>
        public static double DistanceSqToBox(Entity e, double x, double y, double z)
        {
            var b = e.BoundingBox;
            double dx = Math.Max(Math.Max(b.MinX - x, 0), x - b.MaxX), dy = Math.Max(Math.Max(b.MinY - y, 0), y - b.MaxY), dz = Math.Max(Math.Max(b.MinZ - z, 0), z - b.MaxZ);
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
