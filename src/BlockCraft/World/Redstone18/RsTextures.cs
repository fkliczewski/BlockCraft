using BlockCraft.Core;

namespace BlockCraft.Render
{
    public static partial class TextureGen
    {
        /// <summary>Tekstury elementów RS (komparator, czujnik, klapy, drzwi żelazne, hak, linka) i ich ikony.</summary>
        private static void TexRedstoneComponents()
        {
            Add("comparator_off", ComparatorTop(false));
            Add("comparator_on", ComparatorTop(true));
            Add("daylight_detector_top", DaylightTop(false));
            Add("daylight_detector_inverted_top", DaylightTop(true));
            Add("daylight_detector_side", DaylightSide());
            Add("trapdoor", TrapdoorTex(Palette(0x9C7F4E, 0xB08F5A, 0x86683E), Hex(0x6B5331), 261));
            Add("iron_trapdoor", TrapdoorTex(Palette(0xC8C8C8, 0xD8D8D8, 0xB4B4B4), Hex(0x8A8A8A), 262));
            Add("door_iron_lower", IronDoor(false));
            Add("door_iron_upper", IronDoor(true));
            Add("trip_wire_source", HookTex());
            Add("trip_wire", WireTex());
            Add("hopper_outside", HopperTex(false));
            Add("hopper_inside", HopperTex(true));
            Add("dispenser_front_horizontal", DispenserFront(false, false));
            Add("dispenser_front_vertical", DispenserFront(false, true));
            Add("dropper_front_horizontal", DispenserFront(true, false));
            Add("dropper_front_vertical", DispenserFront(true, true));
            Add("noteblock", NoteBlockTex());
            var tc = (uint[])Get16("chest_front").Clone();
            for (int y = 2; y < 9; y++) for (int x = 6; x < 10; x++) if (x == 6 || x == 9 || y == 2 || y == 8) tc[y * 16 + x] = Hex(0xA01818);
            Add("trapped_chest_front", tc);

            var p = New();
            var up = Get16("door_iron_upper"); var lo = Get16("door_iron_lower");
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    p[y * 16 + x + 4] = up[(y * 2) * 16 + x * 2];
                    p[(y + 8) * 16 + x + 4] = lo[(y * 2) * 16 + x * 2];
                }
            Add("item_iron_door", p);
            Add("item_comparator", Sprite(new[]
            {
                "................", "................", "................", "................",
                "..r.r......r....",
                ".rRrRr....rRr...",
                "..k.k......k....",
                "..k.k......k....",
                "..sssssssssssss.",
                "..sddsssssssdds.",
                "..sssssssssssss.",
                "..ggggggggggggg.",
            }, Pal(('r', 0xFF2A2A), ('R', 0xFF9A9A), ('k', 0x6B4E2A), ('s', 0xACACAC), ('d', 0xB01A1A), ('g', 0x7E7E7E))));
        }

        private static uint[] ComparatorTop(bool on)
        {
            var p = SlabTop();
            uint rd = on ? Hex(0xFF2A1A) : Hex(0x5A1010);
            // ścieżki pyłu: od dwóch pochodni z tyłu do pochodni z przodu
            for (int y = 4; y < 12; y++) { p[y * 16 + 4] = rd; p[y * 16 + 11] = rd; }
            for (int x = 4; x < 12; x++) p[11 * 16 + x] = rd;
            for (int y = 2; y < 5; y++) { p[y * 16 + 7] = Mul(rd, 0.8f); p[y * 16 + 8] = Mul(rd, 0.8f); }
            return p;
        }
        private static uint[] DaylightTop(bool inverted)
        {
            var p = New(); var r = new Rng(inverted ? 264 : 263);
            uint frame = Hex(0xB59A6A), frameD = Hex(0x8C7449);
            uint g0 = inverted ? Hex(0x34466A) : Hex(0xA8C4D8), g1 = inverted ? Hex(0x26344F) : Hex(0x8FB0C8);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool fr = x == 0 || y == 0 || x == 15 || y == 15 || x == 5 || x == 10 || y == 5 || y == 10;
                    p[y * 16 + x] = fr ? (r.NextBool() ? frame : frameD) : Lerp(g0, g1, r.NextFloat());
                }
            return p;
        }
        private static uint[] DaylightSide()
        {
            var p = Planks(Palette(0xB08F5A, 0x9C7F4E, 0x86683E, 0xBC9862), Hex(0x6B5331), 265);
            for (int y = 0; y < 10; y++) for (int x = 0; x < 16; x++) p[y * 16 + x] = 0;
            for (int x = 0; x < 16; x++) p[10 * 16 + x] = Hex(0xA8C4D8);
            return p;
        }
        private static uint[] TrapdoorTex(uint[] pal, uint frame, int seed)
        {
            var p = New(); var r = new Rng(seed);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    bool edge = x <= 1 || x >= 14 || y <= 1 || y >= 14;
                    bool bar = y == 7 || y == 8 || x == 7 || x == 8;
                    bool hole = !edge && !bar && ((x - 2) % 5 < 3) && ((y - 2) % 5 < 3) && (x is >= 3 and <= 5 or >= 10 and <= 12) && (y is >= 3 and <= 5 or >= 10 and <= 12);
                    p[y * 16 + x] = hole ? 0 : edge ? frame : pal[r.Next(pal.Length)];
                }
            return p;
        }
        private static uint[] IronDoor(bool upper)
        {
            var p = New(); var r = new Rng(upper ? 267 : 266);
            uint frame = Hex(0x9A9A9A), panel = Hex(0xD4D4D4), panelL = Hex(0xE6E6E6), panelD = Hex(0xBEBEBE);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    uint c = r.NextFloat() < 0.6f ? panel : (r.NextBool() ? panelL : panelD);
                    if (x == 0 || x == 15 || (upper && y == 0) || (!upper && y == 15)) c = frame;
                    p[y * 16 + x] = c;
                }
            if (upper)
            {
                for (int y = 2; y < 8; y++) for (int x = 3; x < 13; x++) p[y * 16 + x] = (x - 3) % 3 == 2 ? frame : 0;
            }
            else
            {
                for (int y = 3; y < 13; y += 3) for (int x = 2; x < 14; x++) p[y * 16 + x] = frame;
                p[1 * 16 + 12] = Hex(0x5A5A5A); p[1 * 16 + 13] = Hex(0x3A3A3A);
            }
            return p;
        }
        private static uint[] HookTex()
        {
            var p = Planks(Palette(0xB08F5A, 0x9C7F4E, 0x86683E, 0xBC9862), Hex(0x6B5331), 268);
            for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++) if (x == 4 || x == 11 || y == 4 || y == 11) p[y * 16 + x] = Hex(0xBFBFBF);
            return p;
        }
        private static uint[] HopperTex(bool inside)
        {
            var p = Noisy(inside ? 270 : 269, Palette(0x3C3C3C, 0x444444, 0x4C4C4C, 0x363636), 0.5f, 1.0f);
            for (int i = 0; i < 16; i++)
            {
                uint e = inside ? Hex(0x2A2A2A) : Hex(0x5C5C5C);
                p[i] = e; p[15 * 16 + i] = e; p[i * 16] = e; p[i * 16 + 15] = e;
            }
            return p;
        }
        private static uint[] DispenserFront(bool dropper, bool vertical)
        {
            var p = (uint[])Get16(vertical ? "furnace_top" : "furnace_side").Clone();
            uint dark = Hex(0x1E1E1E), rim = Hex(0x5A5A5A);
            if (dropper)
            {
                for (int y = 5; y < 11; y++) for (int x = 5; x < 11; x++) p[y * 16 + x] = (x == 5 || x == 10 || y == 5 || y == 10) ? rim : dark;
            }
            else
            {
                for (int y = 4; y < 12; y++)
                    for (int x = 4; x < 12; x++)
                    {
                        double d = Math.Sqrt((x - 7.5) * (x - 7.5) + (y - 7.5) * (y - 7.5));
                        if (d < 3.6) p[y * 16 + x] = d > 2.6 ? rim : dark;
                    }
                for (int x = 3; x < 13; x++) p[12 * 16 + x] = rim;
            }
            return p;
        }
        private static uint[] NoteBlockTex()
        {
            var p = Planks(Palette(0x6E4A2E, 0x7A5434, 0x5E3F27, 0x845C39), Hex(0x3E2716), 271);
            for (int y = 3; y < 13; y++) for (int x = 3; x < 13; x++) if (x == 3 || x == 12 || y == 3 || y == 12) p[y * 16 + x] = Hex(0x2A1A10);
            for (int y = 5; y < 10; y++) p[y * 16 + 9] = Hex(0x111111);
            p[10 * 16 + 7] = p[10 * 16 + 8] = p[9 * 16 + 7] = p[9 * 16 + 8] = Hex(0x111111);
            return p;
        }
        private static uint[] WireTex()
        {
            var p = New();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) p[y * 16 + x] = Rgba(220, 220, 220, 230);
            return p;
        }
    }
}
