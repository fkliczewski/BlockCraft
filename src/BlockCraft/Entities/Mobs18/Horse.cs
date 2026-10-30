using BlockCraft.Items;

namespace BlockCraft.Entities;

// Podstawy jazdy i oswajania. Ekwipunek konia i dziedziczenie umaszczenia pozostają do rozbudowy.
public sealed class Horse : Tameable
{
    public int HorseType, Temper, Variant;
    public double JumpStrength;
    public Horse(World.World w, int type = 0) : base(w)
    {
        HorseType = type; Width = 1.4f; Height = 1.6f;
        MaxHealth = Health = type >= 3 ? 15 : 15 + w.Rand.Next(8) + w.Rand.Next(9);
        MoveSpeed = type == 0 ? (.45 + w.Rand.NextFloat() * .3 + w.Rand.NextFloat() * .3 + w.Rand.NextFloat() * .3) * .25 : .175;
        JumpStrength = type == 0 ? .4 + w.Rand.NextFloat() * .2 + w.Rand.NextFloat() * .2 + w.Rand.NextFloat() * .2 : .5;
        Variant = w.Rand.Next(7) | w.Rand.Next(5) << 8;
    }
    public override string TypeKey => HorseType switch { 1 => "donkey", 2 => "mule", 3 => "zombie_horse", 4 => "skeleton_horse", _ => "horse" };
    public override string DisplayName => HorseType switch { 1 => "Osioł", 2 => "Muł", 3 => "Koń zombie", 4 => "Koń szkielet", _ => "Koń" };
    private string SoundPrefix => HorseType switch { 1 or 2 => "mob.horse.donkey", 3 => "mob.horse.zombie", 4 => "mob.horse.skeleton", _ => "mob.horse" };
    public override string LivingSound => SoundPrefix + ".idle";
    public override string HurtSound => SoundPrefix + ".hit";
    public override string DeathSound => SoundPrefix + ".death";
    public override string StepSoundName => "mob.horse.soft";
    public override bool IsBreedingItem(ItemStack s) => Tamed && HorseType < 2 && s?.Id is I.GoldenApple or I.GoldenCarrot;
    public override Animal CreateChild() => new Horse(World, Mate is Horse h && h.HorseType != HorseType ? 2 : HorseType);
    public override void Tame(Player p) { base.Tame(p); Sitting = false; Temper = 100; }
    public override bool Interact(Player p)
    {
        if (InteractLead(p)) return true;
        var s = p.Inventory.Selected;
        if (s?.Id == I.Saddle && Tamed && !Saddled && !IsBaby)
        { Saddled = true; ConsumeHeld(p); World.PlaySound("mob.horse.leather", X, Y, Z); return true; }
        if (HorseType < 3 && s != null)
        {
            var food = s.Id switch
            {
                I.Sugar => (1, 30, 3), I.Wheat => (2, 20, 3), I.Apple => (3, 60, 3),
                I.GoldenCarrot => (4, 60, 5), I.GoldenApple => (10, 240, 10), _ => (0, 0, 0)
            };
            if (food.Item1 > 0)
            {
                bool changed = Health < MaxHealth || IsBaby || !Tamed && Temper < 100;
                if (IsBreedingItem(s) && !IsBaby && GrowingAge == 0 && InLove == 0) return base.Interact(p);
                if (changed) { Heal(food.Item1); if (IsBaby) GrowingAge = Math.Min(0, GrowingAge + food.Item2 * 20); Temper = Math.Min(100, Temper + food.Item3); ConsumeHeld(p); return true; }
            }
        }
        if (!IsBaby && (Tamed || s == null) && Rider == null) return Mount(p);
        return base.Interact(p);
    }
    public override void Jump() { MotionY = JumpStrength; OnGround = false; }
    protected override void Think() { if (Age % 900 == 0) Heal(1); base.Think(); }
    protected override void DropLoot(bool player, bool burning) { if (!IsBaby) Drop(HorseType >= 3 ? HorseType == 3 ? I.RottenFlesh : I.Bone : I.Leather, World.Rand.Next(3)); }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(HorseType); b.Write(Temper); b.Write(Variant); b.Write(JumpStrength); b.Write(MoveSpeed); b.Write(MaxHealth); }
    public override void Load(BinaryReader b) { base.Load(b); HorseType = b.ReadInt32(); Temper = b.ReadInt32(); Variant = b.ReadInt32(); JumpStrength = b.ReadDouble(); MoveSpeed = b.ReadDouble(); MaxHealth = b.ReadSingle(); }
}
