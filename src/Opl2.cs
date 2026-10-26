namespace TitusTheFox;

// Opl2: port a C# de opl/dbopl.c (DOSBox DBOPL, usado por OpenTitus via chocolate-doom libopl) con
// DBOPL_WAVE = WAVE_TABLEMUL (include/dbopl.h:29), solo modo OPL2 (9 canales, opl3Active = 0).
// Mismas tablas y logica; las lineas citadas son de dbopl.c. Sin la parte opl.c (no esta en las fuentes).
// Interfaz: Write(reg, val) y NextSample44k() (PCM 16 bit mono a 44100 Hz, sin remuestreo: Setup(44100)).
sealed class Opl2
{
    // ---- constantes (dbopl.c:64-110) ----
    const double OPLRATE = 14318180.0 / 288.0;
    const int TREMOLO_TABLE = 52;
    const int WAVE_BITS = 10;
    const int WAVE_SH = 32 - WAVE_BITS;
    const uint WAVE_MASK = (1u << WAVE_SH) - 1;
    const int LFO_SH = WAVE_SH - 10;
    const uint LFO_MAX = 256u << LFO_SH;
    const int ENV_BITS = 9;
    const int ENV_EXTRA = ENV_BITS - 9;
    const int ENV_MAX = 511 << ENV_EXTRA;
    const int ENV_LIMIT = (12 * 256) >> (3 - ENV_EXTRA);
    const int RATE_SH = 24;
    const uint RATE_MASK = (1u << RATE_SH) - 1;
    const int MUL_SH = 16;
    const int SHIFT_KSLBASE = 16, SHIFT_KEYCODE = 24;
    const int MASK_KSR = 0x10, MASK_SUSTAIN = 0x20, MASK_VIBRATO = 0x40, MASK_TREMOLO = 0x80;
    const int OFF = 0, RELEASE = 1, SUSTAIN = 2, DECAY = 3, ATTACK = 4; // OperatorState
    const int M2AM = 0, M2FM = 1, M2PERC = 2; // SynthMode (solo OPL2)

    static bool Silent(long v) => v >= ENV_LIMIT; // ENV_SILENT

    // ---- tablas (dbopl.c:143-235) ----
    static readonly byte[] KslCreateTable = { 64, 32, 24, 19, 16, 12, 11, 10, 8, 6, 5, 4, 3, 2, 1, 0 };
    static readonly byte[] FreqCreateTable = { 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 20, 24, 24, 30, 30 }; // M(x)=(x*2)
    static readonly byte[] AttackSamplesTable = { 69, 55, 46, 40, 35, 29, 23, 20, 19, 15, 11, 10, 9 };
    static readonly byte[] EnvelopeIncreaseTable = { 4, 5, 6, 7, 8, 10, 12, 14, 16, 20, 24, 28, 32 };
    static readonly short[] WaveTable = new short[8 * 512];
    static readonly ushort[] WaveBaseTable = { 0x000, 0x200, 0x200, 0x800, 0xa00, 0xc00, 0x100, 0x400 };
    static readonly ushort[] WaveMaskTable = { 1023, 1023, 511, 511, 1023, 1023, 512, 1023 };
    static readonly ushort[] WaveStartTable = { 512, 0, 0, 0, 0, 512, 512, 256 };
    static readonly ushort[] MulTable = new ushort[384];
    static readonly byte[] KslTable = new byte[8 * 16];
    static readonly byte[] TremoloTable = new byte[TREMOLO_TABLE];
    static readonly sbyte[] VibratoTable = { 1 - 0x00, 0 - 0x00, 1 - 0x00, 30 - 0x00, 1 - 0x80, 0 - 0x80, 1 - 0x80, 30 - 0x80 };
    static readonly byte[] KslShiftTable = { 31, 1, 2, 0 };
    // registro -> canal / operador (equivalente a ChanOffsetTable / OpOffsetTable, dbopl.c:1519-1550), -1 = ninguno
    static readonly int[] ChanIdx = new int[16];
    static readonly int[] OpChan = new int[32];
    static readonly int[] OpNum = new int[32];

    // dbopl.c:237 EnvelopeSelect
    static void EnvelopeSelect(int val, out int index, out int shift)
    {
        if (val < 13 * 4) { shift = 12 - (val >> 2); index = val & 3; }
        else if (val < 15 * 4) { shift = 0; index = val - 12 * 4; }
        else { shift = 0; index = 12; }
    }

    // dbopl.c:1455 DBOPL_InitTables (rama WAVE_TABLEMUL)
    static Opl2()
    {
        for (int i = 0; i < 384; i++)
        {
            int s = i * 8;
            double val = 0.5 + Math.Pow(2.0, -1.0 + (255 - s) * (1.0 / 256)) * (1 << MUL_SH);
            MulTable[i] = (ushort)val;
        }
        for (int i = 0; i < 512; i++)
        {
            WaveTable[0x0200 + i] = (short)(Math.Sin((i + 0.5) * (Math.PI / 512.0)) * 4084);
            WaveTable[0x0000 + i] = (short)-WaveTable[0x200 + i];
        }
        for (int i = 0; i < 256; i++)
        {
            WaveTable[0x700 + i] = (short)(0.5 + Math.Pow(2.0, -1.0 + (255 - i * 8) * (1.0 / 256)) * 4085);
            WaveTable[0x6ff - i] = (short)-WaveTable[0x700 + i];
        }
        for (int i = 0; i < 256; i++)
        {
            WaveTable[0x400 + i] = WaveTable[0];
            WaveTable[0x500 + i] = WaveTable[0];
            WaveTable[0x900 + i] = WaveTable[0];
            WaveTable[0xc00 + i] = WaveTable[0];
            WaveTable[0xd00 + i] = WaveTable[0];
            WaveTable[0x800 + i] = WaveTable[0x200 + i];
            WaveTable[0xa00 + i] = WaveTable[0x200 + i * 2];
            WaveTable[0xb00 + i] = WaveTable[0x000 + i * 2];
            WaveTable[0xe00 + i] = WaveTable[0x200 + i * 2];
            WaveTable[0xf00 + i] = WaveTable[0x200 + i * 2];
        }
        for (int oct = 0; oct < 8; oct++)
        {
            int b = oct * 8;
            for (int i = 0; i < 16; i++)
            {
                int val = b - KslCreateTable[i];
                if (val < 0) val = 0;
                KslTable[oct * 16 + i] = (byte)(val * 4);
            }
        }
        for (int i = 0; i < TREMOLO_TABLE / 2; i++)
        {
            byte val = (byte)(i << ENV_EXTRA);
            TremoloTable[i] = val;
            TremoloTable[TREMOLO_TABLE - 1 - i] = val;
        }
        for (int i = 0; i < 16; i++)
        {
            int index = i;
            if (index >= 9) { ChanIdx[i] = -1; continue; }
            if (index < 6) index = (index % 3) * 2 + (index / 3);
            ChanIdx[i] = index;
        }
        for (int i = 0; i < 32; i++)
        {
            if (i % 8 >= 6 || ((i / 8) % 4 == 3)) { OpChan[i] = -1; continue; }
            int chNum = (i / 8) * 3 + (i % 8) % 3;
            OpChan[i] = ChanIdx[chNum];
            OpNum[i] = (i % 8) / 3;
        }
    }

    // ---- Operator (dbopl.h struct _Operator) ----
    sealed class Operator
    {
        public int waveBase;          // offset en WaveTable
        public uint waveMask, waveStart;
        public uint waveIndex, waveAdd, waveCurrent, chanData, freqMul, vibrato;
        public int sustainLevel, totalLevel;
        public uint currentLevel;
        public int volume;
        public uint attackAdd, decayAdd, releaseAdd, rateIndex;
        public byte rateZero, keyOn;
        public byte reg20, reg40, reg60, reg80, regE0;
        public int state;
        public int tremoloMask;       // 0xff si tremolo
        public byte vibStrength, ksr;

        public Operator()           // dbopl.c:655
        {
            SetState(OFF);
            rateZero = 1 << OFF;
            sustainLevel = ENV_MAX; currentLevel = ENV_MAX; totalLevel = ENV_MAX; volume = ENV_MAX;
            waveBase = WaveBaseTable[0]; waveMask = WaveMaskTable[0]; waveStart = (uint)WaveStartTable[0] << WAVE_SH;
        }

        public void SetState(int s) => state = s;

        void UpdateAttack(Opl2 chip)
        {
            int rate = reg60 >> 4;
            if (rate != 0)
            {
                int val = (rate << 2) + ksr;
                attackAdd = chip.attackRates[val];
                rateZero &= unchecked((byte)~(1 << ATTACK));
            }
            else { attackAdd = 0; rateZero |= 1 << ATTACK; }
        }

        void UpdateDecay(Opl2 chip)
        {
            int rate = reg60 & 0xf;
            if (rate != 0)
            {
                int val = (rate << 2) + ksr;
                decayAdd = chip.linearRates[val];
                rateZero &= unchecked((byte)~(1 << DECAY));
            }
            else { decayAdd = 0; rateZero |= 1 << DECAY; }
        }

        void UpdateRelease(Opl2 chip)
        {
            int rate = reg80 & 0xf;
            if (rate != 0)
            {
                int val = (rate << 2) + ksr;
                releaseAdd = chip.linearRates[val];
                rateZero &= unchecked((byte)~(1 << RELEASE));
                if ((reg20 & MASK_SUSTAIN) == 0) rateZero &= unchecked((byte)~(1 << SUSTAIN));
            }
            else
            {
                rateZero |= 1 << RELEASE;
                releaseAdd = 0;
                if ((reg20 & MASK_SUSTAIN) == 0) rateZero |= 1 << SUSTAIN;
            }
        }

        public void UpdateAttenuation()
        {
            int kslBase = (int)((chanData >> SHIFT_KSLBASE) & 0xff);
            int tl = reg40 & 0x3f;
            int kslShift = KslShiftTable[reg40 >> 6];
            totalLevel = tl << (ENV_BITS - 7);
            totalLevel += (kslBase << ENV_EXTRA) >> kslShift;
        }

        public void UpdateFrequency()
        {
            uint freq = chanData & ((1u << 10) - 1);
            int block = (int)((chanData >> 10) & 0xff);
            unchecked { waveAdd = (freq << block) * freqMul; }
            if ((reg20 & MASK_VIBRATO) != 0)
            {
                vibStrength = (byte)(freq >> 7);
                unchecked { vibrato = ((uint)vibStrength << block) * freqMul; }
            }
            else { vibStrength = 0; vibrato = 0; }
        }

        public void UpdateRates(Opl2 chip)
        {
            byte newKsr = (byte)((chanData >> SHIFT_KEYCODE) & 0xff);
            if ((reg20 & MASK_KSR) == 0) newKsr >>= 2;
            if (ksr == newKsr) return;
            ksr = newKsr;
            UpdateAttack(chip);
            UpdateDecay(chip);
            UpdateRelease(chip);
        }

        int RateForward(uint add)
        {
            unchecked { rateIndex += add; }
            int ret = (int)(rateIndex >> RATE_SH);
            rateIndex &= RATE_MASK;
            return ret;
        }

        // dbopl.c:422 TemplateVolume
        int Volume()
        {
            int vol = volume;
            int change;
            switch (state)
            {
                case OFF:
                    return ENV_MAX;
                case ATTACK:
                    change = RateForward(attackAdd);
                    if (change == 0) return vol;
                    vol += ((~vol) * change) >> 3;
                    if (vol < 0)
                    {
                        volume = 0; rateIndex = 0; SetState(DECAY);
                        return 0;
                    }
                    break;
                case DECAY:
                    vol += RateForward(decayAdd);
                    if (vol >= sustainLevel)
                    {
                        if (vol >= ENV_MAX) { volume = ENV_MAX; SetState(OFF); return ENV_MAX; }
                        rateIndex = 0;
                        SetState(SUSTAIN);
                    }
                    break;
                case SUSTAIN:
                    if ((reg20 & MASK_SUSTAIN) != 0) return vol;
                    goto case RELEASE;
                case RELEASE:
                    vol += RateForward(releaseAdd);
                    if (vol >= ENV_MAX) { volume = ENV_MAX; SetState(OFF); return ENV_MAX; }
                    break;
            }
            volume = vol;
            return vol;
        }

        public uint ForwardVolume() => unchecked(currentLevel + (uint)Volume());

        public uint ForwardWave()
        {
            unchecked { waveIndex += waveCurrent; }
            return waveIndex >> WAVE_SH;
        }

        public void Write20(Opl2 chip, byte val)
        {
            byte change = (byte)(reg20 ^ val);
            if (change == 0) return;
            reg20 = val;
            tremoloMask = ((sbyte)val) >> 7 & 0xff;
            if ((change & MASK_KSR) != 0) UpdateRates(chip);
            if ((reg20 & MASK_SUSTAIN) != 0 || releaseAdd == 0) rateZero |= 1 << SUSTAIN;
            else rateZero &= unchecked((byte)~(1 << SUSTAIN));
            if ((change & (0xf | MASK_VIBRATO)) != 0)
            {
                freqMul = chip.freqMul[val & 0xf];
                UpdateFrequency();
            }
        }

        public void Write40(Opl2 chip, byte val)
        {
            if (reg40 == val) return;
            reg40 = val;
            UpdateAttenuation();
        }

        public void Write60(Opl2 chip, byte val)
        {
            byte change = (byte)(reg60 ^ val);
            reg60 = val;
            if ((change & 0x0f) != 0) UpdateDecay(chip);
            if ((change & 0xf0) != 0) UpdateAttack(chip);
        }

        public void Write80(Opl2 chip, byte val)
        {
            byte change = (byte)(reg80 ^ val);
            if (change == 0) return;
            reg80 = val;
            int sustain = val >> 4;
            sustain |= (sustain + 1) & 0x10;
            sustainLevel = sustain << (ENV_BITS - 5);
            if ((change & 0x0f) != 0) UpdateRelease(chip);
        }

        public void WriteE0(Opl2 chip, byte val)
        {
            if (regE0 == val) return;
            int waveForm = val & ((0x3 & chip.waveFormMask) | (0x7 & chip.opl3Active));
            regE0 = val;
            waveBase = WaveBaseTable[waveForm];
            waveStart = (uint)WaveStartTable[waveForm] << WAVE_SH;
            waveMask = WaveMaskTable[waveForm];
        }

        public bool IsSilent()
        {
            if (!(totalLevel + volume >= ENV_LIMIT)) return false;
            if ((rateZero & (1 << state)) == 0) return false;
            return true;
        }

        public void Prepare(Opl2 chip)
        {
            currentLevel = (uint)(totalLevel + (chip.tremoloValue & tremoloMask));
            waveCurrent = waveAdd;
            if ((vibStrength >> chip.vibratoShift) != 0)
            {
                int add = (int)(vibrato >> chip.vibratoShift);
                int neg = chip.vibratoSign;
                add = (add ^ neg) - neg;
                unchecked { waveCurrent += (uint)add; }
            }
        }

        public void KeyOn(byte mask)
        {
            if (keyOn == 0)
            {
                waveIndex = waveStart;
                rateIndex = 0;
                SetState(ATTACK);
            }
            keyOn |= mask;
        }

        public void KeyOff(byte mask)
        {
            keyOn &= (byte)~mask;
            if (keyOn == 0 && state != OFF) SetState(RELEASE);
        }

        public int GetWave(long index, long vol)
            => (WaveTable[waveBase + (int)(index & waveMask)] * MulTable[(int)(vol >> ENV_EXTRA)]) >> MUL_SH;

        public int GetSample(long modulation)
        {
            long vol = ForwardVolume();
            if (Silent(vol))
            {
                unchecked { waveIndex += waveCurrent; }
                return 0;
            }
            long index = ForwardWave();
            index += modulation;
            return GetWave(index, vol);
        }
    }

    // ---- Channel (dbopl.h struct _Channel) ----
    sealed class Channel
    {
        public readonly Operator[] op = { new Operator(), new Operator() };
        public int mode = M2FM;
        public uint chanData;
        public int old0, old1;
        public int feedback = 31;
        public byte regB0, regC0;
        public byte fourMask;

        public void SetChanData(Opl2 chip, uint data)
        {
            uint change = chanData ^ data;
            chanData = data;
            op[0].chanData = data;
            op[1].chanData = data;
            op[0].UpdateFrequency();
            op[1].UpdateFrequency();
            if ((change & (0xffu << SHIFT_KSLBASE)) != 0) { op[0].UpdateAttenuation(); op[1].UpdateAttenuation(); }
            if ((change & (0xffu << SHIFT_KEYCODE)) != 0) { op[0].UpdateRates(chip); op[1].UpdateRates(chip); }
        }

        void UpdateFrequency(Opl2 chip)
        {
            uint data = chanData & 0xffff;
            uint kslBase = KslTable[data >> 6];
            uint keyCode = (data & 0x1c00) >> 9;
            if ((chip.reg08 & 0x40) != 0) keyCode |= (data & 0x100) >> 8;
            else keyCode |= (data & 0x200) >> 9;
            data |= (keyCode << SHIFT_KEYCODE) | (kslBase << SHIFT_KSLBASE);
            SetChanData(chip, data);
        }

        public void WriteA0(Opl2 chip, byte val)
        {
            uint change = (chanData ^ val) & 0xff;
            if (change != 0) { chanData ^= change; UpdateFrequency(chip); }
        }

        public void WriteB0(Opl2 chip, byte val)
        {
            uint change = (chanData ^ ((uint)val << 8)) & 0x1f00;
            if (change != 0) { chanData ^= change; UpdateFrequency(chip); }
            if (((val ^ regB0) & 0x20) == 0) return;
            regB0 = val;
            if ((val & 0x20) != 0) { op[0].KeyOn(0x1); op[1].KeyOn(0x1); }
            else { op[0].KeyOff(0x1); op[1].KeyOff(0x1); }
        }

        public void WriteC0(Opl2 chip, byte val)
        {
            byte change = (byte)(val ^ regC0);
            if (change == 0) return;
            regC0 = val;
            feedback = (val >> 1) & 7;
            if (feedback != 0) feedback = 9 - feedback; else feedback = 31;
            // opl2 activo
            if ((fourMask & 0x40) != 0 && (chip.regBD & 0x20) != 0) { }
            else if ((val & 1) != 0) mode = M2AM;
            else mode = M2FM;
        }

        public void ResetC0(Opl2 chip)
        {
            byte val = regC0;
            regC0 ^= 0xff;
            WriteC0(chip, val);
        }
    }

    // ---- Chip (dbopl.h struct _Chip) ----
    uint lfoCounter, lfoAdd, noiseCounter, noiseAdd, noiseValue;
    readonly uint[] freqMul = new uint[16];
    readonly uint[] linearRates = new uint[76];
    readonly uint[] attackRates = new uint[76];
    readonly Channel[] chan = new Channel[18];
    byte reg08, regBD, vibratoIndex, tremoloIndex, vibratoShift, tremoloValue, vibratoStrength, tremoloStrength, waveFormMask;
    int vibratoSign;
    readonly byte opl3Active = 0; // solo OPL2

    public Opl2() : this(44100) { }

    public Opl2(uint rate)
    {
        for (int i = 0; i < 18; i++) chan[i] = new Channel();
        Setup(rate);
    }

    // dbopl.c:1311 Chip__Setup (el borrado en modo OPL3 inicial se omite; se hace el borrado OPL2)
    void Setup(uint rate)
    {
        double scale = OPLRATE / rate;
        noiseAdd = (uint)(0.5 + scale * (1 << LFO_SH));
        noiseCounter = 0;
        noiseValue = 1;
        lfoAdd = (uint)(0.5 + scale * (1 << LFO_SH));
        lfoCounter = 0;
        vibratoIndex = 0;
        tremoloIndex = 0;

        uint freqScale = (uint)(0.5 + scale * (1 << (WAVE_SH - 1 - 10)));
        for (int i = 0; i < 16; i++) freqMul[i] = freqScale * FreqCreateTable[i];

        unchecked
        {
            for (int i = 0; i < 76; i++)
            {
                EnvelopeSelect(i, out int index, out int shift);
                linearRates[i] = (uint)(scale * (EnvelopeIncreaseTable[index] << (RATE_SH + ENV_EXTRA - shift - 3)));
            }
            for (int i = 0; i < 62; i++)
            {
                EnvelopeSelect(i, out int index, out int shift);
                int original = (int)(uint)((AttackSamplesTable[index] << shift) / scale);
                int guessAdd = (int)(uint)(scale * (EnvelopeIncreaseTable[index] << (RATE_SH - shift - 3)));
                int bestAdd = guessAdd;
                uint bestDiff = 1u << 30;
                for (uint passes = 0; passes < 16; passes++)
                {
                    int volume = ENV_MAX;
                    int samples = 0;
                    uint count = 0;
                    while (volume > 0 && samples < original * 2)
                    {
                        count += (uint)guessAdd;
                        int change = (int)(count >> RATE_SH);
                        count &= RATE_MASK;
                        if (change != 0) volume += (~volume * change) >> 3;
                        samples++;
                    }
                    int diff = original - samples;
                    uint lDiff = (uint)Math.Abs((long)diff);
                    if (lDiff < bestDiff)
                    {
                        bestDiff = lDiff;
                        bestAdd = guessAdd;
                        if (bestDiff == 0) break;
                    }
                    if (diff < 0)
                    {
                        int mul = ((original - diff) << 12) / original;
                        guessAdd = (guessAdd * mul) >> 12;
                        guessAdd++;
                    }
                    else if (diff > 0)
                    {
                        int mul = ((original - diff) << 12) / original;
                        guessAdd = (guessAdd * mul) >> 12;
                        guessAdd--;
                    }
                }
                attackRates[i] = (uint)bestAdd;
            }
            for (int i = 62; i < 76; i++) attackRates[i] = 8u << RATE_SH;
        }

        // canales de percusion (dbopl.c:1425)
        chan[6].fourMask = 0x40; chan[7].fourMask = 0x40; chan[8].fourMask = 0x40;

        // borrado en modo OPL2 (dbopl.c:1438)
        for (int i = 0; i < 255; i++) { WriteReg(i, 0xff); WriteReg(i, 0x0); }
    }

    // dbopl.c:1083 Chip__ForwardNoise
    uint ForwardNoise()
    {
        unchecked { noiseCounter += noiseAdd; }
        uint count = noiseCounter >> LFO_SH;
        noiseCounter &= WAVE_MASK;
        for (; count > 0; --count)
        {
            noiseValue ^= 0x800302u & (0u - (noiseValue & 1));
            noiseValue >>= 1;
        }
        return noiseValue;
    }

    // dbopl.c:1096 Chip__ForwardLFO
    uint ForwardLFO(uint samples)
    {
        vibratoSign = VibratoTable[vibratoIndex >> 2] >> 7;
        vibratoShift = (byte)((VibratoTable[vibratoIndex >> 2] & 7) + vibratoStrength);
        tremoloValue = (byte)(TremoloTable[tremoloIndex] >> tremoloStrength);
        uint todo = LFO_MAX - lfoCounter;
        uint count = (todo + lfoAdd - 1) / lfoAdd;
        if (count > samples)
        {
            count = samples;
            lfoCounter += count * lfoAdd;
        }
        else
        {
            lfoCounter += count * lfoAdd;
            lfoCounter &= LFO_MAX - 1;
            vibratoIndex = (byte)((vibratoIndex + 1) & 31);
            if (tremoloIndex + 1 < TREMOLO_TABLE) ++tremoloIndex; else tremoloIndex = 0;
        }
        return count;
    }

    // dbopl.c:1126 Chip__WriteBD
    void WriteBD(byte val)
    {
        byte change = (byte)(regBD ^ val);
        if (change == 0) return;
        regBD = val;
        vibratoStrength = (byte)((val & 0x40) != 0 ? 0x00 : 0x01);
        tremoloStrength = (byte)((val & 0x80) != 0 ? 0x00 : 0x02);
        if ((val & 0x20) != 0)
        {
            if ((change & 0x20) != 0) chan[6].mode = M2PERC;
            if ((val & 0x10) != 0) { chan[6].op[0].KeyOn(0x2); chan[6].op[1].KeyOn(0x2); }
            else { chan[6].op[0].KeyOff(0x2); chan[6].op[1].KeyOff(0x2); }
            if ((val & 0x1) != 0) chan[7].op[0].KeyOn(0x2); else chan[7].op[0].KeyOff(0x2);
            if ((val & 0x8) != 0) chan[7].op[1].KeyOn(0x2); else chan[7].op[1].KeyOff(0x2);
            if ((val & 0x4) != 0) chan[8].op[0].KeyOn(0x2); else chan[8].op[0].KeyOff(0x2);
            if ((val & 0x2) != 0) chan[8].op[1].KeyOn(0x2); else chan[8].op[1].KeyOff(0x2);
        }
        else if ((change & 0x20) != 0)
        {
            chan[6].ResetC0(this);
            chan[6].op[0].KeyOff(0x2); chan[6].op[1].KeyOff(0x2);
            chan[7].op[0].KeyOff(0x2); chan[7].op[1].KeyOff(0x2);
            chan[8].op[0].KeyOff(0x2); chan[8].op[1].KeyOff(0x2);
        }
    }

    // dbopl.c:1204 Chip__WriteReg (rango OPL2: 0x00-0xFF)
    void WriteReg(int reg, byte val)
    {
        int index;
        switch ((reg & 0xf0) >> 4)
        {
            case 0x0:
                if (reg == 0x01) waveFormMask = (byte)((val & 0x20) != 0 ? 0x7 : 0x0);
                else if (reg == 0x08) reg08 = val;
                break;
            case 0x2: case 0x3:
                index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (index < 32 && OpChan[index] >= 0) chan[OpChan[index]].op[OpNum[index]].Write20(this, val);
                break;
            case 0x4: case 0x5:
                index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (index < 32 && OpChan[index] >= 0) chan[OpChan[index]].op[OpNum[index]].Write40(this, val);
                break;
            case 0x6: case 0x7:
                index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (index < 32 && OpChan[index] >= 0) chan[OpChan[index]].op[OpNum[index]].Write60(this, val);
                break;
            case 0x8: case 0x9:
                index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (index < 32 && OpChan[index] >= 0) chan[OpChan[index]].op[OpNum[index]].Write80(this, val);
                break;
            case 0xa:
                index = ((reg >> 4) & 0x10) | (reg & 0xf);
                if (ChanIdx[index] >= 0) chan[ChanIdx[index]].WriteA0(this, val);
                break;
            case 0xb:
                if (reg == 0xbd) WriteBD(val);
                else
                {
                    index = ((reg >> 4) & 0x10) | (reg & 0xf);
                    if (ChanIdx[index] >= 0) chan[ChanIdx[index]].WriteB0(this, val);
                }
                break;
            case 0xc:
                index = ((reg >> 4) & 0x10) | (reg & 0xf);
                if (ChanIdx[index] >= 0) chan[ChanIdx[index]].WriteC0(this, val);
                break;
            case 0xe: case 0xf:
                index = ((reg >> 3) & 0x20) | (reg & 0x1f);
                if (index < 32 && OpChan[index] >= 0) chan[OpChan[index]].op[OpNum[index]].WriteE0(this, val);
                break;
        }
    }

    // Interfaz usada por Audio.cs
    public void Write(int reg, int v)
    {
        if (reg < 0 || reg > 0xff) return;
        WriteReg(reg, (byte)(v & 0xFF));
    }

    // dbopl.c:853 Channel__GeneratePercussion (mono OPL2)
    int GeneratePercussion(Channel self)
    {
        Operator[] o = { chan[6].op[0], chan[6].op[1], chan[7].op[0], chan[7].op[1], chan[8].op[0], chan[8].op[1] };
        int mod = (int)((uint)(self.old0 + self.old1) >> self.feedback);
        self.old0 = self.old1;
        self.old1 = o[0].GetSample(mod);
        mod = (self.regC0 & 1) != 0 ? 0 : self.old0;
        int sample = o[1].GetSample(mod);

        uint noiseBit = ForwardNoise() & 1;
        uint c2 = o[2].ForwardWave();
        uint c5 = o[5].ForwardWave();
        uint phaseBit = (((c2 & 0x88) ^ ((c2 << 5) & 0x80)) | ((c5 ^ (c5 << 2)) & 0x20)) != 0 ? 0x02u : 0x00u;

        uint hhVol = o[2].ForwardVolume();
        if (!Silent(hhVol))
        {
            uint hhIndex = (phaseBit << 8) | (0x34u << (int)(phaseBit ^ (noiseBit << 1)));
            sample += o[2].GetWave(hhIndex, hhVol);
        }
        uint sdVol = o[3].ForwardVolume();
        if (!Silent(sdVol))
        {
            uint sdIndex = (0x100u + (c2 & 0x100)) ^ (noiseBit << 8);
            sample += o[3].GetWave(sdIndex, sdVol);
        }
        sample += o[4].GetSample(0);
        uint tcVol = o[5].ForwardVolume();
        if (!Silent(tcVol))
        {
            uint tcIndex = (1 + phaseBit) << 8;
            sample += o[5].GetWave(tcIndex, tcVol);
        }
        return sample << 1;
    }

    // dbopl.c:1245 Chip__GenerateBlock2 con samples = 1 (Prepare y ForwardLFO por muestra)
    int GenerateOne()
    {
        ForwardLFO(1);
        int output = 0;
        int ch = 0;
        while (ch < 9)
        {
            Channel c = chan[ch];
            int m = c.mode;
            if (m == M2AM)
            {
                if (c.op[0].IsSilent() && c.op[1].IsSilent()) { c.old0 = c.old1 = 0; ch++; continue; }
            }
            else if (m == M2FM)
            {
                if (c.op[1].IsSilent()) { c.old0 = c.old1 = 0; ch++; continue; }
            }
            c.op[0].Prepare(this);
            c.op[1].Prepare(this);
            if (m == M2PERC)
            {
                chan[7].op[0].Prepare(this); chan[7].op[1].Prepare(this);
                chan[8].op[0].Prepare(this); chan[8].op[1].Prepare(this);
                output += GeneratePercussion(c);
                ch += 3;
                continue;
            }
            int mod = (int)((uint)(c.old0 + c.old1) >> c.feedback);
            c.old0 = c.old1;
            c.old1 = c.op[0].GetSample(mod);
            int out0 = c.old0;
            int sample = m == M2AM ? out0 + c.op[1].GetSample(0) : c.op[1].GetSample(out0);
            output += sample;
            ch++;
        }
        return output;
    }

    // PCM 16 bit mono; sin ganancia extra (la suma de canales se recorta a 16 bit)
    public short NextSample44k() => (short)Math.Clamp(GenerateOne(), short.MinValue, short.MaxValue);
}
