namespace TitusTheFox;

// Port de gates.c (solo check_finish; check_gates pendiente)
public static class Gates
{
    internal static bool NEWLEVEL_FLAG;

    // gates.c:39 CROSSING_GATE
    internal static bool CrossingGate(TITUS_level level)
    {
        bool fin = CheckFinish(level);
        CheckGates(level);
        return fin;
    }

    // gates.c:75 check_gates: agachado sobre una puerta -> salida con el scroll de la puerta
    internal static void CheckGates(TITUS_level level)
    {
        TITUS_player player = level.player;
        if (Player.CROSS_FLAG == 0 || NEWLEVEL_FLAG) return;
        int i;
        for (i = 0; i < level.gatecount; i++)
            if (level.gate[i].exists &&
                level.gate[i].entranceX == (uint)(player.sprite.x >> 4) &&
                level.gate[i].entranceY == (uint)(player.sprite.y >> 4)) break;
        if (i == level.gatecount) return;
        TITUS_gate g = level.gate[i];
        player.sprite.speedX = 0;
        player.sprite.speedY = 0;
        short origXLimit = Scroll.XLIMIT;
        Scroll.XLIMIT = (short)(level.width - Scroll.screen_width);
        player.sprite.x = (short)(g.exitX << 4);
        player.sprite.y = (short)(g.exitY << 4);
        // CLOSE/OPEN_SCREEN omitidos; el break evita bucle infinito si el scroll topa
        while (Scroll.BITMAP_Y < g.screenY) if (Scroll.D_SCROLL(level)) break;
        while (Scroll.BITMAP_Y > g.screenY) if (Scroll.U_SCROLL(level)) break;
        while (Scroll.BITMAP_X < g.screenX) if (Scroll.R_SCROLL(level)) break;
        while (Scroll.BITMAP_X > g.screenX) if (Scroll.L_SCROLL(level)) break;
        Scroll.XLIMIT = origXLimit;
        Scroll.NOSCROLL_FLAG = g.noscroll;
    }

    // gates.c:45 check_finish: true si el nivel termina
    internal static bool CheckFinish(TITUS_level level)
    {
        TITUS_sprite s = level.player.sprite;
        if (Enemies.boss_alive) return false; // aun hay un boss vivo
        if (level.levelid == 9) // nivel de la jaula
        {
            if (level.player.sprite2.number != Objects.FIRST_OBJET + 26 &&
                level.player.sprite2.number != Objects.FIRST_OBJET + 27) return false;
        }
        if ((s.x & 0xFFF0) != level.finishX && (s.x & 0xFFF0) - 16 != level.finishX) return false;
        if ((s.y & 0xFFF0) != level.finishY && (s.y & 0xFFF0) - 16 != level.finishY) return false;
        NEWLEVEL_FLAG = true; Audio.SelectMusic(4); // gates.c:65 (WAIT_SONG/CLOSE_SCREEN en Game.Run)
        return true;
    }
}
