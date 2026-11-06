using System.Text.Json;
using BlockCraft.Core;
using BlockCraft.World;
using static BlockCraft.Render.PackModels;

namespace BlockCraft.Render;

internal sealed class PackModelDefinition
{
    public bool AmbientOcclusion = true;
    public Dictionary<string, string> Textures = new();
    public JsonElement? Elements;
    public bool Custom;
}

internal sealed class PackModelLoader
{
    /// <summary>Wczytuje modele z paczek (pierwsza ma pierwszeństwo); zwraca ścieżki tekstur, których używają.
    /// Warstwy atlasu przypisuje potem <see cref="PackModels.Activate"/>.</summary>
    public static Loaded Load(IReadOnlyList<Source> packs)
    {
        var next = new Variants[256][][];
        var paths = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        if (packs != null && packs.Count > 0 && packs.Any(p => !p.Builtin))
        {
            var ctx = new PackModelLoader(packs);
            foreach (var b in Block.ById)
            {
                if (b == null || b.Id == 0 || b.Render is not (RenderType.Cube or RenderType.Cross)) continue;
                for (int meta = 0; meta < 16; meta++)
                    for (int snowy = 0; snowy < 2; snowy++)
                    {
                        var state = PackModelStates.For(b, meta, snowy == 1);
                        if (state == null || (snowy == 1 && !state.Value.Props.ContainsKey("snowy"))) continue;
                        Baked[] baked;
                        try { baked = ctx.Bake(state.Value.File, state.Value.Props); }
                        catch (Exception ex) { Log.Error($"Model z paczki ({b.Key}:{meta}): {ex.Message}"); baked = null; }
                        if (baked == null) continue;
                        next[b.Id] ??= new Variants[16][];
                        next[b.Id][meta] ??= new Variants[2];
                        next[b.Id][meta][snowy] = new Variants(baked);
                        foreach (var q in baked.SelectMany(m => m.Quads)) paths.Add(q.Texture);
                        count++;
                    }
            }
            foreach (var e in ctx.Errors.Take(10)) Log.Error("Model z paczki: " + e);
        }
        return new Loaded(next, paths, count);
    }


    private readonly IReadOnlyList<Source> _packs;
    private readonly Dictionary<string, (JsonElement json, bool custom)?> _files = new();
    public readonly List<string> Errors = new();
    public PackModelLoader(IReadOnlyList<Source> packs) { _packs = packs; }

    /// <summary>JSON z pierwszej paczki, która ma plik; custom = nie z wbudowanej paczki 1.8.</summary>
    private (JsonElement json, bool custom)? Json(string path)
    {
        if (_files.TryGetValue(path, out var cached)) return cached;
        (JsonElement, bool)? r = null;
        foreach (var p in _packs)
        {
            byte[] data;
            try { data = p.Read(path); }
            catch (Exception ex)
            {
                Errors.Add(path + ": " + ex.Message);
                continue;
            }
            if (data == null) continue;
            try
            {
                using var doc = JsonDocument.Parse(data, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                r = (doc.RootElement.Clone(), !p.Builtin);
            }
            catch (JsonException ex) { Errors.Add(path + ": " + ex.Message); }
            break;
        }
        return _files[path] = r;
    }

    private readonly Dictionary<string, PackModelDefinition> _models = new();

    /// <summary>Model z dziedziczeniem: tekstury rodzica nadpisywane przez dziecko, elementy z najbliższego, który je ma.</summary>
    private PackModelDefinition Model(string name, int depth = 0)
    {
        if (depth > 16) throw new InvalidOperationException("za głęboki łańcuch parent: " + name);
        if (name.StartsWith("builtin/")) return null;
        if (!name.Contains('/')) name = "block/" + name;
        if (_models.TryGetValue(name, out var m)) return m;
        var j = Json("assets/minecraft/models/" + name + ".json");
        if (j == null) { Errors.Add("brak modelu " + name); return _models[name] = null; }
        var root = j.Value.json;
        m = new PackModelDefinition { Custom = j.Value.custom };
        if (root.TryGetProperty("parent", out var par) && par.ValueKind == JsonValueKind.String)
        {
            // brak rodzica (np. block/block z nowszych wersji): model działa z własnymi teksturami i elementami
            var parent = Model(par.GetString(), depth + 1);
            if (parent != null)
            {
                foreach (var (k, v) in parent.Textures) m.Textures[k] = v;
                m.Elements = parent.Elements;
                m.AmbientOcclusion = parent.AmbientOcclusion;
                m.Custom |= parent.Custom;
            }
        }
        if (root.TryGetProperty("ambientocclusion", out var ao) && ao.ValueKind is JsonValueKind.True or JsonValueKind.False)
            m.AmbientOcclusion = ao.GetBoolean();
        if (root.TryGetProperty("textures", out var tex) && tex.ValueKind == JsonValueKind.Object)
            foreach (var t in tex.EnumerateObject()) if (t.Value.ValueKind == JsonValueKind.String) m.Textures[t.Name] = t.Value.GetString();
        if (root.TryGetProperty("elements", out var el) && el.ValueKind == JsonValueKind.Array) m.Elements = el;
        return _models[name] = m;
    }

    /// <summary>Warianty z blockstates dla stanu; null = brak pliku, brak pasującego wariantu albo żaden plik nie z paczki.</summary>
    public Baked[] Bake(string file, Dictionary<string, string> props)
    {
        var blockstate = Json("assets/minecraft/blockstates/" + file + ".json");
        if (blockstate == null || !blockstate.Value.json.TryGetProperty("variants", out var variants) || variants.ValueKind != JsonValueKind.Object)
            return null;
        var chosen = SelectVariant(variants, props);
        if (chosen == null) return null;
        return BakeVariants(chosen.Value, blockstate.Value.custom);
    }

    private static JsonElement? SelectVariant(JsonElement variants, Dictionary<string, string> properties)
    {
        JsonElement? chosen = null;
        int bestMatchCount = -1;
        foreach (var variant in variants.EnumerateObject())
        {
            if (variant.Name == "inventory") continue;
            var requiredProperties = ParseVariant(variant.Name);
            if (requiredProperties.Any(property => !properties.TryGetValue(property.Key, out var value) || value != property.Value))
                continue;
            if (requiredProperties.Count > bestMatchCount)
            {
                bestMatchCount = requiredProperties.Count;
                chosen = variant.Value;
            }
        }
        return chosen;
    }

    private Baked[] BakeVariants(JsonElement variant, bool custom)
    {
        var entries = variant.ValueKind == JsonValueKind.Array ? variant.EnumerateArray().ToList() : new List<JsonElement> { variant };
        var result = new List<Baked>();
        foreach (var entry in entries)
        {
            if (!entry.TryGetProperty("model", out var modelName) || modelName.ValueKind != JsonValueKind.String) continue;
            var model = Model(modelName.GetString());
            if (model?.Elements == null) return null;
            custom |= model.Custom;
            var baked = new PackModelBaker(Errors).Bake(model, ReadInteger(entry, "x", 0), ReadInteger(entry, "y", 0));
            if (baked.Quads.Count == 0) return null;
            baked.Weight = Math.Max(1, ReadInteger(entry, "weight", 1));
            result.Add(baked);
        }
        return custom && result.Count > 0 ? result.ToArray() : null;
    }

    private static int ReadInteger(JsonElement entry, string property, int fallback)
        => entry.TryGetProperty(property, out var value) && value.TryGetInt32(out int number) ? number : fallback;

    private static Dictionary<string, string> ParseVariant(string name)
    {
        var d = new Dictionary<string, string>();
        if (name == "normal" || name.Length == 0) return d;
        foreach (var part in name.Split(','))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) d[part[..eq]] = part[(eq + 1)..];
        }
        return d;
    }

}
