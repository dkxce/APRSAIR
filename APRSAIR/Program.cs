using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime;
using System.Runtime.InteropServices;

using System.Net;
using System.Net.Sockets;

using Un4seen.Bass;
using Un4seen.BassAsio;
using Un4seen.BassWasapi;

using NAudio.Wave;

namespace DTMFCoder
{
    class Program
    {
        public static bool StartedAsService = false;
    
        static void Main(string[] args)
        {
            StartedAsService = !Environment.UserInteractive;

            Console.WriteLine("**************************************************");
            Console.WriteLine("**                                              **");
            Console.WriteLine("**         APRSAIR  by milokz@gmail.com         **");
            Console.WriteLine("**  DTMF GPS + AX.25 via AFSK1200 AGW KISS TNC  **");
            Console.WriteLine("**             AIR as/to APRS-IS                **");
            Console.WriteLine("**              AIR as HTTP MAP                 **");
            Console.WriteLine("**                                              **");
            Console.WriteLine("**************************************************");
            Console.WriteLine();
            Console.WriteLine("## /listen /source=0 [/afsk=0]                  ##");
            Console.WriteLine("## /agw    /source=127.0.0.1:8000:0             ##");
            Console.WriteLine("## /kiss   /source=127.0.0.1:8100               ##");
            Console.WriteLine("## /kiss   /source=COM3:9600                    ##");
            Console.WriteLine();
            
            // List Record Devices /listrecorddevices
            List_RecDevs();
            List_WIDevs();
            if ((args != null) && (args.Length > 0) && (args[0] == "/listrecorddevices")) return;
            Console.WriteLine("------------------------------------------");

            // if parameters specified
            if((args != null) && (args.Length > 1))
            {
                string func = args[0].ToLower().Trim(new char[] { '-','/' });
                if ((func == "listen")) { (new DTMFCoder.DTMFListener()).InitArgsAndRun(args); return; };
                if ((func == "kiss") || (func == "agw")) { (new DTMFCoder.KISSListener()).InitArgsAndRun(args); return; };
                
                string argm = args[1].ToUpper().Trim(new char[] { '"' });                
                if ((func == "encode") && (args.Length > 2)) { Encode(argm, args[2].ToUpper().Trim(new char[] { '"' })); return; };
                if ((func == "decode")) { Decode(argm, args.Length > 2 ? args[2].ToUpper().Trim(new char[] { '"' }) : null); return; };
                if ((func == "encgeo") && (args.Length > 2)) { EncGeo(argm, args[2].ToUpper().Trim(new char[] { '"' })); return; };
                if ((func == "decgeo")) { DecGeo(argm, args.Length > 2 ? args[2].ToUpper().Trim(new char[] { '"' }) : null); return; };
                if ((func == "decaprs")) { DecAPRS(argm, args.Length > 2 ? args[2].ToUpper().Trim(new char[] { '"' }) : null); return; };
                if ((func == "encaprs") && (args.Length > 2)) { EncAPRS(argm, args[2].ToUpper().Trim(new char[] { '"' })); return; };
                if ((func == "encaprsf") && (args.Length > 2)) { EncAPRSF(argm, args[2].ToUpper().Trim(new char[] { '"' })); return; };
            };
            if (StartedAsService) return;

            // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST // // TEST //             
            
            // Std
            Test1_DTMFDG();

            // Excellent
            Test2_DTMFG();
            Test2A_DTMFG();

            // Works Good
            Test3_FromFile();
            Test3A_FromFile();
            
            // BAD
            // Test4_Bass_PlayFromFile();

            // Excellent -- Streaming
            Test5_Bass_Streaming();

            //  Wait
            System.Threading.Thread.Sleep(5000);
        }

        #region // Test
        private const short FRAME_SIZE = 160;

        private static void List_RecDevs()
        {
            try
            {
                Bass.BASS_RecordInit(-1);
                int dc = Bass.BASS_RecordGetDeviceCount();
                Console.WriteLine("Record Devices Found (/listen=...): ");
                for (int i = 0; i < dc; i++)
                    Console.WriteLine("  {0} - {1}", i, Bass.BASS_RecordGetInputName(i));
            }
            catch { };
        }

        private static void List_WIDevs()
        {
            try
            {
                int waveInDevices = WaveIn.DeviceCount;
                if (waveInDevices > 0)
                {
                    Console.WriteLine("Input/Direct/AFSK Devices Found (/afsk=...): ");
                    for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++)
                    {
                        WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                        Console.WriteLine(String.Format("  {0} - {1}", waveInDevice, deviceInfo.ProductName));
                    };
                };
            }
            catch { };
        }

        private static void Test1_DTMFDG()
        {
            short[] shortsArray = new short[FRAME_SIZE];
            DtmfGenerator dtmfGenerator = new DtmfGenerator(FRAME_SIZE, 180, 150);
            DtmfDetector dtmfDetector = new DtmfDetector(FRAME_SIZE);
            char[] dialButtons = new char[DtmfGenerator.NUMBER_BUTTONS];
            dialButtons[00] = '1';
            dialButtons[01] = '2';
            dialButtons[02] = '3';
            dialButtons[03] = '4';
            dialButtons[04] = '5';
            dialButtons[05] = '6';
            dialButtons[06] = '7';
            dialButtons[07] = '8';
            dialButtons[08] = '9';
            dialButtons[09] = 'A';
            dialButtons[10] = 'B';
            dialButtons[11] = 'C';
            dialButtons[12] = 'D';
            dialButtons[13] = '*';
            dialButtons[14] = '0';
            dialButtons[15] = '#';

            Console.WriteLine("TEST #1 - DtmfGenerator + DtmfDetector");
            Console.Write(" SET: ");
            for (int i = 0; i <= 15; i++)
                Console.Write(dialButtons[i]);
            Console.WriteLine();
            Console.Write(" GET: ");
            // Installation symbols for following generation
            dtmfGenerator.transmitNewDialButtonsArray(dialButtons, 20);

            while (!dtmfGenerator.getReadyFlag())
            {
                // 8 kHz, 16 bit's PCM frame's generation
                dtmfGenerator.dtmfGenerating(shortsArray);

                // 8 kHz, 16 bit's PCM frame's detection
                dtmfDetector.dtmfDetecting(shortsArray);

                if (dtmfDetector.getIndexDialButtons() > 0)
                {
                    char[] buttons = dtmfDetector.getDialButtonsArray();
                    for (int ii = 0; ii < dtmfDetector.getIndexDialButtons(); ++ii)
                        Console.Write(buttons[ii]);
                    dtmfDetector.zerosIndexDialButtons();
                };
            };
            Console.WriteLine();
            Console.WriteLine();
        }

        private static void Test2_DTMFG()
        {
            Console.WriteLine("TEST #2 - DtmfGenerator.Generate + DtmfDetector.Decode --> test2_result.wav");

            string test2text = "359230 B0B 023C#7B0 034#A030 00198000";
            Console.WriteLine(" SET: " + test2text);
            
            short[] dataFrames = DtmfGenerator.Generate(test2text);
            
            PCM2WAV pcf = new PCM2WAV(dataFrames);
            pcf.WriteWavFile(AppDomain.CurrentDomain.BaseDirectory + @"\test2_result.wav");
            pcf.Free();
            
            string res2txt = DtmfDetector.Decode(dataFrames);
            Console.WriteLine(" GET: " + res2txt);
            Console.WriteLine();
            Console.WriteLine();            
        }

        private static void Test2A_DTMFG()
        {
            Console.WriteLine("TEST #2A - DtmfGenerator.Generate + ToneDetectorHz.DecodeDTMF --> test2a_result.wav");

            string test2text = "359231 B0B 023C#7B0 034#A030 00198123";
            Console.WriteLine(" SET: " + test2text);

            short[] dataFrames = DtmfGenerator.Generate(test2text);
            PCM2WAV pcf = new PCM2WAV(dataFrames);
            pcf.WriteWavFile(AppDomain.CurrentDomain.BaseDirectory + @"\test2a_result.wav");
            pcf.Free();

            dataFrames = PCM2WAV.From16bit8kMonoWav(AppDomain.CurrentDomain.BaseDirectory + @"\test2a_result.wav");                        
            ToneDetectorHz td = new ToneDetectorHz();
            td.initDTMF(8000);
            string decoded = td.DecodeDTMF(dataFrames, 100);
            
            Console.Write(" GET: ");
            Console.WriteLine(decoded);
            
            Console.WriteLine();
            Console.WriteLine();
        }

        private static void Test3_FromFile()
        {
            Console.WriteLine("TEST #3 -- ToneDetectorHz.DecodeDTMF <-- test2_result.wav"); // 33 SYMBOLS
            string fIn = AppDomain.CurrentDomain.BaseDirectory + @"\test2_result.wav";

            short[] dataFrames = PCM2WAV.From16bit8kMonoWav(fIn);
            ToneDetectorHz td = new ToneDetectorHz();
            td.initDTMF(8000);
            string decoded = td.DecodeDTMF(dataFrames, 100);

            Console.Write(" GET: ");
            Console.WriteLine(decoded);

            Console.WriteLine();
            Console.WriteLine();
        }

        private static void Test3A_FromFile()
        {
            Console.WriteLine("TEST #3 -- ToneDetectorHz.DecodeDTMF <-- test2a_result.wav"); // 33 SYMBOLS
            string fIn = AppDomain.CurrentDomain.BaseDirectory + @"\test2a_result.wav";

            short[] dataFrames = PCM2WAV.From16bit8kMonoWav(fIn);
            ToneDetectorHz td = new ToneDetectorHz();
            td.initDTMF(8000);
            string decoded = td.DecodeDTMF(dataFrames, 100);

            Console.Write(" GET: ");
            Console.WriteLine(decoded);

            Console.WriteLine();
            Console.WriteLine();
        }

        // https://stackoverflow.com/questions/12616109/audio-playback-and-spectrum-analysis-libarary-for-c-sharp/12620852
        private static void Test4_Bass_PlayFromFile()
        {
            Console.WriteLine("Test #4 -- Reading test2_result.wav");
            DateTime previousRcvd = new DateTime(0);
            DateTime previousSame = new DateTime(0);


            float[] buffer = new float[512]; // FFT1024
            string filepath = AppDomain.CurrentDomain.BaseDirectory + @"\test2_result.wav";
            Bass.BASS_Init(-1, 8000, BASSInit.BASS_DEVICE_DEFAULT, System.Diagnostics.Process.GetCurrentProcess().Handle);

            int h1 = Bass.BASS_StreamCreateFile(filepath, 0, 0, BASSFlag.BASS_SAMPLE_FLOAT);

            long len = Bass.BASS_ChannelGetLength(h1, BASSMode.BASS_POS_BYTES); // the length in bytes
            double time = Bass.BASS_ChannelBytes2Seconds(h1, len); // the length in seconds
            int steps = (int)Math.Floor(100 * time);
                                    
            string prev_letter = "";
            Bass.BASS_ChannelSetPosition(h1, 0);
            Bass.BASS_ChannelPlay(h1, false);

            while (Bass.BASS_ChannelIsActive(h1) == BASSActive.BASS_ACTIVE_PLAYING)
            {                
                int read = Bass.BASS_ChannelGetData(h1, buffer, (int)BASSData.BASS_DATA_FFT1024);
                if (read != 0)
                {
                    string letter = DetectDTMFfromStream.AnalyzeSpectrum_8k_Mono_512_FFT1024(buffer).ToString();
                    if (letter != "\0")
                    {
                        if (DateTime.Now.Subtract(previousRcvd).TotalMilliseconds < 180)
                            continue;
                        previousRcvd = DateTime.Now;
                    };
                    if ((prev_letter != letter) || (DateTime.Now.Subtract(previousSame).TotalMilliseconds > 400))
                    {
                        if (letter != "\0")
                        {
                            previousSame = DateTime.Now;
                            Console.Write(letter[0]);
                        };
                    };
                    prev_letter = letter;
                };
            };
            Bass.BASS_ChannelStop(h1);
            Console.WriteLine();
            Console.WriteLine();
        }

        private static void Test5_Bass_Streaming()
        {
            Console.WriteLine("Test #5 - DetectDTMFfromStream");

            DetectDTMFfromStream dtmfd = new DetectDTMFfromStream();
            dtmfd.Start();
            Console.WriteLine("--- press Enter to Stop ---");
            Console.ReadLine();
            dtmfd.Stop();
            dtmfd.Dispose();
        }
        #endregion // Test

        #region // EncDec
        private static void Encode(string packet, string file)
        {
            Console.WriteLine("ENCODE");
            Console.WriteLine("PACKET: {0}", packet);
            Console.WriteLine("FILE: {0}", file);

            short[] dataFrames = DtmfGenerator.Generate(packet);
            PCM2WAV pcf = new PCM2WAV(dataFrames);
            pcf.WriteWavFile(file);
            pcf.Free();

            Console.WriteLine("DONE");
        }

        private static void EncGeo(string packet, string file)
        {
            Console.WriteLine("ENCODE GEO");
            Console.WriteLine("ORIGIN: {0}", packet);

            string[] det = packet.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string lat = ((int)(double.Parse(det[2], System.Globalization.CultureInfo.InvariantCulture) * 1000000)).ToString("X");
            while (lat.Length < 8) lat = "0" + lat;
            string lon = ((int)(double.Parse(det[3], System.Globalization.CultureInfo.InvariantCulture) * 1000000)).ToString("X");
            while (lon.Length < 8) lon = "0" + lon;
            packet = det[0] + " " + det[1] + " " + lat + " " + lon + " " + det[4];
            packet = packet.Replace("E", "*").Replace("F", "#");
            
            Console.WriteLine("PACKET: {0}", packet);
            Console.WriteLine("FILE: {0}", file);

            short[] dataFrames = DtmfGenerator.Generate(packet);
            PCM2WAV pcf = new PCM2WAV(dataFrames);
            pcf.WriteWavFile(file);
            pcf.Free();

            Console.WriteLine("DONE");
        }

        private static void EncAPRS(string command, string file)
        {
            if (String.IsNullOrEmpty(command))
            {
                Console.WriteLine("Bad command");
                return;
            };

            Console.WriteLine("ENCODE APRS");
            Console.WriteLine("ORIGIN: {0}", command);

            Regex rx = new Regex(@"^(?<from>[\w-]{1,7})>((?<to>[\w-*]{1,7}),?)+:(?<packet>.+)", RegexOptions.None);
            Match mc = rx.Match(command);
            if(!mc.Success)
            {
                Console.WriteLine("Bad command");
                return;
            };

            string dest = mc.Groups["to"].Captures[0].Value;
            List<string> via = new List<string>();
            for (int i = 1; i < mc.Groups["to"].Captures.Count; i++) via.Add(mc.Groups["to"].Captures[i].Value);

            ax25.AFSK1200Modulator mod = new ax25.AFSK1200Modulator(44100);
            mod.txDelayMs = 500;

            ax25kiss.Packet packet = new ax25kiss.Packet(
                "APRS", mc.Groups["from"].Value, via.ToArray(),
                ax25kiss.Packet.AX25_CONTROL_APRS, ax25kiss.Packet.AX25_PROTOCOL_NO_LAYER_3,
                System.Text.Encoding.ASCII.GetBytes(mc.Groups["packet"].Value)
                );

            float[] samples;
            mod.GetSamples(packet, out samples);

            Console.WriteLine("PACKET: {0}", ax25kiss.Packet.Format(packet.bytesWithoutCRC()));
            Console.WriteLine("FILE: {0}", file);

            ReadWave.WaveStream.SaveWav16BitMono(file, 44100, samples);
            
            Console.WriteLine("DONE");
        }


        private static void EncAPRSF(string inFile, string outFile)
        {
            Console.WriteLine("ENCODE APRS FILE");
            Console.WriteLine("ORIGIN FILE: {0}", inFile);

            List<string> coms = new List<string>();
            FileStream fs = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs, true);
            while (!sr.EndOfStream) coms.Add(sr.ReadLine().Trim());
            sr.Close();
            fs.Close();

            if (coms.Count == 0)
            {
                Console.WriteLine("Bad commands");
                return;
            };
            
            ax25.AFSK1200Modulator mod = new ax25.AFSK1200Modulator(44100);
            mod.txDelayMs = 500;

            List<float> sList = new List<float>();

            int ccounter = 0;
            foreach (string command in coms)
            {
                Regex rx = new Regex(@"^(?<from>[\w-]{1,7})>((?<to>[\w-*]{1,7}),?)+:(?<packet>.+)", RegexOptions.None);
                Match mc = rx.Match(command);
                if (!mc.Success) continue;
                
                string dest = mc.Groups["to"].Captures[0].Value;
                List<string> via = new List<string>();
                for (int i = 1; i < mc.Groups["to"].Captures.Count; i++) via.Add(mc.Groups["to"].Captures[i].Value);


                ax25kiss.Packet packet = new ax25kiss.Packet(
                    "APRS", mc.Groups["from"].Value, via.ToArray(),
                    ax25kiss.Packet.AX25_CONTROL_APRS, ax25kiss.Packet.AX25_PROTOCOL_NO_LAYER_3,
                    System.Text.Encoding.ASCII.GetBytes(mc.Groups["packet"].Value)
                    );

                float[] samples;
                mod.GetSamples(packet, out samples);
                sList.AddRange(samples);
                sList.AddRange(new float[2000]);
                ccounter++;
            };

            Console.WriteLine("PACKETS FOUND: {0}", ccounter);
            Console.WriteLine("RESULT FILE: {0}", outFile);

            ReadWave.WaveStream.SaveWav16BitMono(outFile, 44100, sList.ToArray());

            Console.WriteLine("DONE");
        }

        private static void Decode(string wavFile, string textFile)
        {
            Console.WriteLine("DECODE");
            Console.WriteLine("WAVFILE: {0}", wavFile);
            Console.WriteLine("TXTFILE: {0}", String.IsNullOrEmpty(textFile) ? "stdout" : textFile);

            short[] dataFrames = PCM2WAV.From16bit8kMonoWav(wavFile);            

            ToneDetectorHz td = new ToneDetectorHz();
            td.initDTMF(8000);
            string packet = td.DecodeDTMF(dataFrames);
            Console.WriteLine("PACKET: {0}", packet);
            if (!String.IsNullOrEmpty(textFile))
            {
                FileStream fs = new FileStream(textFile, FileMode.Create, FileAccess.Write);
                byte[] buff = Encoding.ASCII.GetBytes(packet);
                fs.Write(buff, 0, buff.Length);
                fs.Close();
                Console.WriteLine("File writed");
            };
            Console.WriteLine("DONE");
        }

        private static void DecGeo(string wavFile, string textFile)
        {
            Console.WriteLine("DECODE GEO");
            Console.WriteLine("WAVFILE: {0}", wavFile);
            Console.WriteLine("TXTFILE: {0}", String.IsNullOrEmpty(textFile) ? "stdout" : textFile);

            short[] dataFrames = PCM2WAV.From16bit8kMonoWav(wavFile);

            ToneDetectorHz td = new ToneDetectorHz();
            td.initDTMF(8000);
            string packet = td.DecodeDTMF(dataFrames);
            Console.WriteLine("PACKET: {0}", packet);
            packet = packet.Replace("*", "E").Replace("#", "F");
            string lat = (((double)(Convert.ToInt32(packet.Substring(9, 8), 16))) / 1000000.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string lon = (((double)(Convert.ToInt32(packet.Substring(17, 8), 16))) / 1000000.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
            packet = packet.Substring(0, 6) + " " + packet.Substring(6, 3) + " " + lat + " " + lon + " " + packet.Substring(25);            

            Console.WriteLine("ORIGIN: {0}", packet);

    
            if (!String.IsNullOrEmpty(textFile))
            {
                FileStream fs = new FileStream(textFile, FileMode.Create, FileAccess.Write);
                byte[] buff = Encoding.ASCII.GetBytes(packet);
                fs.Write(buff, 0, buff.Length);
                fs.Close();
                Console.WriteLine("File writed");
            };
            Console.WriteLine("DONE");
        }

        private static void DecAPRS(string wavFile, string textFile)
        {
            Console.WriteLine("DECODE GEO");
            Console.WriteLine("WAVFILE: {0}", wavFile);
            Console.WriteLine("TXTFILE: {0}", String.IsNullOrEmpty(textFile) ? "stdout" : textFile);

            float[] L, R;
            int ch, sr, bd;
            ReadWave.WaveStream.ReadWavFile(wavFile, out ch, out sr, out bd, out L, out R);

            ax25.AFSK1200Demodulator dem = new ax25.AFSK1200Demodulator(sr, 36, 0, null);
            dem.AddSamples(L, L.Length);
            string packet = ax25kiss.Packet.Format(dem.LastPacket);            

            Console.WriteLine("ORIGIN: {0}", packet);


            if (!String.IsNullOrEmpty(textFile))
            {
                FileStream fs = new FileStream(textFile, FileMode.Create, FileAccess.Write);
                byte[] buff = Encoding.ASCII.GetBytes(packet);
                fs.Write(buff, 0, buff.Length);
                fs.Close();
                Console.WriteLine("File writed");
            };
            Console.WriteLine("DONE");
        }
        #endregion // EncDec

    }    
}
