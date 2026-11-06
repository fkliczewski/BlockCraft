using static BlockCraft.Render.PackModelDirections;
using System.Numerics;
using System.Text.Json;
using static BlockCraft.Render.PackModels;

namespace BlockCraft.Render;

internal sealed class PackModelBaker
{
    // Model face order matches UV corners (u0,v0), (u0,v1), (u1,v1), (u1,v0).
    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
        new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
        new[] { new Vector3(1,1,0), new Vector3(1,0,0), new Vector3(0,0,0), new Vector3(0,1,0) },
        new[] { new Vector3(0,1,1), new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1) },
        new[] { new Vector3(0,1,0), new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1) },
        new[] { new Vector3(1,1,1), new Vector3(1,0,1), new Vector3(1,0,0), new Vector3(1,1,0) },
    };

    private readonly List<string> _errors;
    public PackModelBaker(List<string> errors) => _errors = errors;

    private static string ResolveTexture(PackModelDefinition m, string t)
    {
        for (int i = 0; i < 16 && t != null && t.StartsWith('#'); i++) t = m.Textures.GetValueOrDefault(t[1..]);
        if (t == null || t.StartsWith('#')) return null;
        if (t.StartsWith("minecraft:")) t = t["minecraft:".Length..];
        return t;
    }

    public Baked Bake(PackModelDefinition m, int rotX, int rotY)
    {
        var baked = new Baked { AmbientOcclusion = m.AmbientOcclusion };
        foreach (var element in m.Elements.Value.EnumerateArray())
        {
            var from = Vec(element.GetProperty("from")) / 16f; var to = Vec(element.GetProperty("to")) / 16f;
            bool shade = !(element.TryGetProperty("shade", out var shadeElement) && shadeElement.ValueKind == JsonValueKind.False);
            var elementRotation = ElementRotation(element);
            if (!element.TryGetProperty("faces", out var faces)) continue;
            foreach (var face in faces.EnumerateObject())
            {
                int f = Array.IndexOf(PackModelDirections.Names, face.Name);
                if (f < 0) continue;
                var faceDefinition = face.Value;
                string texturePath = faceDefinition.TryGetProperty("texture", out var textureReference) ? ResolveTexture(m, textureReference.GetString()) : null;
                if (texturePath == null) { _errors.Add("brak tekstury ściany " + face.Name); continue; }
                var q = new Quad { Texture = texturePath, Shade = shade, Tint = faceDefinition.TryGetProperty("tintindex", out var tintIndexElement) && tintIndexElement.TryGetInt32(out int tintIndex) && tintIndex >= 0 };
                if (faceDefinition.TryGetProperty("cullface", out var cullFaceElement) && cullFaceElement.ValueKind == JsonValueKind.String)
                    q.CullFace = Array.IndexOf(PackModelDirections.Names, cullFaceElement.GetString() == "bottom" ? "down" : cullFaceElement.GetString());
                BakeFace(q, faceDefinition, f, from, to, elementRotation, rotX, rotY);
                baked.Quads.Add(q);
            }
        }
        return baked;
    }

    private static void BakeFace(Quad q, JsonElement faceDefinition, int f, Vector3 from, Vector3 to,
        Func<Vector3, Vector3> elementRotation, int rotX, int rotY)
    {
        float u0, v0, u1, v1;
        if (faceDefinition.TryGetProperty("uv", out var uv) && uv.ValueKind == JsonValueKind.Array && uv.GetArrayLength() == 4)
        { u0 = uv[0].GetSingle() / 16f; v0 = uv[1].GetSingle() / 16f; u1 = uv[2].GetSingle() / 16f; v1 = uv[3].GetSingle() / 16f; }
        else DefaultUv(f, from, to, out u0, out v0, out u1, out v1);
        int uvRotation = faceDefinition.TryGetProperty("rotation", out var uvRotationElement) && uvRotationElement.TryGetInt32(out int uvRotationDegrees) ? ((uvRotationDegrees / 90) % 4 + 4) % 4 : 0;
        var faceCorners = FaceCorners[f];
        var uvs = new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };
        for (int k = 0; k < 4; k++)
        {
            var p = from + faceCorners[k] * (to - from);
            q.P[k] = RotateBlock(elementRotation(p), rotX, rotY);
            q.Uv[k] = uvs[(k + uvRotation) & 3];
        }
        q.Normal = NearestFace(Vector3.Cross(q.P[1] - q.P[0], q.P[3] - q.P[0]));
        if (q.CullFace >= 0) q.CullFace = NearestFace(RotateBlock(new Vector3(.5f, .5f, .5f) + DirVec(q.CullFace), rotX, rotY) - new Vector3(.5f, .5f, .5f));
    }

    private static Func<Vector3, Vector3> ElementRotation(JsonElement element)
    {
        Func<Vector3, Vector3> elementRotation = p => p;
        if (element.TryGetProperty("rotation", out var r) && r.ValueKind == JsonValueKind.Object)
        {
            var origin = Vec(r.GetProperty("origin")) / 16f;
            float angle = r.GetProperty("angle").GetSingle() * MathF.PI / 180f;
            string axis = r.GetProperty("axis").GetString();
            bool rescale = r.TryGetProperty("rescale", out var rescaleElement) && rescaleElement.ValueKind == JsonValueKind.True;
            float scale = rescale ? 1f / MathF.Cos(MathF.Abs(angle)) : 1f;
            elementRotation = p =>
            {
                var d = p - origin; float c = MathF.Cos(angle), s = MathF.Sin(angle);
                d = axis switch
                {
                    "x" => new Vector3(d.X, d.Y * c - d.Z * s, d.Y * s + d.Z * c),
                    "y" => new Vector3(d.X * c + d.Z * s, d.Y, -d.X * s + d.Z * c),
                    _ => new Vector3(d.X * c - d.Y * s, d.X * s + d.Y * c, d.Z),
                };
                if (rescale) d = axis switch { "x" => new Vector3(d.X, d.Y * scale, d.Z * scale), "y" => new Vector3(d.X * scale, d.Y, d.Z * scale), _ => new Vector3(d.X * scale, d.Y * scale, d.Z) };
                return d + origin;
            };
        }
        return elementRotation;
    }

    private static Vector3 Vec(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    /// <summary>UV ściany z pozycji elementu (FaceBakery 1.8), jak w EmitBox.</summary>
    private static void DefaultUv(int f, Vector3 a, Vector3 b, out float u0, out float v0, out float u1, out float v1)
    {
        switch (f)
        {
            case 0: u0 = a.X; u1 = b.X; v0 = 1 - b.Z; v1 = 1 - a.Z; break;
            case 1: u0 = a.X; u1 = b.X; v0 = a.Z; v1 = b.Z; break;
            case 2: u0 = 1 - b.X; u1 = 1 - a.X; v0 = 1 - b.Y; v1 = 1 - a.Y; break;
            case 3: u0 = a.X; u1 = b.X; v0 = 1 - b.Y; v1 = 1 - a.Y; break;
            case 4: u0 = a.Z; u1 = b.Z; v0 = 1 - b.Y; v1 = 1 - a.Y; break;
            default: u0 = 1 - b.Z; u1 = 1 - a.Z; v0 = 1 - b.Y; v1 = 1 - a.Y; break;
        }
    }
}
