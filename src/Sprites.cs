namespace TitusTheFox;

/// Port de sprites.c (solo Titus): carga de sprites.
/// NOTA: se omiten copysurface/flip/flash y spritebuffer[0..1]; aqui solo se guardan
/// los indices 0-15. El flip horizontal y el flash se portaran junto con el render.
public static class Sprites
{
    /// sprites.c:75 SDL_LoadSprite(): planar de 4 planos, cada plano de (w*h)>>3 bytes,
    /// MSB = pixel mas a la izquierda. Generaliza Level.LoadTile (fijo 16x16, plano=0x20).
    public static byte[] LoadSprite(byte[] first, int offset, int width, int height)
    {
        int groupsize = (width * height) >> 3;
        var pixels = new byte[width * height];
        int p = 0;
        for (int i = offset; i < offset + groupsize; i++)
            for (int j = 7; j >= 0; j--)
            {
                pixels[p++] = (byte)(((first[i] >> j) & 0x01)
                    | (((first[i + groupsize] >> j) << 1) & 0x02)
                    | (((first[i + groupsize * 2] >> j) << 2) & 0x04)
                    | (((first[i + groupsize * 3] >> j) << 3) & 0x08));
            }
        return pixels;
    }

    /// sprites.c:100 loadsprites(). Requiere Original.InitOriginal() ya ejecutado.
    public static TITUS_spritedata[] LoadSprites(byte[] spritedata)
    {
        var sprites = new TITUS_spritedata[Original.SPRITECOUNT];
        int offset = 0;
        for (int i = 0; i < Original.SPRITECOUNT; i++)
        {
            int w = Original.SPRITEWIDTH[i];
            int h = Original.SPRITEHEIGHT[i];
            sprites[i] = new TITUS_spritedata
            {
                data = LoadSprite(spritedata, offset, w, h),
                collheight = Original.SPRITECOLLHEIGHT[i],
                collwidth = Original.SPRITECOLLWIDTH[i],
                refheight = (short)(0 - ((short)Original.SPRITEREFHEIGHT[i] - h)),
                refwidth = Original.SPRITEREFWIDTH[i],
            };
            offset += (w * h) >> 1;
        }
        return sprites;
    }
}
