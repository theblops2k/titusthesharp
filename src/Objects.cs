namespace TitusTheFox;

// Port parcial de OpenTitus objects.c: init (reset.c:344), move_objects, SPRITES_VS_SPRITES, updateobjectsprite.
// Sin: alfombra voladora (tapis), humo (FUME), shock (objeto que cae contra el jugador), objeto vs enemigo.
public static class Objects
{
    // globals.h:108-109
    public const int FIRST_OBJET = 30;
    public const int FIRST_NMI = 101;

    // globals.h:117-144
    const byte HFLAG_WALL = 1, HFLAG_DEADLY = 3, HFLAG_PADLOCK = 5;
    const byte FFLAG_NOFLOOR = 0, FFLAG_LADDER = 6, FFLAG_WATER = 8, FFLAG_FIRE = 9;
    const byte CFLAG_NOCEILING = 0;

    // reset.c:344 (SET_ALL_SPRITES, parte de objetos)
    public static void Init(TITUS_level level)
    {
        for (int i = 0; i < level.objectcount; i++)
        {
            TITUS_object o = level.@object[i];
            o.sprite = new TITUS_sprite();
            o.mass = 0;
            if (o.init_enabled)
            {
                UpdateObjectSprite(level, o, (short)(o.initsprite & 0x1FFF), true);
                o.sprite.visible = (o.initsprite & 0x2000) == 0x2000;
                o.sprite.flash = (o.initsprite & 0x4000) == 0x4000;
                o.sprite.flipped = (o.initsprite & 0x8000) == 0x8000;
                o.sprite.x = (short)o.initX;
                o.sprite.y = (short)o.initY;
                if (level.player.cageY != 0 && (o.sprite.number == FIRST_OBJET + 26 || o.sprite.number == FIRST_OBJET + 27))
                {
                    o.sprite.x = level.player.cageX;
                    o.sprite.y = level.player.cageY;
                }
            }
        }
    }

    // reset.c:329 (SET_ALL_SPRITES, parte de ascensores)
    public static void InitElevators(TITUS_level level)
    {
        foreach (TITUS_elevator el in level.elevator)
        {
            el.sprite = new TITUS_sprite();
            if (!el.init_enabled) continue;
            Game.UpdateSprite(level, el.sprite, (short)((el.initsprite & 0x1FFF) + 30), true);
            el.sprite.visible = (el.initsprite & 0x2000) == 0x2000;
            el.sprite.flash = (el.initsprite & 0x4000) == 0x4000;
            el.sprite.flipped = (el.initsprite & 0x8000) == 0x8000;
            el.sprite.x = (short)el.initX;
            el.sprite.y = (short)el.initY;
            el.counter = 0;
            el.sprite.speedX = el.initspeedX;
            el.sprite.speedY = el.initspeedY;
        }
    }

    // elevators.c:40 MOVE_TRP
    public static void MoveElevators(TITUS_level level)
    {
        foreach (TITUS_elevator el in level.elevator)
        {
            if (!el.enabled) continue;
            TITUS_sprite s = el.sprite;
            s.x += s.speedX;
            s.y += s.speedY;
            el.counter++; // MTSBR
            if (el.counter >= el.range)
            {
                el.counter = 0;
                s.speedX = (short)-s.speedX;
                s.speedY = (short)-s.speedY;
            }
            s.invisible = !(s.x + 16 - (Scroll.BITMAP_X << 4) >= 0 &&
                            s.x - 16 - (Scroll.BITMAP_X << 4) <= Scroll.screen_width * 16 &&
                            s.y - (Scroll.BITMAP_Y << 4) >= 0 &&
                            s.y - (Scroll.BITMAP_Y << 4) - 16 <= Scroll.screen_height * 16);
        }
    }

    // objects.c:400 updateobjectsprite
    public static void UpdateObjectSprite(TITUS_level level, TITUS_object obj, short number, bool clearflags)
    {
        int index = number - FIRST_OBJET;
        Game.UpdateSprite(level, obj.sprite, number, clearflags);
        if (index < 0 || index >= Original.ORIG_OBJECT_COUNT) index = 0;
        obj.objectdata = level.objectdata![index];
    }

    // sprites.c copysprite
    public static void CopySprite(TITUS_sprite dst, TITUS_sprite src)
    {
        dst.x = src.x;
        dst.y = src.y;
        dst.speedX = src.speedX;
        dst.speedY = src.speedY;
        dst.number = src.number;
        dst.visible = src.visible;
        dst.flash = src.flash;
        dst.flipped = src.flipped;
        dst.enabled = src.enabled;
        dst.spritedata = src.spritedata;
        dst.UNDER = src.UNDER;
        dst.ONTOP = src.ONTOP;
        dst.animation = src.animation;
        dst.animationIndex = src.animationIndex;
        dst.droptobottom = src.droptobottom;
        dst.killing = src.killing;
        dst.invisible = src.invisible;
    }

    // objects.c:38 move_objects
    public static void Move(TITUS_level level)
    {
        if (Player.GRAVITY_FLAG == 0) return; // sin objetos activos

        for (int i = 0; i < level.objectcount; i++)
        {
            TITUS_object o = level.@object[i];
            TITUS_sprite s = o.sprite;
            bool objVsSprite = false;
            TITUS_object? offObject = null;
            if (!s.enabled) continue;
            if (s.x <= 8) { s.speedX = 2 * 16; s.speedY = 0; }                       // borde izquierdo
            if (s.x >= level.width * 16 - 8) { s.speedX = -2 * 16; s.speedY = 0; }  // borde derecho
            if (s.number == FIRST_OBJET + 21 || s.number == FIRST_OBJET + 22)
            {
                // Alfombra voladora, volando
                Player.GRAVITY_FLAG = 4;
                if (Player.TAPISWAIT_FLAG != 0) // lista: sigue la altura del jugador
                {
                    o.mass = 0;
                    short py = level.player.sprite.y;
                    if (s.y == py - 8) s.speedY = 0;
                    else if (s.y < py - 8) s.speedY = 16;
                    else s.speedY = -16;
                }
                if (Player.TAPISFLY_FLAG == 0) // se acabo el tiempo
                {
                    UpdateObjectSprite(level, o, FIRST_OBJET + 19, true);
                    s.speedX = 0;
                    Player.TAPISWAIT_FLAG = 2;
                }
            }
            else if ((s.number == FIRST_OBJET + 19 || s.number == FIRST_OBJET + 20) &&
                     (Player.IMAGE_COUNTER & 0x03) == 0 && s.speedY > 0 && Player.TAPISWAIT_FLAG != 2)
            {
                // La alfombra es lanzada
                if (s.number == FIRST_OBJET + 19)
                {
                    s.speedX = (short)(s.speedX >> 1);
                    UpdateObjectSprite(level, o, FIRST_OBJET + 20, false);
                }
                else
                {
                    s.speedX = 0;
                    UpdateObjectSprite(level, o, FIRST_OBJET + 21, false);
                }
                Player.TAPISWAIT_FLAG = 1;
                Player.TAPISFLY_FLAG = 200;
            }

            // Movimiento en X
            if (s.speedX != 0)
            {
                int tileX = s.x >> 4;
                int tileY = s.y >> 4;
                if ((s.y & 0x000F) == 0) tileY--;
                byte hflag = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
                if (hflag == HFLAG_WALL || hflag == HFLAG_DEADLY || hflag == HFLAG_PADLOCK)
                {
                    s.speedX = (short)(0 - s.speedX);
                    s.x += (short)(s.speedX >> 4);
                }
                else if ((((s.speedX >> 4) + s.x) >> 4) != tileX)
                {
                    if (s.speedX < 0) tileX--; else tileX++;
                    if (tileX < level.width && tileX >= 0)
                    {
                        hflag = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
                        if (hflag == HFLAG_WALL || hflag == HFLAG_DEADLY || hflag == HFLAG_PADLOCK)
                        {
                            s.speedX = (short)(0 - s.speedX);
                            s.x += (short)(s.speedX >> 4);
                        }
                    }
                }

                Player.GRAVITY_FLAG = 4;
                s.x += (short)(s.speedX >> 4);
                int reduction = Math.Abs((int)s.speedY) >= 16 ? 1 : 3;
                if (s.speedX < 0) reduction = -reduction;
                s.speedX = (short)(s.speedX - reduction);
                if (Math.Abs((int)s.speedX) < 16) s.speedX = 0;
            }

            if (s.speedY < 0)
            {
                // Sube
                int tileX = s.x >> 4;
                int tileY = (s.y >> 4) - (s.spritedata!.collheight >> 4) - 1;
                if (Level.GetCeilFlag(level, (short)tileY, (short)tileX) != CFLAG_NOCEILING)
                {
                    s.speedY = 0;
                    if (!o.objectdata!.bounce) s.y = (short)(s.y & 0xFFF0);
                    continue;
                }
                // (el test original es un bug: casi siempre verdadero)
                if (((s.speedY >> 4) + s.y) != (s.y >> 4))
                {
                    tileY--;
                    if (Level.GetCeilFlag(level, (short)tileY, (short)tileX) != CFLAG_NOCEILING)
                    {
                        s.speedY = 0;
                        if (!o.objectdata!.bounce) s.y = (short)(s.y & 0xFFF0);
                        continue;
                    }
                }
            }
            else if (s.droptobottom || (o.objectdata!.droptobottom && s.speedY >= 10 * 16))
            {
                // Cae hasta el fondo sin colision; se borra si no es visible
                if (!s.visible) { s.enabled = false; continue; }
            }
            else
            {
                // Cae con colision
                int tileX = s.x >> 4;
                int tileY = s.y >> 4;
                byte hflag = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
                byte fflag = Level.GetFloorFlag(level, (short)tileY, (short)tileX);
                if (s.y <= 6 || s.y >= (level.height << 4))
                {
                    fflag = FFLAG_NOFLOOR;
                    if (s.y >= (level.height << 4) + 64) { s.enabled = false; continue; }
                }
                if (fflag == FFLAG_FIRE) { s.enabled = false; continue; }
                if (fflag == FFLAG_WATER)
                {
                    if (s.number == FIRST_OBJET + 9) { s.speedY = 0; continue; } // pelota
                    s.enabled = false;
                    continue;
                }
                if (fflag != FFLAG_LADDER && (fflag != FFLAG_NOFLOOR || hflag == HFLAG_WALL || hflag == HFLAG_DEADLY || hflag == HFLAG_PADLOCK))
                {
                    s.speedY = 0;
                    if (!o.objectdata!.bounce) s.y = (short)(s.y & 0xFFF0);
                    continue;
                }

                // Colision con otros objetos
                int tileCount = ((s.y + (s.speedY >> 4)) >> 4) - (s.y >> 4);
                if (tileCount != 0) objVsSprite = SpritesVsSprites(level, s, s, out offObject);
                int j;
                for (j = 0; j < tileCount; j++)
                {
                    if (objVsSprite) break;
                    tileY += 1;
                    hflag = Level.GetHorizFlag(level, (short)tileY, (short)tileX);
                    fflag = Level.GetFloorFlag(level, (short)tileY, (short)tileX);
                    if (fflag == FFLAG_FIRE) { s.enabled = false; break; }
                    if (fflag != FFLAG_LADDER && (fflag != FFLAG_NOFLOOR || hflag == HFLAG_WALL || hflag == HFLAG_DEADLY || hflag == HFLAG_PADLOCK))
                    {
                        if (!o.objectdata!.bounce)
                        {
                            s.speedY = 0;
                            s.y = (short)((s.y & 0xFFF0) + 16);
                            if (s.number >= FIRST_OBJET + 19 && s.number <= FIRST_OBJET + 22)
                            {
                                UpdateObjectSprite(level, o, FIRST_OBJET + 19, false); // alfombra cerrada
                                Player.TAPISWAIT_FLAG = 0;
                            }
                            if (s.visible && !level.player.sprite2.enabled)
                            {
                                Player.FUME_FLAG = 32; // humo
                                level.player.sprite2.y = s.y;
                                level.player.sprite2.x = s.x;
                                Game.UpdateSprite(level, level.player.sprite2, FIRST_OBJET + 16, true);
                            }
                        }
                        else
                        {
                            s.y = (short)((s.y & 0xFFF0) + 16);
                            Player.GRAVITY_FLAG = 4;
                            o.mass = 0;
                            s.speedY = (short)(0 - s.speedY + 16 * 3);
                            if (s.speedY > 0) s.speedY = 0;
                        }
                        break;
                    }
                }
                if (!objVsSprite && (j < tileCount || !s.enabled)) continue;
                if (!objVsSprite) objVsSprite = SpritesVsSprites(level, s, s, out offObject);
                if (objVsSprite)
                {
                    TITUS_object off = offObject!;
                    o.mass = 0;
                    if (off.objectdata!.bounce)
                    {
                        off.sprite.UNDER |= 0x01;
                        off.sprite.ONTOP = s;
                        if (s.speedY > 64)
                        {
                            off.sprite.speedY = (short)(((0 - s.speedY) >> 1) + 32);
                            s.speedY = (short)(((0 - s.speedY) >> 1) + 16);
                            off.sprite.speedX = (short)(s.speedX >> 1);
                        }
                        else
                        {
                            s.speedY = 0;
                            s.y = (short)(off.sprite.y - off.sprite.spritedata!.collheight);
                            continue;
                        }
                    }
                    else if (o.objectdata!.bounce)
                    {
                        s.y = (short)(off.sprite.y - off.sprite.spritedata!.collheight);
                        if (s.speedY >= 16 || s.speedY < 0)
                        {
                            Player.GRAVITY_FLAG = 4;
                            s.speedY = (short)(0 - s.speedY + 16 * 3);
                            if (s.speedY > 0) s.speedY = 0;
                        }
                        else s.speedY = 0;
                        continue;
                    }
                    else
                    {
                        s.y = (short)(off.sprite.y - off.sprite.spritedata!.collheight);
                        s.speedY = 0;
                    }
                }
            }

            // Gravedad
            int maxSpeed = 15;
            if (s.number < FIRST_NMI) maxSpeed = o.objectdata!.maxspeedY;
            int speed = s.speedY >> 4;
            if (speed != 0) Player.GRAVITY_FLAG = 4;
            s.y += (short)speed;
            if (speed < maxSpeed)
            {
                s.speedY += 16;
                if (s.speedY > 0) o.mass++;
            }
            Shock(level, o);
        }
    }

    // objects.c:321 shock: objeto que cae contra el jugador
    // TODO: FX_START(5) (sonido); GODMODE no existe en el port
    static void Shock(TITUS_level level, TITUS_object obj)
    {
        TITUS_sprite p = level.player.sprite;
        TITUS_sprite s = obj.sprite;
        if (obj.mass < 10) return;
        if (p.speedY >= 12 * 16) return; // MAX_Y*16
        if (Math.Abs(p.y - s.y) >= 32) return;
        if (Math.Abs(p.x - s.x) >= 32) return;
        if (s.x > p.x)
        {
            if (s.x > p.x + 24) return;
        }
        else
        {
            if (s.x + s.spritedata!.collwidth < p.x) return;
        }
        if (s.y < p.y)
        {
            if (s.y <= p.y - 32) return;
        }
        else
        {
            if (s.y - s.spritedata!.collheight + 1 >= p.y) return;
        }
        Audio.FxStart(5); // objects.c:353
        Player.CHOC_FLAG = 24;
        if (s.killing)
        {
            Player.DEC_ENERGY(level);
            s.killing = false;
        }
    }

    // sprites.c:277 animate_sprite (solo jaula y alfombra voladora) para sprite2, sprite3 y objetos
    public static void Animate(TITUS_level level)
    {
        AnimateSprite(level, level.player.sprite2);
        AnimateSprite(level, level.player.sprite3);
        for (int i = 0; i < level.objectcount; i++) AnimateSprite(level, level.@object[i].sprite);
        for (int i = 0; i < level.enemycount; i++) AnimateSprite(level, level.enemy[i].sprite); // sprites.c:268
        for (int i = 0; i < level.elevatorcount; i++) AnimateSprite(level, level.elevator[i].sprite); // sprites.c:272
    }

    static void AnimateSprite(TITUS_level level, TITUS_sprite s)
    {
        if (!s.visible || !s.enabled) return;
        int c = Player.IMAGE_COUNTER;
        if (s.number == FIRST_OBJET + 26) { if ((c & 0x07) == 0) Game.UpdateSprite(level, s, FIRST_OBJET + 27, false); }
        else if (s.number == FIRST_OBJET + 27) { if ((c & 0x3F) == 0) Game.UpdateSprite(level, s, FIRST_OBJET + 26, false); }
        else if (s.number == FIRST_OBJET + 21) { if ((c & 0x07) == 0) Game.UpdateSprite(level, s, FIRST_OBJET + 22, false); }
        else if (s.number == FIRST_OBJET + 22) { if ((c & 0x07) == 0) Game.UpdateSprite(level, s, FIRST_OBJET + 21, false); }
        else if (s.number == FIRST_OBJET + 24) // sprites.c:296 resorte pequeno
        {
            if ((c & 0x01) == 0)
            {
                if (s.UNDER == 0) Game.UpdateSprite(level, s, FIRST_OBJET + 25, false); // sin carga: pasa a resorte grande
                else if (Player.GRAVITY_FLAG > 1) s.UNDER = 0;
                else s.UNDER = (byte)(s.UNDER & 0x01); // conserva carga de objeto, quita la del jugador
            }
        }
        else if (s.number == FIRST_OBJET + 25) // sprites.c:306 resorte grande
        {
            if ((c & 0x01) == 0)
            {
                if (s.UNDER == 0) return; // sin carga: sigue grande
                else if (Player.GRAVITY_FLAG > 1) s.UNDER = 0;
                else s.UNDER = (byte)(s.UNDER & 0x01);
                if (s.ONTOP != null) s.ONTOP.y += 5; // sprites.c:315
                Player.GRAVITY_FLAG = 3;
                Game.UpdateSprite(level, s, FIRST_OBJET + 24, false); // resorte pequeno
            }
        }
    }

    // objects.c:366 SPRITES_VS_SPRITES (solo objetos que soportan)
    public static bool SpritesVsSprites(TITUS_level level, TITUS_sprite sprite1, TITUS_sprite sprite1ref, out TITUS_object? object2)
    {
        object2 = null;
        int obj1left = sprite1.x - (Original.SPRITEWIDTH[sprite1ref.number] >> 1);
        for (int i = 0; i < level.objectcount; i++)
        {
            TITUS_object o = level.@object[i];
            if (o.sprite == sprite1 || !o.sprite.enabled || !o.objectdata!.support) continue;
            if (Math.Abs(o.sprite.x - obj1left) > 64) continue;
            if (Math.Abs(o.sprite.y - sprite1.y) > 70) continue;
            int obj2left = o.sprite.x - (o.sprite.spritedata!.collwidth >> 1);
            if (obj2left > obj1left)
            {
                if (obj1left + sprite1ref.spritedata!.collwidth <= obj2left) continue;
            }
            else
            {
                if (obj2left + o.sprite.spritedata!.collwidth <= obj1left) continue;
            }
            if (o.sprite.y - (o.sprite.spritedata!.collheight >> 3) >= sprite1.y)
            {
                if (o.sprite.y - o.sprite.spritedata!.collheight <= sprite1.y)
                {
                    object2 = o;
                    return true;
                }
            }
        }
        return false;
    }
}
