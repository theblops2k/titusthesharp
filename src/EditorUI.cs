using System.Numerics;
using Raylib_cs;

namespace TitusTheFox;

// Kit visual del visor/editor: tema oscuro, tipografia TTF del sistema, paneles y
// widgets con soporte de mouse (hover, cursor, click). Se dibuja directo en la ventana,
// fuera de la textura 320x200 del juego, para que no quede pixelado como el MS-DOS.
static class EditorUI
{
    public static readonly Color Bg = new(15, 17, 21, 255);
    public static readonly Color PanelBg = new(26, 29, 35, 255);
    public static readonly Color Panel2 = new(36, 40, 47, 255);
    public static readonly Color Fg = new(232, 235, 240, 255);
    public static readonly Color Muted = new(136, 144, 156, 255);
    public static readonly Color Accent = new(228, 168, 58, 255);
    public static readonly Color AccentSoft = new(70, 55, 25, 255);
    public static readonly Color Danger = new(220, 92, 80, 255);
    public static readonly Color Line = new(255, 255, 255, 20);

    // Modal abierto: los widgets dejan de responder al mouse
    public static bool Locked;

    // Escala de la interfaz respecto al diseno base (ventana de 960x600).
    // Los textos llevan ademas un +25% para que se lean mejor en pantallas grandes.
    public static float Scale => Math.Clamp(Raylib.GetScreenHeight() / 600f, 0.72f, 3.2f);
    public static int FontSize(int designSize) => Math.Max(8, (int)Math.Round(designSize * 1.25f * Scale));

    static Font font, bold;
    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        font = LoadFont("segoeui.ttf", 42);
        bold = LoadFont("segoeuib.ttf", 42);
        if (bold.Texture.Id == 0) bold = font;
    }

    static Font LoadFont(string file, int size)
    {
        try
        {
            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
            if (!File.Exists(path)) return Raylib.GetFontDefault();
            Font f = Raylib.LoadFontEx(path, size, null!, 0);
            if (f.Texture.Id != 0)
            {
                Raylib.SetTextureFilter(f.Texture, TextureFilter.Bilinear);
                return f;
            }
        }
        catch { }
        return Raylib.GetFontDefault();
    }

    public static void Unload()
    {
        if (!loaded) return;
        loaded = false;
        Font def = Raylib.GetFontDefault();
        if (font.Texture.Id != 0 && font.Texture.Id != def.Texture.Id) Raylib.UnloadFont(font);
        if (bold.Texture.Id != 0 && bold.Texture.Id != def.Texture.Id && bold.Texture.Id != font.Texture.Id) Raylib.UnloadFont(bold);
    }

    public static bool Hover(Rectangle r) => Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);

    public static void Text(string s, float x, float y, int size, Color c, bool strong = false)
        => Raylib.DrawTextEx(strong ? bold : font, s, new Vector2(x, y), FontSize(size), 0.4f, c);

    public static float TextW(string s, int size, bool strong = false)
        => Raylib.MeasureTextEx(strong ? bold : font, s, FontSize(size), 0.4f).X;

    public static void TextCentered(string s, Rectangle r, int size, Color c, bool strong = false)
        => Text(s, r.X + (r.Width - TextW(s, size, strong)) / 2f,
            r.Y + (r.Height - FontSize(size)) / 2f - Scale, size, c, strong);

    public static void TextRight(string s, float right, float y, int size, Color c, bool strong = false)
        => Text(s, right - TextW(s, size, strong), y, size, c, strong);

    public static void Panel(Rectangle r, string? title = null)
    {
        float s = Scale;
        Raylib.DrawRectangleRounded(r, 0.06f, 10, PanelBg);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.06f, 10, Math.Max(1f, s), Line);
        if (title != null) Text(title, r.X + 14 * s, r.Y + 12 * s, 11, Muted, true);
    }

    // Boton: devuelve true al hacer click. primary = relleno dorado.
    public static bool Button(Rectangle r, string label, bool primary = false, int size = 14, bool force = false)
    {
        bool hover = (!Locked || force) && Hover(r);
        bool down = hover && Raylib.IsMouseButtonDown(MouseButton.Left);
        Color fill = primary ? Accent : Panel2;
        if (hover) fill = primary ? new Color(240, 184, 74, 255) : new Color(47, 52, 61, 255);
        if (down) fill = primary ? new Color(198, 143, 45, 255) : new Color(31, 35, 42, 255);
        Raylib.DrawRectangleRounded(r, 0.28f, 10, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.28f, 10, Math.Max(1f, Scale), primary ? new Color(255, 220, 140, 90) : Line);
        TextCentered(label, r, size, primary ? new Color(22, 22, 24, 255) : Fg, primary);
        if (hover) Raylib.SetMouseCursor(MouseCursor.PointingHand);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    // Boton destructivo (borrar / quitar).
    public static bool DangerButton(Rectangle r, string label, int size = 14, bool force = false)
    {
        bool hover = (!Locked || force) && Hover(r);
        bool down = hover && Raylib.IsMouseButtonDown(MouseButton.Left);
        Color fill = new(92, 38, 37, 255);
        if (hover) fill = new(132, 48, 46, 255);
        if (down) fill = new(74, 30, 29, 255);
        Raylib.DrawRectangleRounded(r, 0.28f, 10, fill);
        Raylib.DrawRectangleRoundedLinesEx(r, 0.28f, 10, Math.Max(1f, Scale), new Color(255, 150, 140, 80));
        TextCentered(label, r, size, new Color(255, 226, 222, 255), true);
        if (hover) Raylib.SetMouseCursor(MouseCursor.PointingHand);
        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    // Fila de capa: punto de color, nombre, atajo y switch.
    // selected marca la capa que se esta editando; switchHit distingue el click en el
    // interruptor (visibilidad) del click en el resto de la fila (seleccionar capa).
    public static bool Toggle(Rectangle r, string label, bool on, string key, Color dot,
        bool selected, out bool switchHit)
    {
        float s = Scale;
        var mouse = Raylib.GetMousePosition();
        bool hover = Hover(r);
        var sw = new Rectangle(r.X + r.Width - 46 * s, r.Y + r.Height / 2f - 9 * s, 36 * s, 18 * s);

        if (selected) Raylib.DrawRectangleRounded(r, 0.2f, 8, AccentSoft);
        else if (hover && !Locked) Raylib.DrawRectangleRounded(r, 0.2f, 8, Panel2);
        if (selected) Raylib.DrawRectangle((int)r.X, (int)(r.Y + 3 * s), (int)(3 * s), (int)(r.Height - 6 * s), Accent);

        var marker = new Rectangle(r.X + 10 * s, r.Y + r.Height / 2f - 4 * s, 8 * s, 8 * s);
        Raylib.DrawRectangleRounded(marker, 0.5f, 6, on ? dot : new Color(68, 74, 84, 255));
        Text(label, r.X + 26 * s, r.Y + (r.Height - FontSize(14)) / 2f - s, 14,
            selected ? Accent : on ? Fg : Muted, selected);

        Raylib.DrawRectangleRounded(sw, 0.5f, 12, on ? Accent : new Color(52, 57, 65, 255));
        float kx = on ? sw.X + sw.Width - 12 * s : sw.X + 12 * s;
        Raylib.DrawCircleV(new Vector2(kx, sw.Y + 9 * s), 6 * s, on ? new Color(25, 25, 28, 255) : new Color(175, 183, 194, 255));
        TextRight(key, r.X + r.Width - 54 * s, r.Y + (r.Height - FontSize(11)) / 2f - s, 11, Muted, true);

        if (hover && !Locked) Raylib.SetMouseCursor(MouseCursor.PointingHand);
        bool clicked = !Locked && hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
        switchHit = clicked && Raylib.CheckCollisionPointRec(mouse, sw);
        return clicked;
    }
}
