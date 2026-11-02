namespace BlockCraft.World;

/// <summary>Tablica MathHelper z 1.8: geometria jaskiń i żył używa zaokrąglonych sinusów float.</summary>
public static class McMath
{
    private static readonly float[] Sines = Enumerable.Range(0, 65536).Select(i => (float)Math.Sin(i * Math.PI * 2.0 / 65536.0)).ToArray();
    public static float Sin(float x) => Sines[(int)(x * 10430.378f) & 65535];
    public static float Cos(float x) => Sines[(int)(x * 10430.378f + 16384f) & 65535];
}

/// <summary>MapGenCaves i MapGenRavine 1.8, bez stanu współdzielonego między wątkami.</summary>
public static class OwCaves
{
    public static void Generate(long seed, int cx, int cz, ProtoChunk p)
    {
        var r = new JRandom(seed);
        long mx = r.NextLong(), mz = r.NextLong();
        for (int mode = 0; mode < 2; mode++)
            for (int sx = cx - 8; sx <= cx + 8; sx++)
                for (int sz = cz - 8; sz <= cz + 8; sz++)
                {
                    r.SetSeed(unchecked(sx * mx ^ sz * mz ^ seed));
                    if (mode == 1)
                    {
                        if (r.NextInt(50) != 0) continue;
                        double x = sx * 16 + r.NextInt(16), y = r.NextInt(r.NextInt(40) + 8) + 20, z = sz * 16 + r.NextInt(16);
                        float yaw = r.NextFloat() * MathF.PI * 2f, pitch = (r.NextFloat() - 0.5f) * 2f / 8f;
                        float width = (r.NextFloat() * 2f + r.NextFloat()) * 2f;
                        Tunnel(p, cx, cz, r.NextLong(), x, y, z, width, yaw, pitch, 0, 0, 3, true);
                        continue;
                    }
                    int count = r.NextInt(r.NextInt(r.NextInt(15) + 1) + 1);
                    if (r.NextInt(7) != 0) count = 0;
                    for (int i = 0; i < count; i++)
                    {
                        double x = sx * 16 + r.NextInt(16), y = r.NextInt(r.NextInt(120) + 8), z = sz * 16 + r.NextInt(16);
                        int branches = 1;
                        if (r.NextInt(4) == 0)
                        {
                            long roomSeed = r.NextLong();
                            Tunnel(p, cx, cz, roomSeed, x, y, z, 1f + r.NextFloat() * 6f, 0, 0, -1, -1, 0.5, false);
                            branches += r.NextInt(4);
                        }
                        for (int b = 0; b < branches; b++)
                        {
                            float yaw = r.NextFloat() * MathF.PI * 2f, pitch = (r.NextFloat() - 0.5f) * 2f / 8f;
                            float width = r.NextFloat() * 2f + r.NextFloat();
                            if (r.NextInt(10) == 0) width *= r.NextFloat() * r.NextFloat() * 3f + 1f;
                            Tunnel(p, cx, cz, r.NextLong(), x, y, z, width, yaw, pitch, 0, 0, 1, false);
                        }
                    }
                }
    }

    private static void Tunnel(ProtoChunk p, int cx, int cz, long seed, double x, double y, double z,
        float width, float yaw, float pitch, int step, int end, double scaleY, bool ravine)
    {
        var r = new JRandom(seed);
        double centerX = cx * 16 + 8, centerZ = cz * 16 + 8;
        float yawVelocity = 0, pitchVelocity = 0;
        if (end <= 0) end = 112 - r.NextInt(28);
        bool room = step == -1;
        if (room) step = end / 2;
        int branch = 0; bool steep = false;
        float[] shape = null;
        if (ravine)
        {
            shape = new float[256]; float f = 1;
            for (int j = 0; j < 256; j++)
            {
                if (j == 0 || r.NextInt(3) == 0) f = 1f + r.NextFloat() * r.NextFloat();
                shape[j] = f * f;
            }
        }
        else { branch = r.NextInt(end / 2) + end / 4; steep = r.NextInt(6) == 0; }
        for (; step < end; step++)
        {
            double rh = 1.5 + McMath.Sin(step * MathF.PI / end) * width, rv = rh * scaleY;
            if (ravine) { rh *= r.NextFloat() * 0.25 + 0.75; rv *= r.NextFloat() * 0.25 + 0.75; }
            float cp = McMath.Cos(pitch);
            x += McMath.Cos(yaw) * cp; y += McMath.Sin(pitch); z += McMath.Sin(yaw) * cp;
            pitch *= steep ? 0.92f : 0.7f;
            pitch += pitchVelocity * (ravine ? 0.05f : 0.1f); yaw += yawVelocity * (ravine ? 0.05f : 0.1f);
            pitchVelocity *= ravine ? 0.8f : 0.9f; yawVelocity *= ravine ? 0.5f : 0.75f;
            pitchVelocity += (r.NextFloat() - r.NextFloat()) * r.NextFloat() * 2f;
            yawVelocity += (r.NextFloat() - r.NextFloat()) * r.NextFloat() * 4f;
            if (!ravine && !room && step == branch && width > 1f && end > 0)
            {
                long s = r.NextLong(); float w = r.NextFloat() * 0.5f + 0.5f;
                Tunnel(p, cx, cz, s, x, y, z, w, yaw - MathF.PI / 2f, pitch / 3f, step, end, 1, false);
                s = r.NextLong(); w = r.NextFloat() * 0.5f + 0.5f;
                Tunnel(p, cx, cz, s, x, y, z, w, yaw + MathF.PI / 2f, pitch / 3f, step, end, 1, false);
                return;
            }
            if (!room && r.NextInt(4) == 0) continue;
            double dx = x - centerX, dz = z - centerZ, remaining = end - step, reach = width + 2f + 16f;
            if (dx * dx + dz * dz - remaining * remaining > reach * reach) return;
            if (x < centerX - 16 - rh * 2 || z < centerZ - 16 - rh * 2 || x > centerX + 16 + rh * 2 || z > centerZ + 16 + rh * 2) continue;
            if (Carve(p, cx, cz, x, y, z, rh, rv, shape) && room) break;
        }
    }

    private static bool Carve(ProtoChunk p, int cx, int cz, double x, double y, double z, double rh, double rv, float[] shape)
    {
        int x0 = Math.Max(0, (int)Math.Floor(x - rh) - cx * 16 - 1), x1 = Math.Min(16, (int)Math.Floor(x + rh) - cx * 16 + 1);
        int z0 = Math.Max(0, (int)Math.Floor(z - rh) - cz * 16 - 1), z1 = Math.Min(16, (int)Math.Floor(z + rh) - cz * 16 + 1);
        int y0 = Math.Max(1, (int)Math.Floor(y - rv) - 1), y1 = Math.Min(248, (int)Math.Floor(y + rv) + 1);
        for (int xx = x0; xx < x1; xx++)
            for (int zz = z0; zz < z1; zz++)
                for (int yy = y1 + 1; yy >= y0 - 1; yy--)
                {
                    if ((uint)yy < 256 && p.Get(xx, yy, zz) == B.Water) return false;
                    if (yy != y0 - 1 && xx != x0 && xx != x1 - 1 && zz != z0 && zz != z1 - 1) yy = y0;
                }
        for (int xx = x0; xx < x1; xx++)
        {
            double dx = (xx + cx * 16 + 0.5 - x) / rh;
            for (int zz = z0; zz < z1; zz++)
            {
                double dz = (zz + cz * 16 + 0.5 - z) / rh;
                bool surface = false;
                if (dx * dx + dz * dz >= 1) continue;
                for (int yy = y1; yy > y0; yy--)
                {
                    double dy = (yy - 1 + 0.5 - y) / rv;
                    if (shape == null ? dy <= -0.7 || dx * dx + dy * dy + dz * dz >= 1 : (dx * dx + dz * dz) * shape[yy - 1] + dy * dy / 6 >= 1) continue;
                    int id = p.Get(xx, yy, zz), above = p.Get(xx, yy + 1, zz);
                    if (id == B.Grass || shape == null && id == B.Mycelium) surface = true;
                    bool replace = id is B.Stone or B.Dirt or B.Grass;
                    if (shape == null) replace |= id is B.HardenedClay or B.StainedClay or B.Sandstone or B.RedSandstone or B.Mycelium or B.SnowLayer
                        || id is B.Sand or B.Gravel && above != B.Water;
                    if (!replace) continue;
                    p.Set(xx, yy, zz, yy - 1 < 10 ? B.Lava : B.Air);
                    if (yy - 1 < 10) continue;
                    if (shape == null && above == B.Sand) p.Set(xx, yy + 1, zz, p.Meta[Chunk.Index(xx, yy + 1, zz)] == 1 ? B.RedSandstone : B.Sandstone);
                    if (surface && p.Get(xx, yy - 1, zz) == B.Dirt)
                    {
                        var b = Biome.Get(p.Biomes[xx + zz * 16]);
                        p.Set(xx, yy - 1, zz, b.Top, shape == null ? 0 : b.TopMeta);
                    }
                }
            }
        }
        return true;
    }
}
