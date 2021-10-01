using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime;

using System.Net;
using System.Net.Sockets;
using System.Threading;

using NAudio.Wave;

namespace DTMFCoder
{
    public class KISSListener : ax25kiss.AX25Handler, ax25kiss.PacketHandler
    {        
        private HTTPListener HTTPServer = null;
        private APRSListener APRSServer = null;
        private int afskl = -1;
        private ReadWave.DirectAudioAFSKDemodulator AFSK = null;

        private Dictionary<string, string> users = new Dictionary<string, string>();
        private Regex rxTCP = new Regex(@"tcp://(?<user>[^:@]+):?(?<pass>[\-\d]+)*@(?<host>[^:/]+):?(?<port>[\d]+)*", RegexOptions.IgnoreCase);
        private Regex rxUDP = new Regex(@"udp://(?<user>[^:@]+):?(?<pass>[\-\d]+)*@(?<host>[^:/]+):?(?<port>[\d]+)*", RegexOptions.IgnoreCase);
        private APRSForwarder.APRSClient APRSClient = null;

        private string send = null;
        private Match aprstcp = null;
        private Match aprsudp = null;
        private bool useNormalPassw = false;
        private bool out2console = true;
        private string mode = "????";

        public KISSListener() { }

        /// <summary>
        ///     Init arguments from Command Line and Start
        /// </summary>
        /// <param name="args"></param>
        public void InitArgsAndRun(string[] args)
        {
            LoadUsers();

            string initString = "127.0.0.1:8000:0";
            string srvName = "APRSAIR";

            foreach (string arg in args)
            {
                if (arg == "/nogps2console") out2console = false;
                if (arg == "/useNormalPassw") useNormalPassw = true;
                if (arg.StartsWith("/source=")) initString = arg.Substring(8).Trim();
                if (arg.StartsWith("/send=")) send = arg.Substring(6).Trim();
                if (arg.StartsWith("/aprs=tcp")) aprstcp = rxTCP.Match(arg.Substring(6).Trim());
                if (arg.StartsWith("/aprs=udp")) aprsudp = rxUDP.Match(arg.Substring(6).Trim());
                if (arg.StartsWith("/serverName=")) srvName = arg.Substring(12).Trim();
                if (arg.StartsWith("/afsk=")) { afskl = int.Parse(arg.Substring(6).Trim()); };


                if (arg.StartsWith("/httpserv="))
                {
                    int port = 80;
                    int.TryParse(arg.Substring(10).Trim(), out port);
                    HTTPServer = new HTTPListener(port);
                    HTTPServer.AllowBrowseFiles = true;
                };

                if (arg.StartsWith("/aprsserv="))
                {
                    int port = 14580;
                    int.TryParse(arg.Substring(10).Trim(), out port);
                    APRSServer = new APRSListener(port);
                };
            };

            Console.WriteLine("Run KISS/AGW Mode - TCP/IP or Serial");
            Console.WriteLine("  Init sttring: {0}", initString);

            if (afskl >= 0)
                Console.WriteLine("  AFSK Listen from Device: {0}", afskl);

            if (!String.IsNullOrEmpty(send)) Console.WriteLine("  Using HTTP: {0}", send);
            if ((aprstcp != null) && (aprstcp.Success)) Console.WriteLine("  Using APRS: {0}", aprstcp.Value);
            if ((aprsudp != null) && (aprsudp.Success)) Console.WriteLine("  Using APRS: {0}", aprsudp.Value);
            if (HTTPServer != null) Console.WriteLine("  Using HTTP Server: {0} as `{1}`", HTTPServer.ServerPort, HTTPServer.ServerName = srvName);
            if (APRSServer != null) Console.WriteLine("  Using APRS Server: {0} as `{1}`", APRSServer.ServerPort, APRSServer.ServerName = srvName);

            // APRS Client
            if ((aprstcp != null) && aprstcp.Success)
            {
                string user = aprstcp.Groups["user"].Value;
                string pass = String.IsNullOrEmpty(aprstcp.Groups["pass"].Value) ? "-1" : aprstcp.Groups["pass"].Value;
                string host = aprstcp.Groups["host"].Value;
                int port = String.IsNullOrEmpty(aprstcp.Groups["port"].Value) ? 14580 : int.Parse(aprstcp.Groups["port"].Value);
                APRSClient = new APRSForwarder.APRSClient(host, port, user, pass);
            };

            // Cleints & Servers
            if ((APRSClient != null) || (HTTPServer != null) || (APRSServer != null)) Console.WriteLine("Starting Client & Servers...");

            // APRS Client
            if (APRSClient != null) { APRSClient.Start(); Console.WriteLine("  APRS Client started"); System.Threading.Thread.Sleep(2000); };

            // HTTP Server
            if (HTTPServer != null) { HTTPServer.Start(); Console.WriteLine("  HTTP Server started at {0}", HTTPServer.ServerPort); };

            // APRS Server
            if (APRSServer != null) { APRSServer.Start(); Console.WriteLine("  APRS Server started at {0}", APRSServer.ServerPort); };

            // DTMF Parser
            ax25kiss.KISSTNC kiss = new ax25kiss.KISSTNC(initString);
            kiss.onPacket = this;
            mode = (kiss.Mode == ax25kiss.KISSTNC.ConnectionMode.AGW ? kiss.Mode.ToString() : "KISS " + kiss.Mode.ToString());
            Console.WriteLine("Start working with mode: " + mode);
            kiss.Start();            
            if (afskl >= 0)
            {
                AFSK = new ReadWave.DirectAudioAFSKDemodulator(afskl, this);
                AFSK.OutLogToConsole = out2console;
                AFSK.Start();
            };

            System.Threading.Thread.Sleep(2500);
            Console.WriteLine("--- Press Enter to Stop ---");
            Console.ReadLine();
            Console.Write("Stopping Listeners... ");
            if (AFSK != null) { AFSK.Stop(); AFSK.Dispose(); };
            if (kiss != null) { kiss.Stop(); kiss = null; };
            if (HTTPServer != null) { HTTPServer.Stop(); HTTPServer.Dispose(); };
            if (APRSServer != null) { APRSServer.Stop(); APRSServer.Dispose(); };
            if (APRSClient != null) { APRSClient.Stop(); };
            Console.WriteLine("Done");
        }

        /// <summary>
        ///     Load Users from file (ANI_CODE=CALLSIGN)
        /// </summary>
        private void LoadUsers()
        {
            string fName = GetCurrentDir() + @"\users_replace_list.txt";
            if (!File.Exists(fName)) return;
            try
            {
                FileStream fs = new FileStream(fName, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.GetEncoding(1251));
                Regex uReg = new Regex(@"^(?<id>\d{3,6})[\=\s]*(?<user>.+)$", RegexOptions.None);
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    if (line.Length < 3) continue;
                    if (!char.IsDigit(line[0])) continue;
                    Match mx = uReg.Match(line);
                    if (!mx.Success) continue;
                    if (!users.ContainsKey(mx.Groups["id"].Value))
                        users.Add(mx.Groups["id"].Value, mx.Groups["user"].Value);
                };
                sr.Close();
                fs.Close();
            }
            catch { };
        }

        /// <summary>
        ///     On GPS from DTFM
        /// </summary>
        /// <param name="ani">ANI_CODE</param>
        /// <param name="lat">LAT</param>
        /// <param name="lon">LON</param>
        /// <param name="alt">ALT</param>
        private void ListenGetGPS(string anicode, double lat, double lon, double alt)
        {
            string ani = anicode;
            string callsign = ani;
            if (users.ContainsKey(callsign))
                callsign = users[callsign];
            else
                callsign = "U" + callsign;

            // HTTP Server
            if (HTTPServer != null)
                HTTPServer.UpdateLastPackets(new DTMFListener.RcvdRcrd(callsign, anicode, lat, lon, alt, "/" + anicode.Substring(anicode.Length - 1)));

            // http
            if (!String.IsNullOrEmpty(send))
            {
                string url = send.Replace("{ID}", callsign).Replace("{LAT}", lat.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{LON}", lon.ToString(System.Globalization.CultureInfo.InvariantCulture)).Replace("{ALT}", alt.ToString(System.Globalization.CultureInfo.InvariantCulture));
                try
                {
                    HttpWebRequest wReq = (HttpWebRequest)HttpWebRequest.Create(url);
                    wReq.Timeout = 10000; // ms
                    wReq.GetResponse().Close();
                    Console.WriteLine("HTTP>> " + url);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("HTTP ERROR: " + url + " - " + ex.Message);
                };
            };

            // aprs
            if ((APRSServer != null) || ((aprstcp != null) && aprstcp.Success) || ((aprsudp != null) && aprsudp.Success))
            {
                string APRS =
                        callsign + ">APRS,TCPIP*:=" + // Position without timestamp + APRS message
                        Math.Truncate(lat).ToString("00") + ((lat - Math.Truncate(lat)) * 60).ToString("00.00").Replace(",", ".") +
                        (lat > 0 ? "N" : "S") +
                        "/" + // icon symbol 1
                        Math.Truncate(lon).ToString("000") + ((lon - Math.Truncate(lon)) * 60).ToString("00.00").Replace(",", ".") +
                        (lon > 0 ? "E" : "W") +
                        ani.Substring(ani.Length - 1) + // icon symbol 2
                        "000/000" +
                        "From DTMF" + (callsign == ani ? "" : " " + anicode) + " /A=" + ((int)alt).ToString("000000");

                // aprs-is
                if (APRSServer != null)
                    APRSServer.Broadcast(APRS);

                // aprs tcp
                if ((aprstcp != null) && aprstcp.Success && (APRSClient != null))
                {
                    try
                    {
                        APRSClient.SendToServer(APRS);
                        Console.WriteLine("ATCP>> " + APRS);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ATCP Error: " + ex.Message);
                    };
                };

                // aprs udp
                if ((aprsudp != null) && aprsudp.Success)
                {
                    string user = aprsudp.Groups["user"].Value;
                    string pass = String.IsNullOrEmpty(aprsudp.Groups["pass"].Value) ? "-1" : aprsudp.Groups["pass"].Value;
                    if (useNormalPassw)
                    {
                        user = callsign;
                        pass = APRSForwarder.APRSClient.CallsignChecksum(callsign).ToString();
                    };
                    string host = aprsudp.Groups["host"].Value;
                    int port = String.IsNullOrEmpty(aprsudp.Groups["port"].Value) ? 8080 : int.Parse(aprsudp.Groups["port"].Value);

                    string data2s = String.Format("user {0} pass {1} vers DTMFSample 0.1\r\n{2}\r\n", user, pass, APRS);
                    try
                    {
                        SendUDP(host, port, data2s);
                        Console.WriteLine("UTCP>> " + APRS);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("UTCP Error: " + ex.Message);
                    };
                };
            };
        }

        private void ListenGetAFSK(string packet)
        {
            if (out2console)
                Console.WriteLine(mode + ": " + packet);

            APRSForwarder.APRSParser parser = new APRSForwarder.APRSParser();
            parser.Parse(packet);

            if (parser.PacketType == "Location")
            {

                // HTTP Server
                if (HTTPServer != null)
                    HTTPServer.UpdateLastPackets(new DTMFListener.RcvdRcrd(parser.Callsign, APRSForwarder.APRSParser.Hash(parser.Callsign).ToString(), String.IsNullOrEmpty(parser.Latitude) ? 0 : double.Parse(parser.Latitude, System.Globalization.CultureInfo.InvariantCulture), String.IsNullOrEmpty(parser.Longitude) ? 0 : double.Parse(parser.Longitude, System.Globalization.CultureInfo.InvariantCulture), String.IsNullOrEmpty(parser.Altitude) ? 0 : double.Parse(parser.Altitude, System.Globalization.CultureInfo.InvariantCulture), parser.Symbol));

                // http
                if (!String.IsNullOrEmpty(send))
                {
                    string url = send.Replace("{ID}", parser.Callsign).Replace("{LAT}", parser.Latitude).Replace("{LON}", parser.Longitude).Replace("{ALT}", parser.Altitude);
                    try
                    {
                        HttpWebRequest wReq = (HttpWebRequest)HttpWebRequest.Create(url);
                        wReq.Timeout = 10000; // ms
                        wReq.GetResponse().Close();
                        Console.WriteLine("HTTP>> " + url);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("HTTP ERROR: " + url + " - " + ex.Message);
                    };
                };
            };

            // aprs
            if ((APRSServer != null) || ((aprstcp != null) && aprstcp.Success) || ((aprsudp != null) && aprsudp.Success))
            {
                // aprs-is
                if (APRSServer != null)
                    APRSServer.Broadcast(packet);

                // aprs tcp
                if ((aprstcp != null) && aprstcp.Success && (APRSClient != null))
                {
                    try
                    {
                        APRSClient.SendToServer(packet);
                        Console.WriteLine("ATCP>> " + packet);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ATCP Error: " + ex.Message);
                    };
                };

                // aprs udp
                if ((aprsudp != null) && aprsudp.Success)
                {
                    string user = aprsudp.Groups["user"].Value;
                    string pass = String.IsNullOrEmpty(aprsudp.Groups["pass"].Value) ? "-1" : aprsudp.Groups["pass"].Value;
                    if (useNormalPassw)
                    {
                        user = parser.Callsign;
                        pass = APRSForwarder.APRSClient.CallsignChecksum(parser.Callsign).ToString();
                    };
                    string host = aprsudp.Groups["host"].Value;
                    int port = String.IsNullOrEmpty(aprsudp.Groups["port"].Value) ? 8080 : int.Parse(aprsudp.Groups["port"].Value);

                    string data2s = String.Format("user {0} pass {1} vers DTMFSample 0.1\r\n{2}\r\n", user, pass, packet);
                    try
                    {
                        SendUDP(host, port, data2s);
                        Console.WriteLine("UTCP>> " + packet);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("UTCP Error: " + ex.Message);
                    };
                };
            };
        }

        private static void SendUDP(string host, int port, string data)
        {
            UdpClient udp = new UdpClient();
            udp.Connect(host, port);
            byte[] dt = System.Text.Encoding.GetEncoding(1251).GetBytes(data);
            udp.Send(dt, dt.Length);
            udp.Close();
        }

        public static string GetCurrentDir()
        {
            string fname = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();
            fname = fname.Replace("file:///", "");
            fname = fname.Replace("/", @"\");
            fname = fname.Substring(0, fname.LastIndexOf(@"\") + 1);
            return fname;
        }

        // from AFSK
        public void handlePacket(sbyte[] bytes)
        {
            try
            {
                string packet = ax25kiss.Packet.Format(bytes);
                ListenGetAFSK(packet);
            }
            catch { };
        }     

        // from KISS/AGW
        public void handlePacket(ax25kiss.Packet packet)
        {
            try
            {
                ListenGetAFSK(packet.ToString());
            }
            catch { };
        }
    }
}
