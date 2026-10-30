using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World
{
    public sealed class SavedEffect
    {
        public Effect Id { get; set; }
        public int Duration { get; set; }
        public int Amplifier { get; set; }
        public bool Ambient { get; set; }
        public bool ShowParticles { get; set; } = true;
    }
    public sealed partial class PlayerData
    {
        public List<SavedEffect> Effects { get; set; } = new();
        public float Absorption { get; set; }
        public int EnchantmentSeed { get; set; }
        public List<SlotData> EnderItems { get; set; } = new();
        public Dictionary<string, int> Statistics { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
    }
}
namespace BlockCraft.Entities
{
    public partial class Player
    {
        public readonly SimpleInventory EnderInventory = new(27);
        public int EnchantmentSeed = Random.Shared.Next();
        public readonly Dictionary<string, int> Statistics = new();
        public readonly HashSet<string> Achievements = new();
        private void SaveMechanics(World.PlayerData data)
        {
            data.Effects = Effects.Values.Select(e => new World.SavedEffect { Id = e.Id, Duration = e.Duration, Amplifier = e.Amplifier, Ambient = e.Ambient, ShowParticles = e.ShowParticles }).ToList();
            data.Absorption = Absorption; data.EnchantmentSeed = EnchantmentSeed;
            for (int i = 0; i < EnderInventory.Size; i++) if (EnderInventory.Get(i) is { } s) data.EnderItems.Add(BlockCraft.World.SlotData.From(i, s));
            data.Statistics = new(Statistics); data.Achievements = Achievements.ToList();
        }
        private void LoadMechanics(World.PlayerData data)
        {
            float health = Health; ClearEffects();
            foreach (var e in data.Effects ?? new()) AddEffect(new EffectInstance(e.Id, e.Duration, e.Amplifier, e.Ambient, e.ShowParticles));
            Health = Math.Min(MaxHealth, health); Absorption = Math.Max(0, data.Absorption); EnchantmentSeed = data.EnchantmentSeed;
            for (int i = 0; i < EnderInventory.Size; i++) EnderInventory.Set(i, null);
            foreach (var s in data.EnderItems ?? new()) if (s.Slot >= 0 && s.Slot < 27) EnderInventory.Set(s.Slot, s.ToStack());
            Statistics.Clear(); foreach (var kv in data.Statistics ?? new()) Statistics[kv.Key] = kv.Value;
            Achievements.Clear(); foreach (var a in data.Achievements ?? new()) Achievements.Add(a);
        }
    }
}
