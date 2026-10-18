namespace TitusTheFox;

// Port of OpenTitus scroll.c (Titus only, no SDL).
public static class Scroll
{
    public const int MAX_X = 4;
    public const int LIMIT_TIMER = 22;
    public const int ALERT_X = 32;
    public const int screen_width = 20;
    public const int screen_height = 12;

    // Globals (globals.h)
    public static byte BITMAP_X;          // Screen offset (X) in tiles
    public static byte BITMAP_XM;         // Left tile in the tile screen (0..19)
    public static byte BITMAP_Y;          // Screen offset (Y) in tiles
    public static byte BITMAP_YM;         // Top tile in the tile screen (0..11)
    public static bool XSCROLL_CENTER;
    public static short XLIMIT_SCROLL;
    public static short XLIMIT;
    public static bool YSCROLL_CENTER;
    public static byte YLIMIT_SCROLL;
    public static byte ALTITUDE_ZERO;
    public static bool NOSCROLL_FLAG;
    public static bool PERMUT_FLAG;
    public static byte tile_anim;
    public static byte loop_cycle;
    public static byte ACTION_TIMER;
    // Used by X_ADJUST/Y_ADJUST, not yet mapped elsewhere
    public static bool LADDER_FLAG;
    public static byte LAST_ORDER;

    // Tile screen abstraction: 8-bit indices, (screen_width*16) x (screen_height*16)
    public const int TILESCREEN_W = screen_width * 16;
    public const int TILESCREEN_H = screen_height * 16;
    public static byte[] tilescreen = new byte[TILESCREEN_W * TILESCREEN_H];

    // Vista del nivel en px (solo dibujo). 320 salvo WideScreen=T + FullScreen=T (ver SetViewWidth); la logica sigue usando 20 tiles
    public static int VIEW_W = TILESCREEN_W;
    public static int MARGIN => (VIEW_W - TILESCREEN_W) / 2; // px extra a cada lado

    // Ancho de vista = round(200 * monW / monH) a multiplo de 16; sin ampliar si la pantalla no es mas ancha que 4:3
    public static void SetViewWidth(int monW, int monH)
    {
        if (monH <= 0 || monW * 3 <= monH * 4) return;
        int w = (int)Math.Round(200.0 * monW / monH / 16.0) * 16;
        VIEW_W = Math.Clamp(w, TILESCREEN_W, 512);
    }

    // Hook to copy a 16x16 tile into the tile screen; default writes into 'tilescreen'.
    public static Action<byte[], int, int> BlitTile = DefaultBlitTile;

    private static void DefaultBlitTile(byte[] tiledata, int destX, int destY)
    {
        for (int row = 0; row < 16; row++)
            Array.Copy(tiledata, row * 16, tilescreen, (destY + row) * TILESCREEN_W + destX, 16);
    }

    public static void scroll(TITUS_level level)
    {
        // Scroll screen and update tile animation
        loop_cycle++; // Cycle from 0 to 3
        if (loop_cycle > 3)
        {
            loop_cycle = 0;
        }
        if (loop_cycle == 0) // Every 4th call
        {
            tile_anim++; // Cycle tile animation (0-1-2)
            if (tile_anim > 2)
            {
                tile_anim = 0;
            }
        }
        if (!NOSCROLL_FLAG)
        {
            X_ADJUST(level);
            Y_ADJUST(level);
        }
    }

    public static void X_ADJUST(TITUS_level level)
    {
        bool block;
        TITUS_player player = level.player;
        short pstileX = (short)((player.sprite.x >> 4) - BITMAP_X); // Player screen tile X (0 to 19)
        if (!XSCROLL_CENTER)
        {
            if (ACTION_TIMER < LIMIT_TIMER)
            {
                if ((!XSCROLL_CENTER) &&
                    ((pstileX > screen_width - ALERT_X / 16) ||
                     (pstileX < ALERT_X / 16)))
                {
                    XLIMIT_SCROLL = screen_width / 2;
                    XSCROLL_CENTER = true;
                }
            }
            else if ((LAST_ORDER & 0x0F) == 1) // Walking, scroll timer is activated
            {
                if (Math.Abs((int)player.sprite.speedX) != MAX_X * 16)
                {
                    if (!player.sprite.flipped)
                    {
                        XLIMIT_SCROLL = 2;
                        XSCROLL_CENTER = true;
                    }
                    else
                    {
                        XLIMIT_SCROLL = screen_width - 2;
                        XSCROLL_CENTER = true;
                    }
                }
                else // Walking at full speed
                {
                    if (player.sprite.speedX < 0) // Walking left
                    {
                        if (pstileX <= screen_width / 2 - 3)
                        {
                            XLIMIT_SCROLL = screen_width - 2;
                            XSCROLL_CENTER = true;
                        }
                    }
                    else if (pstileX >= screen_width / 2 + 3)
                    {
                        XLIMIT_SCROLL = 2;
                        XSCROLL_CENTER = true;
                    }
                }
            }
            else // Not walking
            {
                if ((player.sprite.speedX == 0) &&
                    (player.sprite.speedY == 0) &&
                    (ACTION_TIMER == (LIMIT_TIMER * 3 / 2)))
                {
                    XLIMIT_SCROLL = BARRYCENTRE(level);
                    XSCROLL_CENTER = true;
                }
                else
                {
                    if ((!XSCROLL_CENTER) &&
                        ((pstileX > screen_width - ALERT_X / 16) ||
                         (pstileX < ALERT_X / 16)))
                    {
                        XLIMIT_SCROLL = screen_width / 2;
                        XSCROLL_CENTER = true;
                    }
                }
            }
        }

        if (XSCROLL_CENTER)
        {
            if (XLIMIT_SCROLL == pstileX)
            {
                XSCROLL_CENTER = false;
            }
            else if (XLIMIT_SCROLL > pstileX)
            {
                block = L_SCROLL(level);
                if (block)
                {
                    XSCROLL_CENTER = false;
                }
            }
            else
            {
                block = R_SCROLL(level);
                if (block)
                {
                    XSCROLL_CENTER = false;
                }
            }
        }
    }

    public static void Y_ADJUST(TITUS_level level)
    {
        TITUS_player player = level.player;
        if (player.sprite.speedY == 0)
        {
            YSCROLL_CENTER = false;
        }
        short pstileY = (short)((player.sprite.y >> 4) - BITMAP_Y); // Player screen tile Y (0 to 11)
        if (!YSCROLL_CENTER)
        {
            if ((player.sprite.speedY == 0) &&
                (!LADDER_FLAG))
            {
                if (pstileY >= screen_height - 1)
                {
                    YLIMIT_SCROLL = screen_height - 2;
                    YSCROLL_CENTER = true;
                }
                else if (pstileY <= 2)
                {
                    YLIMIT_SCROLL = screen_height - 3;
                    YSCROLL_CENTER = true;
                }
            }
            else
            {
                if (pstileY >= screen_height - 2)
                {
                    YLIMIT_SCROLL = 3;
                    YSCROLL_CENTER = true;
                }
                else if (pstileY <= 2)
                {
                    YLIMIT_SCROLL = screen_height - 3;
                    YSCROLL_CENTER = true;
                }
            }
        }

        if ((player.sprite.y <= ((ALTITUDE_ZERO + screen_height) << 4)) &&
            (BITMAP_Y > ALTITUDE_ZERO + 1))
        {
            if (U_SCROLL(level))
            {
                YSCROLL_CENTER = false;
            }
        }
        else if ((BITMAP_Y > ALTITUDE_ZERO - 5) &&
                 (BITMAP_Y <= ALTITUDE_ZERO) &&
                 (player.sprite.y + (7 * 16) > ((ALTITUDE_ZERO + screen_height) << 4)))
        {
            if (D_SCROLL(level))
            {
                YSCROLL_CENTER = false;
            }
        }
        else if (YSCROLL_CENTER)
        {
            if (YLIMIT_SCROLL == pstileY)
            {
                YSCROLL_CENTER = false;
            }
            else if (YLIMIT_SCROLL > pstileY)
            {
                if (U_SCROLL(level))
                {
                    YSCROLL_CENTER = false;
                }
            }
            else if ((player.sprite.y <= ((ALTITUDE_ZERO + screen_height) << 4)) &&
                     (BITMAP_Y > ALTITUDE_ZERO))
            {
                YSCROLL_CENTER = false;
            }
            else
            {
                if (D_SCROLL(level))
                {
                    YSCROLL_CENTER = false;
                }
            }
        }
    }

    public static byte BARRYCENTRE(TITUS_level level)
    {
        // If an enemy is behind the player, max. 12.5 tiles away horizontally, scroll until player is in the middle
        // If not, scroll until player is in the 3rd screen tile
        bool enemy_left;
        for (int i = 0; i < level.enemycount; i++)
        {
            if (!level.enemy[i].sprite.enabled || !level.enemy[i].visible)
            {
                continue;
            }
            enemy_left = level.enemy[i].sprite.x < level.player.sprite.x;
            if ((enemy_left != level.player.sprite.flipped) &&
                (Math.Abs(level.enemy[i].sprite.x - level.player.sprite.x) < 200))
            {
                return screen_width / 2;
            }
        }
        if (!level.player.sprite.flipped)
        {
            return 3;
        }
        else
        {
            return screen_width - 3;
        }
    }

    public static bool L_SCROLL(TITUS_level level)
    {
        if (BITMAP_X == 0)
        {
            return true;
        }
        BITMAP_X--;
        if (BITMAP_XM <= 0)
        {
            BITMAP_XM = screen_width;
        }
        BITMAP_XM--; // 0 to 19
        REFRESH_COLUMNS(level, 0);
        return false;
    }

    public static bool R_SCROLL(TITUS_level level)
    {
        byte maxX;
        if (((level.player.sprite.x >> 4) - screen_width) > XLIMIT)
        {
            maxX = (byte)(level.width - screen_width); // 256 - 20
        }
        else
        {
            maxX = (byte)XLIMIT;
        }
        if (BITMAP_X >= maxX)
        {
            return true;
        }
        BITMAP_X++;
        BITMAP_XM++;
        if (BITMAP_XM >= screen_width)
        {
            BITMAP_XM = 0;
        }
        REFRESH_COLUMNS(level, screen_width - 1);
        return false;
    }

    public static bool U_SCROLL(TITUS_level level)
    {
        if (BITMAP_Y == 0)
        {
            return true;
        }
        BITMAP_Y--;
        if (BITMAP_YM <= 0)
        {
            BITMAP_YM = screen_height;
        }
        BITMAP_YM--; // 0 to 11
        REFRESH_LINE(level, 0);
        return false;
    }

    public static bool D_SCROLL(TITUS_level level)
    {
        if (BITMAP_Y >= (level.height - screen_height))
        {
            return true;
        }
        BITMAP_Y++;
        BITMAP_YM++;
        if (BITMAP_YM >= screen_height)
        {
            BITMAP_YM = 0;
        }
        REFRESH_LINE(level, screen_height - 1);
        return false;
    }

    public static void REFRESH_COLUMNS(TITUS_level level, sbyte column)
    {
        // Redraw one column on the hidden tile screen: 0 if scroll left; 19 if scroll right
        byte tmpX = (byte)(BITMAP_X + column);
        byte tmpY = BITMAP_Y;
        byte tmpYM = BITMAP_YM;
        byte tmpXM = (byte)(BITMAP_XM + column);
        byte cur_tile;
        if (tmpXM >= screen_width)
        {
            tmpXM = (byte)(tmpXM - screen_width);
        }
        for (byte i = 0; i < screen_height; i++)
        {
            cur_tile = level.tilemap[tmpY][tmpX];
            PERMUT_FLAG = PERMUT_FLAG | level.tile[cur_tile].animated;
            DISPLAY_CHAR(level, cur_tile, tmpYM, tmpXM);
            tmpY++;
            tmpYM++;
            if (tmpYM >= screen_height)
            {
                tmpYM = (byte)(tmpYM - screen_height);
            }
        }
    }

    public static void REFRESH_LINE(TITUS_level level, sbyte line)
    {
        // Redraw one line on the hidden tile screen: 0 if scroll up; 11 if scroll down
        byte tmpX = BITMAP_X;
        byte tmpY = (byte)(BITMAP_Y + line);
        byte tmpYM = (byte)(BITMAP_YM + line);
        byte tmpXM = BITMAP_XM;
        byte cur_tile;
        if (tmpYM >= screen_height)
        {
            tmpYM = (byte)(tmpYM - screen_height);
        }
        for (byte i = 0; i < screen_width; i++)
        {
            cur_tile = level.tilemap[tmpY][tmpX];
            PERMUT_FLAG = PERMUT_FLAG | level.tile[cur_tile].animated;
            DISPLAY_CHAR(level, cur_tile, tmpYM, tmpXM);
            tmpX++;
            tmpXM++;
            if (tmpXM >= screen_width)
            {
                tmpXM = (byte)(tmpXM - screen_width);
            }
        }
    }

    public static void DISPLAY_CHAR(TITUS_level level, byte tile, byte y, byte x)
    {
        // Update the tile surface
        BlitTile(level.tile[level.tile[tile].animation[tile_anim]].tiledata, x * 16, y * 16);
    }
}
