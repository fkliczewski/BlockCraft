using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public enum GuiKind { Inventory, Crafting, Furnace, Chest, Brewing, Hopper = 20, Dispenser, TrappedChest, Enchanting = 30, Anvil }

public interface IPlayerHandler
{
    void OpenBlockGui(GuiKind kind, int x, int y, int z);
    void OnMessage(string msg);
    void OnDeath(string message);
    void OnPickup(Entity item, Entity collector);
}

public partial class Player : LivingEntity
{
    public string Name = "Steve";
    public readonly PlayerInventory Inventory = new();
    public Container InventoryContainer;
    public Container OpenContainer;
    public ItemStack Cursor;
    public IPlayerHandler Handler;
    public int GameMode; // 0 przetrwanie, 1 kreatywny, 3 obserwator
    public bool Flying, AllowFlying, Invulnerable;
    public float FlySpeed = 0.05f;
    public int FoodLevel = 20; public float Saturation = 5f, Exhaustion; public int FoodTimer; public int PrevFoodLevel = 20;
    public int XpLevel, XpTotal; public float XpProgress;
    public int SpawnX, SpawnY, SpawnZ; public bool HasSpawn;
    public bool Sleeping; public int SleepTimer; public int BedX, BedY, BedZ;
    public ItemStack ItemInUse; public int ItemInUseCount;
    public int HungerEffect, RegenEffect;
    public string DeathMessage;
    public bool IsLocal;
    public double DistWalked, PrevDistWalked, CameraYawBob, PrevCameraYawBob;
    public float CameraPitchBob, PrevCameraPitchBob;
    private double _lastStatX, _lastStatY, _lastStatZ;
    public int PortalCooldown;
    public int Ping;

    public Player(World.World w) : base(w)
    {
        Width = 0.6f; Height = 1.8f; MaxHealth = 20; Health = 20; StepHeight = 0.6f;
        InventoryContainer = new PlayerContainer(Inventory);
    }

    public override string TypeKey => "player";
    public override string DisplayName => Name;
    public override float EyeHeight => Sneaking ? 1.54f : 1.62f;
    public bool Creative => GameMode == 1;
    public bool Spectator => GameMode == 3;
    public override int ArmorValue => Inventory.ArmorValue;
    protected override bool IsFlyingNow => Flying;
    public override string HurtSound => "game.player.hurt";
    public override string DeathSound => "game.player.die";

    public void SetGameMode(int gm)
    {
        GameMode = gm;
        AllowFlying = gm == 1 || gm == 3;
        Invulnerable = gm == 1 || gm == 3;
        NoClip = gm == 3;
        if (!AllowFlying) Flying = false;
        if (gm == 3) Flying = true;
    }

    public override bool IsInvulnerableTo(DamageSource s) => Invulnerable && (s.Type != "outOfWorld" || GameMode == 3) && s.Type != "kill";

    public override void PrevTick()
    {
        base.PrevTick();
        PrevDistWalked = DistWalked; PrevCameraYawBob = CameraYawBob; PrevCameraPitchBob = CameraPitchBob;
    }

    public override void Tick()
    {
        if (IsRemoteControlled) { base.Tick(); return; }
        if (NetPuppet) NetPuppetMove?.Invoke();
        if (Sleeping)
        {
            SleepTimer++; if (SleepTimer > 100) SleepTimer = 100;
            if (!World.IsRemote && World.IsDaytime() && SleepTimer > 20) WakeUp();
        }
        else if (SleepTimer > 0) { SleepTimer++; if (SleepTimer >= 110) SleepTimer = 0; }
        base.Tick();
        if (Dead) return;
        if (!World.IsRemote) FoodTick();
        UseItemTick();
        if (OnGround || InWater) PortalCooldown = 0;
        // podnoszenie przedmiotów
        if (!World.IsRemote && Health > 0 && !Spectator)
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(1, 0.5, 1), this))
                if (!e.Removed) e.OnCollideWithPlayer(this);
        // statystyki ruchu (wyczerpanie)
        double dx = X - _lastStatX, dy = Y - _lastStatY, dz = Z - _lastStatZ;
        double horiz = Math.Sqrt(dx * dx + dz * dz);
        if (horiz < 5)
        {
            if (InWater) AddExhaustion(0.015f * (float)Math.Sqrt(dx * dx + dy * dy + dz * dz));
            else if (OnGround) AddExhaustion((Sprinting ? 0.1f : 0.01f) * (float)horiz);
        }
        _lastStatX = X; _lastStatY = Y; _lastStatZ = Z;
        // bobbing kamery
        float speed = (float)Math.Min(0.1, horiz);
        float pitchB = (float)Math.Atan(-MotionY * 0.2) * 15f;
        if (speed > 0.1f) speed = 0.1f;
        if (!OnGround || Health <= 0) speed = 0;
        if (OnGround || Health <= 0) pitchB = 0;
        CameraYawBob += (speed - CameraYawBob) * 0.4;
        CameraPitchBob += (pitchB - CameraPitchBob) * 0.8f;
        DistWalked += horiz * 0.6;
        if (Sprinting && (HasEffect(Effect.Blindness) || FoodLevel <= 6 && !Creative || MoveForward <= 0 || CollidedH || Sneaking)) Sprinting = false;
        if (Flying && OnGround && !Spectator && !AllowFlying) Flying = false;
        if (OpenContainer is FurnaceContainer fc && World.GetTileEntity(fc.Furnace.X, fc.Furnace.Y, fc.Furnace.Z) != fc.Furnace) CloseContainer();
    }

    protected override void LivingUpdate()
    {
        if (NetPuppet) return; // zdalny gracz na hoście: ruch liczy klient
        if (Flying)
        {
            if (Sneaking) MotionY -= FlySpeed * 3;
            if (IsJumping) MotionY += FlySpeed * 3;
        }
        bool wasJump = IsJumping;
        if (Flying) IsJumping = false;
        var sneak = Sneaking;
        if (Flying) Sneaking = false;
        float strafe = MoveStrafe, fwd = MoveForward;
        if (sneak && !Flying) { MoveStrafe *= 0.3f; MoveForward *= 0.3f; }
        if (ItemInUse != null) { MoveStrafe *= 0.2f; MoveForward *= 0.2f; }
        bool wasGround = OnGround;
        base.LivingUpdate();
        MoveStrafe = strafe; MoveForward = fwd;
        IsJumping = wasJump; Sneaking = sneak;
    }

    public override void Jump()
    {
        base.Jump();
        AddExhaustion(Sprinting ? 0.8f : 0.2f);
    }

    protected override float JumpMovementFactor => Flying ? FlySpeed * (Sprinting ? 2f : 1f) : (Sprinting ? 0.026f : 0.02f);

    public override void Travel(float strafe, float forward)
    {
        if (TravelMounted(strafe, forward)) return;
        if (Flying)
        {
            double my = MotionY;
            bool g = OnGround;
            if (Spectator) { NoClip = true; }
            base.Travel(strafe, forward);
            MotionY = my * 0.6;
            if (Spectator) FallDistance = 0;
            FallDistance = 0;
        }
        else base.Travel(strafe, forward);
    }

    public override float GetSpeed() => 0.1f * (Sprinting ? 1.3f : 1f) * EffectLogic.Movement(this);

    protected override void OnFall(float distance)
    {
        if (AllowFlying || Flying) return;
        if (distance > 3)
        {
            int dmg = (int)Math.Ceiling(distance - 3 - (EffectLevel(Effect.JumpBoost) + 1));
            if (dmg > 0)
            {
                World.PlaySound(dmg > 4 ? "fall_big" : "fall_small", X, Y, Z);
                Attack(DamageSource.Fall, dmg);
            }
        }
        // deptanie pola
        int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y - 0.2), bz = MathUtil.FloorToInt(Z);
        if (World.GetBlock(bx, by, bz) == B.Farmland && distance > 0.5f && World.Rand.NextFloat() < distance - 0.5f && !World.IsRemote)
            World.SetBlock(bx, by, bz, B.Dirt);
    }

    // ---------- głód ----------
    public void AddExhaustion(float f) { if (!Creative && !Spectator) Exhaustion = Math.Min(40f, Exhaustion + f); }

    private void FoodTick()
    {
        if (Creative || Spectator) { FoodLevel = 20; return; }
        if (HungerEffect > 0) { HungerEffect--; AddExhaustion(0.025f); }
        if (RegenEffect > 0) { RegenEffect--; if (RegenEffect % 25 == 0) Heal(1); }
        var diff = World.Difficulty;
        if (Exhaustion > 4f)
        {
            Exhaustion -= 4f;
            if (Saturation > 0) Saturation = Math.Max(Saturation - 1f, 0);
            else if (diff != Difficulty.Peaceful) FoodLevel = Math.Max(FoodLevel - 1, 0);
        }
        if (diff == Difficulty.Peaceful)
        {
            if (Health < MaxHealth && World.TotalTime % 20 == 0) Heal(1);
            if (FoodLevel < 20 && World.TotalTime % 10 == 0) FoodLevel++;
        }
        if (World.Rules.NaturalRegeneration && FoodLevel >= 18 && Health > 0 && Health < MaxHealth)
        {
            FoodTimer++;
            if (FoodTimer >= 80) { Heal(1); AddExhaustion(3f); FoodTimer = 0; }
        }
        else if (FoodLevel <= 0)
        {
            FoodTimer++;
            if (FoodTimer >= 80)
            {
                if (Health > 10 || diff == Difficulty.Hard || Health > 1 && diff == Difficulty.Normal) Attack(DamageSource.Starve, 1);
                FoodTimer = 0;
            }
        }
        else FoodTimer = 0;
    }

    public bool CanEat(bool always) => always || Creative || FoodLevel < 20;

    public void Eat(Item food) => Eat(new ItemStack(food.Id));


    // ---------- używanie przedmiotów ----------
    public void StartUsing(ItemStack s)
    {
        if (s == null) return;
        ItemInUse = s; ItemInUseCount = s.Item.UseDuration;
    }

    public void StopUsing()
    {
        if (ItemInUse != null && ItemInUse.Id == I.Bow) ReleaseBow();
        ItemInUse = null; ItemInUseCount = 0;
    }

    private void UseItemTick()
    {
        if (ItemInUse == null) return;
        var held = Inventory.Selected;
        if (held != ItemInUse) { ItemInUse = null; ItemInUseCount = 0; return; }
        var act = ItemInUse.Item.Action;
        if (act == UseAction.Eat || act == UseAction.Drink)
        {
            if (ItemInUseCount <= 25 && ItemInUseCount % 4 == 0)
            {
                World.PlaySound(act == UseAction.Eat ? "eat" : "drink", X, Y, Z, 0.5f, 0.9f + World.Rand.NextFloat() * 0.2f);
                if (act == UseAction.Eat) World.SpawnParticles(ParticleKind.BlockBreak, X, Y + EyeHeight - 0.1, Z, 5, -ItemInUse.Id);
            }
            if (--ItemInUseCount <= 0) FinishUsing();
        }
        else if (act == UseAction.Bow) ItemInUseCount--;
    }

    private void FinishUsing()
    {
        var s = ItemInUse; ItemInUse = null; ItemInUseCount = 0;
        if (s == null) return;
        var it = s.Item;
        if (it.IsFood)
        {
            Eat(s);
            World.PlaySound("burp", X, Y, Z, 0.5f, 0.9f + World.Rand.NextFloat() * 0.1f);
            if (!Creative) { s.Count--; if (it.Id == I.MushroomStew) Inventory.Selected = new ItemStack(I.Bowl); else if (s.Count <= 0) Inventory.Selected = null; }
        }
        else if (it.Id == I.Potion) PlayerAlchemy.Drink(this, s);
        else if (it.Id == I.MilkBucket)
        {
            HungerEffect = 0; RegenEffect = 0; ClearEffects();
            if (!Creative) Inventory.Selected = new ItemStack(I.Bucket);
        }
        Inventory.Dirty = true;
    }

    public float BowCharge
    {
        get
        {
            if (ItemInUse == null || ItemInUse.Id != I.Bow) return 0;
            int used = 72000 - ItemInUseCount;
            float f = used / 20f;
            f = (f * f + f * 2f) / 3f;
            return Math.Min(1f, f);
        }
    }

    private void ReleaseBow()
    {
        float f = BowCharge;
        if (f < 0.1f) return;
        var bow = Inventory.Selected;
        bool infinite = Creative || EnchantHelper.GetLevel(bow, Ench.Infinity) > 0;
        bool hasArrow = infinite || Inventory.CountOf(I.Arrow) > 0;
        if (!hasArrow) return;
        var arrow = new ArrowEntity(World, this, f * 3f) { Critical = f >= 1f, PickupAllowed = !infinite };
        // ItemBow.onPlayerStoppedUsing: Moc, Uderzenie, Płomień
        int power = EnchantHelper.GetLevel(bow, Ench.Power), punch = EnchantHelper.GetLevel(bow, Ench.Punch);
        if (power > 0) arrow.Damage += power * 0.5 + 0.5;
        if (punch > 0) arrow.Knockback = punch;
        if (EnchantHelper.GetLevel(bow, Ench.Flame) > 0) arrow.SetFire(100);
        if (NetShootHook != null) NetShootHook(f);
        else World.SpawnEntity(arrow);
        World.PlaySound("bow", X, Y, Z, 1f, 1f / (World.Rand.NextFloat() * 0.4f + 1.2f) + f * 0.5f);
        DamageHeld(1);
        if (!infinite) Inventory.Consume(I.Arrow);
    }
    public Action<float> NetShootHook;

    public void DamageHeld(int amount)
    {
        if (Creative) return;
        var s = Inventory.Selected;
        if (s == null) return;
        if (s.DamageItem(amount) || s.Count <= 0)
        {
            Inventory.Selected = null;
            World.PlaySound("break", X, Y, Z, 0.8f, 0.8f + World.Rand.NextFloat() * 0.4f);
        }
        Inventory.Dirty = true;
    }

    // ---------- XP ----------
    public int XpBarCap => XpLevel >= 30 ? 112 + (XpLevel - 30) * 9 : XpLevel >= 15 ? 37 + (XpLevel - 15) * 5 : 7 + XpLevel * 2;
    public void AddXp(int amount)
    {
        XpTotal += amount;
        XpProgress += (float)amount / XpBarCap;
        while (XpProgress >= 1f)
        {
            XpProgress = (XpProgress - 1f) * XpBarCap;
            XpLevel++;
            XpProgress /= XpBarCap;
            if (XpLevel % 5 == 0) World.PlaySound("levelup", X, Y, Z, 0.75f, 1f);
        }
    }

    public void OnCrafted(ItemStack s) { }

    // ---------- interakcje ----------
    public void SendMessage(string msg) => Handler?.OnMessage(msg);

    public void OpenBlockGui(GuiKind kind, int x, int y, int z) => Handler?.OpenBlockGui(kind, x, y, z);

    public void CloseContainer()
    {
        if (OpenContainer is ChestContainer) World.PlaySound("random.chestclosed", X, Y, Z, 0.5f, World.Rand.NextFloat() * 0.1f + 0.9f);
        if (OpenContainer != null) { OpenContainer.OnClose(this); OpenContainer = null; }
        else InventoryContainer.OnClose(this);
    }

    public void DropStack(ItemStack s, bool fromCursor)
    {
        if (s == null || s.Count <= 0) return;
        if (World.IsRemote) { NetDropHook?.Invoke(s); return; }
        var e = new ItemEntity(World, X, Y + EyeHeight - 0.3, Z, s.Copy());
        e.PickupDelay = 40;
        float yr = Yaw * MathUtil.Deg2Rad, pr = Pitch * MathUtil.Deg2Rad;
        float f = 0.3f;
        e.MotionX = -MathF.Sin(yr) * MathF.Cos(pr) * f;
        e.MotionZ = MathF.Cos(yr) * MathF.Cos(pr) * f;
        e.MotionY = -MathF.Sin(pr) * f + 0.1f;
        float ang = World.Rand.NextFloat() * MathF.PI * 2f, sp = 0.02f * World.Rand.NextFloat();
        e.MotionX += MathF.Cos(ang) * sp; e.MotionY += (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.1f; e.MotionZ += MathF.Sin(ang) * sp;
        World.SpawnEntity(e);
    }
    public Action<ItemStack> NetDropHook;
    public Action NetTeleportHook;
    public Action NetGameModeHook;
    public bool IsLocalOp;

    public void StartSleeping(int x, int y, int z)
    {
        Sleeping = true; SleepTimer = 0; BedX = x; BedY = y; BedZ = z;
        SetPositionAndPrev(x + 0.5, y + 0.5625, z + 0.5);
        MotionX = MotionY = MotionZ = 0;
    }
    public void WakeUp()
    {
        if (!Sleeping) return;
        Sleeping = false;
        SleepTimer = 0;
        SetPositionAndPrev(BedX + 0.5, BedY + 1.0, BedZ + 0.5);
        // znajdź wolne miejsce obok łóżka
        for (int r = 0; r <= 2; r++)
            for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++)
                {
                    int x = BedX + dx, z = BedZ + dz;
                    if (Block.Get(World.GetBlock(x, BedY, z)).Solid && World.GetBlock(x, BedY, z) != B.Bed) continue;
                    if (World.GetBlock(x, BedY, z) == B.Bed) continue;
                    if (!Block.Get(World.GetBlock(x, BedY + 1, z)).Solid && Block.Get(World.GetBlock(x, BedY - 1, z)).Solid)
                    { SetPositionAndPrev(x + 0.5, BedY, z + 0.5); return; }
                }
    }

    public override void OnDeath(DamageSource src)
    {
        if (Dead) return;
        base.OnDeath(src);
        DeathMessage = src.DeathMessage(Name);
        if (!World.IsRemote)
        {
            if (!World.Rules.KeepInventory)
            {
                for (int i = 0; i < 40; i++)
                {
                    var s = Inventory.Get(i);
                    if (s == null) continue;
                    var e = new ItemEntity(World, X, Y + 1, Z, s);
                    float f = World.Rand.NextFloat() * 0.5f, a = World.Rand.NextFloat() * MathF.PI * 2;
                    e.MotionX = -MathF.Sin(a) * f; e.MotionZ = MathF.Cos(a) * f; e.MotionY = 0.2;
                    e.PickupDelay = 40;
                    World.SpawnEntity(e);
                }
                Inventory.Clear();
                if (Cursor != null) { DropStack(Cursor, false); Cursor = null; }
                int xp = Math.Min(XpLevel * 7, 100);
                if (xp > 0) World.SpawnXp(X, Y + 0.5, Z, xp);
                XpLevel = 0; XpProgress = 0; XpTotal = 0;
            }
            World.BroadcastMessage(DeathMessage);
        }
        Handler?.OnDeath(DeathMessage);
    }

    public void Respawn()
    {
        if (!World.Provider.CanRespawn && World.Dimensions != null)
        {
            var home = World.Dimensions.Get(DimensionId.Overworld);
            World.Dimensions.Transfer(this, DimensionId.Overworld, home.SpawnX + .5, home.SpawnY, home.SpawnZ + .5);
        }
        ClearEffects(); Absorption = 0;
        Dead = false; DeathTime = 0; Health = MaxHealth; FoodLevel = 20; Saturation = 5; Exhaustion = 0; Air = 300; FireTicks = 0;
        HungerEffect = 0; RegenEffect = 0; HurtTime = 0; FallDistance = 0;
        MotionX = MotionY = MotionZ = 0;
        int sx = HasSpawn ? SpawnX : World.SpawnX, sy = HasSpawn ? SpawnY : World.SpawnY, sz = HasSpawn ? SpawnZ : World.SpawnZ;
        if (HasSpawn && World.GetBlock(SpawnX, SpawnY - 1, SpawnZ) != B.Bed && false) HasSpawn = false;
        SetPositionAndPrev(sx + 0.5, sy, sz + 0.5);
        Removed = false;
    }

    protected override void OnArmorDamaged(float amount) { Inventory.DamageArmor(amount); }

    protected override void DamageEntity(DamageSource src, float amount)
    {
        if (Sleeping) WakeUp();
        AddExhaustion(0.3f);
        base.DamageEntity(src, amount);
    }

    /// <summary>Atak wręcz na encję.</summary>
    public void AttackEntity(Entity target)
    {
        if (target is not LivingEntity && target.AttackedByPlayer(this)) return;
        if (target is not LivingEntity) { target.Attack(DamageSource.PlayerAttack(this), Inventory.Selected?.Item?.AttackDamage ?? 1f); return; }
        if (target is not LivingEntity le || le.Dead) return;
        var held = Inventory.Selected;
        float dmg = EffectLogic.Melee(this, held?.Item?.AttackDamage ?? 1f);
        // attackTargetEntityWithCurrentItem 1.8: premia zaklęcia po mnożniku krytyka, Odrzut i Zaklęty ogień z przedmiotu
        float bonus = EnchantEffects.DamageBonus(held, EnchantEffects.CreatureOf(le));
        int kb = EnchantHelper.GetLevel(held, Ench.Knockback) + (Sprinting ? 1 : 0);
        if (dmg <= 0 && bonus <= 0) return;
        // 1.8: krytyk bez warunku sprintu i prędkości pionowej (doszły w 1.9)
        bool crit = FallDistance > 0 && !OnGround && !IsOnLadder() && !InWater && !HasEffect(Effect.Blindness) && RidingEntity == null;
        if (crit && dmg > 0) dmg *= 1.5f;
        dmg += bonus;
        int fireAspect = EnchantHelper.GetLevel(held, Ench.FireAspect);
        bool tempFire = false;
        if (fireAspect > 0 && le.FireTicks <= 0) { tempFire = true; le.SetFire(1); }
        bool hit = le.Attack(DamageSource.PlayerAttack(this), dmg);
        if (!hit && tempFire) le.FireTicks = 0;
        if (hit)
        {
            if (bonus > 0) World.SpawnParticles(ParticleKind.Crit, le.X, le.Y + le.Height / 2, le.Z, 10);
            EnchantEffects.ApplyArthropod(this, le);
            if (fireAspect > 0) le.SetFire(fireAspect * 4);
            else if (tempFire) le.FireTicks = 0;
            if (kb > 0)
            {
                float yr = Yaw * MathUtil.Deg2Rad;
                le.MotionX += -MathF.Sin(yr) * kb * 0.5f; le.MotionY += 0.1; le.MotionZ += MathF.Cos(yr) * kb * 0.5f;
                MotionX *= 0.6; MotionZ *= 0.6; Sprinting = false;
            }
            if (crit) World.SpawnParticles(ParticleKind.Crit, le.X, le.Y + le.Height / 2, le.Z, 10);
            if (held != null && held.Item?.IsTool == true) DamageHeld(held.Item.Tool == ToolType.Sword ? 1 : 2);
            AddExhaustion(0.3f);
        }
    }

    public PlayerData ToData()
    {
        var d = new PlayerData
        {
            Dimension = (int)World.Dimension,
            Name = Name, X = X, Y = Y, Z = Z, Yaw = Yaw, Pitch = Pitch, Health = Health, Food = FoodLevel, Saturation = Saturation, Exhaustion = Exhaustion,
            GameMode = GameMode, Flying = Flying, XpLevel = XpLevel, XpProgress = XpProgress, XpTotal = XpTotal, Selected = Inventory.SelectedSlot, Air = Air,
            HasSpawn = HasSpawn, SpawnX = SpawnX, SpawnY = SpawnY, SpawnZ = SpawnZ, Dead = Dead, RidingVehicleId = RidingEntity?.SaveId ?? PendingVehicleId
        };
        for (int i = 0; i < 40; i++) { var s = Inventory.Get(i); if (s != null) d.Inventory.Add(SlotData.From(i, s)); }
        SaveMechanics(d);
        return d;
    }

    public void FromData(PlayerData d)
    {
        if (RidingEntity != null) Mount(null);
        PendingVehicleId = d.RidingVehicleId;
        SetPositionAndPrev(d.X, d.Y, d.Z); Yaw = PrevYaw = d.Yaw; Pitch = PrevPitch = d.Pitch;
        Health = d.Health; FoodLevel = d.Food; Saturation = d.Saturation; Exhaustion = d.Exhaustion;
        SetGameMode(d.GameMode); Flying = d.Flying && AllowFlying; XpLevel = d.XpLevel; XpProgress = d.XpProgress; XpTotal = d.XpTotal;
        Inventory.SelectedSlot = Math.Clamp(d.Selected, 0, 8); Air = d.Air;
        HasSpawn = d.HasSpawn; SpawnX = d.SpawnX; SpawnY = d.SpawnY; SpawnZ = d.SpawnZ;
        Inventory.Clear();
        foreach (var s in d.Inventory) if (Item.ById[s.Id] != null && s.Slot >= 0 && s.Slot < 40) Inventory.Set(s.Slot, s.ToStack());
        LoadMechanics(d);
        if (d.Dead || Health <= 0) { Health = 0; }
    }
}
