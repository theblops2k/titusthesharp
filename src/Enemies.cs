namespace TitusTheFox;

// Port parcial de OpenTitus enemies.c (+ reset.c SET_ALL_SPRITES/SET_DATA_NMI, parte de enemigos).
// Tipos 0-18 (movimiento), boss (BIGNMI_POWER, boss_alive), SEECHOC.
// Con: DEAD1/dying, ACTIONC_NMI (dano al jugador), NMI_VS_DROP, colision con objetos.
// Sin: disparos.
public static class Enemies
{
    // reset.c:310-325 + SET_DATA_NMI (reset.c:260)
    // Globales del boss (globals.h). Fuente unica; INVULNERABLE_FLAG se decrementa en player.c:341 (subto0).
    internal static short BIGNMI_POWER;
    internal static byte INVULNERABLE_FLAG;
    internal static bool boss_alive;

    public static void Init(TITUS_level level)
    {
        boss_alive = false;
        INVULNERABLE_FLAG = 0;
        BIGNMI_POWER = Original.NMI_POWER[level.levelid]; // reset.c:274
        for (int i = 0; i < level.enemycount; i++)
        {
            TITUS_enemy e = level.enemy[i];
            e.sprite = new TITUS_sprite(); // clearsprite
            e.dying = 0;
            e.carry_sprite = -1;
            e.dead_sprite = -1;
            e.phase = 0;
            e.counter = 0;
            e.trigger = false;
            e.visible = false;
            if (!e.init_enabled) continue;
            if (e.type > 18) continue; // fuera de alcance: se queda deshabilitado
            UpdateEnemySprite(level, e, (short)((e.initsprite & 0x1FFF) + Objects.FIRST_NMI), true);
            e.sprite.flipped = (e.initsprite & 0x8000) == 0x8000;
            e.sprite.x = (short)e.initX;
            e.sprite.y = (short)e.initY;
            e.sprite.speedX = (short)e.initspeedX;
            e.sprite.speedY = (short)e.initspeedY;

            // SET_DATA_NMI: puntero a la animacion que empieza con este sprite
            int anim = -1;
            do
            {
                anim++;
            } while (anim < Original.ANIM_ENEMY.Length && Original.ANIM_ENEMY[anim] + Objects.FIRST_NMI != e.sprite.number);
            if (anim >= Original.ANIM_ENEMY.Length) { e.sprite.enabled = false; continue; }
            e.sprite.animation = Original.ANIM_ENEMY;
            e.sprite.animationIndex = anim;
            if (e.boss) boss_alive = true; // reset.c:270
        }
    }

    // enemies.c:1036 updateenemysprite
    static void UpdateEnemySprite(TITUS_level level, TITUS_enemy e, short n, bool clearflags)
    {
        Game.UpdateSprite(level, e.sprite, n, clearflags);

        if (n >= 101 && n <= 105) e.carry_sprite = 105;
        else if (n >= 126 && n <= 130) e.carry_sprite = 130;
        else if (n >= 149 && n <= 153) e.carry_sprite = 149;
        else if (n >= 157 && n <= 158) e.carry_sprite = 158;
        else if (n >= 159 && n <= 167) e.carry_sprite = 167;
        else if (n >= 185 && n <= 191) e.carry_sprite = 186;
        else if (n >= 197 && n <= 203) e.carry_sprite = 203;
        else e.carry_sprite = -1;

        if (n >= 172 && n <= 184) e.dead_sprite = 184;
        else if (n >= 192 && n <= 196) e.dead_sprite = 196;
        else if (n >= 210 && n <= 213) e.dead_sprite = 213;
        else if (n >= 214 && n <= 220) e.dead_sprite = 220;
        else if (n >= 221 && n <= 226) e.dead_sprite = 226;
        else if (n >= 242 && n <= 247) e.dead_sprite = 247;
        else e.dead_sprite = -1;

        e.boss = (n >= 248 && n <= 251) || (n >= 252 && n <= 256) || (n >= 257 && n <= 261) ||
                 (n >= 263 && n <= 267) || (n >= 284 && n <= 288) || (n >= 329 && n <= 332);
    }

    static void UpAnim(TITUS_sprite s)
    {
        short[] a = s.animation!;
        do { s.animationIndex++; } while (a[s.animationIndex] >= 0);
        s.animationIndex++;
    }

    static void DownAnim(TITUS_sprite s)
    {
        short[] a = s.animation!;
        do { s.animationIndex--; } while (a[s.animationIndex] >= 0);
        s.animationIndex--;
    }

    // Movimiento horizontal con giro en los extremos (tipos 0,1,3,4,5,6)
    static void WalkRange(TITUS_enemy e)
    {
        e.sprite.x = (short)(e.sprite.x - e.sprite.speedX);
        if (Math.Abs(e.sprite.x - e.centerX) > (int)e.rangeX)
        {
            if (e.sprite.x >= e.centerX) e.sprite.speedX = (short)Math.Abs(e.sprite.speedX);
            else e.sprite.speedX = (short)(0 - Math.Abs(e.sprite.speedX));
        }
    }

    // enemies.c:50 MOVE_NMI, tipos 0-6
    public static void Move(TITUS_level level)
    {
        TITUS_sprite pl = level.player.sprite;
        for (int i = 0; i < level.enemycount; i++)
        {
            TITUS_enemy e = level.enemy[i];
            if (!e.sprite.enabled) continue;
            if (e.type > 18) continue;
            if (e.dying != 0 && e.type != 12 && e.type != 14 && e.type != 15 && e.type != 17) { DEAD1(level, e); continue; }
            TITUS_sprite s = e.sprite;
            switch (e.type)
            {
                case 0:
                case 1:
                    WalkRange(e);
                    break;

                case 2: // dispara
                    if (!e.visible) continue;
                    if (e.direction == 0) { s.speedX = 0; if (s.x < pl.x) s.speedX = -1; }
                    else if (e.direction == 2) s.speedX = -1;
                    else s.speedX = 0;
                    if (e.phase == 0)
                    {
                        if (e.counter != 0) e.counter--;
                        if (e.counter != 0) continue;
                        if (Math.Abs(pl.y - s.y) > 24) continue;
                        if ((int)e.rangeX < Math.Abs(pl.x - s.x)) continue;
                        if (e.direction != 0)
                        {
                            if (e.direction == 2) { if (s.x > pl.x) continue; }
                            else { if (pl.x > s.x) continue; }
                        }
                        e.phase = 30;
                        UpAnim(s);
                    }
                    else
                    {
                        e.phase--;
                        if (!e.trigger) continue;
                        s.animationIndex += 2;
                        TITUS_sprite? bullet = FindTrash(level);
                        if (bullet != null)
                        {
                            PutBullet(level, e, bullet);
                            e.counter = (byte)e.delay;
                        }
                        e.phase = 0;
                    }
                    break;

                case 3:
                case 4: // salta hacia el jugador (pez)
                    switch (e.phase)
                    {
                        case 0:
                            WalkRange(e);
                            if (!e.visible) continue;
                            if (s.y < pl.y || s.y >= pl.y + 256) continue;
                            if ((int)e.rangeY < s.y - pl.y) continue;
                            if (s.x > pl.x) { if (s.flipped) continue; }
                            else { if (!s.flipped) continue; }
                            if (Math.Abs(s.x - pl.x) >= 48) continue;
                            if (Math.Abs(pl.x - e.centerX) > (int)e.rangeX) continue;
                            e.phase = 1;
                            int sy = 0, j = 0;
                            do { sy++; j += sy; } while (s.y - pl.y > j);
                            s.speedY = (short)(0 - sy);
                            e.delay = (uint)s.y;
                            UpAnim(s);
                            break;
                        case 1:
                            if (!e.visible) continue;
                            s.x = (short)(s.x - (s.speedX << 2));
                            s.y = (short)(s.y + s.speedY);
                            if (s.speedY + 1 < 0)
                            {
                                s.speedY++;
                                if (s.y > (int)e.delay - (int)e.rangeY) continue;
                            }
                            UpAnim(s);
                            e.phase = 2;
                            s.speedY = 0;
                            if (s.x <= e.centerX) s.speedX = (short)Math.Abs(s.speedX);
                            else s.speedX = (short)(0 - Math.Abs(s.speedX));
                            break;
                        case 2:
                            if (!e.visible) continue;
                            s.x = (short)(s.x - s.speedX);
                            s.y = (short)(s.y + s.speedY);
                            s.speedY++;
                            if (s.y < (int)e.delay) continue;
                            s.y = (short)e.delay;
                            s.x = (short)(s.x - s.speedX);
                            e.phase = 0;
                            DownAnim(s);
                            DownAnim(s);
                            break;
                    }
                    break;

                case 5:
                case 6: // vuela hacia el jugador
                    WalkRange(e);
                    if (!e.visible) continue;
                    switch (e.phase)
                    {
                        case 0:
                            if (Math.Abs(s.y - pl.y) > (int)e.rangeY) continue;
                            if (Math.Abs(s.x - pl.x) > 40) continue;
                            e.delay = (uint)s.y;
                            s.speedY = (short)(s.y < pl.y ? 2 : -2);
                            e.phase = 1;
                            UpAnim(s);
                            break;
                        case 1:
                            s.y = (short)(s.y + s.speedY);
                            if (Math.Abs(s.y - (int)e.delay) < (int)e.rangeY) continue;
                            s.speedY = (short)(0 - s.speedY);
                            UpAnim(s);
                            e.phase = 2;
                            break;
                        case 2:
                            s.y = (short)(s.y + s.speedY);
                            if (s.y != (int)e.delay) continue;
                            DownAnim(s);
                            DownAnim(s);
                            e.phase = 0;
                            break;
                    }
                    break;

                case 7: // camina con gravedad, ataca al estar cerca
                    switch (e.phase)
                    {
                        case 0:
                            if (s.y > pl.y) continue;
                            if ((int)e.rangeX < Math.Abs(s.x - pl.x)) continue;
                            if (Math.Abs(s.y - pl.y) > 200) continue;
                            e.phase = 1;
                            UpAnim(s);
                            FaceWalk(e, pl);
                            break;
                        case 1:
                            if (!GravityWalk(level, e, pl)) continue;
                            if (Math.Abs(pl.x - s.x) > 640) { e.phase = 2; continue; }
                            if (Math.Abs(pl.y - s.y) >= 400) { e.phase = 2; continue; }
                            if (Math.Abs(pl.x - s.x) > Original.SPRITEWIDTH[s.number] + 6) continue;
                            if (Math.Abs(pl.y - s.y) > 8) continue;
                            e.phase = 3;
                            UpAnim(s);
                            break;
                        case 2:
                            if (SpawnOnScreen(e, 13, 21, true)) continue;
                            ResetEnemy(e);
                            DownAnim(s);
                            break;
                        case 3:
                            if (e.trigger) { e.phase = 1; continue; }
                            if (!GravityWalk(level, e, pl)) continue;
                            if (Math.Abs(pl.x - s.x) > 640) { e.phase = 2; continue; }
                            if (Math.Abs(pl.y - s.y) >= 400) { e.phase = 2; continue; }
                            break;
                    }
                    break;

                case 8:
                case 14: // camina con gravedad al salir de pantalla (14: inmortal)
                    switch (e.phase)
                    {
                        case 0:
                            if (Math.Abs(s.x - pl.x) > 340 || Math.Abs(s.y - pl.y) >= 230)
                            {
                                e.phase = 1;
                                UpAnim(s);
                                FaceWalk(e, pl);
                            }
                            break;
                        case 1:
                            if (!GravityWalk(level, e, pl)) continue;
                            if (Math.Abs(pl.x - s.x) < 640) continue;
                            e.phase = 2;
                            break;
                        case 2:
                            if (SpawnOnScreen(e, 12, 19, false)) continue;
                            ResetEnemy(e);
                            DownAnim(s);
                            break;
                    }
                    break;

                case 9: // camina y asoma periodicamente
                    switch (e.phase)
                    {
                        case 0:
                            if ((int)e.rangeX < Math.Abs(pl.x - s.x)) continue;
                            if (Math.Abs(pl.y - s.y) > 60) continue;
                            e.phase = 1;
                            UpAnim(s);
                            FaceWalk(e, pl);
                            break;
                        case 1:
                            Player.TAUPE_FLAG++;
                            if ((Player.TAUPE_FLAG & 0x04) == 0 && (Player.IMAGE_COUNTER & 0x01FF) == 0) UpAnim(s);
                            if ((Player.IMAGE_COUNTER & 0x007F) == 0)
                            {
                                e.phase = 3;
                                UpAnim(s);
                                TaupeHead(level, e, pl);
                                continue;
                            }
                            if (Level.GetFloorFlag(level, (short)(s.y >> 4), (short)(s.x >> 4)) == FFLAG_NOFLOOR)
                            {
                                s.speedX = Math.Abs(s.speedX);
                                if (e.initX > s.x) s.speedX = (short)(0 - s.speedX);
                            }
                            s.y = (short)(s.y & 0xFFF0);
                            WallTurn(level, e, false);
                            s.x = (short)(s.x - s.speedX);
                            if (s.x < 0) { s.speedX = (short)(0 - s.speedX); s.x = (short)(s.x - s.speedX); }
                            if (Math.Abs(pl.x - s.x) < 1280) continue;
                            e.phase = 2;
                            break;
                        case 2:
                            if (SpawnOnScreen(e, 12, 25, true)) continue;
                            ResetEnemy(e);
                            DownAnim(s);
                            break;
                        case 3:
                            TaupeHead(level, e, pl);
                            break;
                    }
                    break;

                case 10: // alerta de cerca, corre mas cerca
                    switch (e.phase)
                    {
                        case 0:
                            if (Player.FURTIF_FLAG != 0) continue;
                            if ((int)e.rangeX < Math.Abs(pl.x - s.x)) continue;
                            if (Math.Abs(pl.y - s.y) > 26) continue;
                            e.phase = 1;
                            UpAnim(s);
                            FaceWalk(e, pl);
                            Type10Wait(e, pl, s); // el C continua en case 1 (sin break)
                            break;
                        case 1:
                            Type10Wait(e, pl, s);
                            break;
                        case 2:
                            if (Level.GetFloorFlag(level, (short)(s.y >> 4), (short)(s.x >> 4)) == FFLAG_NOFLOOR)
                            {
                                s.speedX = Math.Abs(s.speedX);
                                if (e.initX > s.x) s.speedX = (short)(0 - s.speedX);
                            }
                            s.y = (short)(s.y & 0xFFF0);
                            WallTurn(level, e, true);
                            s.x = (short)(s.x - s.speedX);
                            if (s.x < 0) { s.speedX = (short)(0 - s.speedX); s.x = (short)(s.x - s.speedX); }
                            if (Math.Abs(pl.x - s.x) >= 640) e.phase = 3;
                            break;
                        case 3:
                            if (SpawnOnScreen(e, 13, 21, true)) continue;
                            ResetEnemy(e);
                            DownAnim(s);
                            DownAnim(s);
                            break;
                    }
                    break;

                case 11: // camina y dispara
                    switch (e.phase)
                    {
                        case 0:
                            if ((int)e.rangeX < Math.Abs(pl.x - s.x)) continue;
                            if (Math.Abs(pl.y - s.y) > 26) continue;
                            e.phase = 1;
                            UpAnim(s);
                            FaceWalk(e, pl);
                            break;
                        case 1:
                            if (Level.GetFloorFlag(level, (short)(s.y >> 4), (short)(s.x >> 4)) == FFLAG_NOFLOOR)
                            {
                                s.speedX = Math.Abs(s.speedX);
                                if (e.initX > s.x) s.speedX = (short)(0 - s.speedX);
                            }
                            s.y = (short)(s.y & 0xFFF0);
                            byte hf = Level.GetHorizFlag(level, (short)((s.y >> 4) - 1), (short)(s.x >> 4));
                            if (hf == HFLAG_WALL || hf == HFLAG_DEADLY || hf == HFLAG_PADLOCK) s.speedX = (short)(0 - s.speedX);
                            s.x = (short)(s.x - s.speedX);
                            if (s.x < 0) { s.speedX = (short)(0 - s.speedX); s.x = (short)(s.x - s.speedX); }
                            if (Math.Abs(pl.x - s.x) >= 640) e.phase = 2;
                            if (e.counter != 0) e.counter--;
                            if (e.counter != 0) continue;
                            if (Math.Abs(pl.x - s.x) > 64) continue;
                            if (Math.Abs(pl.y - s.y) > 20) continue;
                            FaceWalk(e, pl);
                            e.phase = 3;
                            UpAnim(s);
                            e.counter = 20;
                            break;
                        case 2:
                            if (!SpawnOnScreen(e, 13, 21, true))
                            {
                                s.x = (short)e.initX;
                                s.y = (short)e.initY;
                                DownAnim(s);
                                e.phase = 0;
                            }
                            break;
                        case 3:
                            if (!e.trigger) continue;
                            TITUS_sprite? b = FindTrash(level);
                            if (b != null)
                            {
                                s.animationIndex += 2;
                                PutBullet(level, e, b);
                            }
                            DownAnim(s);
                            e.phase = 1;
                            break;
                    }
                    break;

                case 12: // salta (bola de fuego, inmortal)
                    e.dying = 0;
                    switch (e.phase)
                    {
                        case 0:
                            UpAnim(s);
                            s.speedY = (short)e.rangeY;
                            e.initY = s.y;
                            e.phase = 1;
                            break;
                        case 1:
                            s.y = (short)(s.y - s.speedY);
                            s.speedY--;
                            if (s.speedY == 0) e.phase = 2;
                            break;
                        case 2:
                            s.y = (short)(s.y + s.speedY);
                            s.speedY++;
                            if (s.y >= e.initY)
                            {
                                s.y = (short)e.initY;
                                e.counter = (byte)e.delay;
                                e.phase = 3;
                                DownAnim(s);
                            }
                            break;
                        case 3:
                            e.counter--;
                            if (e.counter == 0) e.phase = 0;
                            break;
                    }
                    break;

                case 13: // rebota (bebe grande)
                    switch (e.phase)
                    {
                        case 0:
                            if (pl.x >= s.x) s.speedX = (short)(0 - Math.Abs(s.speedX));
                            else s.speedX = Math.Abs(s.speedX);
                            if (Math.Abs(pl.x - s.x) <= (int)e.rangeX && Math.Abs(pl.y - s.y) <= 40)
                            {
                                UpAnim(s);
                                e.phase = 1;
                                s.speedY = 10;
                            }
                            break;
                        case 1:
                            s.x = (short)(s.x - s.speedX);
                            s.y = (short)(s.y - s.speedY);
                            s.speedY--;
                            if (s.speedY == 0) { UpAnim(s); e.phase = 2; }
                            break;
                        case 2:
                            s.x = (short)(s.x - s.speedX);
                            s.y = (short)(s.y + s.speedY);
                            s.speedY++;
                            if (s.speedY > 10)
                            {
                                e.phase = 3;
                                UpAnim(s);
                                e.counter = (byte)e.delay;
                            }
                            break;
                        case 3:
                            e.counter--;
                            if (e.counter == 0)
                            {
                                DownAnim(s); DownAnim(s); DownAnim(s);
                                e.phase = 0;
                            }
                            break;
                    }
                    break;

                case 15: // nada (inmortal)
                    e.dying = 0;
                    break;

                case 16: // nada
                    break;

                case 17: // suelta objetos (inmortal)
                {
                    e.dying = 0;
                    if (e.counter + 1 < (int)e.delay) { e.counter++; continue; }
                    if ((int)e.rangeX < Math.Abs(s.x - pl.x)) { e.counter = 0; continue; }
                    if ((int)e.rangeY < pl.y - s.y) continue;
                    int j = 0;
                    do { j++; } while (j < level.objectcount && level.@object[j].sprite.enabled);
                    if (j >= level.objectcount) { e.counter = 0; continue; } // C: lee fuera de rango
                    TITUS_object o = level.@object[j];
                    UpAnim(s);
                    Objects.UpdateObjectSprite(level, o, (short)(s.animation![s.animationIndex] & 0x1FFF), true);
                    o.sprite.flipped = true;
                    o.sprite.x = s.x;
                    o.sprite.y = s.y;
                    o.sprite.droptobottom = true;
                    o.sprite.killing = true;
                    o.sprite.speedY = 0;
                    Player.GRAVITY_FLAG = 4;
                    DownAnim(s);
                    e.counter = 0;
                    break;
                }

                case 18: // guardia (helicoptero)
                    if (pl.x < (short)(e.initX - (int)e.rangeX) || pl.x > (short)(e.initX + (int)e.rangeX) ||
                        pl.y < (short)(e.initY - (int)e.rangeY) || pl.y > (short)(e.initY + (int)e.rangeY))
                    {
                        if (e.initX != s.x)
                        {
                            s.speedX = Math.Abs(s.speedX);
                            if (e.initX > s.x) s.speedX = (short)(0 - s.speedX);
                            s.x = (short)(s.x - s.speedX);
                        }
                        if (e.initY != s.y)
                        {
                            if (e.initY > s.y) s.y = (short)(s.y + s.speedY);
                            else s.y = (short)(s.y - s.speedY);
                        }
                    }
                    else
                    {
                        if (pl.x != s.x)
                        {
                            s.speedX = Math.Abs(s.speedX);
                            if (pl.x > s.x) s.speedX = (short)(0 - s.speedX);
                            s.x = (short)(s.x - s.speedX);
                        }
                        if (pl.y != s.y)
                        {
                            if (pl.y > s.y) s.y = (short)(s.y + s.speedY);
                            else s.y = (short)(s.y - s.speedY);
                        }
                    }
                    break;
            }
        }
    }

    // ---- ayudas tipos 7-18 ----
    const byte HFLAG_WALL = 1, HFLAG_DEADLY = 3, HFLAG_PADLOCK = 5;
    const byte FFLAG_NOFLOOR = 0;

    static void FaceWalk(TITUS_enemy e, TITUS_sprite pl)
    {
        if (e.sprite.x > pl.x) e.sprite.speedX = e.walkspeedX;
        else e.sprite.speedX = (short)(0 - e.walkspeedX);
    }

    // true = el punto de aparicion esta dentro de la ventana (el C hace 'continue')
    static bool SpawnOnScreen(TITUS_enemy e, int tilesY, int tilesX, bool yInclusive)
    {
        int dy = (e.initY >> 4) - Scroll.BITMAP_Y;
        int dx = (e.initX >> 4) - Scroll.BITMAP_X;
        bool yOk = yInclusive ? dy <= tilesY : dy < tilesY;
        return yOk && dy >= 0 && dx < tilesX && dx >= 0;
    }

    static void ResetEnemy(TITUS_enemy e)
    {
        e.sprite.y = (short)e.initY;
        e.sprite.x = (short)e.initX;
        e.phase = 0;
    }

    // Gira al chocar con pared; backToInit=true: tipo 10 (vuelve hacia initX)
    static void WallTurn(TITUS_level level, TITUS_enemy e, bool backToInit)
    {
        TITUS_sprite s = e.sprite;
        int j = s.speedX > 0 ? -1 : 1;
        byte hf = Level.GetHorizFlag(level, (short)((s.y >> 4) - 1), (short)((s.x >> 4) + j));
        if (hf == HFLAG_WALL || hf == HFLAG_DEADLY || hf == HFLAG_PADLOCK)
        {
            if (backToInit)
            {
                s.speedX = Math.Abs(s.speedX);
                if (e.initX > s.x) s.speedX = (short)(0 - s.speedX);
            }
            else s.speedX = (short)(0 - s.speedX);
        }
    }

    // Caminar con gravedad (tipos 7, 8, 14). false = cayendo (el C hace 'continue')
    static bool GravityWalk(TITUS_level level, TITUS_enemy e, TITUS_sprite pl)
    {
        TITUS_sprite s = e.sprite;
        if (Level.GetFloorFlag(level, (short)(s.y >> 4), (short)(s.x >> 4)) == FFLAG_NOFLOOR)
        {
            if (s.speedY < 16) s.speedY++;
            s.y = (short)(s.y + s.speedY);
            return false;
        }
        if (s.speedY != 0) FaceWalk(e, pl);
        s.speedY = 0;
        s.y = (short)(s.y & 0xFFF0);
        WallTurn(level, e, false);
        s.x = (short)(s.x - s.speedX);
        if (s.x < 0) { s.speedX = (short)(0 - s.speedX); s.x = (short)(s.x - s.speedX); }
        return true;
    }

    // Tipo 9: quitar la cabeza (fase 3 y salto desde fase 1)
    static void TaupeHead(TITUS_level level, TITUS_enemy e, TITUS_sprite pl)
    {
        TITUS_sprite s = e.sprite;
        if (!e.visible)
        {
            UpAnim(s);
            s.animationIndex--;
            GalForm(level, e);
            FaceWalk(e, pl);
            e.phase = 1;
        }
        else if (e.trigger)
        {
            FaceWalk(e, pl);
            e.phase = 1;
        }
    }

    // Tipo 10 fase 1
    static void Type10Wait(TITUS_enemy e, TITUS_sprite pl, TITUS_sprite s)
    {
        if (Player.FURTIF_FLAG != 0) return;
        if ((int)e.rangeX < Math.Abs(pl.x - s.x)) { DownAnim(s); e.phase = 0; return; }
        if ((int)e.rangeX - 50 >= Math.Abs(pl.x - s.x) && Math.Abs(pl.y - s.y) <= 60)
        {
            e.phase = 2;
            UpAnim(s);
        }
    }

    // enemies.c:1088 SET_NMI: visibilidad y animacion
    public static void SetNmi(TITUS_level level)
    {
        for (int i = 0; i < level.enemycount; i++)
        {
            TITUS_enemy e = level.enemy[i];
            if (!e.sprite.enabled) continue;
            e.visible = false;
            if (e.sprite.x + 32 < Scroll.BITMAP_X << 4 ||
                e.sprite.x - 32 > (Scroll.BITMAP_X << 4) + Scroll.screen_width * 16 ||
                e.sprite.y < Scroll.BITMAP_Y << 4 ||
                e.sprite.y - 32 > (Scroll.BITMAP_Y << 4) + Scroll.screen_height * 16)
            {
                if ((e.dying & 0x03) != 0) e.sprite.enabled = false;
                continue;
            }
            e.visible = true;
            GalForm(level, e);
            if ((e.dying & 0x03) != 0) continue;
            if (Player.KICK_FLAG == 0) // colision con el heroe (sin GODMODE)
            {
                if (e.sprite.invisible) continue;
                ACTIONC_NMI(level, e);
            }
            int hit = 0, k = 0;
            if (Player.GRAVITY_FLAG != 0) // colision con objeto en movimiento
            {
                for (k = 0; k < level.objectcount; k++)
                {
                    TITUS_object o = level.@object[k];
                    if (o.sprite.speedX == 0)
                    {
                        if (o.sprite.speedY == 0) continue;
                        if (o.mass < 10) continue;
                    }
                    if (o.objectdata!.no_damage) continue;
                    if (NMI_VS_DROP(e.sprite, o.sprite)) { hit = 1; break; }
                }
            }
            if (hit == 0 && Player.DROP_FLAG && !Player.CARRY_FLAG && level.player.sprite2.enabled &&
                NMI_VS_DROP(e.sprite, level.player.sprite2)) // enemies.c:1143
            {
                INVULNERABLE_FLAG = 0;
                level.player.sprite2.enabled = false;
                SeeChoc(level);
                hit = 2;
            }
            if (hit != 0)
            {
                if (hit == 1)
                {
                    TITUS_object o = level.@object[k];
                    if (o.sprite.number != 73) o.sprite.speedX = (short)(0 - o.sprite.speedX);
                }
                Audio.FxStart(1); // enemies.c:1162
                Player.DROP_FLAG = false;
                if (e.boss)
                {
                    if (INVULNERABLE_FLAG != 0) continue;
                    INVULNERABLE_FLAG = 10;
                    e.sprite.flash = true;
                    BIGNMI_POWER--;
                    if (BIGNMI_POWER != 0) continue;
                    boss_alive = false;
                }
                e.dying |= 0x02;
            }
        }
    }

    // enemies.c:1302 SEE_CHOC
    static void SeeChoc(TITUS_level level)
    {
        Game.UpdateSprite(level, level.player.sprite2, (short)(Objects.FIRST_OBJET + 15), true);
        level.player.sprite2.speedX = 0;
        level.player.sprite2.speedY = 0;
        Player.SEECHOC_FLAG = 5;
    }

    // enemies.c:1214 ACTIONC_NMI (tipos 0-14 y 18)
    static void ACTIONC_NMI(TITUS_level level, TITUS_enemy e)
    {
        if (!(e.type <= 14 || e.type == 18)) return;
        if (!NMI_VS_DROP(e.sprite, level.player.sprite)) return;
        if (e.type != 11 && e.sprite.number != 178) // 178 = periscopio
            e.sprite.speedX = (short)(0 - e.sprite.speedX);
        if (e.sprite.number >= Objects.FIRST_NMI + 53 && e.sprite.number <= Objects.FIRST_NMI + 55)
            Player.GRANDBRULE_FLAG = true; // bola de fuego
        if (e.power != 0) Player.KICK_ASH(level, e.sprite, e.power);
    }

    // enemies.c:1338 FIND_TRASH
    static TITUS_sprite? FindTrash(TITUS_level level)
    {
        for (int i = 0; i < level.trashcount; i++)
            if (!level.trash[i].enabled) return level.trash[i];
        return null;
    }

    // enemies.c:1349 PUT_BULLET
    static void PutBullet(TITUS_level level, TITUS_enemy e, TITUS_sprite bullet)
    {
        short[] a = e.sprite.animation!;
        int p = e.sprite.animationIndex;
        bullet.x = e.sprite.x;
        bullet.y = (short)(e.sprite.y - (sbyte)(a[p - 1] & 0xFF));
        Game.UpdateSprite(level, bullet, (short)((a[p - 2] & 0x1FFF) + Objects.FIRST_OBJET), true);
        if (e.sprite.x < level.player.sprite.x) { bullet.speedX = 16 * 11; bullet.flipped = true; }
        else { bullet.speedX = -16 * 11; bullet.flipped = false; }
        bullet.speedY = 0;
        bullet.x += (short)(bullet.speedX >> 4);
    }

    // enemies.c:1309 MOVE_TRASH
    public static void MoveTrash(TITUS_level level)
    {
        for (int i = 0; i < level.trashcount; i++)
        {
            TITUS_sprite t = level.trash[i];
            if (!t.enabled) continue;
            if (t.speedX != 0)
            {
                t.x += (short)(t.speedX >> 4);
                int tmp = (t.x >> 4) - Scroll.BITMAP_X;
                if (tmp < 0 || tmp > Scroll.screen_width) { t.enabled = false; continue; }
                if (tmp != 0) // bug del original
                {
                    t.y += (short)(t.speedY >> 4);
                    tmp = (t.y >> 4) - Scroll.BITMAP_Y;
                    if (tmp < 0 || tmp > Scroll.screen_height * 16) { t.enabled = false; continue; }
                }
            }
            if (NMI_VS_DROP(t, level.player.sprite))
            {
                t.x -= t.speedX;
                Player.KICK_ASH(level, t, 70);
                t.enabled = false;
            }
        }
    }

    // enemies.c:1270 NMI_VS_DROP
    static bool NMI_VS_DROP(TITUS_sprite enemysprite, TITUS_sprite sprite)
    {
        if (Math.Abs(sprite.x - enemysprite.x) >= 64) return false;
        if (Math.Abs(sprite.y - enemysprite.y) >= 70) return false;
        if (sprite.y < enemysprite.y)
        {
            if (sprite.y <= enemysprite.y - enemysprite.spritedata!.collheight + 3) return false;
        }
        else
        {
            if (enemysprite.y <= sprite.y - sprite.spritedata!.collheight + 3) return false;
        }
        int enemyleft = enemysprite.x - enemysprite.spritedata!.refwidth;
        int objectleft = sprite.x - sprite.spritedata!.refwidth;
        if (enemyleft >= objectleft)
        {
            if (objectleft + (sprite.spritedata!.collwidth >> 1) <= enemyleft) return false;
        }
        else
        {
            if (enemyleft + (enemysprite.spritedata!.collwidth >> 1) <= objectleft) return false;
        }
        return true;
    }

    // enemies.c:1009 DEAD1
    const int MAX_SPEED_DEAD = 20;
    static void DEAD1(TITUS_level level, TITUS_enemy e)
    {
        if ((e.dying & 0x01) != 0 || e.dead_sprite == -1)
        {
            if ((e.dying & 0x01) == 0)
            {
                e.dying |= 0x01;
                e.sprite.speedY = -10;
                e.phase = 0;
            }
            if (e.phase != 0xFF)
            {
                e.sprite.y += e.sprite.speedY;
                if (Player.SEECHOC_FLAG != 0) level.player.sprite2.y += e.sprite.speedY; // enemies.c:1019
                if (e.sprite.speedY < MAX_SPEED_DEAD) e.sprite.speedY++;
            }
        }
        else
        {
            e.dying |= 0x01;
            UpdateEnemySprite(level, e, e.dead_sprite, false);
            e.sprite.flash = false;
            e.sprite.visible = false;
            e.sprite.speedY = 0;
            e.phase = 0xFF; // C: phase = -1 (uint8)
        }
    }

    // enemies.c:1185 GAL_FORM: animacion del enemigo
    static void GalForm(TITUS_level level, TITUS_enemy e)
    {
        e.sprite.invisible = false;
        if ((e.dying & 0x03) != 0)
        {
            e.sprite.visible = false;
            e.visible = true;
            return;
        }
        e.trigger = false;
        short[] a = e.sprite.animation!;
        int p = e.sprite.animationIndex;
        while (a[p] < 0) p += a[p] >> 1; // salto atras al inicio de la animacion
        if (a[p] == 0x55AA)
        {
            e.sprite.invisible = true;
            return;
        }
        e.trigger = (a[p] & 0x2000) != 0;
        UpdateEnemySprite(level, e, (short)((a[p] & 0x00FF) + Objects.FIRST_NMI), true);
        e.sprite.flipped = e.sprite.speedX < 0;
        p++;
        if (a[p] < 0) p += a[p] >> 1;
        e.sprite.animationIndex = p;
        e.visible = true;
    }
}
