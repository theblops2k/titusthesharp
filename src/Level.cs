namespace TitusTheFox;

/// Port of level.h structures and level.c (loadlevel, flag getters). Titus only; no rendering or physics.

public class TITUS_tile
{
    public byte[] tiledata = new byte[256]; // 16x16 palette indices (0-15)
    public byte[] animation = new byte[3];  // level.h declares [2] but level.c writes [2] (overflow); sized 3 here
    public bool animated;
    public byte horizflag;
    public byte floorflag;
    public byte ceilflag;
    public byte current;
}

public class TITUS_spritedata
{
    public byte[]? data; // 8-bit indices, width x height
    public byte collheight;
    public byte collwidth;
    public short refheight;
    public byte refwidth;
}

public class TITUS_objectdata
{
    public byte maxspeedY;
    public bool support;
    public bool bounce;
    public bool gravity;
    public bool droptobottom;
    public bool no_damage;
}

public class TITUS_sprite
{
    public short x;
    public short y;
    public short prevX, prevY; // posicion al inicio del tick anterior (solo dibujo: interpolacion)
    public short speedX;
    public short speedY;
    public short number;
    public bool visible;
    public bool flash;
    public bool flipped;
    public bool enabled;
    public TITUS_spritedata? spritedata;
    public byte UNDER;
    public TITUS_sprite? ONTOP;
    public short[]? animation; // C: int16* into an animation table
    public int animationIndex; // offset of the C pointer within 'animation'
    public bool droptobottom;
    public bool killing;
    public bool invisible;
}

public class TITUS_object
{
    public TITUS_sprite sprite = new();
    public ushort initsprite;
    public int initX;
    public int initY;
    public bool init_enabled;
    public byte mass;
    public TITUS_objectdata? objectdata;
}

public class TITUS_enemy
{
    public byte dying;
    public byte phase;
    public TITUS_sprite sprite = new();
    public ushort type;
    public short power;
    public int centerX;
    public uint rangeX;
    public uint delay;
    public byte direction;
    public uint rangeY;
    public ushort initsprite;
    public int initX;
    public int initY;
    public bool init_enabled;
    public int initspeedX;
    public int initspeedY;
    public short carry_sprite;
    public short dead_sprite;
    public bool boss;
    public bool trigger;
    public bool visible;
    public byte counter;
    public byte walkspeedX;
}

public class TITUS_bonus
{
    public bool exists;
    public byte bonustile;
    public byte replacetile;
    public int x;
    public int y;
}

public class TITUS_gate
{
    public bool exists;
    public uint entranceX;
    public uint entranceY;
    public uint exitX;
    public uint exitY;
    public uint screenX;
    public uint screenY;
    public bool noscroll;
}

public class TITUS_elevator
{
    public bool enabled;
    public TITUS_sprite sprite = new();
    public uint counter;
    public uint range;
    public byte init_direction;
    public bool init_enabled;
    public short initspeedX;
    public short initspeedY;
    public ushort initsprite;
    public int initX;
    public int initY;
}

public class TITUS_player
{
    public TITUS_sprite sprite = new();
    public TITUS_sprite sprite2 = new();
    public TITUS_sprite sprite3 = new();
    public byte animcycle;
    public short cageX;
    public short cageY;
    public short hp;
    public short initX;
    public short initY;
    public byte inithp;
    public byte GLISSE;
}

public class TITUS_level
{
    public short levelnumber;
    public short levelid;
    public int height;
    public int width;
    public byte[][] tilemap = Array.Empty<byte[]>();
    public TITUS_tile[] tile = new TITUS_tile[256];
    public TITUS_spritedata[]? spritedata;
    public TITUS_objectdata[]? objectdata;
    public int finishX, finishY;
    public int lives, extrabonus;
    public byte[,] palette = new byte[16, 3]; // replaces SDL_PixelFormat palette (R,G,B)

    public TITUS_player player = new();

    public TITUS_object[] @object = Array.Empty<TITUS_object>();
    public short objectcount;
    public TITUS_enemy[] enemy = Array.Empty<TITUS_enemy>();
    public short enemycount;
    public TITUS_bonus[] bonus = Array.Empty<TITUS_bonus>();
    public short bonuscount;
    public TITUS_gate[] gate = Array.Empty<TITUS_gate>();
    public short gatecount;
    public TITUS_elevator[] elevator = Array.Empty<TITUS_elevator>();
    public short elevatorcount;
    public TITUS_sprite[] trash = Array.Empty<TITUS_sprite>();
    public short trashcount;
}

public static class Level
{
    public const byte HFLAG_NOWALL = 0, HFLAG_WALL = 1;
    public const byte FFLAG_NOFLOOR = 0, FFLAG_FLOOR = 1;
    public const byte CFLAG_NOCEILING = 0;

    // Globals written by loadlevel (PERMUT_FLAG, ALTITUDE_ZERO, XLIMIT) live in Scroll

    /// level.c:47 loadlevel(). levelnumber must be set beforehand.
    public static int LoadLevel(TITUS_level level, byte[] leveldata, TITUS_spritedata[]? spritedata, TITUS_objectdata[]? objectdata)
    {
        level.player.inithp = 16;
        level.player.cageX = 0;
        level.player.cageY = 0;

        int preSize = (leveldata.Length - 35828) >> 8;
        level.width = 256;
        level.height = preSize;
        level.tilemap = new byte[preSize][];
        for (int i = 0; i < preSize; i++)
        {
            level.tilemap[i] = new byte[level.width];
            Array.Copy(leveldata, i * level.width, level.tilemap[i], 0, level.width);
        }

        // level.c:86-89: palette with colour 14 replaced by the level colour
        for (int c = 0; c < 16; c++)
            for (int k = 0; k < 3; k++)
                level.palette[c, k] = OriginalTables.PaletteColour[c, k];
        for (int k = 0; k < 3; k++)
            level.palette[14, k] = OriginalTables.PaletteLevelColour[level.levelnumber, k];

        int b = level.height * level.width;
        int offset = b;
        int j = 256;
        for (int i = 0; i < 256; i++)
        {
            var t = level.tile[i] = new TITUS_tile();
            t.tiledata = LoadTile(leveldata, offset + i * 128);
            t.current = (byte)i;
            t.horizflag = leveldata[offset + 32768 + i];
            t.floorflag = leveldata[offset + 32768 + 256 + i];
            t.ceilflag = (byte)(leveldata[offset + 32768 + 512 + i] & 0x7F);

            t.animated = true;
            t.animation[0] = (byte)i;
            if (j == i - 1)
            {
                t.animation[1] = (byte)(i + 1);
                t.animation[2] = (byte)(i - 1);
                Scroll.PERMUT_FLAG = true;
            }
            else if (j == i - 2)
            {
                t.animation[1] = (byte)(i - 2);
                t.animation[2] = (byte)(i - 1);
                Scroll.PERMUT_FLAG = true;
            }
            else if ((leveldata[offset + 32768 + 512 + i] & 0x80) == 0x80)
            {
                t.animation[1] = (byte)(i + 1);
                t.animation[2] = (byte)(i + 2);
                j = i;
                Scroll.PERMUT_FLAG = true;
            }
            else
            {
                t.animation[1] = (byte)i;
                t.animation[2] = (byte)i;
                t.animated = false;
            }
        }
        level.spritedata = spritedata;
        level.objectdata = objectdata;

        level.player.initX = (short)LoadInt16(leveldata[b + 33779], leveldata[b + 33778]);
        level.player.initY = (short)LoadInt16(leveldata[b + 33781], leveldata[b + 33780]);
        level.finishX = LoadInt16(leveldata[b + 35825], leveldata[b + 35824]);
        level.finishY = LoadInt16(leveldata[b + 35827], leveldata[b + 35826]);

        // Objects
        level.objectcount = 40;
        level.@object = new TITUS_object[level.objectcount];
        offset = b + 33536;
        for (int i = 0; i < level.objectcount; i++)
        {
            var o = level.@object[i] = new TITUS_object();
            o.initsprite = (ushort)LoadUInt16(leveldata[offset + i * 6 + 1], leveldata[offset + i * 6 + 0]);
            o.init_enabled = o.initsprite != 0xFFFF;
            if (o.init_enabled)
            {
                o.initX = LoadInt16(leveldata[offset + i * 6 + 3], leveldata[offset + i * 6 + 2]);
                o.initY = LoadInt16(leveldata[offset + i * 6 + 5], leveldata[offset + i * 6 + 4]);
            }
        }

        // Enemies
        level.enemycount = 50;
        level.enemy = new TITUS_enemy[level.enemycount];
        offset = b + 33782;
        for (int i = 0; i < level.enemycount; i++)
        {
            var e = level.enemy[i] = new TITUS_enemy();
            e.initspeedY = 0;
            e.initsprite = (ushort)LoadUInt16(leveldata[offset + 5], leveldata[offset + 4]);
            e.init_enabled = e.initsprite != 0xFFFF;
            e.power = 0;
            e.walkspeedX = 0;
            if (e.init_enabled)
            {
                e.initX = LoadInt16(leveldata[offset + 1], leveldata[offset + 0]);
                e.initY = LoadInt16(leveldata[offset + 3], leveldata[offset + 2]);
                e.type = (ushort)(LoadUInt16(leveldata[offset + 7], leveldata[offset + 6]) & 0x1FFF);
                e.initspeedX = LoadInt16(leveldata[offset + 9], leveldata[offset + 8]);
                e.power = (short)LoadInt16(leveldata[offset + 13], leveldata[offset + 12]);

                switch (e.type)
                {
                    case 0: case 1:
                        e.centerX = LoadInt16(leveldata[offset + 16], leveldata[offset + 15]);
                        e.rangeX = LoadUInt16(leveldata[offset + 18], leveldata[offset + 17]);
                        break;
                    case 2:
                        e.delay = leveldata[offset + 16];
                        e.rangeX = LoadUInt16(leveldata[offset + 18], leveldata[offset + 17]);
                        e.direction = (byte)((e.rangeX >> 14) & 0x0003);
                        e.rangeX &= 0x3FFF;
                        break;
                    case 3: case 4: case 5: case 6:
                        e.centerX = LoadInt16(leveldata[offset + 16], leveldata[offset + 15]);
                        e.rangeX = LoadUInt16(leveldata[offset + 18], leveldata[offset + 17]);
                        e.rangeY = leveldata[offset + 19];
                        break;
                    case 7: case 9: case 10: case 11:
                        e.walkspeedX = leveldata[offset + 19];
                        e.rangeX = LoadUInt16(leveldata[offset + 24], leveldata[offset + 23]);
                        break;
                    case 8: case 14:
                        e.walkspeedX = leveldata[offset + 19];
                        break;
                    case 12:
                        e.rangeY = LoadUInt16(leveldata[offset + 16], leveldata[offset + 15]);
                        e.delay = leveldata[offset + 19];
                        break;
                    case 13:
                        e.delay = leveldata[offset + 20];
                        e.rangeX = LoadUInt16(leveldata[offset + 24], leveldata[offset + 23]);
                        break;
                    case 15: case 16:
                        break;
                    case 17:
                        e.rangeX = LoadUInt16(leveldata[offset + 16], leveldata[offset + 15]);
                        e.delay = LoadUInt16(leveldata[offset + 18], leveldata[offset + 17]);
                        e.rangeY = LoadUInt16(leveldata[offset + 22], leveldata[offset + 21]);
                        break;
                    case 18:
                        e.rangeX = LoadUInt16(leveldata[offset + 16], leveldata[offset + 15]);
                        e.rangeY = LoadUInt16(leveldata[offset + 18], leveldata[offset + 17]);
                        e.initspeedY = leveldata[offset + 19];
                        break;
                }
            }
            else
            {
                e.sprite.enabled = false;
            }
            offset += 26;
        }

        // Bonuses
        level.bonuscount = 100;
        level.bonus = new TITUS_bonus[level.bonuscount];
        offset = b + 35082;
        for (int i = 0; i < level.bonuscount; i++)
        {
            var bo = level.bonus[i] = new TITUS_bonus();
            bo.x = leveldata[offset + 2];
            bo.y = leveldata[offset + 3];
            bo.exists = bo.x != 0xFF && bo.y != 0xFF;
            if (bo.exists)
            {
                bo.bonustile = leveldata[offset];
                bo.replacetile = leveldata[offset + 1];
                level.tilemap[bo.y][bo.x] = leveldata[offset + 0];
            }
            offset += 4;
        }

        // Gates
        level.gatecount = 20;
        level.gate = new TITUS_gate[level.gatecount];
        offset = b + 35484;
        for (int i = 0; i < level.gatecount; i++)
        {
            var g = level.gate[i] = new TITUS_gate();
            g.entranceY = leveldata[offset + 1];
            g.exists = g.entranceY != 0xFF;
            if (g.exists)
            {
                g.entranceX = leveldata[offset + 0];
                g.screenX = leveldata[offset + 2];
                g.screenY = leveldata[offset + 3];
                g.exitX = leveldata[offset + 4];
                g.exitY = leveldata[offset + 5];
                g.noscroll = leveldata[offset + 6] != 0;
            }
            offset += 7;
        }

        // Elevators
        level.elevatorcount = 10;
        level.elevator = new TITUS_elevator[level.elevatorcount];
        offset = b + 35624;
        for (int i = 0; i < level.elevatorcount; i++)
        {
            var el = level.elevator[i] = new TITUS_elevator();
            el.sprite.enabled = false;
            el.initsprite = (ushort)LoadUInt16(leveldata[offset + 5], leveldata[offset + 4]);
            el.initspeedX = 0;
            el.initspeedY = 0;
            el.initX = LoadInt16(leveldata[offset + 13], leveldata[offset + 12]);
            el.initY = LoadInt16(leveldata[offset + 15], leveldata[offset + 14]);
            int speed = leveldata[offset + 7];
            el.init_enabled = el.initsprite != 0xFFFF && speed < 8 && el.initX >= -16 && el.initY >= 0;
            el.enabled = el.init_enabled;
            if (el.enabled)
            {
                el.range = LoadUInt16(leveldata[offset + 11], leveldata[offset + 10]);
                el.init_direction = leveldata[offset + 16];
                if (el.init_direction == 0 || el.init_direction == 3) // up or left
                    speed = -speed;
                if (el.init_direction == 0 || el.init_direction == 2) // up or down
                    el.initspeedY = (short)speed;
                else
                    el.initspeedX = (short)speed;
            }
            offset += 20;
        }

        Scroll.ALTITUDE_ZERO = (byte)LoadInt16(leveldata[b + 33777], leveldata[b + 33776]);
        Scroll.XLIMIT = (short)LoadInt16(leveldata[b + 35483], leveldata[b + 35482]);

        level.trashcount = 4;
        level.trash = new TITUS_sprite[level.trashcount];
        for (int i = 0; i < level.trashcount; i++)
            level.trash[i] = new TITUS_sprite();

        return 0;
    }

    /// sprites.c:186 SDL_LoadTile(): 16x16 tile, 4 planes of 32 bytes each, MSB = leftmost pixel.
    public static byte[] LoadTile(byte[] first, int i)
    {
        var pixels = new byte[256];
        int p = 0;
        for (int j = i; j < i + 0x20; j++)
            for (int k = 7; k >= 0; k--)
            {
                pixels[p++] = (byte)(((first[j] >> k) & 0x01)
                    | (((first[j + 0x20] >> k) << 1) & 0x02)
                    | (((first[j + 0x40] >> k) << 2) & 0x04)
                    | (((first[j + 0x60] >> k) << 3) & 0x08));
            }
        return pixels;
    }

    public static byte GetHorizFlag(TITUS_level level, short tileY, short tileX)
    {
        if (tileX < 0 || tileX >= level.width) return HFLAG_WALL;
        if (tileY < 0 || tileY >= level.height) return HFLAG_NOWALL;
        return level.tile[level.tilemap[tileY][tileX]].horizflag;
    }

    public static byte GetFloorFlag(TITUS_level level, short tileY, short tileX)
    {
        if (tileX < 0 || tileX >= level.width) return FFLAG_FLOOR;
        if (tileY < 0 || tileY >= level.height) return FFLAG_NOFLOOR;
        return level.tile[level.tilemap[tileY][tileX]].floorflag;
    }

    public static byte GetCeilFlag(TITUS_level level, short tileY, short tileX)
    {
        if (tileY < 0 || tileY >= level.height || tileX < 0 || tileX >= level.width) return CFLAG_NOCEILING;
        return level.tile[level.tilemap[tileY][tileX]].ceilflag;
    }

    /// c1 = high byte, c2 = low byte.
    public static uint LoadUInt16(byte c1, byte c2) => (uint)(((c1 * 256) & 0xFF00) + c2);

    public static int LoadInt16(byte c1, byte c2) => (short)((c1 << 8) + c2);
}
