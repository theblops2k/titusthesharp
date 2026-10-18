using Raylib_cs;

namespace TitusTheFox;

// Audio: port de OpenTitus src/audio.c (reproductor de music.bin + efectos FX_START) sobre Opl2 (src/Opl2.cs,
// Salida PCM 16 bit mono 44100 Hz por Raylib (AudioStream).
public static class Audio
{
    // audio.c:40-49
    const int ADLIB_DATA_COUNT = 10;
    const int ADLIB_INSTRUMENT_COUNT = 19;
    const int ADLIB_SFX_COUNT = 14;
    const int FREQ_RATE = 44100;
    const int MUS_OFFSET = 0, INS_OFFSET = 352, SFX_OFFSET = 1950;
    const int SEG_REDUCTION = 1301; // audio.c:586 (Titus)
    const int MUSIC_BIN_SIZE = 18749; // audio.c:594

    // audio.c:56-59
    static readonly int[] opera = { 0, 0, 1, 2, 3, 4, 5, 8, 9, 0xA, 0xB, 0xC, 0xD, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15 };
    static readonly int[] voxp = { 1, 2, 3, 7, 8, 9, 13, 17, 15, 18, 14 };
    static readonly int[] gamme = { 343, 363, 385, 408, 432, 458, 485, 514, 544, 577, 611, 647, 0 };
    static readonly int[] songType = { 0, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };

    sealed class Instr // audio.c:73 ADLIB_INSTR
    {
        public byte[] op0 = new byte[5], op1 = new byte[5];
        public byte fb_alg;
        public byte vox = 0xFF; // 0xFE melodico, 0xFF desactivado
        public byte[] Op(int n) => n == 0 ? op0 : op1;
    }

    // Estado ADLIB_DATA (audio.c:79). pointer = -1 equivale a NULL
    const int N = ADLIB_DATA_COUNT + 1;
    static readonly byte[] duration = new byte[N], volume = new byte[N], tempo = new byte[N], triple_duration = new byte[N];
    static readonly byte[] lie = new byte[N], vox = new byte[N], delay_counter = new byte[N], freq = new byte[N], octave = new byte[N];
    static readonly byte[] loop_counter = new byte[N], lie_late = new byte[N];
    static readonly int[] instrument = new int[N], return_point = new int[N], pointer = new int[N];
    static readonly Instr[] instrument_data = new Instr[ADLIB_INSTRUMENT_COUNT + 6];
    static readonly Instr[] sfx = new Instr[ADLIB_SFX_COUNT];
    static byte perc_stat, skip_delay, skip_delay_counter;
    static int cutsong; // numero de canales de musica activos
    static byte[] data = Array.Empty<byte>();
    static int lastSong;

    // audio.c:119-123
    static bool FX_ON;
    static ushort FX_TIME;
    static byte AUDIOTIMING;

    static Opl2? chip;
    static readonly object sync = new();
    static bool enabled;
    static AudioStream stream;
    static Thread? pump;
    [System.Runtime.InteropServices.DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [System.Runtime.InteropServices.DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    static volatile bool running;
    const int SUB = 1024; // frames por sub-buffer de Raylib (= muestras por relleno)
    static double tickLeft; // muestras (44100 Hz) hasta el proximo tick de fillchip

    public static bool Enabled => enabled;
    public static string Status = "audio no iniciado";
    /// <summary>audio.c:709 cutsong == 0: la cancion termino</summary>
    public static bool SongDone { get { lock (sync) return !enabled || cutsong == 0; } }

    // audio.c:576 initaudio
    public static bool Init(string dir)
    {
        // Solo FILES\music.bin (generado por Extractor desde FOX.COM)
        string path = Path.Combine(dir, "music.bin");
        if (!File.Exists(path)) { Status = "music.bin no encontrado: sin audio"; return false; }
        data = File.ReadAllBytes(path);
        if (data.Length != MUSIC_BIN_SIZE) { Status = "music.bin invalido (tamano " + data.Length + ")"; return false; } // audio.c:594

        for (int i = 0; i < instrument_data.Length; i++) instrument_data[i] = new Instr();
        for (int i = 0; i < N; i++) { pointer[i] = -1; instrument[i] = -1; return_point[i] = -1; }
        chip = new Opl2();
        chip.Write(0x01, 0x20); // habilita seleccion de forma de onda (OPL_REG_WAVEFORM_ENABLE)
        InitSfx();
        AUDIOTIMING = 0;
        tickLeft = 0;
        cutsong = 0;
        lastSong = 0;

        Raylib.InitAudioDevice();
        if (!Raylib.IsAudioDeviceReady()) { Status = "dispositivo de audio no disponible"; return false; }
        Raylib.SetAudioStreamBufferSizeDefault(SUB);
        stream = Raylib.LoadAudioStream(FREQ_RATE, 16, 1);
        Raylib.PlayAudioStream(stream);
        Raylib.SetAudioStreamVolume(stream, Config.Volume() / 100f);
        enabled = true;
        running = true;
        timeBeginPeriod(1);
        pump = new Thread(Pump) { IsBackground = true, Name = "TitusAudio", Priority = ThreadPriority.AboveNormal };
        pump.Start();
        Status = "audio OK (" + path + ")";
        return true;
    }

    // Volumen en caliente (Config.Volume 0..100); musica y efectos salen por el mismo stream
    public static void ApplyVolume()
    {
        if (enabled) Raylib.SetAudioStreamVolume(stream, Config.Volume() / 100f);
    }

    // Pausa del nivel: con el stream pausado Raylib no procesa sub-buffers, el anillo del Pump queda lleno
    // y el sintetizador no avanza (se conserva la posicion)
    public static void PauseMusic()
    {
        if (enabled) Raylib.PauseAudioStream(stream);
    }

    public static void ResumeMusic()
    {
        if (enabled) Raylib.ResumeAudioStream(stream);
    }

    public static void Close()
    {
        if (!enabled) return;
        running = false;
        pump?.Join(500);
        timeEndPeriod(1);
        enabled = false;
        Raylib.UnloadAudioStream(stream);
        Raylib.CloseAudioDevice();
    }

    // Alimenta el AudioStream: cada sub-buffer procesado se rellena con 1024 muestras
    static void Pump()
    {
        // Anillo de RING sub-buffers generados por adelantado; el lock solo cubre Render (estado del chip)
        const int RING = 3;
        var ring = new short[RING][];
        for (int r = 0; r < RING; r++) ring[r] = new short[SUB];
        int head = 0, count = 0; // head = mas antiguo listo, count = listos
        while (running)
        {
            bool work = false;
            while (count < RING)
            {
                lock (sync) Render(ring[(head + count) % RING]);
                count++;
                work = true;
            }
            while (count > 0 && Raylib.IsAudioStreamProcessed(stream))
            {
                var b = ring[head];
                Raylib.UpdateAudioStream(stream, b.AsSpan(), b.Length);
                head = (head + 1) % RING;
                count--;
                work = true;
            }
            if (!work) Thread.Sleep(1);
        }
    }

    // TimerCallback (audio.c:523): tick cada 14 ms (13 ms cada 4to), luego fillchip
    static void Render(short[] buf)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            while (tickLeft <= 0)
            {
                int delay = 14;
                AUDIOTIMING++;
                if (AUDIOTIMING > 3) { AUDIOTIMING = 0; delay--; }
                FillChip();
                tickLeft += delay * FREQ_RATE / 1000.0;
            }
            tickLeft -= 1;
            buf[i] = chip!.NextSample44k();
        }
    }

    static void UpdateChip(int reg, int val) => chip!.Write(reg, val & 0xFF);

    static int U16(int j) => (j >= 0 && j + 1 < data.Length) ? data[j] | (data[j + 1] << 8) : 0xFFFF;
    static byte Raw(int idx) => (idx >= 0 && idx < data.Length) ? data[idx] : (byte)0;

    // audio.c:143 fillchip
    static void FillChip()
    {
        FxDriver();
        skip_delay_counter--;
        if (skip_delay_counter == 0) { skip_delay_counter = skip_delay; return; } // tempo

        for (int i = 0; i < ADLIB_DATA_COUNT; i++)
        {
            if (pointer[i] < 0) continue;
            if (delay_counter[i] > 1) { delay_counter[i]--; continue; }

            int b, oct, fr;
            do
            {
                b = Raw(pointer[i]++);
                oct = (b >> 4) & 7;
                fr = b & 0x0F;
                if ((b & 0x80) != 0)
                {
                    switch (oct)
                    {
                        case 0: duration[i] = (byte)fr; break;
                        case 1: // volumen
                            {
                                volume[i] = (byte)fr;
                                if (instrument[i] < 0) break;
                                byte tmpC = (byte)((instrument_data[instrument[i]].op0[2] & 0x3F) - 63);
                                uint t1 = (uint)(((256 - tmpC) << 4) & 0x0FF0) * (uint)(fr + 1);
                                t1 = 63 - ((t1 >> 8) & 0xFF);
                                int t2 = voxp[vox[i]];
                                if (t2 <= 13) t2 += 3;
                                t2 = opera[t2];
                                UpdateChip(0x40 + t2, (int)t1);
                                break;
                            }
                        case 2: tempo[i] = (byte)fr; break;
                        case 3: triple_duration[i] = (byte)fr; break;
                        case 4: lie[i] = (byte)fr; break;
                        case 5: vox[i] = (byte)fr; break;
                        case 6: // instrumento
                            {
                                if (fr == 1) // percusion
                                {
                                    instrument[i] = octave[i] + 15;
                                    vox[i] = instrument_data[instrument[i]].vox;
                                    perc_stat = (byte)(perc_stat | (0x10 >> (vox[i] - 6)));
                                }
                                else
                                {
                                    if (fr > 1) fr--;
                                    instrument[i] = fr;
                                    if (vox[i] - 6 >= 0)
                                    {
                                        int t1 = (0x10 << (vox[i] - 6)) & 0xFF;
                                        perc_stat = (byte)(perc_stat | t1);
                                        t1 = ~t1 & 0xFF;
                                        perc_stat = (byte)(perc_stat & t1);
                                        UpdateChip(0xBD, perc_stat);
                                    }
                                }
                                int t2 = voxp[vox[i]];
                                if (vox[i] <= 6) t2 += 3;
                                t2 = opera[t2];
                                var ins = instrument_data[instrument[i]];
                                InsMaker(ins.op0, t2);
                                if (vox[i] < 7)
                                {
                                    InsMaker(ins.op1, t2 - 3);
                                    UpdateChip(0xC0 + vox[i], ins.fb_alg);
                                }
                                break;
                            }
                        case 7: // funciones extra
                            switch (fr)
                            {
                                case 0: // call
                                    return_point[i] = pointer[i] + 2;
                                    pointer[i] = U16(pointer[i]) - SEG_REDUCTION;
                                    break;
                                case 1: loop_counter[i] = Raw(pointer[i]); pointer[i]++; break;
                                case 2: // loop
                                    if (loop_counter[i] > 1) { loop_counter[i]--; pointer[i] = U16(pointer[i]) - SEG_REDUCTION; }
                                    else pointer[i] += 2;
                                    break;
                                case 3: pointer[i] = return_point[i]; break;
                                case 4: pointer[i] = U16(pointer[i]) - SEG_REDUCTION; break;
                                case 15: pointer[i] = -1; cutsong--; break;
                            }
                            break;
                    }
                }
            } while ((b & 0x80) != 0 && pointer[i] >= 0);
            if (pointer[i] < 0) continue;

            octave[i] = (byte)oct;
            freq[i] = (byte)fr;

            // Tocar nota (audio.c:286). gamme solo tiene 13 entradas: indices 13-15 = silencio
            int g = freq[i] < gamme.Length ? gamme[freq[i]] : 0;
            if (g != 0 && instrument[i] >= 0)
            {
                var cur = instrument_data[instrument[i]];
                if (cur.vox == 0xFE) // frecuencia melodica
                {
                    UpdateChip(0xA0 + vox[i], g & 0xFF);
                    if (lie_late[i] != 1) UpdateChip(0xB0 + vox[i], 0);
                    int t1 = (octave[i] + 2) & 0x07;
                    int t2 = (g >> 8) & 0x03;
                    UpdateChip(0xB0 + vox[i], 0x20 + (t1 << 2) + t2);
                    lie_late[i] = lie[i];
                }
                else // percusion
                {
                    if (instrument[i] != octave[i] + 15)
                    {
                        instrument[i] = octave[i] + 15;
                        var pi = instrument_data[instrument[i]];
                        vox[i] = pi.vox;
                        perc_stat = (byte)(perc_stat | (0x10 >> (vox[i] - 6)));
                        int t2 = voxp[vox[i]];
                        if (vox[i] <= 6) t2 += 3;
                        t2 = opera[t2];
                        InsMaker(pi.op0, t2);
                        if (vox[i] < 7)
                        {
                            InsMaker(pi.op1, t2 - 3);
                            UpdateChip(0xC0 + vox[i], pi.fb_alg);
                        }
                        byte tmpC = (byte)((pi.op0[2] & 0x3F) - 63);
                        uint t1 = (uint)(((256 - tmpC) << 4) & 0x0FF0) * (uint)(volume[i] + 1);
                        t1 = 63 - ((t1 >> 8) & 0xFF);
                        t2 = voxp[vox[i]];
                        if (t2 <= 13) t2 += 3;
                        t2 = opera[t2];
                        UpdateChip(0x40 + t2, (int)t1);
                    }
                    int tc = 0x10 >> (vox[i] - 6);
                    UpdateChip(0xBD, perc_stat & ~tc);
                    if (vox[i] == 6)
                    {
                        UpdateChip(0xA6, 0x57);
                        UpdateChip(0xB6, 0);
                        UpdateChip(0xB6, 5);
                    }
                    UpdateChip(0xBD, perc_stat);
                }
            }
            else lie_late[i] = lie[i];

            if (duration[i] == 7) delay_counter[i] = (byte)(0x40 >> triple_duration[i]);
            else delay_counter[i] = (byte)(0x60 >> duration[i]);
        }
    }

    // audio.c:347 insmaker
    static void InsMaker(byte[] ins, int ch)
    {
        UpdateChip(0x60 + ch, ins[0]);
        UpdateChip(0x80 + ch, ins[1]);
        UpdateChip(0x40 + ch, ins[2]);
        UpdateChip(0x20 + ch, ins[3]);
        UpdateChip(0xE0 + ch, ins[4]);
    }

    // audio.c:390 SELECT_MUSIC
    public static void SelectMusic(int song)
    {
        if (!enabled || song < 0 || song >= songType.Length) return;
        lock (sync)
        {
            if (songType[song] == 0) lastSong = song;
            perc_stat = 0x20;
            int j = INS_OFFSET;
            uint tmp1 = 0xFFFF, tmp2;
            for (int i = 0; i < song; i++)
            {
                do { j += 2; tmp1 = (uint)U16(j); } while (tmp1 != 0xFFFF);
                j += 2;
            }
            AllVoxZero();
            tmp2 = (uint)U16(j);
            for (int i = 0; i < ADLIB_INSTRUMENT_COUNT + 1; i++) instrument_data[i].vox = 0xFF;
            for (int i = 0; i < ADLIB_INSTRUMENT_COUNT + 1 && j + 2 < data.Length; i++)
            {
                tmp1 = tmp2;
                tmp2 = (uint)U16(j + 2);
                j += 2;
                if (tmp2 == 0xFFFF) break;
                if (tmp1 == 0) continue;
                var ins = instrument_data[i];
                if (i > 14) ins.vox = Raw((int)(tmp1++) - SEG_REDUCTION); // percusion (15-18): byte extra
                else ins.vox = 0xFE;
                for (int k = 0; k < 5; k++) ins.op0[k] = Raw((int)(tmp1++) - SEG_REDUCTION);
                for (int k = 0; k < 5; k++) ins.op1[k] = Raw((int)(tmp1++) - SEG_REDUCTION);
                ins.fb_alg = Raw((int)tmp1 - SEG_REDUCTION);
            }
            skip_delay = (byte)tmp1;
            skip_delay_counter = (byte)tmp1;

            j = MUS_OFFSET;
            for (int i = 0; i < song; i++)
            {
                do { j += 2; tmp1 = (uint)U16(j); } while (tmp1 != 0xFFFF);
                j += 2;
            }
            cutsong = -1;
            for (int i = 0; i < ADLIB_DATA_COUNT + 1 && j < data.Length; i++)
            {
                tmp1 = (uint)U16(j);
                cutsong++;
                if (tmp1 == 0xFFFF) break;
                duration[i] = 0; volume[i] = 0; tempo[i] = 0; triple_duration[i] = 0; lie[i] = 0;
                vox[i] = (byte)i;
                instrument[i] = -1;
                delay_counter[i] = 0; freq[i] = 0; octave[i] = 0;
                return_point[i] = -1; loop_counter[i] = 0;
                pointer[i] = (int)tmp1 - SEG_REDUCTION;
                lie_late[i] = 0;
                j += 2;
            }
            if (cutsong < 0) cutsong = 0;
        }
    }

    // audio.c:492 all_vox_zero
    static void AllVoxZero()
    {
        for (int i = 0xB0; i < 0xB9; i++) UpdateChip(i, 0);
        for (int i = 0xA0; i < 0xB9; i++) UpdateChip(i, 0);
        UpdateChip(0x08, 0x00);
        UpdateChip(0xBD, 0x00);
        foreach (int b in new[] { 0x40, 0x48, 0x50 })
            for (int k = 0; k < 6; k++) UpdateChip(b + k, 0x3F); // 40-45, 48-4D, 50-55
    }

    // audio.c:659 initsfx: 14 instrumentos de efecto a partir de SFX_OFFSET (11 bytes cada uno)
    static void InitSfx()
    {
        FX_ON = false; FX_TIME = 0;
        int t = SFX_OFFSET;
        for (int i = 0; i < ADLIB_SFX_COUNT; i++)
        {
            sfx[i] = new Instr();
            for (int k = 0; k < 5; k++) sfx[i].op0[k] = Raw(t++);
            for (int k = 0; k < 5; k++) sfx[i].op1[k] = Raw(t++);
            sfx[i].fb_alg = Raw(t++);
        }
    }

    // audio.c:715 FX_START
    public static void FxStart(int n)
    {
        if (!enabled || n < 0 || n >= ADLIB_SFX_COUNT) return;
        lock (sync)
        {
            FX_TIME = 15;
            FX_ON = true;
            InsMaker(sfx[n].op0, 0x13); // canal 6 operador 1
            InsMaker(sfx[n].op1, 0x10); // canal 6 operador 2
            UpdateChip(0xC6, sfx[n].fb_alg);
        }
    }

    // audio.c:727 FX_DRIVER
    static void FxDriver()
    {
        if (!FX_ON) return;
        UpdateChip(0xBD, 0xEF & perc_stat);
        UpdateChip(0xA6, 0x57);
        UpdateChip(0xB6, 1);
        UpdateChip(0xB6, 5);
        UpdateChip(0xBD, 0x10 | perc_stat);
        FX_TIME--;
        if (FX_TIME == 0) FxStop();
    }

    // audio.c:742 FX_STOP
    static void FxStop()
    {
        UpdateChip(0xB6, 32);
        InsMaker(new byte[] { 0xF5, 0x7F, 0x00, 0x11, 0x00 }, 0x13);
        InsMaker(new byte[] { 0xF8, 0xFF, 0x04, 0x30, 0x00 }, 0x10);
        UpdateChip(0xC6, 0x08);
        FX_ON = false;
    }

    // audio.c:754 RETURN_MUSIC: reinicia la cancion del nivel cuando termina
    public static void ReturnMusic()
    {
        if (!enabled) return;
        bool done;
        lock (sync) done = cutsong == 0;
        if (done) SelectMusic(lastSong);
    }
}
