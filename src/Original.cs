namespace TitusTheFox;

/// Port of original.c (initoriginal, getlevelid): runtime tables built from OriginalTables. Titus only.
public static class Original
{
    public const int SPRITECOUNT = 356;
    public const int ANIM_PLAYER_MAX = 15;
    public const int ANIM_PLAYER_COUNT = 30;
    public const int ORIG_ANIM_MAX = 20;
    public const int ORIG_OBJECT_COUNT = 71;
    public const int ORIG_NMI_COUNT = 230;
    public const int ORIG_LEVEL_COUNT = 20;
    public const int NMI_ANIM_TABLE_COUNT = 1758;

    public static readonly byte[] SPRITEWIDTH = new byte[SPRITECOUNT];
    public static readonly byte[] SPRITEHEIGHT = new byte[SPRITECOUNT];
    public static readonly byte[] SPRITECOLLWIDTH = new byte[SPRITECOUNT];
    public static readonly byte[] SPRITECOLLHEIGHT = new byte[SPRITECOUNT];
    public static readonly byte[] SPRITEREFWIDTH = new byte[SPRITECOUNT];
    public static readonly byte[] SPRITEREFHEIGHT = new byte[SPRITECOUNT];

    public static readonly short[,] ANIM_PLAYER = new short[ANIM_PLAYER_COUNT, ANIM_PLAYER_MAX];
    public static readonly short[] ANIM_ENEMY = new short[NMI_ANIM_TABLE_COUNT];
    public static readonly byte[] NMI_POWER = new byte[ORIG_LEVEL_COUNT];
    public static readonly byte[] LEVEL_MUSIC = new byte[ORIG_LEVEL_COUNT];

    public static readonly short[] ANIM_ZOUBIDA = new short[ORIG_ANIM_MAX];
    public static readonly short[] ANIM_MOKTAR = new short[ORIG_ANIM_MAX];
    public static readonly short[] ANIM_SMOKE = new short[ORIG_ANIM_MAX];
    public static readonly short[] COEUR_POS = new short[ORIG_ANIM_MAX * 2];

    public static readonly byte[] OBJECT_MAXSPEEDY = new byte[ORIG_OBJECT_COUNT];
    public static readonly bool[] OBJECT_SUPPORT = new bool[ORIG_OBJECT_COUNT];
    public static readonly bool[] OBJECT_BOUNCE = new bool[ORIG_OBJECT_COUNT];
    public static readonly bool[] OBJECT_GRAVITY = new bool[ORIG_OBJECT_COUNT];
    public static readonly bool[] OBJECT_DROPTOBOTTOM = new bool[ORIG_OBJECT_COUNT];
    public static readonly bool[] OBJECT_NO_DAMAGE = new bool[ORIG_OBJECT_COUNT];

    // original.c:41-70 (C array [30][15], unspecified entries are 0)
    static readonly short[][] orig_anim_player = {
        new short[] {0,-2},
        new short[] {-1},
        new short[] {9,8,-4},
        new short[] {5,6,6,7,7,-8},
        new short[] {4,-2},
        new short[] {5,-2},
        new short[] {11,11,12,12,-8},
        new short[] {22,-2},
        new short[] {22,-2},
        new short[] {9,-2},
        new short[] {8,8,9,9,-8},
        new short[] {25,26,27,-6},
        new short[] {15,15,15,15,15,15,15,15,10,-2},
        new short[] {28,28,28,28,28,28,28,28,10,-2},
        new short[] {-1},
        new short[] {-1},
        new short[] {16,-2},
        new short[] {17,17,17,18,18,18,18,19,19,19,18,18,18,18,-28},
        new short[] {20,21,-4},
        new short[] {17,17,17,18,18,18,18,19,19,19,18,18,18,18,-28},
        new short[] {16,-2},
        new short[] {22,-2},
        new short[] {23,23,24,24,-8},
        new short[] {22,-2},
        new short[] {22,-2},
        new short[] {-1},
        new short[] {-1},
        new short[] {25,26,27,-6},
        new short[] {15,15,15,15,15,15,15,15,10,-2},
        new short[] {28,28,28,28,28,28,28,28,10,-2},
    };

    static readonly short[] levelid_titus = {1,3,4,5,6,7,8,9,10,11,-1,12,13,-1,14,-1,15,-1,-1,2};

    static short At(short[] a, int i) => i < a.Length ? a[i] : (short)0;

    /// original.c:37 initoriginal()
    public static int InitOriginal()
    {
        for (int i = 0; i < ORIG_LEVEL_COUNT; i++) LEVEL_MUSIC[i] = OriginalTables.tmp_level_music[i];
        for (int i = 0; i < ORIG_LEVEL_COUNT; i++) NMI_POWER[i] = OriginalTables.tmp_nmi_power[i];

        var nmi = OriginalTables.tmpanimnmi;
        // La tabla original tiene 1758 bytes (879 valores de 16 bits); el C lee más allá, fuera de rango.
        for (int i = 0; i < nmi.Length / 2; i++)
            ANIM_ENEMY[i] = (short)((nmi[i * 2 + 1] << 8) | nmi[i * 2]);

        for (int i = 0; i < ORIG_OBJECT_COUNT; i++)
        {
            OBJECT_MAXSPEEDY[i] = OriginalTables.object_maxspeedY_data[i];
            byte f = OriginalTables.tmpobjectflag[i];
            OBJECT_SUPPORT[i] = (f & 0x01) != 0;
            OBJECT_BOUNCE[i] = (f & 0x02) != 0;
            OBJECT_GRAVITY[i] = (f & 0x04) != 0;
            OBJECT_DROPTOBOTTOM[i] = (f & 0x08) != 0;
            OBJECT_NO_DAMAGE[i] = (f & 0x10) != 0;
        }

        CopyUntilNegative(OriginalTables.anim_zoubida_data, ANIM_ZOUBIDA);
        CopyUntilNegative(OriginalTables.anim_moktar_data, ANIM_MOKTAR);
        CopyUntilNegative(OriginalTables.anim_smoke_data, ANIM_SMOKE);
        CopyUntilNegative(OriginalTables.COEUR_POS_data, COEUR_POS);

        for (int i = 0; i < ANIM_PLAYER_COUNT; i++)
            for (int j = 0; j < ANIM_PLAYER_MAX; j++)
            {
                ANIM_PLAYER[i, j] = At(orig_anim_player[i], j);
                if (ANIM_PLAYER[i, j] < 0) break;
            }

        // Titus (game == 0)
        for (int i = 0; i < SPRITECOUNT; i++)
        {
            SPRITEWIDTH[i] = OriginalTables.spritewidth_titus[i];
            SPRITEHEIGHT[i] = OriginalTables.spriteheight_titus[i];
            SPRITECOLLWIDTH[i] = OriginalTables.tmpspritecoll_titus[i * 2];
            SPRITECOLLHEIGHT[i] = OriginalTables.tmpspritecoll_titus[i * 2 + 1];
            SPRITEREFWIDTH[i] = OriginalTables.tmpspriteref_titus[i * 2];
            SPRITEREFHEIGHT[i] = OriginalTables.tmpspriteref_titus[i * 2 + 1];
        }
        // original.c:395-397: overwrites the whole row 1 (no stop at negative)
        for (int i = 0; i < ANIM_PLAYER_MAX; i++)
            ANIM_PLAYER[1, i] = OriginalTables.anim_player_titus[i];

        // Palettes are already in OriginalTables (PaletteColour/PaletteLevelColour/PaletteFontColour).
        return 0;
    }

    static void CopyUntilNegative(short[] src, short[] dst)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = At(src, i);
            if (dst[i] < 0) break;
        }
    }

    /// original.c:665 getlevelid(). Returns -1 if not found (undefined in C).
    public static short GetLevelId(short levelnumber)
    {
        for (short i = 0; i < 20; i++)
            if (levelid_titus[i] == levelnumber + 1)
                return i;
        return -1;
    }
}
