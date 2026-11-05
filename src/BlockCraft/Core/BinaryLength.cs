namespace BlockCraft.Core;

/// <summary>Odczyt długości z danych binarnych (zapis, sieć) przed alokacją: zła długość daje błąd danych, nie ogromną tablicę.</summary>
public static class BinaryLength
{
    /// <summary>Porcja odczytu ze strumienia bez znanej długości: pamięć rośnie tylko o dane, które naprawdę przyszły.</summary>
    private const int ChunkSize = 1 << 16;

    /// <summary>Długość nieujemna; w strumieniu o znanej długości nie większa, niż zmieści reszta (minSize bajtów na element).</summary>
    public static int Read(BinaryReader br, int minSize = 1)
    {
        int n = br.ReadInt32();
        var st = br.BaseStream;
        if (n < 0 || st.CanSeek && (long)n * minSize > st.Length - st.Position) throw new InvalidDataException("Błędna długość danych " + n);
        return n;
    }

    /// <summary>Tablica bajtów poprzedzona długością (Int32).</summary>
    public static byte[] ReadBytes(BinaryReader br) => ReadExactly(br, Read(br));

    /// <summary>Dokładnie count bajtów; bez znanej długości strumienia czyta porcjami, więc krótkie dane nie wymuszą dużej alokacji.</summary>
    public static byte[] ReadExactly(BinaryReader br, int count)
    {
        if (br.BaseStream.CanSeek || count <= ChunkSize) return Complete(br.ReadBytes(count), count);
        using var ms = new MemoryStream();
        for (int left = count; left > 0; left -= ChunkSize)
        {
            int part = Math.Min(left, ChunkSize);
            ms.Write(Complete(br.ReadBytes(part), part));
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Rekord poprzedzony długością czytany bez kopiowania: load widzi tylko ten rekord, a nieprzeczytana reszta jest pomijana.
    /// Długość z nagłówka nie wymusza więc alokacji (np. gigabajt zer w skompresowanym pakiecie).
    /// </summary>
    public static T ReadRecord<T>(BinaryReader br, Func<BinaryReader, T> load)
    {
        var record = new BoundedStream(br.BaseStream, Read(br));
        try { return load(new BinaryReader(record, System.Text.Encoding.UTF8, leaveOpen: true)); }
        finally { record.SkipRest(); }
    }

    private static byte[] Complete(byte[] data, int expected) =>
        data.Length == expected ? data : throw new EndOfStreamException("Dane urwane: " + data.Length + " z " + expected + " bajtów");
}

/// <summary>Widok tylko do odczytu na kolejne length bajtów strumienia, bez buforowania.</summary>
internal sealed class BoundedStream(Stream inner, long length) : Stream
{
    private long _left = length;

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _left <= 0 ? 0 : inner.Read(buffer, offset, (int)Math.Min(count, _left));
        _left -= read;
        return read;
    }

    /// <summary>Przeczytanie do końca rekordu przez stały bufor; urwane dane dają błąd.</summary>
    public void SkipRest()
    {
        var buf = new byte[4096];
        while (_left > 0) if (Read(buf, 0, buf.Length) == 0) throw new EndOfStreamException("Dane urwane: brakuje " + _left + " bajtów rekordu");
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
