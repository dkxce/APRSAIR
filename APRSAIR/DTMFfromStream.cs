using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Un4seen.Bass;
using System.Media;


namespace DTMFCoder
{
    public class DetectDTMFfromStream : IDisposable
    {
        public delegate void OnToneData(char tone);
        public delegate void OnTonesData(string tones);
        public delegate void OnTonesGPS(string callsign, double lat, double lon, double alt);

        /// <summary>
        ///     On Getting DTMF Tone From Stream
        /// </summary>
        public OnToneData OnGetTone;

        /// <summary>
        ///     On Getting DTMF Packet From String
        /// </summary>
        public OnTonesData OnGetData;

        /// <summary>
        ///     On Getting DTMF GPS Packet
        /// </summary>
        public OnTonesGPS OnGetGPS;

        /// <summary>
        ///     Out Receiving to Console
        /// </summary>
        public bool OutLogToConsole = true; // bool

        /// <summary>
        ///     Max DTMF Sinlge Tone Length in ms
        /// </summary>
        public const int MaxToneLengthMS = 350; // ms

        /// <summary>
        ///     Max DTMF Tones Packet Silence in ms
        /// </summary>
        public const int MaxTonePauseMS = 500; // ms

        private const string ToneSymbols = "123A456B789C*0#D"; // DTMF symbols
        private const int MaxFreqError = 30; // Hz

        /// <summary>
        ///     Recording Input Source ID
        /// </summary>
        public int InputSource = 1;

        /// <summary>
        ///     Name of InputSource
        /// </summary>
        public string InputName = "";

        /// <summary>
        ///     Recording Volume (max 1.0)
        /// </summary>
        public float InputVolume = 1.0F;

        private bool _isListening = false;

        /// <summary>
        ///     Is DTMF Detecting Enabled
        /// </summary>
        public bool IsListening { get { return _isListening; } }

        private DateTime previousRcvd = new DateTime(0);
        private DateTime previousSame = new DateTime(0);
        private string previousTone = "\0";
        private string currentPacket = "";
        private RECORDPROC recProc = null;
        private ToneDetectorHz tdhz = new ToneDetectorHz();

        /// <summary>
        ///     Start Detecting
        /// </summary>
        public void Start()
        {
            if (_isListening) return;
            
            InputName = Bass.BASS_RecordGetInputName(InputSource);
            _isListening = true;

            if (OutLogToConsole)
            {
                Console.WriteLine("Start Listening {0}...", InputName);
            }; 
           
            StartRecordAndAnalyze();
        }

        /// <summary>
        ///     Stop Detecting
        /// </summary>
        public void Stop()
        {
            if (!_isListening) return;
            _isListening = false;
            Bass.BASS_RecordFree();
            Console.WriteLine();
            Console.WriteLine("Listening Stopped");
        }

        /// <summary>
        ///     Init DTMF Stream Detector
        ///     Correct DTFM:
        ///         Single tone length: 180 ms (SoundForge); 160 ms (DtmfGenerator); 200 ms (Tranceiver)
        ///         Break length: 140 ms (SoundForge); 130 ms (DtmfGenerator); 200 ms (Tranceiver)
        ///         Pause length: 500 ms; (SoundForge)
        /// </summary>
        public DetectDTMFfromStream()
        {
            // Init Bass //
            Bass.BASS_RecordInit(-1);            
        }

        /// <summary>
        ///     Init DTMF Stream Detector
        ///     Correct DTFM:
        ///         Single tone length: 180 ms (SoundForge); 160 ms (DtmfGenerator); 200 ms (Tranceiver)
        ///         Break length: 140 ms (SoundForge); 130 ms (DtmfGenerator); 200 ms (Tranceiver)
        ///         Pause length: 500 ms; (SoundForge)
        /// </summary>
        public DetectDTMFfromStream(int inputSource)
        {
            // Init Bass //
            Bass.BASS_RecordInit(-1);
            InputSource = inputSource;
        }

        public void Dispose()
        {
            this.Stop();            
        }

        ~DetectDTMFfromStream()
        {
            this.Dispose();
        }

        private void StartRecordAndAnalyze()
        {
            // set Defaults
            previousRcvd = new DateTime(0);
            previousSame = new DateTime(0);
            previousTone = "\0";
            currentPacket = "";
            
            // Set Input                            
            Bass.BASS_RecordSetInput(InputSource, BASSInput.BASS_INPUT_ON, InputVolume);

            // Prepare Recording
            recProc = new RECORDPROC(OnRecData);
            int recChannel = Bass.BASS_RecordStart(8000, 1, BASSFlag.BASS_RECORD_PAUSE, 5, recProc, IntPtr.Zero);
            tdhz.initDTMF(8000);

            // Really start Recording
            Bass.BASS_ChannelPlay(recChannel, false);

        }        

        // Recording Callback
        private bool OnRecData(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // Spectrum Buffer 8000 Hz / 1024 Harmonics = (7.8125 Hz per cell)
            float[] spectrum = new float[512];

            // Reading Spectrum From Channel to 1024 Harmonics
            int read = Bass.BASS_ChannelGetData(handle, spectrum, (int)BASSData.BASS_DATA_FFT1024);

            string letter = "\0";
            // if something
            if (read != 0)
                letter = AnalyzeSpectrum_8k_Mono_512_FFT1024(spectrum).ToString();

            // if has tone
            if (letter != "\0")
            {
                previousRcvd = DateTime.Now;
                if ((previousTone != letter) || (DateTime.Now.Subtract(previousSame).TotalMilliseconds > MaxToneLengthMS))
                {
                    previousSame = DateTime.Now;
                    OnTone(letter[0]);
                    currentPacket += letter;
                };
            };

            // on packet end
            if ((!String.IsNullOrEmpty(currentPacket)) && (DateTime.Now.Subtract(previousRcvd).TotalMilliseconds > MaxTonePauseMS))
            {
                OnPacket(currentPacket);
                currentPacket = "";
            };

            previousTone = letter;

            return _isListening;
        }

        // Analyze Spectrum 8kHz Mono FFT1024
        public static char AnalyzeSpectrum_8k_Mono_512_FFT1024(float[] spectrum)
        {
            float maxLV = float.MinValue;
            float maxHV = float.MinValue;
            int maxLI = -1;
            int maxHI = -1;

            for (int i = 0; i < 128; i++) // freq < 1kHz // 128 = (int)((float)1000.0 / ((float)freq / (float)fft));
                if (maxLV < spectrum[i])
                    maxLV = spectrum[maxLI = i];

            for (int i = 128; i < 256; i++) // 2kHz > freq > 1kHz // 256 = (int)((float)1000.0 / ((float)freq / (float)fft));
                if (maxHV < spectrum[i])
                    maxHV = spectrum[maxHI = i];

            // Frequencies
            double freqLo = 8000.0 / 1024.0 * maxLI;
            double freqHi = 8000.0 / 1024.0 * maxHI;

            // Console.WriteLine(freqLo.ToString() + " " + freqHi.ToString());

            return AnalyzeHiLo(freqLo, freqHi);
        }

        public static char AnalyzeHiLo(double freqLo, double freqHi)
        {
            // Calculating Tone
            int indexL = -1; int indexH = -1;
            if (Math.Abs(0697 /* Hz */ - freqLo) < MaxFreqError) indexL = 0;
            if (Math.Abs(0770 /* Hz */ - freqLo) < MaxFreqError) indexL = 1;
            if (Math.Abs(0852 /* Hz */ - freqLo) < MaxFreqError) indexL = 2;
            if (Math.Abs(0941 /* Hz */ - freqLo) < MaxFreqError) indexL = 3;
            if (Math.Abs(1209 /* Hz */ - freqHi) < MaxFreqError) indexH = 0;
            if (Math.Abs(1336 /* Hz */ - freqHi) < MaxFreqError) indexH = 1;
            if (Math.Abs(1477 /* Hz */ - freqHi) < MaxFreqError) indexH = 2;
            if (Math.Abs(1633 /* Hz */ - freqHi) < MaxFreqError) indexH = 3;

            if ((indexL >= 0) && (indexH >= 0))
                return ToneSymbols[indexL * 4 + indexH];
            else
                return '\0';
        }

        // On Tone Received
        private void OnTone(char tone)
        {
            if (OutLogToConsole)
            {
                if (String.IsNullOrEmpty(currentPacket))
                    Console.Write("Incoming: ");
                Console.Write(tone);
            };

            if (OnGetTone != null)
                OnGetTone(tone);
        }

        // On Tones Received
        public virtual void OnPacket(string tones)
        {
            if (String.IsNullOrEmpty(tones)) return;

            string hextones = tones.Replace("*", "E").Replace("#", "F");

            if (OutLogToConsole)
            {
                Console.WriteLine();
                Console.WriteLine("  PR_*#: " + tones);
                Console.WriteLine("  PR_EF: " + hextones);
            };

            if (OnGetData != null)
                OnGetData(tones);

            if ((tones.Length == 33) && (tones.Substring(6, 3) == "B0B"))
                OnGPSPacket(hextones);
        }

        // On GPS Received //
        public virtual void OnGPSPacket(string hextones)
        {
            if (String.IsNullOrEmpty(hextones)) return;

            if (hextones.Length < 33) return;

            string _csn = hextones.Substring(0, 6);
            string _lon = hextones.Substring(9, 8);
            string _lat = hextones.Substring(17, 8);
            string _alt = hextones.Substring(25, 8);

            double lat = int.Parse(_lat, System.Globalization.NumberStyles.HexNumber) / 1000000.0;
            double lon = int.Parse(_lon, System.Globalization.NumberStyles.HexNumber) / 1000000.0;
            double alt = int.Parse(_alt, System.Globalization.NumberStyles.Integer) / 1000.0;

            if (OutLogToConsole)
            {
                Console.WriteLine("  GPS Received:");
                Console.WriteLine("   Callsign: " + _csn);
                Console.WriteLine("   Lat: N " + lat.ToString("00.000000", System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("   Lon: E " + lon.ToString("000.000000", System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("   Alt: " + alt.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "M");
            };

            if (OnGetGPS != null)
                OnGetGPS(_csn, lat, lon, alt);
        }        
    }

    public class PCM2WAV
    {
        public MemoryStream FileData;

        public static short[] From16bit8kMonoWav(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            fs.Position = 48;
            int len = (int)((fs.Length - 48) / 2);
            short[] frames = new short[len];
            for (int i = 0; i < frames.Length; i++)
            {
                byte b0 = (byte)fs.ReadByte();
                byte b1 = (byte)fs.ReadByte();
                frames[i] = (short)(b0 + (b1 << 8));
            };
            fs.Close();

            return frames;
        }

        public PCM2WAV(byte[] bit8frames)
        {
            // WRITE WAV HEADER
            FileData = new MemoryStream(48 + bit8frames.Length);
            FileData.Write(Encoding.UTF8.GetBytes("RIFF"), 0, 4); //Содержит символы "RIFF" в ASCII кодировке
            FileData.Write(BitConverter.GetBytes(bit8frames.Length + 40), 0, 4); //Это оставшийся размер цепочки, начиная с этой позиции. Иначе говоря, это размер файла - 8
            FileData.Write(Encoding.UTF8.GetBytes("WAVE"), 0, 4); //     Содержит символы "WAVE" 
            FileData.Write(Encoding.UTF8.GetBytes("fmt "), 0, 4); //     Содержит символы "fmt "
            FileData.Write(BitConverter.GetBytes(18), 0, 4);   //длинна заголовка fmt - 18 для формата PCM
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Для PCM = 1.Значения, отличающиеся от 1, обозначают некоторый формат сжатия.
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Количество каналов. Моно = 1, Стерео = 2 и т.д.
            FileData.Write(BitConverter.GetBytes(8000), 0, 4); //Частота дискретизации. 8000 Гц, 44100 Гц и т.д.
            FileData.Write(BitConverter.GetBytes(8000), 0, 4); //Количество байт, переданных за секунду воспроизведения.
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Количество байт для одного сэмпла, включая все каналы.
            FileData.Write(BitConverter.GetBytes(8), 0, 4);    //Количество бит в сэмпле. Так называемая "глубина" или точность звучания. 8 бит, 16 бит и т.д.
            FileData.Write(Encoding.UTF8.GetBytes("data"), 0, 4); //Содержит символы "data"
            FileData.Write(BitConverter.GetBytes(bit8frames.Length), 0, 4); //Количество байт в области данных.

            FileData.Write(bit8frames, 0, bit8frames.Length); //Непосредственно WAV-данные.
        }

        public PCM2WAV(short[] bit16frames)
        {
            // WRITE WAV HEADER
            FileData = new MemoryStream(48 + bit16frames.Length);
            FileData.Write(Encoding.UTF8.GetBytes("RIFF"), 0, 4); //Содержит символы "RIFF" в ASCII кодировке
            FileData.Write(BitConverter.GetBytes(bit16frames.Length + 40), 0, 4); //Это оставшийся размер цепочки, начиная с этой позиции. Иначе говоря, это размер файла - 8
            FileData.Write(Encoding.UTF8.GetBytes("WAVE"), 0, 4); //     Содержит символы "WAVE" 
            FileData.Write(Encoding.UTF8.GetBytes("fmt "), 0, 4); //     Содержит символы "fmt "
            FileData.Write(BitConverter.GetBytes(18), 0, 4);   //длинна заголовка fmt - 18 для формата PCM
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Для PCM = 1.Значения, отличающиеся от 1, обозначают некоторый формат сжатия.
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Количество каналов. Моно = 1, Стерео = 2 и т.д.
            FileData.Write(BitConverter.GetBytes(8000), 0, 4); //Частота дискретизации. 8000 Гц, 44100 Гц и т.д.
            FileData.Write(BitConverter.GetBytes(8000), 0, 4); //Количество байт, переданных за секунду воспроизведения.
            FileData.Write(BitConverter.GetBytes(1), 0, 2);    //Количество байт для одного сэмпла, включая все каналы.
            FileData.Write(BitConverter.GetBytes(16), 0, 4);   //Количество бит в сэмпле. Так называемая "глубина" или точность звучания. 8 бит, 16 бит и т.д.
            FileData.Write(Encoding.UTF8.GetBytes("data"), 0, 4); //Содержит символы "data"
            FileData.Write(BitConverter.GetBytes(bit16frames.Length * 2), 0, 4); //Количество байт в области данных.

            for (int i = 0; i < bit16frames.Length; i++)
            {
                FileData.WriteByte((byte)(0xFF & (bit16frames[i] >> 0)));
                FileData.WriteByte((byte)(0xFF & (bit16frames[i] >> 8)));
            };
        }

        public void WriteWavFile(string fileName)
        {
            File.WriteAllBytes(fileName, FileData.GetBuffer());
        }

        public void Free()
        {
            FileData.Close();
        }
    }
}
