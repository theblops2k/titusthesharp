using Raylib_cs;

namespace TitusTheFox;

/// Secuencia final del juego: el nivel 15 (LEVELG) NO es jugable, es el ending.
/// Port de reset.c:68-177 (RESET_LEVEL, rama "if (level->levelid == 16)").
/// Titus y Zoubida se encuentran, se besan, sale el humo, los corazones y "THE END".
public static class Ending
{
    public const int Finished = 0; // la cinemata termino: el nivel acaba y se pasa al siguiente (pantalla final)
    public const int ToMenu = 1;   // ESC: volver al menu principal
    public const int Closed = 2;   // se cerro la ventana

    // reset.c:183 MOVE_HIM: avanza la animacion del sprite; al final de la tabla el valor negativo salta atras
    static void MoveHim(TITUS_level level, TITUS_sprite spr)
    {
        short[] anim = spr.animation!;
        int i = spr.animationIndex + 1;
        while (anim[i] < 0) i += anim[i] / 2;
        spr.animationIndex = i;
        Game.UpdateSprite(level, spr, anim[i], true);
    }

    // Dibuja un frame de la cinemata con la interpolacion del tick
    static void Draw(TITUS_level level, Game.Ticker tk)
    {
        Game.SetDrawAlpha(tk.Alpha);
        Game.DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
    }

    // Igual que Draw pero sin interpolar (las posiciones saltan de un sitio a otro)
    static void DrawExact(TITUS_level level)
    {
        Game.SetDrawAlpha(1.0);
        Game.DrawFrame(level, 0, true, 0, 0, 320, 192, Title.FpsGame);
    }

    public static int Run(TITUS_level level)
    {
        var p = level.player;
        Scroll.NOSCROLL_FLAG = true; // reset.c:69: la camara no se mueve (solo los tiles)

        // reset.c:71-75 Titus/Moktar
        Game.UpdateSprite(level, p.sprite, 343, true);
        p.sprite.x = -100;
        p.sprite.y = 180;
        p.sprite.animation = Original.ANIM_MOKTAR;
        p.sprite.animationIndex = 0;
        // reset.c:77-80 Zoubida
        Game.UpdateSprite(level, p.sprite2, 337, true);
        p.sprite2.x = 420;
        p.sprite2.y = 180;
        p.sprite2.animation = Original.ANIM_ZOUBIDA;
        p.sprite2.animationIndex = 0;

        var tk = new Game.Ticker();

        // reset.c:81-90: se encuentran (Titus viene por la izquierda, Zoubida por la derecha)
        bool met = false;
        while (!met)
        {
            if (Raylib.WindowShouldClose()) return Closed;
            int due = tk.Due();
            for (int n = 0; n < due && !met; n++)
            {
                Game.SnapshotPrev(level);
                Scroll.scroll(level); // animacion de tiles (la camara no se mueve)
                p.sprite.x += 3;      // Titus
                MoveHim(level, p.sprite);
                p.sprite2.x -= 3;     // Zoubida
                MoveHim(level, p.sprite2);
                met = p.sprite2.x <= p.sprite.x + 28;
            }
            Draw(level, tk);
        }

        // reset.c:92-96: los dos juntos (el beso)
        Game.UpdateSprite(level, p.sprite2, 346, true);
        p.sprite2.flipped = true;
        p.sprite2.x -= 24;

        // reset.c:97-110: el humo
        p.sprite.animation = Original.ANIM_SMOKE;
        p.sprite.animationIndex = 0;
        p.sprite.y -= 16;
        int steps = 0;
        while (steps < 16)
        {
            if (Raylib.WindowShouldClose()) return Closed;
            int due = tk.Due();
            for (int n = 0; n < due && steps < 16; n++, steps++)
            {
                Game.SnapshotPrev(level);
                MoveHim(level, p.sprite);
                Scroll.scroll(level);
                p.sprite.y++;
            }
            Draw(level, tk);
        }

        // reset.c:111-155: los corazones (posiciones de la tabla COEUR_POS) hasta pulsar una tecla
        Game.UpdateSprite(level, p.sprite, 355, true);
        int heart = 0;
        bool pass = false;
        while (!pass)
        {
            if (Raylib.WindowShouldClose()) return Closed;
            Input.UpdatePad();
            int due = tk.Due();
            for (int n = 0; n < due; n++)
            {
                if (Original.COEUR_POS[heart] < 0) heart += Original.COEUR_POS[heart]; // -36: vuelve al principio
                p.sprite.x = Original.COEUR_POS[heart++];
                p.sprite.y = Original.COEUR_POS[heart++];
                Scroll.scroll(level);
            }
            DrawExact(level);
            if (Input.Skip()) pass = true;
            else if (Input.Pressed(KeyboardKey.Escape) || Input.PadEscape()) return ToMenu;
        }

        // reset.c:157-172: "THE END" entra desde los lados
        Game.UpdateSprite(level, p.sprite, 335, true);
        p.sprite.x = (short)((Scroll.BITMAP_X << 4) - (120 - 2));
        p.sprite.y = (short)((Scroll.BITMAP_Y << 4) + 100);
        Game.UpdateSprite(level, p.sprite3, 336, true);
        p.sprite3.x = (short)((Scroll.BITMAP_X << 4) + (320 + 120 - 2));
        p.sprite3.y = (short)((Scroll.BITMAP_Y << 4) + 100);
        steps = 0;
        while (steps < 31)
        {
            if (Raylib.WindowShouldClose()) return Closed;
            int due = tk.Due();
            for (int n = 0; n < due && steps < 31; n++, steps++)
            {
                Game.SnapshotPrev(level);
                p.sprite.x += 8;
                p.sprite3.x -= 8;
            }
            Draw(level, tk);
        }

        // reset.c:173-176 waitforbutton: una tecla para terminar
        while (true)
        {
            if (Raylib.WindowShouldClose()) return Closed;
            Input.UpdatePad();
            DrawExact(level);
            if (Input.Skip()) break;
            if (Input.Pressed(KeyboardKey.Escape) || Input.PadEscape()) return ToMenu;
        }

        return Finished;
    }
}
