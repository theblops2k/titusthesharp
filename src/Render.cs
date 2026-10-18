using Raylib_cs;

namespace TitusTheFox;

// Dibujo del tilescreen (indices 0-15) con la paleta del nivel en una textura raylib.
public static class Render
{
    public const int SCALE = 3;
    public const byte NO_TILE = 255; // pixel fuera del mapa: negro

    static Texture2D texture;
    static Color[] pixels = Array.Empty<Color>(); // VIEW_W x TILESCREEN_H (stride = ancho de la textura)
    static bool created;

    public static void Init()
    {
        if (created) return;
        InitTexture();
    }

    // Recrea la textura del nivel con el VIEW_W actual (cambio de WideScreen/FullScreen en caliente)
    public static void Resize()
    {
        if (!created) return;
        Raylib.UnloadTexture(texture);
        InitTexture();
    }

    static void InitTexture()
    {
        int th = Math.Max(Scroll.TILESCREEN_H, Fonts.H); // 200: pantallas de fuente (Fonts.W x Fonts.H)
        pixels = new Color[Scroll.VIEW_W * th];
        Image img = Raylib.GenImageColor(Scroll.VIEW_W, th, Color.Black);
        texture = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        created = true;
    }

    // Rellena tilescreen con las 20x12 tiles desde BITMAP_X/Y (sin cambiar la logica de Scroll).
    public static void FillTileScreen(TITUS_level level)
    {
        Scroll.BITMAP_XM = 0;
        Scroll.BITMAP_YM = 0;
        for (sbyte line = 0; line < Scroll.screen_height; line++)
            Scroll.REFRESH_LINE(level, line);
    }

    public static void Draw(TITUS_level level) => Draw(level, Scroll.tilescreen);

    public static void Draw(TITUS_level level, byte[] ts, int w = Scroll.TILESCREEN_W) => Draw(level.palette, ts, w);

    // w = ancho del buffer ts (320 por defecto; Scroll.VIEW_W para el nivel). NO_TILE se dibuja negro (fuera del mapa)
    public static void Draw(byte[,] pal, byte[] ts, int w = Scroll.TILESCREEN_W)
    {
        int tw = Scroll.VIEW_W, rows = ts.Length / w;
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < w; x++)
            {
                int c = ts[y * w + x];
                if (c == NO_TILE) { pixels[y * tw + x] = Color.Black; continue; }
                c &= 15;
                pixels[y * tw + x] = new Color(pal[c, 0], pal[c, 1], pal[c, 2], (byte)255);
            }
        Raylib.UpdateTexture(texture, pixels);
        Raylib.DrawTexturePro(texture,
            new Rectangle(0, 0, w, rows),
            new Rectangle(0, 0, w * SCALE, rows * SCALE),
            System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    public static void Shutdown()
    {
        if (created) Raylib.UnloadTexture(texture);
    }
}
