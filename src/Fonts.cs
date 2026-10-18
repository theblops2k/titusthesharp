using Raylib_cs;

namespace TitusTheFox;

// Port de fonts.c (loadfonts, SDL_LoadChar, SDL_Print_Text) y draw.c:407 viewstatus.
// Pinta sobre un buffer de indices 320x200 (paleta PaletteFontColour).
public static class Fonts
{
    public const int W = 320, H = 200, CW = 8, CH = 12;

    static byte[]? fontdata;

    // fonts.c:53 unSQZ(fontfile)
    public static void Load(string dir) => fontdata = File.ReadAllBytes(Path.Combine(dir, "FONTS.bin"));

    // fonts.c:125-175: 0-9 -> 0-9, '!' 10, '?' 11, '.' 12, '$' 13, '_' 14, A-Z 15-40 (a-z igual); ' ' = 41 (vacio)
    static int Glyph(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'z') c = char.ToUpperInvariant(c);
        if (c >= 'A' && c <= 'Z') return c - 'A' + 15;
        return c switch { '!' => 10, '?' => 11, '.' => 12, '$' => 13, '_' => 14, ' ' => -1, _ => 11 };
    }

    // fonts.c:208 SDL_LoadChar + SDL_Print_Text: cada caracter 8x12, 4 planos de 12 bytes
    public static void Print(byte[] buf, string text, int x, int y)
    {
        foreach (char ch in text)
        {
            int g = Glyph(ch);
            for (int row = 0; row < CH; row++)
                for (int col = 0; col < CW; col++)
                {
                    int px = x + col, py = y + row;
                    if (px < 0 || px >= W || py < 0 || py >= H) continue;
                    byte v = 1; // espacio: indice 1 (negro)
                    if (g >= 0 && fontdata != null)
                    {
                        int i = g * 48 + row, j = 7 - col;
                        v = (byte)(((fontdata[i] >> j) & 1)
                            | ((fontdata[i + 12] >> j << 1) & 2)
                            | ((fontdata[i + 24] >> j << 2) & 4)
                            | ((fontdata[i + 36] >> j << 3) & 8));
                    }
                    buf[py * W + px] = v;
                }
            x += CW;
        }
    }

    static void PrintRight(byte[] buf, string s, int right, int y) => Print(buf, s, right - s.Length * CW, y);

    // draw.c:407 viewstatus (juego Titus): pantalla de estado con nivel, bonus y vidas
    public static void ViewStatus(byte[] buf, TITUS_level level)
    {
        Array.Fill(buf, (byte)1); // SDL_FillRect negro
        Print(buf, "LEVEL", 13 * 8, 12 * 5);
        Print(buf, "EXTRA BONUS", 10 * 8, 10 * 12);
        Print(buf, "LIVES", 10 * 8, 11 * 12);
        PrintRight(buf, (level.levelnumber + 1).ToString(), 25 * 8, 12 * 5);
        PrintRight(buf, level.extrabonus.ToString(), 28 * 8, 10 * 12);
        PrintRight(buf, level.lives.ToString(), 28 * 8, 11 * 12);
    }
}
