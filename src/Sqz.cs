namespace TitusTheFox;

/// Port of sqz.c: decompresses "SQZ" files (LZW with 0x10 type byte, otherwise Huffman).
public static class Sqz
{
    const int LZW_CLEAR_CODE = 0x100;
    const int LZW_END_CODE = 0x101;
    const int LZW_FIRST = 0x102;
    const int LZW_MAX_TABLE = 4096;

    public static byte[] Decompress(string path) => Decompress(File.ReadAllBytes(path));

    public static byte[] Decompress(byte[] file)
    {
        if (file.Length < 4)
            throw new InvalidDataException("SQZ file too short");

        byte b1 = file[0];
        byte compType = file[1];
        byte b3 = file[2];
        byte b4 = file[3];

        int outLen = ((b1 & 0x0F) << 16) | (b4 << 8) | b3;
        if (outLen == 0)
            throw new InvalidDataException("Invalid SQZ file");

        byte[] input = file[4..];
        byte[] output = new byte[outLen];

        if (compType == 0x10)
            LzwDecode(input, output);
        else
            HuffmanDecode(input, output);

        return output;
    }

    /// Writes one byte. Writes past the end are dropped, like the C code's bounded output.
    static void Put(byte[] output, ref int pos, byte value)
    {
        if (pos < output.Length)
            output[pos] = value;
        pos++;
    }

    static void LzwDecode(byte[] input, byte[] output)
    {
        int inLen = input.Length;
        int nbit = 9;
        uint bitadd = 0;
        uint kPos = 0;
        uint w = 0;
        int outPos = 0;
        bool addToDict = false;
        uint dictLength = 0;
        uint[] dictPrefix = new uint[LZW_MAX_TABLE];
        byte[] dictCharacter = new byte[LZW_MAX_TABLE];
        byte[] dictStack = new byte[LZW_MAX_TABLE + 2];

        while (kPos < inLen && outPos < output.Length)
        {
            uint k = 0;
            int i = 0;
            while (i < 4)
            {
                k <<= 8;
                if (kPos + i < inLen && i * 8 < bitadd + nbit)
                    k += input[kPos + i];
                i++;
            }
            k <<= (int)bitadd;
            k >>= 32 - nbit;
            bitadd += (uint)nbit;
            while (bitadd > 8)
            {
                bitadd -= 8;
                kPos++;
            }

            if (k == LZW_CLEAR_CODE)
            {
                nbit = 9;
                dictLength = 0;
                addToDict = false;
            }
            else if (k != LZW_END_CODE)
            {
                if (k > 255 && k < LZW_FIRST + dictLength)
                {
                    i = 0;
                    uint tmpK = k;
                    while (tmpK >= LZW_FIRST)
                    {
                        dictStack[i] = dictCharacter[tmpK - LZW_FIRST];
                        tmpK = dictPrefix[tmpK - LZW_FIRST];
                        if (i++ >= LZW_MAX_TABLE)
                            throw new InvalidDataException("SQZ dictionary overflow");
                    }
                    dictStack[i++] = (byte)tmpK;
                    tmpK = (uint)(i - 1);
                    while (--i > 0)
                        Put(output, ref outPos, dictStack[i]);
                    Put(output, ref outPos, dictStack[0]);
                    dictStack[0] = dictStack[tmpK];
                }
                else if (k > 255 && k >= LZW_FIRST + dictLength)
                {
                    i = 1;
                    uint tmpK = w;
                    while (tmpK >= LZW_FIRST)
                    {
                        dictStack[i] = dictCharacter[tmpK - LZW_FIRST];
                        tmpK = dictPrefix[tmpK - LZW_FIRST];
                        if (i++ >= LZW_MAX_TABLE)
                            throw new InvalidDataException("SQZ dictionary overflow");
                    }
                    dictStack[i++] = (byte)tmpK;
                    tmpK = dictLength > 0 ? dictCharacter[dictLength - 1] : w;
                    dictStack[0] = (byte)tmpK;
                    tmpK = (uint)(i - 1);
                    while (--i > 0)
                        Put(output, ref outPos, dictStack[i]);
                    Put(output, ref outPos, dictStack[0]);
                    dictStack[0] = dictStack[tmpK];
                }
                else
                {
                    Put(output, ref outPos, (byte)k);
                    dictStack[0] = (byte)k;
                }

                if (addToDict && LZW_FIRST + dictLength < LZW_MAX_TABLE)
                {
                    dictCharacter[dictLength] = dictStack[0];
                    dictPrefix[dictLength] = w;
                    dictLength++;
                }
                w = k;
                addToDict = true;
            }

            if (LZW_FIRST + dictLength == (1u << nbit) && nbit < 12)
                nbit++;
        }
    }

    static void HuffmanDecode(byte[] input, byte[] output)
    {
        int inLen = input.Length;
        int treesize = (input[1] << 8) + input[0];
        int node = 0;
        int state = 0;
        int count = 0;
        byte last = 0;
        int outPos = 0;

        for (int i = 2 + treesize; i < inLen; i++)
        {
            for (int bit = 128; bit >= 1; bit >>= 1)
            {
                if ((input[i] & bit) != 0)
                    node++;

                int bintree = (input[3 + node * 2] << 8) + input[2 + node * 2];
                if (bintree <= 0x7FFF)
                {
                    node = bintree >> 1;
                }
                else
                {
                    node = bintree & 0x7FFF;
                    byte nodeL = (byte)(node & 0x00FF);
                    if (state == 0)
                    {
                        if (node < 0x100)
                        {
                            last = nodeL;
                            Put(output, ref outPos, last);
                        }
                        else if (nodeL == 0)
                        {
                            state = 1;
                        }
                        else if (nodeL == 1)
                        {
                            state = 2;
                        }
                        else
                        {
                            for (int j = 1; j <= nodeL; j++)
                                Put(output, ref outPos, last);
                        }
                    }
                    else if (state == 1)
                    {
                        for (int j = 1; j <= node; j++)
                            Put(output, ref outPos, last);
                        state = 0;
                    }
                    else if (state == 2)
                    {
                        count = 256 * nodeL;
                        state = 3;
                    }
                    else if (state == 3)
                    {
                        count += nodeL;
                        for (int j = 1; j <= count; j++)
                            Put(output, ref outPos, last);
                        state = 0;
                    }
                    node = 0;
                }
            }
        }
    }
}
