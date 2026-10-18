namespace TitusTheFox;

// Packed: descompresor del empaquetado ZIV de FOX.COM (LZW, bits MSB-first, codigos 9-12 bits).
// Port fiel de docs\ghidra\Unpack.java (hallazgos en docs\ghidra\hallazgos.md).
public static class Packed
{
    const int HEADER_POS = 0x316;      // Unpack.java:7 cabecera (00 10 F1 F9); datos desde 0x31A
    public const int EXE_SIZE = 63985; // tamano del EXE MZ resultante
    public const int MUSIC_POS = 0x7005; // offset de la musica dentro del EXE
    public const int MUSIC_SIZE = 18749;

    // Unpack.java:3-22
    public static byte[]? Unpack(byte[] d, int pos = HEADER_POS)
    {
        if (d.Length < pos + 4) return null;
        long bp = (long)(pos + 4) * 8;
        int width = 9, limit = 0x200, next = 0x102;
        var pre = new int[4096]; var suf = new int[4096];
        var o = new List<byte>(EXE_SIZE + 16);
        var st = new byte[5000];
        bool fresh = true; int old = 0, last = 0;
        bool eof = false;

        int Get() // Unpack.java:4
        {
            if (next == limit && width != 12) { width++; limit <<= 1; }
            int v = 0;
            for (int i = 0; i < width; i++)
            {
                long by = bp >> 3;
                if (by >= d.Length) { eof = true; return 0x101; }
                v = (v << 1) | ((d[by] >> (int)(7 - (bp & 7))) & 1);
                bp++;
            }
            return v;
        }

        while (true)
        {
            if (o.Count > EXE_SIZE) return null;
            if (fresh) // Unpack.java:13
            {
                width = 9; limit = 0x200; next = 0x102;
                int c0 = Get();
                if (c0 == 0x101) break;
                if (c0 == 0x100) continue;
                old = c0; last = c0; o.Add((byte)c0); fresh = false; continue;
            }
            int c = Get();
            if (c == 0x100) { fresh = true; continue; }
            if (c == 0x101) break;
            int inc = c, sp = 0;
            if (c >= next) { st[sp++] = (byte)last; c = old; } // caso KwKwK
            while (c >= 0x100)
            {
                if (sp >= st.Length - 1 || c >= next) return null;
                st[sp++] = (byte)suf[c]; c = pre[c];
            }
            st[sp++] = (byte)c; last = c;
            while (sp > 0) o.Add(st[--sp]);
            if (next < 0x1000) { suf[next] = last; pre[next] = old; next++; }
            old = inc;
        }
        return eof ? null : o.ToArray();
    }

    /// <summary>Extrae la musica de FOX.COM; null si el tamano o la cabecera MZ no coinciden.</summary>
    public static byte[]? ReadMusic(string foxCom)
    {
        try
        {
            var exe = Unpack(File.ReadAllBytes(foxCom));
            if (exe == null || exe.Length != EXE_SIZE) return null;
            if (exe[0] != (byte)'M' || exe[1] != (byte)'Z') return null;
            if (MUSIC_POS + MUSIC_SIZE > exe.Length) return null;
            var m = new byte[MUSIC_SIZE];
            Array.Copy(exe, MUSIC_POS, m, 0, MUSIC_SIZE);
            return m;
        }
        catch { return null; }
    }
}
