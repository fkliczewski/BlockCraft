using BlockCraft.Core;

namespace BlockCraft.Render;

public static partial class TextureGen
{
    private static Dictionary<char, uint> Pal(params (char c, uint hex)[] e)
    {
        var d = new Dictionary<char, uint>();
        foreach (var (c, h) in e) d[c] = Hex(h);
        return d;
    }

    private static uint[] Sprite(string[] rows, Dictionary<char, uint> pal)
    {
        var p = New();
        Stamp(p, rows, pal);
        return p;
    }

    private static readonly (uint o, uint d, uint m, uint l)[] ToolMats =
    {
        (0x3D2C14, 0x6B5331, 0x9C7F4E, 0xBC9862),   // drewno
        (0x2A2A2A, 0x555555, 0x7F7F7F, 0xA0A0A0),   // kamień
        (0x353535, 0x8C8C8C, 0xC6C6C6, 0xEFEFEF),   // żelazo
        (0x5E4A0E, 0xC79F21, 0xF2D33A, 0xFFFBA0),   // złoto
        (0x0E3D3A, 0x1BA59D, 0x4AEDD9, 0xC6FFF6),   // diament
    };

    private static void StickDiag(uint[] p, int fromRow, int toRow)
    {
        uint k = Hex(0x28190A), s = Hex(0x8F6A3B), t = Hex(0x5E4426);
        for (int r = fromRow; r <= toRow; r++)
        {
            int c = 14 - r;
            void Set(int x, uint col) { if (x >= 0 && x < 16) p[r * 16 + x] = col; }
            Set(c - 1, k); Set(c, s); Set(c + 1, t); Set(c + 2, k);
        }
        if (toRow >= 13) { p[14 * 16 + 0] = k; p[14 * 16 + 1] = t; p[14 * 16 + 2] = k; p[15 * 16 + 1] = k; }
    }

    private static uint[] Tool(string[] head, int stickFrom, (uint o, uint d, uint m, uint l) mat, bool stickFirst = true)
    {
        var p = New();
        StickDiag(p, stickFrom, 13);
        Stamp(p, head, new() { ['o'] = Hex(mat.o), ['d'] = Hex(mat.d), ['m'] = Hex(mat.m), ['l'] = Hex(mat.l) });
        return p;
    }

    private static readonly string[] PickHead =
    {
        "................",
        ".....ooooo......",
        "...oolllllmoo...",
        "..olmmmmmmmmdo..",
        "...ooooommmddmo.",
        "..........oddmo.",
        "...........odmo.",
        "...........odmo.",
        "...........odmo.",
        "...........odo..",
        "...........odo..",
        "............o...",
    };
    private static readonly string[] AxeHead =
    {
        "................",
        "......ooo.......",
        ".....olmmo......",
        "....olmmmmo.....",
        "...olmmmmdo.....",
        "...olmmmddo.....",
        "....ooddo.......",
        "......oo........",
    };
    private static readonly string[] ShovelHead =
    {
        "...........ooo..",
        "..........olllo.",
        ".........olmmmdo",
        ".........ommmddo",
        "..........omddo.",
        "...........ooo..",
    };
    private static readonly string[] HoeHead =
    {
        "................",
        "......ooooooo...",
        ".....olllmmmdo..",
        "......oooooddo..",
        "..........oodo..",
    };
    private static readonly string[] SwordFull =
    {
        "..............oo",
        ".............olo",
        "............olmo",
        "...........olmo.",
        "..........olmo..",
        ".........olmo...",
        "........olmo....",
        ".......olmo.....",
        "..oo..olmo......",
        "..odoolmo.......",
        "...odmmo........",
        "...oddo.........",
        "..o.ooo.........",
        "................",
        "................",
        "................",
    };

    private static uint[] Sword((uint o, uint d, uint m, uint l) mat)
    {
        var p = New();
        uint k = Hex(0x28190A), s = Hex(0x8F6A3B), t = Hex(0x5E4426);
        // rękojeść
        int[,] h = { { 1, 14 }, { 2, 13 }, { 3, 12 } };
        p[15 * 16 + 0] = k; p[14 * 16 + 0] = k; p[15 * 16 + 1] = k;
        for (int i = 0; i < 3; i++) { int x = h[i, 0], y = h[i, 1]; p[y * 16 + x] = i == 1 ? t : s; p[y * 16 + x - 1] = k; p[(y + 1) * 16 + x] = k; }
        p[12 * 16 + 2] = k;
        Stamp(p, SwordFull, new() { ['o'] = Hex(mat.o), ['d'] = Hex(mat.d), ['m'] = Hex(mat.m), ['l'] = Hex(mat.l) });
        return p;
    }

    private static void GenerateItems()
    {
        string[] mats = { "wooden", "stone", "iron", "golden", "diamond" };
        for (int m = 0; m < 5; m++)
        {
            var mat = ToolMats[m];
            Add($"item_{mats[m]}_pickaxe", Tool(PickHead, 5, mat));
            Add($"item_{mats[m]}_axe", Tool(AxeHead, 4, mat));
            Add($"item_{mats[m]}_shovel", Tool(ShovelHead, 4, mat));
            Add($"item_{mats[m]}_sword", Sword(mat));
            Add($"item_{mats[m]}_hoe", Tool(HoeHead, 4, mat));
        }

        {   // patyk
            var p = New();
            for (int i = 0; i < 11; i++)
            {
                int x = 2 + i, y = 13 - i;
                p[y * 16 + x] = Hex(0x8F6A3B); p[y * 16 + x + 1] = Hex(0x5E4426);
                if (i % 3 == 0) p[y * 16 + x] = Hex(0x6B5126);
            }
            p[13 * 16 + 1] = Hex(0x4F3A1C); p[2 * 16 + 14] = Hex(0x4F3A1C);
            Add("item_stick", p);
        }

        var lump = new[]
        {
            "................", "................", "................",
            "......oooo......",
            "....oollmmoo....",
            "...olllmmmmdo...",
            "..olllmmmmmddo..",
            "..ollmmmmmmddo..",
            "..olmmmmmmdddo..",
            "...ommmmmdddo...",
            "....oodddddo....",
            "......oooo......",
        };
        Add("item_coal", Sprite(lump, Pal(('o', 0x0A0A0A), ('l', 0x4A4A4A), ('m', 0x2E2E2E), ('d', 0x1A1A1A))));
        Add("item_charcoal", Sprite(lump, Pal(('o', 0x150E08), ('l', 0x5A4632), ('m', 0x3A2C1E), ('d', 0x241A10))));

        var ingot = new[]
        {
            "................", "................", "................", "................", "................",
            "......oooooo....",
            ".....olllllmo...",
            "....olllllmmdo..",
            "...olllllmmddo..",
            "..olllllmmdddo..",
            ".oommmmmddddo...",
            ".odddddddddoo...",
            "..oooooooooo....",
        };
        Add("item_iron_ingot", Sprite(ingot, Pal(('o', 0x353535), ('l', 0xEFEFEF), ('m', 0xC6C6C6), ('d', 0x8C8C8C))));
        Add("item_gold_ingot", Sprite(ingot, Pal(('o', 0x5E4A0E), ('l', 0xFFFBA0), ('m', 0xF2D33A), ('d', 0xC79F21))));
        Add("item_brick", Sprite(ingot, Pal(('o', 0x3A1A10), ('l', 0xC9846A), ('m', 0x9C5A42), ('d', 0x6E3A28))));

        var gem = new[]
        {
            "................", "................",
            "......oooo......",
            ".....ollllo.....",
            "....olllmmmo....",
            "...ollmmmmmdo...",
            "...olmmmmmddo...",
            "....olmmmddo....",
            ".....olmmdo.....",
            "......omdo......",
            ".......oo.......",
        };
        Add("item_diamond", Sprite(gem, Pal(('o', 0x0E3D3A), ('l', 0xD5FFF6), ('m', 0x4AEDD9), ('d', 0x1BA59D))));
        Add("item_lapis_lazuli", Sprite(new[]
        {
            "................", "................", "................",
            ".......ooo......",
            ".....oolllo.....",
            "....olllmmmo....",
            "...ollmmmmmdo...",
            "...olmmmmmmdo...",
            "..olmmmmmmddo...",
            "..ommmmmmdddo...",
            "...oodmmdddo....",
            ".....ooooo......",
        }, Pal(('o', 0x0A1A50), ('l', 0x5A8BFF), ('m', 0x1F48B0), ('d', 0x0F2470))));

        var dust = new[]
        {
            "................", "................", "................", "................", "................",
            ".......l........",
            ".....m..md......",
            "...m.lmdm..m....",
            "....mdmlmmd.m...",
            "..m.mmdmmmdm....",
            "...dmmlmmdmmd...",
            ".m.mmdmmmmmm.m..",
            "..ddmmmdmmdmd...",
            "....d..d.d......",
        };
        Add("item_redstone", Sprite(dust, Pal(('l', 0xFF6A6A), ('m', 0xE00000), ('d', 0x8F0000))));
        Add("item_gunpowder", Sprite(dust, Pal(('l', 0x8A8A8A), ('m', 0x5A5A5A), ('d', 0x353535))));
        Add("item_sugar", Sprite(dust, Pal(('l', 0xFFFFFF), ('m', 0xEAEAEA), ('d', 0xBDBDBD))));
        Add("item_bone_meal", Sprite(dust, Pal(('l', 0xFFFFFF), ('m', 0xE6E6DC), ('d', 0xB4B4A6))));

        Add("item_flint", Sprite(new[]
        {
            "................", "................", "................",
            ".......oo.......",
            "......olmo......",
            ".....olmmdo.....",
            "....olmmmdo.....",
            "....olmmmddo....",
            "...olmmmmddo....",
            "...olmmmddo.....",
            "....olmmddo.....",
            "....oommdo......",
            "......ooo.......",
        }, Pal(('o', 0x101010), ('l', 0x7A7A7A), ('m', 0x454545), ('d', 0x2A2A2A))));

        Add("item_shears", Sprite(new[]
        {
            "................",
            "................",
            "..........oo....",
            ".........olmo...",
            "........olmo....",
            ".......olmo.....",
            "......olmoooo...",
            ".....olmmmlllo..",
            "....ommoooooo...",
            "...okko.........",
            "..okeeko........",
            "..okeko.........",
            "...okko.........",
            "....oo..........",
        }, Pal(('o', 0x2A2A2A), ('l', 0xEFEFEF), ('m', 0xB0B0B0), ('k', 0x1A1A1A), ('e', 0x5A3A2A))));

        var bucket = new[]
        {
            "................", "................", "................",
            "...oooooooooo...",
            "..ollllllllmdo..",
            "..oiiiiiiiiiio..",
            "..olliiiiiimdo..",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "....olmmmmdo....",
            "....olmmmmdo....",
            ".....oooooo.....",
        };
        var bpal = new (char, uint)[] { ('o', 0x2A2A2A), ('l', 0xEFEFEF), ('m', 0xBDBDBD), ('d', 0x7A7A7A) };
        Add("item_bucket", Sprite(bucket, Pal(bpal.Append(('i', 0x3A3A3Au)).ToArray())));
        Add("item_water_bucket", Sprite(bucket, Pal(bpal.Append(('i', 0x2F5BD6u)).ToArray())));
        Add("item_lava_bucket", Sprite(bucket, Pal(bpal.Append(('i', 0xFC9B1Eu)).ToArray())));
        Add("item_milk_bucket", Sprite(bucket, Pal(bpal.Append(('i', 0xF8F8F8u)).ToArray())));

        Add("item_flint_and_steel", Sprite(new[]
        {
            "................", "................",
            "....oooo........",
            "...ollllo.......",
            "..olo..olo......",
            "..oo....oo......",
            "..olo...........",
            "...ollo.........",
            "....ooo..oo.....",
            ".........olo....",
            "........olmdo...",
            ".......olmmdo...",
            "........ommdo...",
            ".........ooo....",
        }, Pal(('o', 0x202020), ('l', 0xC6C6C6), ('m', 0x454545), ('d', 0x2A2A2A))));

        {   // łuk
            var p = New();
            uint wood = Hex(0x8F6A3B), woodD = Hex(0x5E4426), str = Hex(0xE6E6E6);
            for (int a = 0; a <= 40; a++)
            {
                double t = a / 40.0 * Math.PI / 2;
                int x = (int)Math.Round(14 - 12 * Math.Cos(t)), y = (int)Math.Round(14 - 12 * Math.Sin(t));
                x = 16 - x; // lustrzane: łuk wygięty do lewego górnego rogu
                x = Math.Clamp(15 - x, 0, 15);
                p[Math.Clamp(y, 0, 15) * 16 + x] = a % 6 < 3 ? wood : woodD;
            }
            for (int i = 2; i <= 13; i++) p[i * 16 + (15 - i + 1 > 15 ? 15 : 15 - i + 1)] = str;
            Add("item_bow", p);
        }
        {   // strzała
            var p = New();
            for (int i = 3; i <= 12; i++) p[(15 - i) * 16 + i] = Hex(0x8F6A3B);
            p[2 * 16 + 13] = Hex(0xC6C6C6); p[1 * 16 + 14] = Hex(0xEFEFEF); p[2 * 16 + 14] = Hex(0x8C8C8C); p[1 * 16 + 13] = Hex(0x8C8C8C); p[3 * 16 + 13] = Hex(0x6A6A6A);
            p[13 * 16 + 1] = Hex(0xEAEAEA); p[14 * 16 + 1] = Hex(0xBDBDBD); p[13 * 16 + 2] = Hex(0xBDBDBD); p[12 * 16 + 1] = Hex(0xEAEAEA); p[14 * 16 + 2] = Hex(0xEAEAEA);
            p[12 * 16 + 3] = Hex(0xEAEAEA); p[11 * 16 + 2] = Hex(0xBDBDBD);
            Add("item_arrow", p);
        }

        var apple = new[]
        {
            "................", "................",
            "........k.......",
            ".......kb.gg....",
            "....ooookggg....",
            "...orrrrrrrro...",
            "..orwrrrrrrrro..",
            "..orwrrrrrrrdo..",
            "..orrrrrrrrrdo..",
            "..orrrrrrrrrdo..",
            "..orrrrrrrrddo..",
            "...orrrrrrddo...",
            "....orrdrddo....",
            ".....oo.ooo.....",
        };
        Add("item_apple", Sprite(apple, Pal(('o', 0x3D0A0A), ('r', 0xE21F1F), ('w', 0xFF9C9C), ('d', 0x9C0F0F), ('b', 0x5E4426), ('k', 0x2A1A0A), ('g', 0x3C8A1E))));
        Add("item_golden_apple", Sprite(apple, Pal(('o', 0x5E4A0E), ('r', 0xF2D33A), ('w', 0xFFFBA0), ('d', 0xC79F21), ('b', 0x5E4426), ('k', 0x2A1A0A), ('g', 0x3C8A1E))));

        Add("item_bread", Sprite(new[]
        {
            "................", "................", "................", "................", "................", "................",
            "................",
            ".....oooooo.....",
            "...oollmlmloo...",
            "..olmlmlmlmmdo..",
            ".olmmmmmmmmmddo.",
            ".oddmmmmmmmdddo.",
            "..ooddddddddoo..",
            "....oooooooo....",
        }, Pal(('o', 0x3D2510), ('l', 0xE8C07A), ('m', 0xB5803A), ('d', 0x8A5A22))));

        var meat = new[]
        {
            "................", "................", "................",
            "......oooo......",
            "....oowwwwoo....",
            "...owppppppwo...",
            "..owppppppppwo..",
            "..owpppdppppwo..",
            "..owppppppdpwo..",
            "..owpdppppppwo..",
            "...owppppppwo...",
            "....owppppwo....",
            ".....owwwwo.....",
            "......oooo......",
        };
        Add("item_porkchop", Sprite(meat, Pal(('o', 0x5A2020), ('w', 0xF8E0E0), ('p', 0xF0A0A0), ('d', 0xD87878))));
        Add("item_cooked_porkchop", Sprite(meat, Pal(('o', 0x3A2010), ('w', 0xE0B080), ('p', 0xA0602F), ('d', 0x7A4520))));
        var steak = new[]
        {
            "................", "................", "................",
            ".......ooooo....",
            ".....oopppppo...",
            "....oppwppdppo..",
            "...oppppwppppo..",
            "..opppdppwpppo..",
            "..oppppppwppo...",
            "..opwwpppppdo...",
            "...opppdppppo...",
            "....oopppppo....",
            "......ooooo.....",
        };
        Add("item_beef", Sprite(steak, Pal(('o', 0x4A1010), ('w', 0xF4D0D0), ('p', 0xD03030), ('d', 0x9A1A1A))));
        Add("item_cooked_beef", Sprite(steak, Pal(('o', 0x2A1508), ('w', 0xB07A4A), ('p', 0x7A4A28), ('d', 0x5A3018))));
        Add("item_mutton", Sprite(steak, Pal(('o', 0x4A1515), ('w', 0xF8E8E8), ('p', 0xC84040), ('d', 0xE8C0C0))));
        Add("item_cooked_mutton", Sprite(steak, Pal(('o', 0x2A1508), ('w', 0xD8B08A), ('p', 0x8A5030), ('d', 0xB08060))));
        var drum = new[]
        {
            "................", "................",
            "......oooo......",
            "....oopppdoo....",
            "...opppppppdo...",
            "...oppppppppdo..",
            "...opppppppddo..",
            "....oppppppddo..",
            ".....opppdddo...",
            "......ooodoo....",
            ".......owo......",
            "......owwo......",
            ".....owwwo......",
            ".....owwo.......",
            "......oo........",
        };
        Add("item_chicken", Sprite(drum, Pal(('o', 0x5A3A30), ('p', 0xF2C6B4), ('d', 0xD8A090), ('w', 0xF4F4EA))));
        Add("item_cooked_chicken", Sprite(drum, Pal(('o', 0x3A2010), ('p', 0xD99A4E), ('d', 0xA8682A), ('w', 0xF4F4EA))));
        Add("item_rotten_flesh", Sprite(steak, Pal(('o', 0x2A2A10), ('w', 0x9C5A3A), ('p', 0x6E7B3A), ('d', 0x4E5A28))));
        Add("item_cookie", Sprite(new[]
        {
            "................", "................", "................", "................",
            ".....oooooo.....",
            "....occkccco....",
            "...occccckcco...",
            "...ockcccccco...",
            "...occcckccco...",
            "...occkcccckco..",
            "....occccccco...",
            ".....oooooooo...",
        }, Pal(('o', 0x5A3010), ('c', 0xC7843B), ('k', 0x3A1A08))));
        var bowl = new[]
        {
            "................", "................", "................", "................", "................", "................",
            "................",
            "..oooooooooooo..",
            "..oiiiiiiiiiio..",
            "..olmmmmmmmmdo..",
            "...olmmmmmmdo...",
            "....olmmmmdo....",
            ".....oooooo.....",
        };
        Add("item_bowl", Sprite(bowl, Pal(('o', 0x3D2C14), ('i', 0x5E4426), ('l', 0xBC9862), ('m', 0x9C7F4E), ('d', 0x6B5331))));
        Add("item_mushroom_stew", Sprite(bowl, Pal(('o', 0x3D2C14), ('i', 0xA8784E), ('l', 0xBC9862), ('m', 0x9C7F4E), ('d', 0x6B5331))));

        Add("item_wheat", Sprite(new[]
        {
            "................",
            "...........yy...",
            "..........yYy...",
            ".........yYyy...",
            "....y...yYyy....",
            "...yYy.yYyy.....",
            "...yYyyYyy......",
            "....yYYyy.ggg...",
            ".....yYyggg.....",
            "......gYg.......",
            ".....ggg........",
            "....gg.g........",
            "...gg...........",
            "..g.............",
        }, Pal(('y', 0xB89A3A), ('Y', 0xE8D07A), ('g', 0x8A7A2A))));
        Add("item_wheat_seeds", Sprite(new[]
        {
            "................", "................", "................", "................", "................",
            "................",
            ".......g........",
            "....g...gG......",
            "...gG..g...g....",
            "......gG..gG....",
            "..g.......g.....",
            "...gG..g........",
            "......gG..g.....",
            "..........gG....",
        }, Pal(('g', 0x2E6A1A), ('G', 0x6AB040))));
        Add("item_bone", Sprite(new[]
        {
            "................",
            "..........oo....",
            ".........owwo...",
            ".........owwwo..",
            "........owwgo...",
            ".......owwo.....",
            "......owwo......",
            ".....owwo.......",
            "....owwo........",
            "...owwo.........",
            "..owwgo.........",
            ".owwwo..........",
            ".owwo...........",
            "..oo............",
        }, Pal(('o', 0x6B6B5A), ('w', 0xF2F2E6), ('g', 0xC8C8B4))));
        Add("item_string", Sprite(new[]
        {
            "................", "................", "................",
            "..........ww....",
            ".........w..w...",
            "........w....w..",
            ".......w.....w..",
            "......w.....w...",
            ".....w....ww....",
            "....w...........",
            "...w............",
            "..w.............",
            "..w.............",
            "...w............",
        }, Pal(('w', 0xEAEAEA))));
        Add("item_feather", Sprite(new[]
        {
            "................",
            "...........ww...",
            "..........wwWw..",
            ".........wwWwg..",
            "........wwWwg...",
            ".......wwWwg....",
            "......wwWwg.....",
            ".....wwWwg......",
            "....wwWwg.......",
            "...wwWwg........",
            "....Wg..........",
            "...W............",
            "..k.............",
            ".k..............",
        }, Pal(('w', 0xF4F4F4), ('W', 0xC8C8C8), ('g', 0xA0A0A0), ('k', 0x5A5A5A))));
        Add("item_leather", Sprite(new[]
        {
            "................", "................",
            "...oo......oo...",
            "..ollo....olmo..",
            "..olmmooooomdo..",
            "...olmmmmmmmdo..",
            "...olmmmmmmdo...",
            "....olmmmmmdo...",
            "....olmmmmmdo...",
            "...olmmmmmmmdo..",
            "..olmmoooommdo..",
            "..oddo....oddo..",
            "...oo......oo...",
        }, Pal(('o', 0x3D2412), ('l', 0xC68A5A), ('m', 0xA0653E), ('d', 0x6B3F22))));
        Add("item_egg", Sprite(new[]
        {
            "................", "................", "................",
            "......oooo......",
            ".....ollllo.....",
            "....olllmmmo....",
            "....ollmmmmo....",
            "...ollmmkmmdo...",
            "...olmmmmmmdo...",
            "...olmkmmmmdo...",
            "....ommmmmdo....",
            "....oommmddo....",
            "......oooo......",
        }, Pal(('o', 0x8A7A5A), ('l', 0xFFFFF0), ('m', 0xEADCB4), ('d', 0xC8B48A), ('k', 0xA89470))));
        Add("item_spider_eye", Sprite(new[]
        {
            "................", "................", "................", "................",
            ".....oooooo.....",
            "....orrrrrro....",
            "...orrwrrrrdo...",
            "...orrrkkrrdo...",
            "...orrrkkrrdo...",
            "...orrrrrrddo...",
            "....orrrrddo....",
            ".....oooooo.....",
        }, Pal(('o', 0x3A0A1A), ('r', 0xA82A4A), ('w', 0xF080A0), ('d', 0x6A1A2A), ('k', 0x1A0A0A))));
        Add("item_ender_pearl", Sprite(new[]
        {
            "................", "................", "................",
            "......oooo......",
            "....oollmmoo....",
            "...olwlmmmmdo...",
            "...ollmkkmmdo...",
            "..olmmkkkkmddo..",
            "..olmmkkkkmddo..",
            "...olmmkkmmdo...",
            "...odmmmmmddo...",
            "....ooddddoo....",
            "......oooo......",
        }, Pal(('o', 0x0A2A2A), ('l', 0x3AA090), ('w', 0xA0FFF0), ('m', 0x1E6E64), ('d', 0x0E4A44), ('k', 0x08302C))));
        Add("item_reeds", Sprite(new[]
        {
            "................",
            "......ld........",
            "......ld...ld...",
            "......Kd...ld...",
            "......ld...Kd...",
            "...ld.ld...ld...",
            "...ld.ld.g.ld...",
            "...Kd.Kdg..ld...",
            "...ld.ld...ld...",
            "..gld.ld...Kd...",
            "...ld.ld...ld...",
            "...ld.ld...ld...",
            "...Kd.ld...ld...",
            "...ld.ld...ld...",
        }, Pal(('l', 0xAACC77), ('d', 0x7FA855), ('K', 0x5E7F34), ('g', 0x6B9A3A))));
        Add("item_paper", Sprite(new[]
        {
            "................", "................",
            "....oooooooo....",
            "...owwwwwwwwo...",
            "...owwwwwwwgo...",
            "...owwwwwwwgo...",
            "..owwwwwwwwgo...",
            "..owwwwwwwwgo...",
            "..owwwwwwwgo....",
            "..owwwwwwwgo....",
            ".owwwwwwwwgo....",
            ".oggggggggo.....",
            ".oooooooooo.....",
        }, Pal(('o', 0x8A8A7A), ('w', 0xF4F4EA), ('g', 0xD0D0C0))));
        Add("item_clay_ball", Sprite(new[]
        {
            "................", "................", "................", "................",
            "......oooo......",
            ".....ollmmo.....",
            "....ollmmmdo....",
            "....olmmmmdo....",
            "....ommmmddo....",
            ".....oddddo.....",
            "......oooo......",
        }, Pal(('o', 0x4A505A), ('l', 0xC8CEDA), ('m', 0xA0A6B3), ('d', 0x7A808C))));
        Add("item_book", Sprite(new[]
        {
            "................", "................",
            "....oooooooo....",
            "...obbbbbbbbo...",
            "..obbbbbbbbbo...",
            "..obbbbbbbbo....",
            ".obbbbbbbbbo....",
            ".obbbbbbbbo.....",
            ".owwwwwwwwo.....",
            ".oggggggggo.....",
            ".oooooooooo.....",
        }, Pal(('o', 0x3A1A08), ('b', 0x8A4A1E), ('w', 0xF4F4EA), ('g', 0xD0D0C0))));
        Add("item_gold_nugget", Sprite(new[]
        {
            "................", "................", "................", "................", "................",
            "......oo........",
            ".....olmo.......",
            "....olmmdo..oo..",
            ".....oddo..olmo.",
            "......oo...omdo.",
            "............oo..",
        }, Pal(('o', 0x5E4A0E), ('l', 0xFFFBA0), ('m', 0xF2D33A), ('d', 0xC79F21))));

        // zbroje
        var armorMats = new (string key, uint o, uint d, uint m, uint l)[]
        {
            ("leather", 0x3D2412, 0x6B3F22, 0xA0653E, 0xC68A5A),
            ("iron", 0x353535, 0x8C8C8C, 0xC6C6C6, 0xEFEFEF),
            ("golden", 0x5E4A0E, 0xC79F21, 0xF2D33A, 0xFFFBA0),
            ("diamond", 0x0E3D3A, 0x1BA59D, 0x4AEDD9, 0xC6FFF6),
        };
        var helmet = new[]
        {
            "................", "................", "................",
            "................",
            "...oooooooooo...",
            "..ollllllllmdo..",
            "..olmmmmmmmmdo..",
            "..olmmmmmmmmdo..",
            "..olmoooooomdo..",
            "..olmo....omdo..",
            "..oddo....oddo..",
            "...oo......oo...",
        };
        var chest = new[]
        {
            "................",
            "................",
            "..ooo......ooo..",
            ".ollmo....olmdo.",
            ".olmmmoooommmdo.",
            ".olmmmmmmmmmmdo.",
            "..oolmmmmmmdoo..",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "...olmmmmmmdo...",
            "...oddddddddo...",
            "....oooooooo....",
        };
        var legs = new[]
        {
            "................",
            "................",
            "...oooooooooo...",
            "...olllllllmo...",
            "...olmmmmmmdo...",
            "...olmmoomddo...",
            "...olmo..oldo...",
            "...olmo..olmo...",
            "...olmo..olmo...",
            "...olmo..olmo...",
            "...olmo..olmo...",
            "...oddo..oddo...",
            "...oooo..oooo...",
        };
        var boots = new[]
        {
            "................", "................", "................", "................", "................", "................",
            "................",
            "..oooo....oooo..",
            "..olmo....olmo..",
            "..olmo....olmo..",
            ".olmmo...olmmo..",
            "olmmmo..olmmmo..",
            "oddddo..oddddo..",
            "oooooo..oooooo..",
        };
        foreach (var am in armorMats)
        {
            var pal = Pal(('o', am.o), ('d', am.d), ('m', am.m), ('l', am.l));
            Add($"item_{am.key}_helmet", Sprite(helmet, pal));
            Add($"item_{am.key}_chestplate", Sprite(chest, pal));
            Add($"item_{am.key}_leggings", Sprite(legs, pal));
            Add($"item_{am.key}_boots", Sprite(boots, pal));
        }

        {   // drzwi
            var p = New();
            var door = Get16("door_wood_upper"); var lower = Get16("door_wood_lower");
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                {
                    p[(y) * 16 + x + 4] = door[(y * 2) * 16 + x * 2];
                    p[(y + 8) * 16 + x + 4] = lower[(y * 2) * 16 + x * 2];
                }
            Add("item_wooden_door", p);
        }
        Add("item_bed", Sprite(new[]
        {
            "................", "................", "................", "................", "................",
            "................",
            "................",
            ".wwwrrrrrrrrrrr.",
            ".wwwrrrrrrrrrrr.",
            ".ggggggggggggggg",
            ".bbbbbbbbbbbbbbb",
            ".b.............b",
        }, Pal(('w', 0xE6E6E6), ('r', 0xA02828), ('g', 0x7A1A1A), ('b', 0x8F6A3B))));
        Add("item_repeater", Sprite(new[]
        {
            "................", "................", "................", "................",
            "...r........r...",
            "..rRr......rRr..",
            "...k........k...",
            "...k........k...",
            "..sssssssssssss.",
            "..sddddddddddds.",
            "..sssssssssssss.",
            "..ggggggggggggg.",
        }, Pal(('r', 0xFF2A2A), ('R', 0xFF9A9A), ('k', 0x6B4E2A), ('s', 0xACACAC), ('d', 0xB01A1A), ('g', 0x7E7E7E))));
    }

    private static uint[] Get16(string name) => _tex[_idxBuilding[name]];
}
