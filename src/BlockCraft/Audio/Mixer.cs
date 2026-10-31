using NAudio.Wave;
using NVorbis;
using BlockCraft.Core;

namespace BlockCraft.Audio;

/// <summary>Zdekodowane próbki (interleaved float) z pliku ogg.</summary>
public sealed class PcmData
{
    public float[] Data; public int Channels; public int Rate;
    public int Frames => Data.Length / Channels;
}

/// <summary>Jeden głos miksera: dźwięk statyczny albo strumień z pliku ogg.</summary>
public sealed class Voice
{
    public float[] Data; public int Channels = 1; public int Frames; public int Rate = 44100;
    public double Pos; public double Step = 1;
    public bool Positional; public double X, Y, Z; public float AttDist = 16f;
    public float Volume = 1f;            // głośność zdarzenia (0..1) bez kategorii
    public string Category = "master";
    public bool Done; public bool Loop;
    public string Tag;                   // np. klucz płyty "x,y,z"
    public bool IsMusic;
    public string EventKey;
    public float EventPitch = 1f;
    internal long StartedAtFrame;
    // strumień
    public VorbisReader Reader; float[] _tmp;
    public float Fade = 1f, FadeSpeed;   // FadeSpeed < 0: wyciszanie do 0 i koniec

    public void FadeOut(float seconds) { FadeSpeed = -1f / Math.Max(0.05f, seconds) / 44100f; }

    internal bool Refill()
    {
        // wczytuje kolejny kawałek strumienia; false = koniec
        if (Reader == null) return false;
        _tmp ??= new float[8192 * Channels];
        int n = Reader.ReadSamples(_tmp, 0, _tmp.Length);
        if (n <= 0) return false;
        Data = _tmp; Frames = n / Channels;
        return true;
    }
    public void Dispose() { try { Reader?.Dispose(); } catch { } Reader = null; }
}

/// <summary>Mikser float stereo 44.1 kHz z limitem kanałów, pitchem (resampling), tłumieniem 3D i panoramą.</summary>
public sealed class Mixer : ISampleProvider
{
    public const int MaxVoices = 32;
    public const float OutputGain = 0.5f; // 6 dB zapasu przy sumowaniu wielu źródeł
    public const float PeakCeiling = 0.65f;
    const int DuplicateFrames = 44100 / 30; // powtórne zgłoszenie w tej samej klatce audio
    static readonly float LimiterRelease = 1f - MathF.Exp(-1f / (44100f * .15f));
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    readonly List<Voice> _voices = new();
    readonly object _lock = new();
    WaveOutEvent _out;
    long _mixedFrames;
    float _limiterGain = 1f;
    public long SuppressedDuplicates { get; private set; }

    // słuchacz
    public volatile float LX, LY, LZ, LYaw;
    public Func<string, float> CategoryVolume = _ => 1f;

    public bool Start()
    {
        try
        {
            _out = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 3 };
            _out.Init(this);
            _out.Play();
            return true;
        }
        catch (Exception ex) { Log.Error("Mixer: brak urządzenia audio: " + ex.Message); _out = null; return false; }
    }

    public bool Running => _out != null;

    public void Stop() { try { _out?.Stop(); _out?.Dispose(); } catch { } _out = null; Clear(); }

    public void Clear(bool includeMusic = true)
    {
        lock (_lock)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (includeMusic || !_voices[i].IsMusic)
                { _voices[i].Done = true; _voices[i].Dispose(); _voices.RemoveAt(i); }
            if (_voices.Count == 0) _limiterGain = 1f;
        }
    }

    public void Add(Voice v)
    {
        lock (_lock)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].Done) { _voices[i].Dispose(); _voices.RemoveAt(i); }
            if (v.Positional && v.EventKey != null && !v.EventKey.StartsWith("note.") && v.Category != "record")
            {
                int overlaps = 0;
                foreach (var other in _voices)
                {
                    if (other.EventKey != v.EventKey || !other.Positional) continue;
                    double dx = other.X - v.X, dy = other.Y - v.Y, dz = other.Z - v.Z;
                    if (dx * dx + dy * dy + dz * dz > .0625) continue;
                    overlaps++;
                    if (_mixedFrames - other.StartedAtFrame < DuplicateFrames && Math.Abs(other.EventPitch - v.EventPitch) < .01f)
                    { SuppressedDuplicates++; v.Done = true; v.Dispose(); return; }
                }
                if (overlaps >= 2) { SuppressedDuplicates++; v.Done = true; v.Dispose(); return; }
            }
            if (_voices.Count >= MaxVoices)
            {
                // wyrzuć najstarszy nie-muzyczny głos
                for (int i = 0; i < _voices.Count; i++)
                    if (!_voices[i].IsMusic && _voices[i].Tag == null) { _voices[i].Dispose(); _voices.RemoveAt(i); break; }
                if (_voices.Count >= MaxVoices) { v.Done = true; v.Dispose(); return; }
            }
            v.StartedAtFrame = _mixedFrames;
            _voices.Add(v);
        }
    }

    public void ForEach(Action<Voice> a) { lock (_lock) foreach (var v in _voices) a(v); }
    /// <summary>Jeden zapętlony ambient na klucz. Aktualizacja nie tworzy kolejnych kopii próbki.</summary>
    public void SetLoop(string tag, float volume, Func<Voice> create)
    {
        lock (_lock)
        {
            var voice = _voices.FirstOrDefault(v => !v.Done && v.Tag == tag);
            if (volume <= .001f) { if (voice != null) voice.Done = true; return; }
            if (voice != null) { voice.Volume = volume; return; }
        }
        // Dekodowanie OGG nie może zatrzymywać wątku miksera.
        var candidate = create();
        if (candidate == null) return;
        lock (_lock)
        {
            var voice = _voices.FirstOrDefault(v => !v.Done && v.Tag == tag);
            if (voice != null) { voice.Volume = volume; candidate.Dispose(); return; }
            voice = candidate;
            voice.Tag = tag; voice.Loop = true; voice.Volume = volume;
            Add(voice);
        }
    }
    public int Count { get { lock (_lock) return _voices.Count; } }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        int frames = count / 2;
        lock (_lock)
        {
            for (int vi = _voices.Count - 1; vi >= 0; vi--)
            {
                var v = _voices[vi];
                if (v.Done) { v.Dispose(); _voices.RemoveAt(vi); continue; }
                try { MixVoice(v, buffer, offset, frames); } catch (Exception ex) { v.Done = true; Log.Error("Mixer voice: " + ex.Message); }
                if (v.Done) { v.Dispose(); _voices.RemoveAt(vi); }
            }
            // Wspólny poziom dla stereo: szczyt nie przesuwa panoramy.
            // Atak natychmiastowy, powrót 150 ms; stan trwa pomiędzy buforami.
            for (int i = 0; i < frames; i++)
            {
                int p = offset + i * 2;
                float l = buffer[p] * OutputGain, r = buffer[p + 1] * OutputGain;
                if (!float.IsFinite(l)) l = 0;
                if (!float.IsFinite(r)) r = 0;
                float peak = Math.Max(Math.Abs(l), Math.Abs(r));
                float target = peak > PeakCeiling ? PeakCeiling / peak : 1f;
                if (target < _limiterGain) _limiterGain = target;
                else _limiterGain += (target - _limiterGain) * LimiterRelease;
                buffer[p] = l * _limiterGain; buffer[p + 1] = r * _limiterGain;
            }
            _mixedFrames += frames;
        }
        return count;
    }

    void MixVoice(Voice v, float[] buf, int off, int frames)
    {
        float cat = CategoryVolume(v.Category);
        float master = v.Category == "master" ? 1f : CategoryVolume("master");
        float gain = v.Volume * cat * master;
        float gl = 1, gr = 1;
        if (v.Positional)
        {
            double dx = v.X - LX, dy = v.Y - LY, dz = v.Z - LZ;
            double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float att = (float)Math.Max(0, 1 - dist / v.AttDist);
            gain *= att;
            if (dist > 0.01)
            {
                double yaw = LYaw * Math.PI / 180.0;
                double rx = -Math.Cos(yaw), rz = -Math.Sin(yaw);
                double pan = (dx * rx + dz * rz) / Math.Sqrt(dx * dx + dz * dz + 0.0001);
                pan *= Math.Min(1.0, Math.Sqrt(dx * dx + dz * dz) / 2.0); // blisko słuchacza: środek
                if (pan > 0) gl = (float)(1 - pan * 0.85); else gr = (float)(1 + pan * 0.85);
            }
        }
        if (gain <= 0.0001f && v.FadeSpeed == 0) { Advance(v, frames); return; }

        int ch = v.Channels;
        for (int i = 0; i < frames; i++)
        {
            if (v.Data == null || v.Pos >= v.Frames)
            {
                if (v.Reader != null)
                {
                    double over = v.Pos - v.Frames;
                    if (!v.Refill()) { v.Done = true; return; }
                    v.Pos = Math.Max(0, over);
                }
                else if (v.Loop) v.Pos -= v.Frames;
                else { v.Done = true; return; }
            }
            int i0 = (int)v.Pos; float fr = (float)(v.Pos - i0);
            int i1 = i0 + 1 < v.Frames ? i0 + 1 : i0;
            float l, r;
            if (ch == 1)
            {
                l = r = v.Data[i0] + (v.Data[i1] - v.Data[i0]) * fr;
            }
            else
            {
                float a0 = v.Data[i0 * ch], a1 = v.Data[i1 * ch], b0 = v.Data[i0 * ch + 1], b1 = v.Data[i1 * ch + 1];
                l = a0 + (a1 - a0) * fr; r = b0 + (b1 - b0) * fr;
                if (v.Positional) l = r = (l + r) * 0.5f;
            }
            float f = v.Fade;
            if (v.FadeSpeed != 0) { v.Fade += v.FadeSpeed; if (v.Fade <= 0) { v.Done = true; return; } }
            float g = gain * f;
            buf[off + i * 2] += l * g * gl;
            buf[off + i * 2 + 1] += r * g * gr;
            v.Pos += v.Step;
        }
    }

    void Advance(Voice v, int frames)
    {
        v.Pos += v.Step * frames;
        if (v.Reader != null)
        {
            while (v.Pos >= v.Frames) { double over = v.Pos - v.Frames; if (!v.Refill()) { v.Done = true; return; } v.Pos = over; }
        }
        else if (v.Pos >= v.Frames) { if (v.Loop) v.Pos %= Math.Max(1, v.Frames); else v.Done = true; }
    }
}
