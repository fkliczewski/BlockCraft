using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Cmd;

public static class Adventure
{
    public static bool Allows(ItemStack stack, string key, int blockId)
    {
        var list = stack?.Tag?.GetList(key); if (list == null) return false;
        foreach (string name in list.OfType<string>())
        {
            try { if (Arg.Block(name) == blockId) return true; } catch (CommandException) { }
        }
        return false;
    }
    public static string Tooltip(ItemStack stack)
    {
        string text = ""; int flags = stack.Tag?.GetInt("HideFlags") ?? 0;
        foreach (var (key, title, flag) in new[] { ("CanDestroy", "Może niszczyć:", 8), ("CanPlaceOn", "Można postawić na:", 16) })
        {
            if ((flags & flag) != 0 || stack.Tag?.GetList(key) is not { Count: > 0 } list) continue;
            text += "\n\n§7" + title;
            foreach (string name in list.OfType<string>())
            {
                string display; try { display = Block.Get(Arg.Block(name)).Name; } catch (CommandException) { display = "Nieznany blok"; }
                text += "\n§8" + display;
            }
        }
        return text;
    }
}
