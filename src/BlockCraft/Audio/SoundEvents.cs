using BlockCraft.World;

namespace BlockCraft.Audio;

/// <summary>
/// Nazwy zdarzeń dźwiękowych MC 1.8 używane przez grę (klucze sounds.json). Inne moduły wołają
/// <c>World.PlaySound(nazwa, x, y, z, vol, pitch)</c> albo <c>Sound.Play(...)</c> z takimi nazwami.
///
/// Bloki:      dig.stone dig.wood dig.gravel dig.grass dig.sand dig.glass dig.cloth dig.snow
///             step.stone step.wood step.gravel step.grass step.sand step.cloth step.snow step.ladder
/// Interakcje: random.click random.door_open random.door_close random.chestopen random.chestclosed
///             random.bow random.bowhit random.eat random.burp random.drink random.pop random.orb random.levelup
///             random.break random.explode random.fizz random.anvil_land random.anvil_use random.anvil_break
///             random.splash random.successful_hit random.wood_click
/// Tłok/TNT:   tile.piston.in tile.piston.out game.tnt.primed fire.fire fire.ignite
/// Płyny:      liquid.water liquid.lava liquid.lavapop game.neutral.swim game.neutral.swim.splash game.player.swim
/// Gracz:      game.player.hurt game.player.die game.player.hurt.fall.small game.player.hurt.fall.big (analogicznie game.neutral.* i game.hostile.*)
/// Ambient:    ambient.cave.cave ambient.weather.rain ambient.weather.thunder ambient.weather.rain
/// Portale:    portal.portal portal.travel portal.trigger
/// Moby:       mob.&lt;nazwa&gt;.say / .hurt / .death / .step / .idle / .hit  (cow pig sheep chicken horse wolf cat villager
///             zombie skeleton creeper spider ghast blaze enderdragon endermen silverfish slime magmacube bat irongolem
///             zombiepig wither guardian rabbit ...), np. mob.zombie.say, mob.creeper.say, mob.sheep.shear, mob.chicken.plop
/// Muzyka:     music.menu music.game music.game.creative music.game.nether music.game.end music.game.end.dragon
///             music.game.end.credits  (przez <c>Music.SetContext</c>)
/// Płyty:      records.13 records.cat records.blocks records.chirp records.far records.mall records.mellohi
///             records.stal records.strad records.ward records.11 records.wait  (przez <c>Sound.PlayRecord</c>)
/// UI:         gui.button.press (przez <c>Sound.PlayUi</c>)
///
/// Dla zgodności ze starym kodem działają też aliasy (tabela Alias) oraz wzorce step_*, dig_*, place_*.
/// </summary>
public static class SoundEvents
{
    /// <summary>SoundType bloku na rodzinę dźwięków MC (stone, wood, gravel, grass, sand, glass, cloth, ladder).</summary>
    public static string Family(SoundType t) => t switch
    {
        SoundType.Stone => "stone", SoundType.Wood => "wood", SoundType.Gravel => "gravel", SoundType.Grass => "grass",
        SoundType.Sand => "sand", SoundType.Glass => "stone", SoundType.Wool => "cloth", SoundType.Metal => "stone",
        SoundType.Ladder => "ladder", _ => "stone"
    };
    /// <summary>Zdarzenie kopania/niszczenia/stawiania (Glass ma własne dig.glass).</summary>
    public static string Dig(SoundType t) => t switch
    {
        SoundType.Glass => "dig.glass", SoundType.Ladder => "dig.wood", _ => "dig." + Family(t)
    };
    public static string Step(SoundType t) => "step." + Family(t);

    static readonly Dictionary<string, string> Alias = new()
    {
        ["splash"] = "game.neutral.swim.splash", ["pop"] = "random.pop", ["orb"] = "random.orb",
        ["arrow_hit"] = "random.bowhit", ["explode"] = "random.explode", ["fuse"] = "game.tnt.primed", ["bow"] = "random.bow",
        ["shear"] = "mob.sheep.shear", ["chicken_plop"] = "mob.chicken.plop", ["fizz"] = "random.fizz",
        ["endermen_stare"] = "mob.endermen.stare", ["endermen_portal"] = "mob.endermen.portal",
        ["endermen_scream"] = "mob.endermen.scream", ["endermen_idle"] = "mob.endermen.idle",
        ["endermen_hit"] = "mob.endermen.hit", ["endermen_death"] = "mob.endermen.death",
        ["fall_big"] = "game.player.hurt.fall.big", ["fall_small"] = "game.player.hurt.fall.small",
        ["eat"] = "random.eat", ["drink"] = "random.drink", ["burp"] = "random.burp", ["break"] = "random.break",
        ["levelup"] = "random.levelup", ["bucket_fill"] = "game.neutral.swim.splash", ["bucket_empty"] = "game.neutral.swim.splash",
        ["door_open"] = "random.door_open", ["door_close"] = "random.door_close", ["click"] = "random.click",
        ["piston_out"] = "tile.piston.out", ["piston_in"] = "tile.piston.in",
        ["hurt"] = "game.neutral.hurt", ["player_hurt"] = "game.player.hurt",
        ["cow"] = "mob.cow.say", ["cow_hurt"] = "mob.cow.hurt", ["pig"] = "mob.pig.say", ["sheep"] = "mob.sheep.say",
        ["chicken"] = "mob.chicken.say", ["chicken_hurt"] = "mob.chicken.hurt",
        ["zombie"] = "mob.zombie.say", ["zombie_hurt"] = "mob.zombie.hurt", ["zombie_death"] = "mob.zombie.death",
        ["skeleton"] = "mob.skeleton.say", ["skeleton_hurt"] = "mob.skeleton.hurt", ["skeleton_death"] = "mob.skeleton.death",
        ["creeper_hurt"] = "mob.creeper.say", ["creeper_death"] = "mob.creeper.death",
        ["spider"] = "mob.spider.say", ["spider_death"] = "mob.spider.death",
    };

    static readonly Dictionary<string, string> Fam = new()
    {
        ["stone"] = "stone", ["wood"] = "wood", ["gravel"] = "gravel", ["grass"] = "grass", ["sand"] = "sand",
        ["glass"] = "glass", ["wool"] = "cloth", ["metal"] = "stone", ["ladder"] = "ladder"
    };

    /// <summary>Zamienia stare klucze na zdarzenia MC. Zwraca true, gdy nazwa była już zgodna.</summary>
    public static bool Resolve(ref string ev, ref float vol, ref float pitch)
    {
        if (ev.IndexOf('.') >= 0) return true;
        if (Alias.TryGetValue(ev, out var a)) { ev = a; return true; }
        int us = ev.IndexOf('_');
        if (us > 0)
        {
            string kind = ev.Substring(0, us), fam = ev.Substring(us + 1);
            if (Fam.TryGetValue(fam, out var f))
            {
                if (kind == "step") { ev = "step." + (f == "glass" ? "stone" : f); return true; }
                if (kind == "dig" || kind == "place") { ev = f == "ladder" ? "dig.wood" : "dig." + f; return true; }
            }
        }
        return true;
    }
}
