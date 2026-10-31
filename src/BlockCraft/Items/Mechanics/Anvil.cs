using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items
{
    /// <summary>Wynik ContainerRepair.updateRepairOutput z 1.8.</summary>
    public sealed record AnvilResult(ItemStack Output, int Cost, int MaterialCost);

    /// <summary>
    /// Reguły kowadła 1:1 z ContainerRepair 1.8 (naprawa materiałem, łączenie, zaklęte książki, nazwy,
    /// RepairCost 2n+1, limit 40 "Zbyt drogie!"). Niezależne od GUI, liczone tak samo u hosta i gościa.
    /// </summary>
    public static class AnvilRules
    {
        public const int MaxNameLength = 30;

        public static int RepairCost(ItemStack s) => s?.Tag != null && s.Tag.ContainsKey("RepairCost") ? s.Tag.GetInt("RepairCost") : 0;
        public static void SetRepairCost(ItemStack s, int cost) => s.GetOrCreateTag()["RepairCost"] = cost;
        public static bool HasCustomName(ItemStack s) => s?.CustomName != null;

        public static void SetCustomName(ItemStack s, string name) => s.GetOrCreateTag().GetOrCreateCompound("display")["Name"] = name;
        public static void ClearCustomName(ItemStack s)
        {
            var d = s.Tag?.GetCompound("display"); if (d == null) return;
            d.Remove("Name");
            if (d.Count == 0) s.Tag.Remove("display");
            if (s.Tag.Count == 0) s.Tag = null;
        }

        private static bool Damageable(ItemStack s) => (s.Item?.MaxDamage ?? 0) > 0;

        /// <summary>Item.getIsRepairable: materiał narzędzia albo zbroi.</summary>
        public static bool IsRepairMaterial(ItemStack tool, ItemStack mat)
        {
            if (tool?.Item == null || mat == null) return false;
            int id = tool.Id; var it = tool.Item;
            if (it.ArmorSlot >= 0)
                return it.ArmorMaterial switch { 0 => mat.Id == I.Leather, 1 or 4 => mat.Id == I.IronIngot, 2 => mat.Id == I.GoldIngot, 3 => mat.Id == I.Diamond, _ => false };
            return id switch
            {
                >= I.WoodPickaxe and <= I.WoodHoe => mat.Id is B.OakPlanks or B.BirchPlanks or B.SprucePlanks or B.JunglePlanks or B.AcaciaPlanks or B.DarkOakPlanks,
                >= I.StonePickaxe and <= I.StoneHoe => mat.Id == B.Cobblestone,
                >= I.IronPickaxe and <= I.IronHoe => mat.Id == I.IronIngot,
                >= I.GoldPickaxe and <= I.GoldHoe => mat.Id == I.GoldIngot,
                >= I.DiamondPickaxe and <= I.DiamondHoe => mat.Id == I.Diamond,
                _ => false
            };
        }

        /// <summary>EnchantmentHelper.getEnchantments: lista "ench" albo StoredEnchantments książki, w kolejności zapisu (LinkedHashMap).</summary>
        public static List<(Ench id, int lvl)> Enchantments(ItemStack s)
        {
            var res = new List<(Ench, int)>();
            var l = s?.Tag?.GetList(s.Id == I.EnchantedBook ? "StoredEnchantments" : "ench");
            if (l != null) foreach (var o in l) if (o is Nbt n) Put(res, (Ench)n.GetInt("id"), n.GetInt("lvl"));
            return res;
        }

        private static void Put(List<(Ench id, int lvl)> map, Ench id, int lvl)
        {
            int i = map.FindIndex(e => e.id == id);
            if (i >= 0) map[i] = (id, lvl); else map.Add((id, lvl));
        }

        /// <summary>EnchantmentHelper.setEnchantments.</summary>
        public static void SetEnchantments(List<(Ench id, int lvl)> map, ItemStack s)
        {
            if (s.Id == I.EnchantedBook)
            {
                foreach (var (id, lvl) in map) EnchantingRules.AddStored(s, new EnchantmentRoll(id, lvl));
                return;
            }
            if (map.Count > 0)
            {
                var list = new List<object>();
                foreach (var (id, lvl) in map) list.Add(new Nbt { ["id"] = (int)id, ["lvl"] = lvl });
                s.GetOrCreateTag()["ench"] = list;
            }
            else if (s.Tag != null) s.Tag.Remove("ench");
        }

        /// <summary>Koszt zaklęcia za poziom w zależności od wagi (rzadkości) w 1.8.</summary>
        private static int WeightCost(Ench id) => EnchantingRules.Weight(id) switch { 1 => 8, 2 => 4, 5 => 2, 10 => 1, _ => 0 };

        public static AnvilResult Compute(ItemStack left, ItemStack right, string name, bool creative)
        {
            if (left == null) return new AnvilResult(null, 0, 0);
            var output = left.Copy();
            var map = Enchantments(output);
            int cost = 0, repairCostSum = RepairCost(left) + (right == null ? 0 : RepairCost(right)), renameCost = 0, materialCost = 0;
            bool book = false;
            if (right != null)
            {
                book = right.Id == I.EnchantedBook && (right.Tag?.GetList("StoredEnchantments")?.Count ?? 0) > 0;
                if (Damageable(output) && IsRepairMaterial(left, right))
                {
                    int max = output.Item.MaxDamage;
                    int fix = Math.Min(output.Damage, max / 4);
                    if (fix <= 0) return new AnvilResult(null, 0, 0);
                    int used;
                    for (used = 0; fix > 0 && used < right.Count; used++)
                    {
                        output.Damage -= fix;
                        cost++;
                        fix = Math.Min(output.Damage, max / 4);
                    }
                    materialCost = used;
                }
                else
                {
                    if (!book && (output.Id != right.Id || !Damageable(output))) return new AnvilResult(null, 0, 0);
                    if (Damageable(output) && !book)
                    {
                        int max = output.Item.MaxDamage;
                        int leftLeft = max - left.Damage, rightLeft = right.Item.MaxDamage - right.Damage;
                        int newDamage = Math.Max(0, max - (leftLeft + rightLeft + max * 12 / 100));
                        if (newDamage < output.Damage) { output.Damage = newDamage; cost += 2; }
                    }
                    foreach (var (id, rightLvl) in Enchantments(right))
                    {
                        int have = map.FirstOrDefault(e => e.id == id).lvl;
                        int lvl = have == rightLvl ? rightLvl + 1 : Math.Max(rightLvl, have);
                        bool ok = EnchantingRules.CanApply(id, left, false) || creative || left.Id == I.EnchantedBook;
                        foreach (var (other, _) in map)
                            if (other != id && !EnchantingRules.Compatible(id, other)) { ok = false; cost++; }
                        if (!ok) continue;
                        lvl = Math.Min(lvl, EnchantHelper.MaxLevel(id));
                        Put(map, id, lvl);
                        int w = WeightCost(id);
                        if (book) w = Math.Max(1, w / 2);
                        cost += w * lvl;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                if (HasCustomName(left)) { renameCost = 1; cost += renameCost; ClearCustomName(output); }
            }
            else if (name != left.Name)
            {
                renameCost = 1; cost += renameCost;
                SetCustomName(output, name);
            }
            int total = repairCostSum + cost;
            if (cost <= 0) output = null;
            if (renameCost == cost && renameCost > 0 && total >= 40) total = 39;
            if (total >= 40 && !creative) output = null;
            if (output != null)
            {
                int rc = RepairCost(output);
                if (right != null && rc < RepairCost(right)) rc = RepairCost(right);
                SetRepairCost(output, rc * 2 + 1);
                SetEnchantments(map, output);
            }
            return new AnvilResult(output, total, materialCost);
        }

        /// <summary>ChatAllowedCharacters.filterAllowedCharacters + limit 30 (C17PacketCustomPayload MC|ItemName).</summary>
        public static string FilterName(string s)
        {
            if (s == null) return "";
            var t = new string(s.Where(c => c != '§' && c >= ' ' && c != 127).ToArray());
            return t.Length > MaxNameLength ? t[..MaxNameLength] : t;
        }
    }

    /// <summary>Slot wyniku kowadła (SlotRepair 1.8).</summary>
    public sealed class AnvilOutputSlot : Slot
    {
        private readonly AnvilContainer _c;
        public AnvilOutputSlot(AnvilContainer c, IInventory inv, int x, int y) : base(inv, 0, x, y) { _c = c; IsOutput = true; }
        public override bool CanTake(Player p) => (p.Creative || p.XpLevel >= _c.MaximumCost) && _c.MaximumCost > 0 && Stack != null;
        public override void OnTake(Player p, ItemStack taken) => _c.OnOutputTaken(p);
    }

    /// <summary>ContainerRepair z 1.8: dwa wejścia, wynik, nazwa, koszt w poziomach, uszkadzanie kowadła (12%).</summary>
    public sealed class AnvilContainer : Container
    {
        public readonly SimpleInventory Inv = new(2), Output = new(1);
        public readonly World.World World;
        public readonly int X, Y, Z;
        private readonly Player _player;
        public int MaximumCost { get; private set; }
        public int MaterialCost { get; private set; }
        public string RepairedName { get; private set; } = "";

        public AnvilContainer(Player player, World.World world, int x, int y, int z)
        {
            World = world; X = x; Y = y; Z = z; _player = player; Title = "Napraw i nazwij";
            Slots.Add(new Slot(Inv, 0, 27, 47));
            Slots.Add(new Slot(Inv, 1, 76, 47));
            Slots.Add(new AnvilOutputSlot(this, Output, 134, 47));
            AddPlayerSlots(player.Inventory, 84, 142);
            Inv.OnChanged = UpdateOutput;
        }

        public void UpdateOutput()
        {
            var r = AnvilRules.Compute(Inv.Get(0), Inv.Get(1), RepairedName, _player.Creative);
            Output.Set(0, r.Output);
            MaximumCost = r.Cost;
            MaterialCost = r.MaterialCost;
        }

        /// <summary>ContainerRepair.updateItemName.</summary>
        public void SetName(string name)
        {
            RepairedName = AnvilRules.FilterName(name);
            UpdateOutput();
        }

        internal void OnOutputTaken(Player p)
        {
            if (!p.Creative) p.AddExperienceLevel(-MaximumCost);
            Inv.Set(0, null);
            var mat = Inv.Get(1);
            if (MaterialCost > 0 && mat != null && mat.Count > MaterialCost) mat.Count -= MaterialCost;
            else Inv.Set(1, null);
            MaximumCost = 0;
            if (!World.IsRemote)
            {
                int meta = World.GetMeta(X, Y, Z);
                if (!p.Creative && World.GetBlock(X, Y, Z) == B.Anvil && World.Rand.NextFloat() < .12f)
                {
                    int dmg = (meta >> 2 & 3) + 1;
                    if (dmg > 2) { World.SetBlock(X, Y, Z, B.Air); World.PlaySound("random.anvil_break", X + .5, Y + .5, Z + .5, 1f, World.Rand.NextFloat() * .1f + .9f); }
                    else { World.SetBlock(X, Y, Z, B.Anvil, meta & 3 | dmg << 2); World.PlaySound("random.anvil_use", X + .5, Y + .5, Z + .5, 1f, World.Rand.NextFloat() * .1f + .9f); }
                }
                else World.PlaySound("random.anvil_use", X + .5, Y + .5, Z + .5, 1f, World.Rand.NextFloat() * .1f + .9f);
            }
            UpdateOutput();
        }

        protected override bool TransferFromPlayer(ItemStack s) => MergeStack(s, 0, 2, false);

        public override void OnClose(Player p)
        {
            base.OnClose(p);
            if (p.World.IsRemote) return;
            for (int i = 0; i < 2; i++) if (Inv.Get(i) is { } s) { Inv.Set(i, null); if (!p.Inventory.Add(s)) p.DropStack(s, false); }
            Output.Set(0, null);
        }

        public bool StillValid(Player p) => World.GetBlock(X, Y, Z) == B.Anvil && p.DistanceSq(X + .5, Y + .5, Z + .5) <= 64;
    }
}

namespace BlockCraft.Entities
{
    public partial class Player
    {
        /// <summary>EntityPlayer.addExperienceLevel z 1.8 (kowadło; nie losuje nowego ziarna zaklinania).</summary>
        public void AddExperienceLevel(int levels)
        {
            XpLevel += levels;
            if (XpLevel < 0) { XpLevel = 0; XpProgress = 0; XpTotal = 0; }
        }
    }
}
