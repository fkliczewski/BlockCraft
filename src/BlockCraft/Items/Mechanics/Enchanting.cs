using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items
{
    /// <summary>
    /// ContainerEnchantment z 1.8: slot przedmiotu (limit 1), slot lazurytu, trzy oferty z ziarna gracza i biblioteczek.
    /// Oferty liczą obie strony tak samo (ziarno przychodzi w statystykach), zaklinanie wykonuje wyłącznie host.
    /// </summary>
    public sealed class EnchantingContainer : Container
    {
        public readonly SimpleInventory Inv = new(2);
        public readonly World.World World;
        public readonly int X, Y, Z;
        private readonly Player _player;
        private EnchantOffer[] _offers = Empty();
        private (int id, int damage, int count, bool enchanted, int seed)? _key;

        public EnchantingContainer(Player player, World.World world, int x, int y, int z)
        {
            World = world; X = x; Y = y; Z = z; _player = player; var inv = player.Inventory; Title = "Zaklinanie";
            Slots.Add(new FilterSlot(Inv, 0, 15, 47, _ => true, 1));
            Slots.Add(new FilterSlot(Inv, 1, 35, 47, s => s.Id == I.Lapis));
            AddPlayerSlots(inv, 84, 142);
        }

        private static EnchantOffer[] Empty() => new EnchantOffer[] { new(0, 1, Array.Empty<EnchantmentRoll>(), null), new(0, 2, Array.Empty<EnchantmentRoll>(), null), new(0, 3, Array.Empty<EnchantmentRoll>(), null) };

        public int Bookshelves { get; private set; }
        public int Lapis => Inv.Get(1)?.Count ?? 0;

        /// <summary>Jak onCraftMatrixChanged: liczone ponownie tylko po zmianie przedmiotu albo ziarna (biblioteczki też tylko wtedy).</summary>
        public EnchantOffer[] Offers
        {
            get
            {
                var s = Inv.Get(0);
                var key = s == null ? ((int, int, int, bool, int)?)null : (s.Id, s.Damage, s.Count, EnchantHelper.IsEnchanted(s), _player.EnchantmentSeed);
                if (key != _key)
                {
                    _key = key;
                    if (s != null && EnchantingRules.CanUseTable(s))
                    {
                        Bookshelves = EnchantingRules.BookshelfPower(World, X, Y, Z);
                        _offers = EnchantingRules.Offers(s, _player.EnchantmentSeed, Bookshelves);
                    }
                    else _offers = Empty();
                }
                return _offers;
            }
        }

        /// <summary>Stan przycisku jak w GuiEnchantment: 0 brak oferty, 1 nie stać, 2 dostępna.</summary>
        public int ButtonState(int slot)
        {
            var o = Offers[slot];
            if (o.RequiredLevel <= 0 || Inv.Get(0) == null) return 0;
            if (_player.Creative) return 2;
            return Lapis < slot + 1 || _player.XpLevel < o.RequiredLevel ? 1 : 2;
        }

        /// <summary>ContainerEnchantment.enchantItem (po stronie hosta).</summary>
        public bool Enchant(Player p, int slot)
        {
            if ((uint)slot > 2 || p.World.IsRemote) return false;
            var stack = Inv.Get(0); var lapis = Inv.Get(1); int cost = slot + 1;
            if ((lapis == null || lapis.Count < cost) && !p.Creative) return false;
            var offer = Offers[slot];
            if (offer.RequiredLevel <= 0 || stack == null || !(p.XpLevel >= cost && p.XpLevel >= offer.RequiredLevel || p.Creative)) return false;
            var list = offer.Enchantments;
            if (list.Count == 0) return true;
            p.RemoveExperienceLevels(cost);
            bool book = stack.Id == I.Book;
            if (book) stack.Id = I.EnchantedBook;
            foreach (var e in list)
            {
                if (book) EnchantingRules.AddStored(stack, e);
                else EnchantingRules.AddEnchantment(stack, e);
            }
            if (!p.Creative) { lapis.Count -= cost; if (lapis.Count <= 0) Inv.Set(1, null); }
            p.Statistics["stat.itemEnchanted"] = p.Statistics.GetValueOrDefault("stat.itemEnchanted") + 1;
            Inv.Set(0, stack); Inv.Changed(); _key = null;
            return true;
        }

        protected override bool TransferFromPlayer(ItemStack s)
        {
            if (s.Id == I.Lapis) { MergeStack(s, 1, 2, true); return true; }
            if (Inv.Get(0) != null || s.Count < 1) return true;
            // ContainerEnchantment.transferStackInSlot: do slotu przedmiotu trafia jedna sztuka
            var one = s.CopyWith(1); s.Count--; Inv.Set(0, one); Inv.Changed();
            return true;
        }

        public override void OnClose(Player p)
        {
            base.OnClose(p);
            if (p.World.IsRemote) return;
            for (int i = 0; i < 2; i++) if (Inv.Get(i) is { } s) { Inv.Set(i, null); if (!p.Inventory.Add(s)) p.DropStack(s, false); }
        }

        public bool StillValid(Player p) => World.GetBlock(X, Y, Z) == B.EnchantingTable && p.DistanceSq(X + .5, Y + .5, Z + .5) <= 64;
    }

    public static partial class EnchantingRules
    {
        /// <summary>ItemStack.addEnchantment: dopisuje wpis (bez scalania, jak 1.8).</summary>
        public static void AddEnchantment(ItemStack s, EnchantmentRoll e)
            => s.GetOrCreateTag().GetOrCreateList("ench").Add(new Nbt { ["id"] = (int)e.Id, ["lvl"] = e.Level });

        /// <summary>ItemEnchantedBook.addEnchantment: ten sam typ zastępuje niższy poziom, inaczej dopisuje.</summary>
        public static void AddStored(ItemStack s, EnchantmentRoll e)
        {
            var list = s.GetOrCreateTag().GetOrCreateList("StoredEnchantments");
            foreach (var o in list)
                if (o is Nbt n && n.GetInt("id") == (int)e.Id)
                {
                    if (n.GetInt("lvl") < e.Level) n["lvl"] = e.Level;
                    return;
                }
            list.Add(new Nbt { ["id"] = (int)e.Id, ["lvl"] = e.Level });
        }
    }
}

namespace BlockCraft.Entities
{
    public partial class Player
    {
        /// <summary>EntityPlayer.removeExperienceLevel z 1.8: zabiera poziomy i losuje nowe ziarno zaklinania.</summary>
        public void RemoveExperienceLevels(int levels)
        {
            XpLevel -= levels;
            if (XpLevel < 0) { XpLevel = 0; XpProgress = 0; XpTotal = 0; }
            EnchantmentSeed = Random.Shared.Next(int.MinValue, int.MaxValue);
        }
    }
}
