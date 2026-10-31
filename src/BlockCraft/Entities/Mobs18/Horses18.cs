using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>EntityHorse z MC 1.8: koń (0), osioł (1), muł (2), koń zombie (3), koń szkielet (4).</summary>
public sealed class Horse : Animal
{
    public int HorseType, Variant, Temper;
    public bool Tamed, Chested;
    public string Owner = "";
    public double JumpStrength = .7;
    public float JumpPower; private int _jumpCharge; private bool _jumpHeld, _horseJumping;
    public int RearTicks, EatTicks, MouthTicks;
    /// <summary>0 siodło, 1 zbroja, 2-16 skrzynia (osioł i muł).</summary>
    public readonly SimpleInventory Inv = new(17);
    public static readonly string[] Names = { "Koń", "Osioł", "Muł", "Koń zombie", "Koń szkielet" };

    public Horse(World.World w, int type = 0) : base(w)
    {
        Width = 1.4f; Height = 1.6f; HorseType = type; Inv.OnChanged = UpdateEquipment;
        var r = w.Rand;
        if (type == 0) { MaxHealth = RandomHealth(r); MoveSpeed = RandomSpeed(r); JumpStrength = RandomJump(r); Variant = r.Next(7) | (r.Next(5) << 8); }
        else { MaxHealth = 15; MoveSpeed = type is 1 or 2 ? .175 : .2; JumpStrength = type is 1 or 2 ? .5 : .7; }
        Health = MaxHealth;
    }
    private static float RandomHealth(Rng r) => 15 + r.Next(8) + r.Next(9);
    private static double RandomSpeed(Rng r) => (.45 + r.NextDouble() * .3 + r.NextDouble() * .3 + r.NextDouble() * .3) * .25;
    private static double RandomJump(Rng r) => .4 + r.NextDouble() * .2 + r.NextDouble() * .2 + r.NextDouble() * .2;

    public override string TypeKey => HorseType switch { 1 => "donkey", 2 => "mule", 3 => "zombie_horse", 4 => "skeleton_horse", _ => "horse" };
    public override string DisplayName => Names[Math.Clamp(HorseType, 0, 4)];
    public bool Undead => HorseType >= 3;
    public bool CanCarryChest => HorseType is 1 or 2;
    public bool CanWearArmor => HorseType == 0;
    public bool CanMate => !Undead && HorseType != 2;
    public bool Rearing => RearTicks > 0;
    public int ArmorItem => Inv.Get(1)?.Id ?? 0;
    public override int ArmorValue => ArmorItem switch { I.IronHorseArmor => 5, I.GoldHorseArmor => 7, I.DiamondHorseArmor => 11, _ => 0 };
    public override float EyeHeight => Height;
    private string SoundBase => HorseType switch { 1 or 2 => "mob.horse.donkey", 3 => "mob.horse.zombie", 4 => "mob.horse.skeleton", _ => "mob.horse" };
    public override string LivingSound => World.Rand.Next(10) == 0 && !Undead && Rider == null ? AngrySound : SoundBase + ".idle";
    public override string HurtSound => SoundBase + ".hit";
    public override string DeathSound => SoundBase + ".death";
    private string AngrySound => HorseType is 1 or 2 ? "mob.horse.donkey.angry" : "mob.horse.angry";

    public static bool IsHorseArmor(int id) => id is I.IronHorseArmor or I.GoldHorseArmor or I.DiamondHorseArmor;
    public override bool IsBreedingItem(ItemStack s) => s?.Id is I.GoldenApple or I.GoldenCarrot;
    public override bool CanMateWith(Animal a) => a is Horse h && h != this && CanMate && h.CanMate && Tamed && h.Tamed && (h.HorseType == HorseType || h.HorseType + HorseType == 1);

    private void UpdateEquipment()
    {
        bool saddled = Inv.Get(0)?.Id == I.Saddle;
        if (saddled && !Saddled && Age > 20) World.PlaySound("mob.horse.leather", X, Y, Z, .5f, 1f);
        Saddled = saddled;
        if (!CanCarryChest || !Chested) for (int i = 2; i < 17; i++) if (Inv.Get(i) != null) { World.DropItem(X, Y + 1, Z, Inv.Get(i)); Inv.Set(i, null); }
    }

    public void MakeRear() { RearTicks = 20; World.PlaySound(AngrySound, X, Y, Z); }

    public void Tame(Player p) { Tamed = true; Owner = p.Name; Temper = 100; PersistenceRequired = true; World.SpawnParticles(ParticleKind.Heart, X, Y + Height, Z, 7); }

    public override bool Interact(Player p)
    {
        var s = p.Inventory.Selected;
        if (s?.Id == I.SpawnEgg) return false;
        if (!Tamed && Undead) return false;
        if (Tamed && !IsBaby && p.Sneaking) { OpenGui(p); return true; }
        if (!IsBaby && Rider != null) return false;
        if (s != null)
        {
            if (CanWearArmor && IsHorseArmor(s.Id)) { if (!Tamed) { MakeRear(); return true; } OpenGui(p); return true; }
            bool ate = Feed(s);
            if (!Tamed && !ate) { if (InteractLead(p)) return true; MakeRear(); return true; }
            if (!ate && CanCarryChest && !Chested && s.Id == B.Chest && !IsBaby)
            { Chested = true; World.PlaySound("mob.chickenplop", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f); Mob.ConsumeHeld(p); return true; }
            if (!ate && !IsBaby && !Saddled && s.Id == I.Saddle) { OpenGui(p); return true; }
            if (ate) { Mob.ConsumeHeld(p); return true; }
            if (InteractLead(p)) return true;
        }
        if (LeashHolder == p) return InteractLead(p);
        if (IsBaby) return false;
        RearTicks = 0; return Mount(p);
    }

    /// <summary>Karmienie jak EntityHorse.func_110256_c (leczenie, wzrost, temperament, miłość).</summary>
    private bool Feed(ItemStack s)
    {
        float heal = 0; int grow = 0, temper = 0; bool love = false;
        switch (s.Id)
        {
            case I.Wheat: heal = 2; grow = 20; temper = 3; break;
            case I.Sugar: heal = 1; grow = 30; temper = 3; break;
            case I.Bread: heal = 7; grow = 180; temper = 3; break;
            case B.HayBale: heal = 20; grow = 180; break;
            case I.Apple: heal = 3; grow = 60; temper = 3; break;
            case I.GoldenCarrot: heal = 4; grow = 60; temper = 5; love = Tamed && GrowingAge == 0; break;
            case I.GoldenApple: heal = 10; grow = 240; temper = 10; love = Tamed && GrowingAge == 0; break;
            default: return false;
        }
        bool ate = false;
        if (Health < MaxHealth && heal > 0) { Heal(heal); ate = true; }
        if (IsBaby && grow > 0) { GrowingAge = Math.Min(0, GrowingAge + grow * 20); ate = true; }
        if (temper > 0 && (ate || !Tamed) && Temper < 100) { Temper = Math.Min(100, Temper + temper); ate = true; }
        if (love && CanMate && InLove <= 0) { InLove = 600; ate = true; }
        if (ate) { EatTicks = 20; MouthTicks = 10; World.PlaySound("eat", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f); }
        return ate;
    }

    private void OpenGui(Player p)
    {
        if (World.IsRemote) return;
        p.OpenEntityContainer(new HorseContainer(p.Inventory, this));
    }

    protected override void UpdateAI()
    {
        base.UpdateAI();
        if (RearTicks > 0) { RearTicks--; MoveForward = MoveStrafe = 0; }
        if (EatTicks > 0) EatTicks--;
        if (MouthTicks > 0) MouthTicks--;
        bool controlled = Rider != null && Saddled && Tamed;
        if (!controlled) { _jumpCharge = 0; _jumpHeld = false; return; }
        IsJumping = false;
        if (MoveForward <= 0) MoveForward *= .25f;
        // skok ładowany jak pasek skoku w 1.8 (GuiIngame.renderHorseJumpBar)
        if (Rider.IsJumping) { _jumpHeld = true; _jumpCharge++; JumpPower = _jumpCharge < 10 ? _jumpCharge * .1f : .8f + 2f / (_jumpCharge - 9) * .1f; }
        else if (_jumpHeld)
        {
            _jumpHeld = false; _jumpCharge = 0;
            if (OnGround && !_horseJumping && JumpPower > 0)
            {
                MotionY = JumpStrength * Math.Clamp(JumpPower, 0, 1);
                if (EffectLevel(Effect.JumpBoost) is int jb and >= 0) MotionY += (jb + 1) * .1f;
                _horseJumping = true;
                if (MoveForward > 0)
                {
                    double yr = Yaw * MathUtil.Deg2Rad;
                    MotionX += -.4 * Math.Sin(yr) * JumpPower; MotionZ += .4 * Math.Cos(yr) * JumpPower;
                    World.PlaySound("mob.horse.jump", X, Y, Z, .4f, 1f);
                }
            }
            JumpPower = 0;
        }
        if (OnGround && _horseJumping && MotionY <= 0) { _horseJumping = false; World.PlaySound("mob.horse.land", X, Y, Z, .4f, 1f); }
    }

    protected override void Think()
    {
        if (Rider != null) { Nav.Stop(); return; }
        if (!Tamed && Rider == null && World.Rand.Next(300) == 0 && !IsBaby && World.GetBlock(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y) - 1, MathUtil.FloorToInt(Z)) == B.Grass) { EatTicks = 40; Nav.Stop(); }
        if (EatTicks > 0) return;
        if (Tamed && !IsBaby && Health < MaxHealth && Age % 900 == 0) Heal(1);
        base.Think();
    }

    public override Animal CreateChild()
    {
        var other = Mate as Horse ?? this;
        int type = other.HorseType == HorseType ? HorseType : 2;
        var c = new Horse(World, type);
        if (type == 0)
        {
            int roll = World.Rand.Next(9), color = roll < 4 ? Variant & 255 : roll < 8 ? other.Variant & 255 : World.Rand.Next(7);
            int m = World.Rand.Next(5), marks = m < 2 ? Variant >> 8 : m < 4 ? other.Variant >> 8 : World.Rand.Next(5);
            c.Variant = color | (marks << 8);
        }
        var r = World.Rand;
        c.MaxHealth = c.Health = (MaxHealth + other.MaxHealth + RandomHealth(r)) / 3f;
        c.MoveSpeed = (MoveSpeed + other.MoveSpeed + RandomSpeed(r)) / 3;
        c.JumpStrength = (JumpStrength + other.JumpStrength + RandomJump(r)) / 3;
        return c;
    }

    protected override void DropLoot(bool player, bool burning)
    {
        int n = World.Rand.Next(3) + World.Rand.Next(Looting + 1);
        Drop(HorseType == 3 ? I.RottenFlesh : HorseType == 4 ? I.Bone : I.Leather, n);
    }

    public override void OnDeath(DamageSource src)
    {
        if (Dead) return;
        if (!World.IsRemote)
        {
            for (int i = 0; i < 17; i++) if (Inv.Get(i) is { } s) { World.DropItem(X, Y + .5, Z, s); Inv.Slots[i] = null; }
            if (Chested) Drop(B.Chest, 1);
            Chested = false; Saddled = false;
        }
        base.OnDeath(src);
    }

    public override bool CanSpawnHere() => HorseType == 0 && base.CanSpawnHere();

    public override void Save(BinaryWriter b)
    {
        base.Save(b);
        b.Write(HorseType); b.Write(Variant); b.Write(Temper); b.Write(Tamed); b.Write(Chested); b.Write(Owner ?? "");
        b.Write(MaxHealth); b.Write(MoveSpeed); b.Write(JumpStrength);
        for (int i = 0; i < 17; i++) b.WriteStack(Inv.Get(i));
    }
    public override void Load(BinaryReader b)
    {
        base.Load(b);
        HorseType = b.ReadInt32(); Variant = b.ReadInt32(); Temper = b.ReadInt32(); Tamed = b.ReadBoolean(); Chested = b.ReadBoolean(); Owner = b.ReadString();
        MaxHealth = b.ReadSingle(); MoveSpeed = b.ReadDouble(); JumpStrength = b.ReadDouble();
        for (int i = 0; i < 17; i++) Inv.Slots[i] = b.ReadStack();
        Saddled = Inv.Get(0)?.Id == I.Saddle;
    }
    public override int NetData => base.NetData | (HorseType << 1) | (Saddled ? 16 : 0) | (Chested ? 32 : 0) | (Tamed ? 64 : 0) | (Rearing ? 128 : 0) | (EatTicks > 0 ? 256 : 0)
        | ((Variant & 7) << 9) | (((Variant >> 8) & 7) << 12) | ((ArmorItem == I.IronHorseArmor ? 1 : ArmorItem == I.GoldHorseArmor ? 2 : ArmorItem == I.DiamondHorseArmor ? 3 : 0) << 15);
    public override void SetNetData(int d)
    {
        base.SetNetData(d); HorseType = (d >> 1) & 7; Saddled = (d & 16) != 0; Chested = (d & 32) != 0; Tamed = (d & 64) != 0;
        RearTicks = (d & 128) != 0 ? Math.Max(RearTicks, 2) : 0; EatTicks = (d & 256) != 0 ? Math.Max(EatTicks, 2) : 0;
        Variant = ((d >> 9) & 7) | (((d >> 12) & 7) << 8);
        int a = (d >> 15) & 3; Inv.Slots[1] = a == 0 ? null : new ItemStack(a == 1 ? I.IronHorseArmor : a == 2 ? I.GoldHorseArmor : I.DiamondHorseArmor);
    }
}

/// <summary>ContainerHorseInventory: siodło, zbroja, 15 slotów skrzyni.</summary>
public sealed class HorseContainer : Container
{
    public readonly Horse Horse;
    private sealed class HorseSlot : Slot
    {
        private readonly Func<ItemStack, bool> _ok;
        public HorseSlot(IInventory inv, int i, int x, int y, Func<ItemStack, bool> ok) : base(inv, i, x, y) { _ok = ok; }
        public override bool CanPlace(ItemStack s) => s == null || _ok(s);
        public override int MaxStack(ItemStack s) => 1;
    }
    public HorseContainer(PlayerInventory inv, Horse h)
    {
        Horse = h; Title = h.CustomName ?? h.DisplayName;
        Slots.Add(new HorseSlot(h.Inv, 0, 8, 18, s => s.Id == I.Saddle));
        if (h.CanWearArmor) Slots.Add(new HorseSlot(h.Inv, 1, 8, 36, s => Horse.IsHorseArmor(s.Id)));
        if (h.Chested && h.CanCarryChest) for (int r = 0; r < 3; r++) for (int c = 0; c < 5; c++) Slots.Add(new Slot(h.Inv, 2 + r * 5 + c, 80 + c * 18, 18 + r * 18));
        AddPlayerSlots(inv, 84, 142);
    }
    protected override bool TransferFromPlayer(ItemStack s)
    {
        if (s.Id == I.Saddle) return MergeStack(s, 0, 1, false);
        if (Horse.IsHorseArmor(s.Id) && Horse.CanWearArmor) return MergeStack(s, 1, 2, false);
        int chestStart = Horse.CanWearArmor ? 2 : 1;
        return FirstPlayerSlot > chestStart && MergeStack(s, chestStart, FirstPlayerSlot, false);
    }
    public bool StillValid(Player p) => !Horse.Dead && !Horse.Removed && Horse.DistanceSq(p) < 64;
}
