using Raylib_cs;

namespace TitusTheFox;

// Port parcial de OpenTitus player.c: move_player() (entrada, reposo, caminar, salto, gravedad, suelo/techo/pared basicos).
// Con escaleras, agachado/arrastrarse, golpes de enemigos (CHOC/KICK), energia y muerte. Sin cuerdas, tapis ni muertes por tile (ver TODO).
public static class Player
{
    // globals.h:98-101
    const int TEST_ZONE = 4;
    const int MAX_X = 4;
    const int MAX_Y = 12;
    const int MAP_LIMIT_Y = -1;
    const int GESTION_X = 40;
    const int GESTION_Y = 20;

    // Flags de tile (tiles.h / OpenTitus)
    const byte HFLAG_WALL = 1, HFLAG_DEADLY = 3;

    // Globales de player.c / globals.h
    static sbyte SENSX;          // direccion: -1 izquierda, 0 reposo/derecha, 1 derecha
    static bool X_FLAG, Y_FLAG;
    static byte SAUT_FLAG;       // 6 = en el aire
    static byte SAUT_COUNT;
    static byte YFALL;
    internal static byte GRAVITY_FLAG;   // objetos activos (Objects.Move)
    internal static bool CARRY_FLAG;      // lleva un objeto en sprite2
    internal static bool DROP_FLAG;       // objeto lanzado en vuelo (sprite2)
    internal static byte SEECHOC_FLAG;    // impacto visible en sprite2 (enemies.c SEE_CHOC)
    static bool DROPREADY_FLAG;  // espacio liberado: se puede soltar/lanzar
    static bool POSEREADY_FLAG;  // ya se cogio/dejo en esta pulsacion
    internal static byte CROSS_FLAG;      // se activa al agacharse (accion 5, ACTION_TIMER == 15)
    static bool PRIER_FLAG;      // techo bajo (agacharse forzado); TODO: FORCE_POSE con objeto
    const byte FFLAG_LADDER = 6;

    // Golpes, energia y muerte (globals.h / player.c)
    public const int MAXIMUM_ENERGY = 16;
    internal static byte CHOC_FLAG;       // cabezazo/aturdido tras chocar con una pared
    internal static byte KICK_FLAG;       // golpeado por un enemigo (vuela, sin dano)
    internal static byte RESETLEVEL_FLAG; // cuenta atras de la muerte (1 = reiniciar)
    internal static bool GAMEOVER_FLAG;
    internal static bool GRANDBRULE_FLAG; // golpe de fuego (accion 13)
    internal static byte FURTIF_FLAG;     // paso furtivo (timer 100 al saltar tras agacharse); enemigos fase 0/1 no despiertan
    internal static ushort IMAGE_COUNTER; // globals.h:196, +1 por frame de juego (Game.PlayLevel), & 0x0FFF
    internal static byte TAUPE_FLAG;      // globals.h:151, contador de topos (reset.c:198)
    internal static byte BAR_FLAG;       // frames que se muestra la barra de energia
    internal static byte TAPISWAIT_FLAG; // alfombra: 0 = no, 1 = lista para volar, 2 = tiempo agotado
    internal static byte TAPISFLY_FLAG;  // alfombra: frames de vuelo restantes
    internal static byte FUME_FLAG;      // humo al caer un objeto (sprite2)

    // Puntero sprite.animation del original: fila de ANIM_PLAYER + indice
    static int animRow = -1, animIdx;

    // reset.c:213-250
    public static void Init()
    {
        TAPISWAIT_FLAG = 0;
        TAPISFLY_FLAG = 0;
        FUME_FLAG = 0;
        SAUT_FLAG = 0;
        SAUT_COUNT = 0;
        YFALL = 0;
        CROSS_FLAG = 0;
        GRAVITY_FLAG = 4;
        CARRY_FLAG = false;
        DROP_FLAG = false;
        DROPREADY_FLAG = false;
        POSEREADY_FLAG = false;
        SENSX = 0;
        PRIER_FLAG = false;
        CHOC_FLAG = 0;
        KICK_FLAG = 0;
        FURTIF_FLAG = 0;
        TAUPE_FLAG = 0;
        RESETLEVEL_FLAG = 0;
        GAMEOVER_FLAG = false;
        GRANDBRULE_FLAG = false;
        BAR_FLAG = 0;
        animRow = -1;
        animIdx = 0;
    }

    static void subto0(ref byte v) { if (v != 0) v--; }

    // Teclado o joystick segun Controller.cfg (ver Controller.cs)
    static bool KeyLeft() => Controller.Left();
    static bool KeyRight() => Controller.Right();
    static bool KeyUp() => Controller.Jump();
    static bool KeyDown() => Controller.Crouch();
    static bool KeySpace() => Controller.Action();
    // "Arriba" para subir escaleras: con joystick vale el boton de salto o el direccional arriba
    static bool KeyClimb() => Controller.Up();
    static short add_carry() => (short)(CARRY_FLAG ? 16 : 0);

    // player.c:57 move_player (partes 1, 2, 3 y 5 reducidas)
    public static void MovePlayer(TITUS_level level)
    {
        TITUS_player player = level.player;
        TITUS_sprite spr = player.sprite;

        // Parte 1/2: entrada
        // TODO: pausa (P), F1/F2/E/F4, GODMODE/NOCLIP, musica
        bool left = KeyLeft(), right = KeyRight();
        bool jump = KeyUp(); // el espacio es coger/soltar (player.c:222)
        X_FLAG = left || right;
        Y_FLAG = KeyClimb() || KeyDown();

        // TODO: FURTIF(+9)
        bool down = KeyDown();
        byte action;
        if (CHOC_FLAG != 0) action = 11; // player.c:193
        else if (KICK_FLAG != 0) action = (byte)(GRANDBRULE_FLAG ? 13 : 12);
        else
        {
            GRANDBRULE_FLAG = false;
            if (Scroll.LADDER_FLAG) action = 6;
            else if (!PRIER_FLAG && jump && SAUT_FLAG == 0)
            {
                action = 2;
                if (Scroll.LAST_ORDER == 5) FURTIF_FLAG = 100; // player.c:208
            }
            else if (PRIER_FLAG || (SAUT_FLAG != 6 && down)) action = X_FLAG ? (byte)3 : (byte)5;
            else if (X_FLAG) action = 1;
            else action = 0;

            // player.c:222 espacio = coger (agachado) / soltar o lanzar (de pie)
            if (KeySpace() && !PRIER_FLAG)
            {
                if (!DROP_FLAG)
                {
                    if (action == 3 || action == 5) { DROPREADY_FLAG = false; action = 7; }
                    else if (CARRY_FLAG && DROPREADY_FLAG) action = 8;
                }
            }
            else
            {
                DROPREADY_FLAG = true;
                POSEREADY_FLAG = false;
            }
        }
        if (CARRY_FLAG) action += 16;

        sbyte newsensX;
        if (CHOC_FLAG != 0 || KICK_FLAG != 0) newsensX = (sbyte)(SENSX < 0 ? -1 : 0); // player.c:240
        else if (left) newsensX = -1;
        else if (right) newsensX = 1;
        else if (SENSX == -1) newsensX = -1;
        else if (action == 0) newsensX = 0;
        else newsensX = 1;

        if (SENSX != newsensX)
        {
            SENSX = newsensX;
            Scroll.ACTION_TIMER = 1;
        }
        else
        {
            if ((action == 0 || action == 1) && FURTIF_FLAG != 0) action += 9; // player.c:262
            if (action != Scroll.LAST_ORDER) Scroll.ACTION_TIMER = 1;
            else if (Scroll.ACTION_TIMER < 0xFF) Scroll.ACTION_TIMER++;
        }
        ACTION_PRG(level, action);

        // Parte 3: mover (X con margen de 8 px a los bordes del nivel)
        if ((spr.speedX < 0 && spr.x + (spr.speedX >> 4) >= 8) ||
            (spr.speedX > 0 && spr.x + (spr.speedX >> 4) <= (level.width << 4) - 8))
            spr.x += (short)(spr.speedX >> 4);
        spr.y += (short)(spr.speedY >> 4);
        BRK_COLLISION(level);

        // Parte 4: mover el objeto lanzado / colocar el objeto llevado (player.c:285)
        TITUS_sprite spr2 = player.sprite2;
        if (DROP_FLAG)
        {
            int newX = (spr2.speedX >> 4) + spr2.x;
            if (newX < (level.width << 4) && newX >= 0 &&
                newX >= (Scroll.BITMAP_X << 4) - GESTION_X &&
                newX <= (Scroll.BITMAP_X << 4) + (Scroll.screen_width << 4) + GESTION_X)
            {
                spr2.x = (short)newX;
                int newY = (spr2.speedY >> 4) + spr2.y;
                if (newY < (level.height << 4) && newY >= 0 &&
                    newY >= (Scroll.BITMAP_Y << 4) - GESTION_Y &&
                    newY <= (Scroll.BITMAP_Y << 4) + (Scroll.screen_height << 4) + GESTION_Y)
                    spr2.y = (short)newY;
                else { spr2.enabled = false; DROP_FLAG = false; }
            }
            else { spr2.enabled = false; DROP_FLAG = false; }
        }
        else if (CARRY_FLAG)
        {
            if (!Scroll.LADDER_FLAG && (Scroll.LAST_ORDER == 16 + 5 || Scroll.LAST_ORDER == 16 + 7))
            {
                spr2.y = (short)(spr.y - 4);
                spr2.x = (short)(spr.flipped ? spr.x - 10 : spr.x + 12);
            }
            else if (spr.number == 14 || ((Scroll.LAST_ORDER & 0x0F) != 7 && (Scroll.LAST_ORDER & 0x0F) != 8))
            {
                spr2.x = (short)(spr.x + 2);
                if (spr.number == 23 || spr.number == 24)
                {
                    spr2.x -= 10;
                    if (spr.flipped) spr2.x += 18;
                }
                spr2.y = (short)(spr.y - spr.spritedata!.collheight + 1);
            }
        }
        if (SEECHOC_FLAG != 0) // player.c:333
        {
            SEECHOC_FLAG--;
            if (SEECHOC_FLAG == 0) spr2.enabled = false;
        }

        // Parte 5: temporizadores (player.c:340)
        subto0(ref Enemies.INVULNERABLE_FLAG);
        subto0(ref GRAVITY_FLAG);
        subto0(ref CROSS_FLAG);
        subto0(ref RESETLEVEL_FLAG);
        subto0(ref FURTIF_FLAG);
        subto0(ref KICK_FLAG);
        if (spr.speedY == 0) subto0(ref CHOC_FLAG);
        if (spr.speedX == 0 && spr.speedY == 0) KICK_FLAG = 0;
        subto0(ref TAPISFLY_FLAG);
        subto0(ref FUME_FLAG); // humo al caer un objeto
        if (FUME_FLAG != 0 && (FUME_FLAG & 0x03) == 0)
        {
            Game.UpdateSprite(level, level.player.sprite2, (short)(level.player.sprite2.number + 1), false);
            if (level.player.sprite2.number == Objects.FIRST_OBJET + 19)
            {
                level.player.sprite2.enabled = false;
                FUME_FLAG = 0;
            }
        }
    }

    // player.c:962 DEC_ENERGY
    internal static void DEC_ENERGY(TITUS_level level)
    {
        BAR_FLAG = 50;
        if (RESETLEVEL_FLAG == 0)
        {
            level.player.hp--;
            if (level.player.hp < 0)
            {
                level.player.hp = 0;
                DEC_LIFE(level);
            }
        }
    }

    // player.c:374 DEC_LIFE
    static void DEC_LIFE(TITUS_level level)
    {
        RESETLEVEL_FLAG = 10;
        BAR_FLAG = 0;
        if (level.lives == 0) GAMEOVER_FLAG = true;
    }

    // player.c:366 CASE_DEAD_IM
    static void CASE_DEAD_IM(TITUS_level level)
    {
        DEC_LIFE(level);
        RESETLEVEL_FLAG = 2;
    }

    // enemies.c:1251 KICK_ASH: dano (2 de energia) y golpe hacia atras
    internal static void KICK_ASH(TITUS_level level, TITUS_sprite enemysprite, short power)
    {
        Audio.FxStart(4); // enemies.c:1253
        TITUS_sprite p = level.player.sprite;
        DEC_ENERGY(level);
        DEC_ENERGY(level);
        KICK_FLAG = 24;
        CHOC_FLAG = 0;
        Scroll.LAST_ORDER = 0;
        p.speedX = power;
        if (p.x <= enemysprite.x) p.speedX = (short)(0 - p.speedX);
        p.speedY = -8 * 16;
        FORCE_POSE(level);
    }

    // player.c:974 ACTION_PRG (casos 0, 1 y 2)
    static void ACTION_PRG(TITUS_level level, byte action)
    {
        TITUS_sprite spr = level.player.sprite;
        switch (action)
        {
            case 0:
            case 9:
            case 16:
                // Reposo: deceleracion y deslizamiento
                Scroll.LAST_ORDER = action;
                DECELERATION(level.player);
                if (Math.Abs((int)spr.speedX) >= 1 * 16 && spr.flipped == (spr.speedX < 0))
                    animRow = 4 + add_carry();
                else
                    animRow = action;
                animIdx = 0;
                Game.UpdateSprite(level, spr, Original.ANIM_PLAYER[animRow, animIdx], true);
                spr.flipped = SENSX < 0;
                break;

            case 1:
            case 17:
            case 19:
                XACCELERATION(level.player, MAX_X * 16);
                NEW_FORM(action);
                GET_IMAGE(level);
                break;

            case 10: // paso furtivo (player.c:1357)
                XACCELERATION(level.player, (MAX_X - 1) * 16);
                NEW_FORM(action);
                GET_IMAGE(level);
                break;

            case 2:
            case 18:
                if (SAUT_COUNT >= 3)
                {
                    SAUT_FLAG = 6; // fin del salto
                }
                else
                {
                    SAUT_COUNT++;
                    YACCELERATION_NEG(level.player, MAX_Y * 16 / 4);
                    XACCELERATION(level.player, MAX_X * 16);
                    NEW_FORM(action);
                    GET_IMAGE(level);
                }
                break;

            case 3: // arrastrarse
                NEW_FORM(action);
                GET_IMAGE(level);
                XACCELERATION(level.player, MAX_X * 16 / 2);
                if (Math.Abs((int)spr.speedX) < 2 * 16)
                {
                    Game.UpdateSprite(level, spr, 6, true); // arrastrandose sin moverse
                    spr.flipped = SENSX < 0;
                }
                break;

            case 21: // agachado con objeto
                NEW_FORM(action);
                GET_IMAGE(level);
                DECELERATION(level.player);
                break;

            case 5: // agachado
                NEW_FORM(action);
                GET_IMAGE(level);
                DECELERATION(level.player);
                if (Scroll.ACTION_TIMER == 15)
                {
                    CROSS_FLAG = 6;
                    spr.speedY = 0;
                }
                break;

            case 6: // escalera
            case 22:
                if (X_FLAG) XACCELERATION(level.player, MAX_X * 16);
                else DECELERATION(level.player);
                if (Scroll.ACTION_TIMER <= 1)
                    Game.UpdateSprite(level, spr, (short)(CARRY_FLAG ? 23 : 12), true); // ultimo sprite de trepar (23 = con objeto)
                if (Y_FLAG)
                {
                    NEW_FORM((byte)(6 + add_carry()));
                    GET_IMAGE(level);
                    spr.x = (short)((spr.x & 0xFFF0) + 8);
                    int tx = spr.x >> 4;
                    int ty = (spr.y & 0xFFF0) >> 4;
                    if (Level.GetFloorFlag(level, (short)ty, (short)tx) != FFLAG_LADDER)
                    {
                        if (Level.GetFloorFlag(level, (short)ty, (short)(tx - 1)) == FFLAG_LADDER) spr.x -= 16;
                        else if (Level.GetFloorFlag(level, (short)ty, (short)(tx + 1)) == FFLAG_LADDER) spr.x += 16;
                    }
                    spr.speedY = (short)(!KeyClimb() ? 4 * 16 : -(4 * 16));
                }
                else spr.speedY = 0;
                break;

            case 7:
            case 23: // coger / dejar en el suelo
                NEW_FORM(action);
                GET_IMAGE(level);
                DECELERATION(level.player);
                if (!POSEREADY_FLAG)
                {
                    if (Scroll.ACTION_TIMER == 1 && CARRY_FLAG)
                    {
                        // Dejar el objeto; si cae sobre suelo duro, le da velocidad lateral
                        TITUS_object? obj = FORCE_POSE(level);
                        if (obj != null)
                        {
                            short tx = (short)(obj.sprite.x >> 4), ty = (short)(obj.sprite.y >> 4);
                            byte ff = Level.GetFloorFlag(level, ty, tx);
                            if (ff != 0 && ff != 8)
                            {
                                tx++;
                                ff = Level.GetFloorFlag(level, ty, tx);
                                obj.sprite.speedX = (short)((ff != 0 && ff != 8) ? 16 * 3 : -(16 * 3));
                            }
                        }
                    }
                    else if (!CARRY_FLAG) TakeObject(level);
                }
                POSEREADY_FLAG = true;
                break;

            case 8:
            case 24: // soltar / lanzar
                NEW_FORM(action);
                GET_IMAGE(level);
                DECELERATION(level.player);
                if (CARRY_FLAG)
                {
                    TITUS_sprite s2 = level.player.sprite2;
                    int sx, sy;
                    if (!KeyUp())
                    {
                        sx = 0x0E * 16; sy = 0; // lanzamiento normal
                        if (spr.flipped) sx = -sx;
                        s2.y = (short)(spr.y - 16);
                    }
                    else { sx = 0; sy = -(0x0A * 16); } // hacia arriba
                    if (sy != 0 || (s2.number < Objects.FIRST_NMI && level.objectdata![s2.number - Objects.FIRST_OBJET].gravity))
                    {
                        // Cae por gravedad: vuelve a ser un objeto libre
                        TITUS_object? obj = FORCE_POSE(level);
                        if (obj != null)
                        {
                            obj.sprite.speedY = (short)sy;
                            obj.sprite.speedX = (short)(sx - (sx >> 2));
                        }
                    }
                    else
                    {
                        DROP_FLAG = true; // vuela recto en sprite2
                        Audio.FxStart(3); // player.c:1337/1345
                        s2.speedX = (short)sx;
                        s2.speedY = (short)sy;
                    }
                    Game.UpdateSprite(level, spr, 10, true); // como en caida libre
                    spr.flipped = SENSX < 0;
                    CARRY_FLAG = false;
                }
                break;

            case 11: // dolor de cabeza
            case 27:
                if (action == 27) FORCE_POSE(level);
                spr.speedX = 0;
                NEW_FORM(action);
                GET_IMAGE(level);
                break;

            case 12: // golpeado
            case 13:
            case 28:
            case 29:
                YACCELERATION(level.player, MAX_Y * 16);
                NEW_FORM(action);
                GET_IMAGE(level);
                break;

            // TODO: 10 sigilo
        }
    }

    // player.c:1120 (parte del caso 7): coge el primer objeto cercano o, si no, un enemigo (carry_sprite)
    static void TakeObject(TITUS_level level)
    {
        TITUS_player player = level.player;
        TITUS_sprite ps = player.sprite;
        for (int i = 0; i < level.objectcount; i++)
        {
            TITUS_sprite os = level.@object[i].sprite;
            if (!os.enabled || Math.Abs(ps.y - os.y) >= 20) continue;
            int diffX = ps.x - os.x;
            if (!ps.flipped) diffX = -diffX;
            if (diffX >= 20) continue; // TTF
            if (os.x > ps.x) { if (os.x > ps.x + 32) continue; }
            else if (os.x + os.spritedata!.collwidth < ps.x) continue;
            if (os.y < ps.y) { if (os.y <= ps.y - 10) continue; }
            else if (os.y - os.spritedata!.collheight + 1 >= ps.y) continue;

            // Cogerlo: sprite2 recibe una copia y el objeto deja de estar libre
            Audio.FxStart(9); // player.c:1177
            FUME_FLAG = 0;
            os.speedY = 0;
            os.speedX = 0;
            GRAVITY_FLAG = 4;
            Objects.CopySprite(player.sprite2, os);
            os.enabled = false;
            CARRY_FLAG = true;
            if (player.sprite2.number == Objects.FIRST_OBJET + 19) TAPISWAIT_FLAG = 0; // alfombra
            player.sprite2.y = (short)(ps.y - 4);
            player.sprite2.x = (short)(ps.flipped ? ps.x - 10 : ps.x + 12);
            return;
        }

        // player.c:1199: sin objeto, intenta coger un enemigo
        for (int i = 0; i < level.enemycount; i++)
        {
            TITUS_enemy en = level.enemy[i];
            TITUS_sprite es = en.sprite;
            if (!es.enabled || Math.Abs(ps.y - es.y) >= 20) continue;
            int dX = ps.x - es.x;
            if (!ps.flipped) dX = -dX;
            if (dX >= 20) continue; // TTF
            if (en.carry_sprite == -1) continue; // no se puede coger
            if (es.x > ps.x) { if (es.x > ps.x + 32) continue; }
            else if (es.x + es.spritedata!.collwidth < ps.x) continue;
            if (es.y < ps.y) { if (es.y <= ps.y - 10) continue; }
            else if (es.y - es.spritedata!.collheight - 1 >= ps.y) continue;
            if (es.number >= Objects.FIRST_NMI)
            {
                dX = ps.x - es.x;
                if (es.flipped) dX = -dX;
                if (dX < 0) continue;
            }

            // player.c:1271: copia flipped/flash/visible a sprite2 con el sprite de carga
            Audio.FxStart(9); // player.c:1269
            FUME_FLAG = 0;
            es.speedY = 0;
            es.speedX = 0;
            GRAVITY_FLAG = 4;
            player.sprite2.flipped = es.flipped;
            player.sprite2.flash = es.flash;
            player.sprite2.visible = es.visible;
            Game.UpdateSprite(level, player.sprite2, en.carry_sprite, false);
            es.enabled = false;
            CARRY_FLAG = true;
            SEECHOC_FLAG = 0;
            player.sprite2.y = (short)(ps.y - 4);
            player.sprite2.x = (short)(ps.flipped ? ps.x - 10 : ps.x + 12);
            return;
        }
    }

    // player.c:686 FORCE_POSE: pasa sprite2 a un objeto libre del nivel
    internal static TITUS_object? FORCE_POSE(TITUS_level level)
    {
        TITUS_sprite s2 = level.player.sprite2;
        if (!s2.enabled || !CARRY_FLAG) return null;
        int i = 0;
        while (i < level.objectcount && level.@object[i].sprite.enabled) i++;
        if (i >= level.objectcount) return null; // sin hueco libre
        TITUS_object o = level.@object[i];
        Objects.UpdateObjectSprite(level, o, s2.number, true);
        s2.enabled = false;
        o.sprite.killing = false;
        o.sprite.droptobottom = s2.number >= Objects.FIRST_NMI;
        o.sprite.x = s2.x;
        o.sprite.y = s2.y;
        o.mass = 0;
        o.sprite.speedY = 0;
        o.sprite.speedX = 0;
        o.sprite.UNDER = 0;
        o.sprite.ONTOP = null;
        POSEREADY_FLAG = true;
        GRAVITY_FLAG = 4;
        CARRY_FLAG = false;
        return o;
    }

    // player.c:1440
    static void NEW_FORM(byte action)
    {
        if (Scroll.LAST_ORDER != action || animRow < 0)
        {
            Scroll.LAST_ORDER = action;
            animRow = action;
            animIdx = 0;
        }
    }

    // player.c:1448
    static void GET_IMAGE(TITUS_level level)
    {
        TITUS_sprite spr = level.player.sprite;
        short frame = Original.ANIM_PLAYER[animRow, animIdx];
        if (frame < 0)
        {
            animIdx += frame / 2; // salto atras al primer frame
            frame = Original.ANIM_PLAYER[animRow, animIdx];
        }
        Game.UpdateSprite(level, spr, frame, true);
        spr.flipped = SENSX < 0;
        animIdx++;
    }

    // player.c:1422
    static void DECELERATION(TITUS_player player)
    {
        int friction = (3 * 4) >> player.GLISSE;
        int speed;
        if (player.sprite.speedX < 0)
        {
            speed = player.sprite.speedX + friction;
            if (speed > 0) speed = 0;
        }
        else
        {
            speed = player.sprite.speedX - friction;
            if (speed < 0) speed = 0;
        }
        player.sprite.speedX = (short)speed;
    }

    // player.c:746
    static void XACCELERATION(TITUS_player player, int maxspeed)
    {
        int changeX = X_FLAG ? (SENSX << 4) >> player.GLISSE : 0;
        int s = player.sprite.speedX;
        if (s + changeX >= maxspeed) player.sprite.speedX = (short)maxspeed;
        else if (s + changeX <= -maxspeed) player.sprite.speedX = (short)-maxspeed;
        else player.sprite.speedX = (short)(s + changeX);
    }

    // player.c:764
    static void YACCELERATION(TITUS_player player, int maxspeed)
    {
        if (player.sprite.speedY + 32 / 2 < maxspeed) player.sprite.speedY = (short)(player.sprite.speedY + 32 / 2);
        else player.sprite.speedY = (short)maxspeed;
    }

    // player.c:1462
    static void YACCELERATION_NEG(TITUS_player player, int maxspeed)
    {
        maxspeed = -maxspeed;
        int speed = player.sprite.speedY - 32;
        if (speed >= maxspeed) speed = maxspeed;
        player.sprite.speedY = (short)speed;
    }

    static TITUS_sprite? restRef;

    // player.c:1540 COLLISION_OBJET (jugador sobre objetos: resorte, alfombra, patinete, pelota)
    static void COLLISION_OBJET(TITUS_level level)
    {
        TITUS_player player = level.player;
        TITUS_sprite ps = player.sprite;
        if (ps.speedY < 0) return;
        restRef ??= new TITUS_sprite { number = 0, spritedata = level.spritedata![0] };
        restRef.spritedata = level.spritedata![0];
        if (!Objects.SpritesVsSprites(level, ps, restRef, out TITUS_object? offObj)) return;
        TITUS_sprite os = offObj!.sprite;
        ps.y = (short)(os.y - os.spritedata!.collheight);
        if (os.number == Objects.FIRST_OBJET + 24 || os.number == Objects.FIRST_OBJET + 25)
        {
            os.UNDER |= 0x02;
            os.ONTOP = ps;
        }
        if (os.number == Objects.FIRST_OBJET + 21 || os.number == Objects.FIRST_OBJET + 22)
        {
            // Saltar sobre la alfombra voladora: arranca
            os.speedX = (short)(!ps.flipped ? 6 * 16 : -6 * 16);
            os.flipped = ps.flipped;
            GRAVITY_FLAG = 4;
            TAPISWAIT_FLAG = 0;
        }
        else if (Scroll.ACTION_TIMER > 10 && (Scroll.LAST_ORDER & 0x0F) == 0 && ps.speedY == 0 &&
                 (os.number == 83 || os.number == 94))
        {
            os.speedX = (short)(!ps.flipped ? 16 * 3 : -16 * 3);
            os.flipped = ps.flipped;
            GRAVITY_FLAG = 4;
        }

        if (os.speedX < 0) ps.speedX = os.speedX;
        else if (os.speedX > 0) ps.speedX = (short)(os.speedX + 16);

        if (CROSS_FLAG == 0 && ps.speedY > 16 * 3 && offObj.objectdata!.bounce)
        {
            if (KeyDown()) ps.speedY = 0;
            else
            {
                if (KeyUp()) ps.speedY += 16 * 3;
                else ps.speedY -= 16;
                ps.speedY = (short)(0 - ps.speedY);
                if (ps.speedY > 0) ps.speedY = 0;
            }
            Scroll.ACTION_TIMER = 0;
            if (os.speedY == 0)
            {
                Audio.FxStart(12); // player.c:1613
                os.speedY = (short)(0 - ps.speedY);
                os.y -= (short)(os.speedY >> 4);
                GRAVITY_FLAG = 4;
            }
        }
        else
        {
            ps.speedY = os.speedY != 0 ? os.speedY : (short)0;
            subto0(ref SAUT_FLAG);
            SAUT_COUNT = 0;
            YFALL = 2;
        }
    }

    // player.c:935 CASE_SECU: candado; guarda posicion de reinicio y, si lleva la jaula, la de la jaula
    static void CASE_SECU(TITUS_level level, int tileY, int tileX)
    {
        Audio.SelectMusic(5); // player.c:939 (antes de CASE_BONUS, como el original)
        if (CASE_BONUS(level, tileY, tileX))
        {
            level.player.initX = level.player.sprite.x;
            level.player.initY = level.player.sprite.y;
            SaveCage(level);
        }
    }

    // player.c:944 si lleva la jaula, guarda su posicion
    internal static void SaveCage(TITUS_level level)
    {
        TITUS_player player = level.player;
        if (player.sprite2.number == Objects.FIRST_OBJET + 26 || player.sprite2.number == Objects.FIRST_OBJET + 27)
        {
            player.cageX = player.sprite.x;
            player.cageY = player.sprite.y;
        }
    }

    // player.c:898 CASE_BONUS (sin sonido): restaura el tile bajo el bonus; HP (>=253) sube energia
    static bool CASE_BONUS(TITUS_level level, int tileY, int tileX)
    {
        int i = 0;
        while (true)
        {
            if (i >= level.bonuscount) return false;
            TITUS_bonus bo = level.bonus[i];
            if (bo.x == tileX && bo.y == tileY) break;
            i++;
        }
        TITUS_bonus b = level.bonus[i];
        if (b.bonustile >= 255 - 2)
        {
            Audio.SelectMusic(6); // player.c:912
            BAR_FLAG = 50; // INC_ENERGY (player.c:952)
            level.player.hp++;
            if (level.player.hp > MAXIMUM_ENERGY)
            {
                level.player.hp = MAXIMUM_ENERGY;
                level.extrabonus++;
            }
        }
        level.tilemap[tileY][tileX] = b.replacetile;
        Scroll.DISPLAY_CHAR(level, b.replacetile, (byte)(tileY % Scroll.screen_height), (byte)(tileX % Scroll.screen_width));
        GRAVITY_FLAG = 4;
        Scroll.PERMUT_FLAG = true;
        return true;
    }

    // player.c:1481 COLLISION_TRP: el jugador sobre un ascensor viaja con el
    static void COLLISION_TRP(TITUS_level level)
    {
        TITUS_player player = level.player;
        TITUS_sprite ps = player.sprite;
        if (ps.speedY < 0 || CROSS_FLAG != 0) return;
        TITUS_spritedata sd0 = level.spritedata![0];
        foreach (TITUS_elevator el in level.elevator)
        {
            TITUS_sprite es = el.sprite;
            if (!el.enabled || !es.visible || es.spritedata == null ||
                Math.Abs(es.x - ps.x) >= 64 || Math.Abs(es.y - ps.y) >= 16) continue;

            if (ps.x - sd0.refwidth < es.x)
            {
                if (ps.x - sd0.refwidth + sd0.collwidth <= es.x) continue;
            }
            else if (ps.x - sd0.refwidth >= es.x + es.spritedata.collwidth) continue;

            if (ps.y - 6 < es.y)
            {
                if (ps.y - 6 + 8 <= es.y) continue;
            }
            else if (ps.y - 6 >= es.y + es.spritedata.collheight) continue;

            if (Scroll.ACTION_TIMER == 14) Scroll.ACTION_TIMER = 16; // salta la accion de caer por el tile (15)

            YFALL = 0;
            ps.y = es.y;
            ps.speedY = 0;
            subto0(ref SAUT_FLAG);
            SAUT_COUNT = 0;
            YFALL = 2;

            ps.x += es.speedX;
            if (es.speedY > 0) ps.y += es.speedY; // bajando
            return;
        }
    }

    // player.c:727 ARAB_TOMBE
    static void ARAB_TOMBE(TITUS_level level)
    {
        TITUS_player player = level.player;
        SAUT_FLAG = 6;
        if (KICK_FLAG != 0) return;
        XACCELERATION(player, MAX_X * 16);
        YACCELERATION(player, MAX_Y * 16);
        if (CHOC_FLAG != 0) Game.UpdateSprite(level, player.sprite, 15, true); // sprite de golpe
        else Game.UpdateSprite(level, player.sprite, (short)(CARRY_FLAG ? 21 : 10), true); // caida libre (21 = con objeto)
        player.sprite.flipped = SENSX < 0;
    }

    static void ARAB_TOMBE_F() { YFALL = (byte)(YFALL | 0x01); }

    // player.c:883
    static void ARAB_BLOCK_YU(TITUS_player player)
    {
        player.GLISSE = 0;
        if (player.sprite.speedY < 0)
        {
            YFALL = (byte)(YFALL | 0x01);
            return;
        }
        player.sprite.y = (short)(player.sprite.y & ~0x0F);
        player.sprite.speedY = 0;
        subto0(ref SAUT_FLAG);
        SAUT_COUNT = 0;
        YFALL = 2;
    }

    // player.c:672
    static void ARAB_BLOCKX(TITUS_player player)
    {
        player.sprite.x -= (short)(player.sprite.speedX >> 4);
        player.sprite.speedX = 0;
        if (KICK_FLAG != 0 && SAUT_FLAG != 6)
        {
            CHOC_FLAG = 20;
            KICK_FLAG = 0;
        }
    }

    // player.c:422 BRK_COLLISION
    static void BRK_COLLISION(TITUS_level level)
    {
        TITUS_player player = level.player;
        TITUS_sprite spr = player.sprite;
        int tileX = spr.x >> 4;
        int tileY = (spr.y >> 4) - 1;
        int initY = tileY;

        if (spr.y > ((level.height + 1) << 4)) CASE_DEAD_IM(level); // demasiado abajo: muerte

        YFALL = 0;
        int colltest = spr.x & 0x0F;
        if (colltest < TEST_ZONE)
        {
            colltest += 256;
            tileX--;
        }
        colltest -= TEST_ZONE;

        int left_tileX = tileX;
        TAKE_BLK_AND_YTEST(level, tileY, tileX);
        if (YFALL == 1)
        {
            colltest += TEST_ZONE * 2;
            if (colltest > 15) tileX++;
            if (tileX != left_tileX) TAKE_BLK_AND_YTEST(level, tileY, tileX);
            if (YFALL == 1)
            {
                if (CROSS_FLAG == 0 && CHOC_FLAG == 0)
                {
                    COLLISION_TRP(level);
                    if (YFALL == 1)
                    {
                        COLLISION_OBJET(level);
                        if (YFALL == 1) ARAB_TOMBE(level);
                        else player.GLISSE = 0;
                    }
                }
                else ARAB_TOMBE(level);
            }
        }

        // Colision horizontal
        int changeX = TEST_ZONE + MAX_X;
        if (spr.speedX < 0) changeX = -changeX;
        else if (spr.speedX == 0) changeX = 0;

        if (spr.y > MAP_LIMIT_Y + 1 && initY >= 0 && initY < level.height)
        {
            tileX = (spr.x + changeX) >> 4;
            int height = spr.spritedata!.collheight;
            int ty = initY;
            bool first = true;
            do
            {
                byte hflag = Level.GetHorizFlag(level, (short)ty, (short)tileX);
                if (first)
                {
                    BLOCK_XXPRG(level, hflag, ty, tileX);
                    first = false;
                }
                else if (hflag == 4 || hflag == 2) // codigo de nivel o bonus
                    BLOCK_XXPRG(level, hflag, ty, tileX);
                if (ty == 0) return;
                ty--;
                height -= 16;
            } while (height > 0);
        }
    }

    // player.c:636 BLOCK_XXPRG (solo muro)
    static void BLOCK_XXPRG(TITUS_level level, byte horiz, int tileY, int tileX)
    {
        switch (horiz)
        {
            case 1: // muro
                ARAB_BLOCKX(level.player);
                break;
            case 2: CASE_BONUS(level, tileY, tileX); break;
            case 3: CASE_DEAD_IM(level); break; // mortal (sin GODMODE)
            case 4: CASE_PASS(level, level.levelnumber, tileY, tileX); break;
            case 5: CASE_SECU(level, tileY, tileX); break;
            case 6: CASE_PASS(level, 14 - 1, tileY, tileX); break;
        }
    }

    // player.c:925 CASE_PASS (sin musica): lampara de codigo; muestra el codigo del nivel
    internal static int level_code = -1; // indice del ultimo codigo de nivel obtenido
    static void CASE_PASS(TITUS_level level, int levelIndex, int tileY, int tileX)
    {
        Audio.SelectMusic(7); // player.c:928
        if (CASE_BONUS(level, tileY, tileX))
        {
            level_code = levelIndex;
            if (!Game.ViewPassword(level, levelIndex)) GAMEOVER_FLAG = true;
        }
    }

    // player.c:519 TAKE_BLK_AND_YTEST
    static void TAKE_BLK_AND_YTEST(TITUS_level level, int tileY, int tileX)
    {
        TITUS_player player = level.player;
        TITUS_sprite spr = player.sprite;
        PRIER_FLAG = false;
        Scroll.LADDER_FLAG = false;

        if (spr.y <= MAP_LIMIT_Y || tileY < -1)
        {
            ARAB_TOMBE(level);
            YFALL = 0xFF;
            return;
        }
        if (tileY + 1 >= level.height)
        {
            ARAB_TOMBE(level);
            YFALL = 0xFF;
            return;
        }
        if (tileY == -1) tileY = 0;

        byte floor = Level.GetFloorFlag(level, (short)(tileY + 1), (short)tileX);
        byte floorAbove = Level.GetFloorFlag(level, (short)tileY, (short)tileX);
        if ((Scroll.LAST_ORDER & 0x0F) != 2) // 2 = saltar
            BLOCK_YYPRG(level, floor, floorAbove, tileY, tileX);

        // Techo / cabeza
        if (tileY < 1 || spr.speedY > 0) return;
        byte horiz = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
        byte ceil = Level.GetCeilFlag(level, (short)(tileY - 1), (short)tileX);
        BLOCK_YYPRGD(level, ceil);

        // Empuja al jugador fuera de un tile duro a la altura de la cabeza (padlock = 5)
        if ((horiz == HFLAG_WALL || horiz == HFLAG_DEADLY || horiz == 5) && spr.y > MAP_LIMIT_Y + 1)
        {
            int change = spr.speedX > 0 ? -1 : 1;
            tileX += change;
            horiz = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
            if (horiz == 0)
            {
                spr.x += (short)(change << 1);
            }
            else
            {
                change = -change;
                tileX += change + change;
                horiz = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
                if (horiz == 0) spr.x += (short)(change << 1);
            }
        }
    }

    // player.c:578 BLOCK_YYPRGD (solo techo solido)
    static void BLOCK_YYPRGD(TITUS_level level, byte ceil)
    {
        TITUS_sprite spr = level.player.sprite;
        switch (ceil)
        {
            case 1: // techo
            case 4: // TODO: mortal (muerte); de momento trata como techo
                if (spr.speedY != 0)
                {
                    spr.speedY = 0;
                    spr.y = (short)((spr.y & ~0x0F) + 16);
                    SAUT_COUNT = 0xFF;
                }
                else if (spr.number != 10 && spr.number != 21 && SAUT_FLAG != 6)
                {
                    PRIER_FLAG = true;
                    if (CARRY_FLAG)
                    {
                        TITUS_object? obj = FORCE_POSE(level);
                        if (obj != null)
                        {
                            short ox = (short)(obj.sprite.x >> 4), oy = (short)(obj.sprite.y >> 4);
                            byte h = Level.GetHorizFlag(level, oy, ox);
                            if (h == HFLAG_WALL || h == HFLAG_DEADLY || h == 5)
                            {
                                h = Level.GetHorizFlag(level, oy, (short)(ox - 1));
                                if (h == HFLAG_WALL || h == HFLAG_DEADLY || h == 5) obj.sprite.x += 16;
                                else obj.sprite.x -= 16;
                            }
                        }
                    }
                }
                break;
            case 2: // escalera
                if (spr.speedY < 0 && spr.speedX == 0)
                {
                    SAUT_COUNT = 10;
                    Scroll.LADDER_FLAG = true;
                }
                break;
            // TODO: 3 candado
        }
    }

    // player.c:774 BLOCK_YYPRG
    static void BLOCK_YYPRG(TITUS_level level, byte floor, byte floorAbove, int tileY, int tileX)
    {
        TITUS_player player = level.player;
        switch (floor)
        {
            case 0: ARAB_TOMBE_F(); break;
            case 1: ARAB_BLOCK_YU(player); break;
            case 2: ARAB_BLOCK_YU(player); player.GLISSE = 1; break;
            case 3: ARAB_BLOCK_YU(player); player.GLISSE = 2; break;
            case 4: ARAB_BLOCK_YU(player); player.GLISSE = 3; break;
            case 5: // atravesable si agachado (CROSS_FLAG)
                player.GLISSE = 0;
                if (CROSS_FLAG == 0) ARAB_BLOCK_YU(player);
                else ARAB_TOMBE_F();
                break;
            case 6: // escalera
            {
                if (CHOC_FLAG != 0) { ARAB_TOMBE_F(); return; }
                int order = Scroll.LAST_ORDER & 0x0F;
                if (order == 1 || order == 3 || order == 7 || order == 8)
                {
                    ARAB_BLOCK_YU(player);
                    return;
                }
                if (order == 5) // agachado: empieza a bajar por la escalera
                {
                    ARAB_TOMBE_F();
                    Game.UpdateSprite(level, player.sprite, 14, true);
                    player.sprite.y += 8;
                }
                if (floorAbove != FFLAG_LADDER)
                {
                    if (order == 0 || (KeyClimb() && order == 6))
                    {
                        ARAB_BLOCK_YU(player);
                        return;
                    }
                }
                subto0(ref SAUT_FLAG);
                SAUT_COUNT = 0;
                YFALL = 2;
                Scroll.LADDER_FLAG = true;
                break;
            }
            case 8:
            case 9:
            case 10: // mortal (sin GODMODE)
                CASE_DEAD_IM(level);
                break;
            case 7: CASE_BONUS(level, tileY, tileX); break;
            case 11: CASE_PASS(level, level.levelnumber, tileY, tileX); break;
            case 12: CASE_SECU(level, tileY, tileX); break;
            case 13: CASE_PASS(level, 14 - 1, tileY, tileX); break;
        }
    }
}
