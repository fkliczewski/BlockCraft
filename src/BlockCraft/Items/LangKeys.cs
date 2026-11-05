using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.Items;

/// <summary>Nazwy przedmiotów z plików językowych MC 1.8 (lang/*.lang z zasobów i paczek): przedmiot gry -> klucz tłumaczenia.
/// Bez wybranego języka albo bez klucza zostaje polska nazwa gry.</summary>
public static partial class LangKeys
{
    private static Dictionary<string, string> _byItem;

    /// <summary>"klucz_przedmiotu:meta" -> klucz tłumaczenia (np. "wool:14" -> "tile.cloth.red.name").</summary>
    public static IReadOnlyDictionary<string, string> ByItem => _byItem ??= Table
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(l => l.Split('=', 2))
        .Where(p => p.Length == 2)
        .ToDictionary(p => p[0], p => p[1]);

    /// <summary>Nazwy efektów mikstur w 1.8 (potion.NAZWA.postfix) według identyfikatora efektu.</summary>
    private static readonly string[] EffectNames =
    {
        null, "moveSpeed", "moveSlowdown", "digSpeed", "digSlowDown", "damageBoost", "heal", "harm", "jump", "confusion", "regeneration",
        "resistance", "fireResistance", "waterBreathing", "invisibility", "blindness", "nightVision", "hunger", "weakness", "poison",
        "wither", "healthBoost", "absorption", "saturation",
    };

    /// <summary>Nazwy encji 1.8 (entity.NAZWA.name) dla jajek przywołujących, według identyfikatora jak w EntityList.</summary>
    private static readonly Dictionary<int, string> EntityNames = new()
    {
        [50] = "Creeper", [51] = "Skeleton", [52] = "Spider", [54] = "Zombie", [55] = "Slime", [56] = "Ghast", [57] = "PigZombie",
        [58] = "Enderman", [59] = "CaveSpider", [60] = "Silverfish", [61] = "Blaze", [62] = "LavaSlime", [65] = "Bat", [66] = "Witch",
        [67] = "Endermite", [68] = "Guardian", [90] = "Pig", [91] = "Sheep", [92] = "Cow", [93] = "Chicken", [94] = "Squid", [95] = "Wolf",
        [96] = "MushroomCow", [98] = "Ozelot", [100] = "EntityHorse", [101] = "Rabbit", [120] = "Villager",
    };

    /// <summary>Nazwa w wybranym języku albo null (nazwa gry).</summary>
    public static string Name(ItemStack s)
    {
        if (!Lang.Active || s.Item == null) return null;
        return s.Item.TranslatedNameFn != null ? s.Item.TranslatedNameFn(s) : VariantName(s, s.Damage);
    }

    public static string VariantName(ItemStack stack, int variant)
    {
        string itemTranslationKey = stack.Item.Id switch
        {
            I.Bed => "item.bed.name",
            I.CakeItem => "item.cake.name",
            I.CauldronItem => "item.cauldron.name",
            I.NetherWartItem => "item.netherStalkSeeds.name",
            _ => null,
        };
        if (itemTranslationKey != null) return Lang.Get(itemTranslationKey)?.Trim();
        string key = stack.Item.Key;
        return ByItem.TryGetValue(key + ":" + variant, out var translationKey)
            || (stack.Item.GetVariants().Length <= 1 && ByItem.TryGetValue(key + ":0", out translationKey))
            ? Lang.Get(translationKey)?.Trim() : null;
    }

    public static string WrittenBookName(ItemStack stack) => Books.Title(stack) is { Length: > 0 }
        ? null : Lang.Get("item.writtenBook.name")?.Trim();

    public static string SkullName(ItemStack stack) => stack.Damage == 3
        ? PlayerHeadName(stack) : VariantName(stack, stack.Damage);

    public static string SpawnEggName(ItemStack stack)
    {
        string egg = Lang.Get("item.monsterPlacer.name");
        if (egg == null) return null;
        return EntityNames.TryGetValue(stack.Damage, out var entity)
            && Lang.Get("entity." + entity + ".name") is { } name ? egg.Trim() + " " + name : null;
    }

    private static string PlayerHeadName(ItemStack stack)
    {
        string owner = stack.Tag?.GetCompound("SkullOwner")?.GetString("Name") ?? stack.Tag?.GetString("SkullOwner");
        if (string.IsNullOrEmpty(owner)) return Lang.Get("item.skull.char.name")?.Trim();
        return Lang.Get("item.skull.player.name")?.Replace("%s", owner).Replace("%1$s", owner).Trim();
    }

    /// <summary>ItemPotion.getItemStackDisplayName 1.8: butelka wody, przedrostek mikstury rozpryskowej, nazwa pierwszego efektu.
    /// Mikstury bez efektów (niezręczna, gęsta...) zostają z nazwą gry.</summary>
    public static string PotionName(ItemStack s)
    {
        if (s.Damage == 0) return Lang.Get("item.emptyPotion.name")?.Trim();
        var effects = Potions.Effects(s);
        if (effects.Count == 0) return null;
        int id = (int)effects[0].Id;
        if (id <= 0 || id >= EffectNames.Length || Lang.Get("potion." + EffectNames[id] + ".postfix") is not { } effect) return null;
        if (!Potions.Splash(s.Damage)) return effect.Trim();
        return Lang.Get("potion.prefix.grenade") is { } splash ? splash.Trim() + " " + effect.Trim() : null;
    }
}
