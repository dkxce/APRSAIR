using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime;

using System.Runtime.InteropServices;

using System.Net;
using System.Net.Sockets;
using System.Threading;

using Un4seen.Bass;
using Un4seen.BassAsio;
using Un4seen.BassWasapi;

using NAudio.Wave;

namespace DTMFCoder
{
    public class DTMFListener : ax25kiss.PacketHandler
    {
        public struct RcvdRcrd
        {
            public DateTime DT;
            public string Callsign;
            public string ANICode;
            public double Lat;
            public double Lon;
            public double Alt;
            public string Icon;

            public RcvdRcrd(string Callsign, string ANICode, double Lat, double Lon, double Alt, string Icon)
            {
                this.DT = DateTime.Now;
                this.Callsign = Callsign;
                this.ANICode = ANICode;
                this.Lat = Lat;
                this.Lon = Lon;
                this.Alt = Alt;
                this.Icon = Icon;
            }

            public class RRComparer : IComparer<KeyValuePair<string, RcvdRcrd>>
            {
                public int Compare(KeyValuePair<string, RcvdRcrd> a, KeyValuePair<string, RcvdRcrd> b)
                {
                    return b.Value.DT.CompareTo(a.Value.DT);
                }
            }
        }

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

        public DTMFListener() { }

        /// <summary>
        ///     Init arguments from Command Line and Start
        /// </summary>
        /// <param name="args"></param>
        public void InitArgsAndRun(string[] args)
        {
            LoadUsers();

            int inputSource = 0;            
            string srvName = "APRSAIR";

            foreach (string arg in args)
            {
                if (arg == "/nogps2console") out2console = false;
                if (arg == "/useNormalPassw") useNormalPassw = true;
                if (arg.StartsWith("/source=")) inputSource = int.Parse(arg.Substring(8).Trim());
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

            Console.WriteLine("Run a Wave Listener - DetectDTMFfromStream");
            Console.WriteLine("  Using source: {0}", inputSource.ToString());

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
            DetectDTMFfromStream dtmfd = new DetectDTMFfromStream(inputSource);
            dtmfd.OnGetGPS = ListenGetGPS;
            dtmfd.OutLogToConsole = out2console;
            dtmfd.Start();
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
            if (dtmfd != null) { dtmfd.Stop(); dtmfd.Dispose(); };
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
                    if(!users.ContainsKey(mx.Groups["id"].Value))
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
                HTTPServer.UpdateLastPackets(new RcvdRcrd(callsign, anicode, lat, lon, alt, "/" + anicode.Substring(anicode.Length - 1)));
            
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
                Console.WriteLine("AFSK: " + packet);

            APRSForwarder.APRSParser parser = new APRSForwarder.APRSParser();
            parser.Parse(packet);
            
            if (parser.PacketType == "Location")
            {

                // HTTP Server
                if (HTTPServer != null)
                    HTTPServer.UpdateLastPackets(new RcvdRcrd(parser.Callsign, APRSForwarder.APRSParser.Hash(parser.Callsign).ToString(), String.IsNullOrEmpty(parser.Latitude) ? 0 : double.Parse(parser.Latitude, System.Globalization.CultureInfo.InvariantCulture), String.IsNullOrEmpty(parser.Longitude) ? 0 : double.Parse(parser.Longitude, System.Globalization.CultureInfo.InvariantCulture), String.IsNullOrEmpty(parser.Altitude) ? 0 : double.Parse(parser.Altitude, System.Globalization.CultureInfo.InvariantCulture), parser.Symbol));

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
    }

    public class APRSListener : TCPListener.ThreadedTCPServer
    {
        public string ServerName = "APRS-IS-DTMFCoder";
        public APRSListener() : base() { }
        public APRSListener(int Port) : base(Port) { }
        public APRSListener(IPAddress IP, int Port) : base(IP, Port) { }
        ~APRSListener() { this.Dispose(); }        

        // Get Client, threaded
        protected override void GetClient(TcpClient Client, ulong clientID)
        {            
            string RecvData = "";
            List<byte> Body = new List<byte>();

            int bRead = -1;
            int posCRLF = -1;
            int receivedBytes = 0;

            // APRS Server Welcome
            byte[] toSend = System.Text.Encoding.ASCII.GetBytes("# " + ServerName + "\r\n");
            Client.GetStream().Write(toSend, 0, toSend.Length);
            Client.GetStream().Flush();

            // check APRS
            //while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) // doesn't work correct
            while ((bRead = Client.GetStream().ReadByte()) >= 0)
            {
                receivedBytes++;
                Body.Add((byte)bRead);
                RecvData += (char)bRead; 

                if ((receivedBytes == 1) && (RecvData != "u")) return;
                if ((receivedBytes == 2) && (RecvData != "us")) return;
                if ((receivedBytes == 3) && (RecvData != "use")) return;
                if ((receivedBytes == 4) && (RecvData != "user")) return;

                if (bRead == 0x0A) posCRLF = RecvData.IndexOf("\r\n"); // End of single packet
                if (posCRLF >= 0 || RecvData.Length > 1024) { break; }; // BAD CLIENT                  
            };

            Match rm = Regex.Match(RecvData, @"^user\s([\w\-]{3,})\spass\s([\d\-]+)\svers\s([\w\d\-.]+)\s([\w\d\-.\+]+)");
            if (rm.Success)
            {
                string callsign = rm.Groups[1].Value.ToUpper();
                string res = "# logresp " + callsign + " verified, server " + ServerName +"\r\n";
                try
                {
                    byte[] ping = System.Text.Encoding.ASCII.GetBytes(res);
                    Client.GetStream().Write(ping, 0, ping.Length);
                    Client.GetStream().Flush();
                }
                catch { return; };
            };

            int rxCount = 0;
            int rxAvailable = 0;
            byte[] rxBuffer = new byte[65536];
            bool loop = true;
            int rCounter = 0;
            while (loop)
            {
                try { rxAvailable = Client.Available; }
                catch { break; };

                // Read Incoming Data
                while (rxAvailable > 0)
                {
                    try { rxAvailable -= (rxCount = Client.GetStream().Read(rxBuffer, 0, rxBuffer.Length > rxAvailable ? rxAvailable : rxBuffer.Length)); }
                    catch { break; };   
                    // ignoring data
                };

                if (!isRunning) loop = false;
                if (rCounter >= 300) // 15s ping
                {
                    try
                    {
                        if (!IsConnected(Client)) return;
                        byte[] ping = System.Text.Encoding.ASCII.GetBytes("#ping; server doesn't support any incoming data\r\n");
                        Client.GetStream().Write(ping, 0, ping.Length);
                        Client.GetStream().Flush();
                        rCounter = 0;
                    }
                    catch { loop = false; };
                };
                System.Threading.Thread.Sleep(50);
                rCounter++;
            };
        }

        //  Send message to all aprs clients
        public void Broadcast(string message)
        {
            byte[] msg = System.Text.Encoding.ASCII.GetBytes(message + "\r\n");
            KeyValuePair<ulong, TcpClient>[] toSend = this.Clients;
            foreach (KeyValuePair<ulong, TcpClient> cl in toSend)
            {
                try
                {
                    cl.Value.GetStream().Write(msg, 0, msg.Length);
                    cl.Value.GetStream().Flush();
                }
                catch { };
            };
        }
    }

    public class HTTPListener : TCPListener.ThreadedHttpServer
    {
        public HTTPListener() : base(80) { }
        public HTTPListener(int Port) : base(Port) { }
        public HTTPListener(IPAddress IP, int Port) : base(IP, Port) { }
        ~HTTPListener() { this.Dispose(); }

        // WebSocket Clients
        private List<ClientRequest> SocketClients = new List<ClientRequest>();
        private Mutex scMutex = new Mutex();

        // Last Received DTMF GPS Packets
        private List<KeyValuePair<string, DTMFCoder.DTMFListener.RcvdRcrd>> LastPackets = new List<KeyValuePair<string, DTMFCoder.DTMFListener.RcvdRcrd>>();
        private Mutex lpMutex = new Mutex();

        // New GPS DATA
        public void UpdateLastPackets(DTMFCoder.DTMFListener.RcvdRcrd rec)
        {
            KeyValuePair<string, DTMFCoder.DTMFListener.RcvdRcrd> update = new KeyValuePair<string, DTMFCoder.DTMFListener.RcvdRcrd>(rec.ANICode, rec);

            // Store in buffer
            lpMutex.WaitOne();
            bool ex = false;
            for (int i = 0; i < LastPackets.Count; i++)
                if (LastPackets[i].Key == rec.ANICode)
                {
                    LastPackets[i] = update;
                    ex = true;
                    break;
                };
            if (!ex) LastPackets.Add(update);
            lpMutex.ReleaseMutex();

            // send to all WebSocket clients
            byte[] toSend = GetWebSocketFrameFromString(RcvdRcrd2WebSocket(rec));
            scMutex.WaitOne();
            for (int i = 0; i < SocketClients.Count; i++)
            {
                try
                {
                    SocketClients[i].Client.GetStream().Write(toSend, 0, toSend.Length);
                    SocketClients[i].Client.GetStream().Flush();
                }
                catch { };
            };
            scMutex.ReleaseMutex();
        }

        // Get HTTP Client Web Request
        protected override void GetClientRequest(ClientRequest Request)
        {
            // if WebSocket
            if (HttpClientWebSocketInit(Request, false)) return;

            if (Request.Query != "/")
            {
                this.PassFileToClientByRequest(Request, GetCurrentDir() + @"\WEB\");
                return;
            };

            // if No WebSocket
            string HTTPRes = GetHTMLTemplate().Replace("%[[DATA]]%", GetList4Web()).Replace("%[[SERVER]]%", ServerName);
            byte[] HTTPData = System.Text.Encoding.UTF8.GetBytes(HTTPRes);
            HttpClientSendData(Request.Client, HTTPData, null, 200, "text/html; charset=utf-8");
        }

        /// <summary>
        ///     Get HTML Template from index.html
        /// </summary>
        /// <returns></returns>
        private string GetHTMLTemplate()
        {
            try
            {
                string fName = GetCurrentDir() + @"\WEB\index.html";
                FileStream fs = new FileStream(fName, FileMode.Open, FileAccess.Read);
                StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8);
                string dData = sr.ReadToEnd();
                sr.Close();
                fs.Close();
                return dData;
            }
            catch (Exception ex)
            {
                return ex.Message;
            };
        }

        // List received GPS coordinates
        private string GetList4Web()
        {
            string res = DateTime.Now.ToString("yyyy-mm-dd HH:mm:ss") + ": NO DATA";
            lpMutex.WaitOne();
            if (LastPackets.Count > 0)
            {
                res = "";
                if (LastPackets.Count > 1)
                    LastPackets.Sort(new DTMFCoder.DTMFListener.RcvdRcrd.RRComparer());
                for (int i = 0; i < LastPackets.Count; i++)
                    res += RcvdRcrd2WebRequest(LastPackets[i].Value);
            };
            lpMutex.ReleaseMutex();
            return res;
        }

        // Prepare result for HTTP Request
        private static string RcvdRcrd2WebRequest(DTMFCoder.DTMFListener.RcvdRcrd val)
        {
            return String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:yyyy-mm-dd HH:mm:ss}: {1} >> {2:000.0000000} {3:000.0000000} {4:00000.00} {5} i:{6}\r\n<br/>", new object[] { val.DT, val.Callsign, val.Lat, val.Lon, val.Alt, val.ANICode, val.Icon });
        }

        // Prepare result for WebSocket
        private static string RcvdRcrd2WebSocket(DTMFCoder.DTMFListener.RcvdRcrd val)
        {
            return String.Format(System.Globalization.CultureInfo.InvariantCulture, "{1} >> {2:000.0000000} {3:000.0000000} {4:00000.00} {5} i:{6}\r\n", new object[] { val.DT, val.Callsign, val.Lat, val.Lon, val.Alt, val.ANICode, val.Icon });
        }

        // On WebSocket Client Connected
        protected override void OnWebSocketClientConnected(ClientRequest clientRequest)
        {
            scMutex.WaitOne();
            SocketClients.Add(clientRequest);
            scMutex.ReleaseMutex();

            byte[] ba = GetWebSocketFrameFromString("Welcome to " + ServerName);
            clientRequest.Client.GetStream().Write(ba, 0, ba.Length);
            clientRequest.Client.GetStream().Flush();
        }

        // On WebSocket Client Disconnected
        protected override void OnWebSocketClientDisconnected(ClientRequest clientRequest)
        {
            scMutex.WaitOne();
            for (int i = 0; i < SocketClients.Count; i++)
                if (SocketClients[i].clientID == clientRequest.clientID)
                {
                    SocketClients.RemoveAt(i);
                    break;
                };
            scMutex.ReleaseMutex();
        }

        // On WebSocket Client Incoming Data
        protected override void OnWebSocketClientData(ClientRequest clientRequest, byte[] data)
        {
            try
            {
                string fws = GetStringFromWebSocketFrame(data, data.Length);
                if (String.IsNullOrEmpty(fws)) return;

                string tws = fws + " ok";
                byte[] toSend = GetWebSocketFrameFromString(tws);
                clientRequest.Client.GetStream().Write(toSend, 0, toSend.Length);
                clientRequest.Client.GetStream().Flush();
            }
            catch { };
        }
    }
}
