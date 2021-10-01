using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace APRSForwarder
{
    public class APRSClient
    {        
        const string softName = "DTMFSampleAPRSClient";
        const string softVer = "0.1";

        private string callsign = "UNKNOWN";
        private string passw = "-1";
        private string filter = "p/R*/U*";

        // http://euro.aprs2.net:14501/
        private string APRSserver = "euro.aprs2.net";
        private int APRSPort = 14580;
        private string message_ping_text = "APRS,TCPIP*:>online";

        private TcpClient tcp_client = null;
        private Thread tcp_listen = null;

        private bool _isRunning = false;

        public APRSClient(string server, int port, string user, string pass)
        {
            this.APRSserver = server;
            this.APRSPort = port;
            this.callsign = user;
            this.passw = pass;
        }        

        #region START/STOP
        public bool Running
        {
            get
            {
                return _isRunning;
            }
            set
            {
                if (value && _isRunning) return;
                if (value != _isRunning)
                {
                    if (value)
                        Start();
                    else
                        Stop();
                };
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            tcp_listen = new Thread(ReadIncomingDataThread);
            tcp_listen.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;

            Console.Write("Closing connection aprs-is `" + APRSserver + ":" + APRSPort.ToString() + "`... ");
            
            _isRunning = false;
            if (tcp_client != null)
            {
                tcp_client.Close();
                tcp_client = null;
            };

            Console.WriteLine(" Closed");
        }

        public bool Connected
        {
            get
            {
                if (!_isRunning) return false;
                if (tcp_client == null) return false;
                return tcp_client.Connected;
            }
        }
        #endregion

        private void ReadIncomingDataThread()
        {
            uint reping = 0; // for ping timeout
            while (_isRunning)
            {
                // ++ CONNECT & RECONNECT APRS-IS ++ //
                {
                    // ++ connect or reconnect APRS-IS ++ //
                    if ((tcp_client == null) || (!tcp_client.Connected))
                    {
                        tcp_client = new TcpClient();
                        try
                        {
                            Console.Write("Open connection to aprs-is `" + APRSserver + ":" + APRSPort.ToString() + "`... ");
                            tcp_client.Connect(APRSserver, APRSPort);
                            Console.WriteLine(" Opened");

                            string txt2send = "user " + callsign + " pass " + passw + " vers " + softName + " " + softVer + (filter != String.Empty ? " filter " + filter : "");
                            SendToServer(txt2send);
                            Console.WriteLine("Authorization at `" + APRSserver + ":" + APRSPort.ToString() + "` as " + callsign + " is ok");

                            reping = 0;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(" Failed");
                            tcp_client.Close();
                            tcp_client = new TcpClient();

                            string txt = "ERROR connect APRS-IS: " + ex.Message;
                            Console.WriteLine(txt);
                            Thread.Sleep(5000);
                            continue;
                        };
                    };
                    // -- connect or reconnect APRS-IS -- //
                    
                };
                // -- CONNECT & CONNECT & RECONNECT APRS-IS -- //

                // ++ READ APRS-IS ++ //
                try
                {
                    byte[] data = new byte[65536];
                    int ava = 0;                    
                    if ((ava = tcp_client.Available) > 0)
                    {
                        int rd = tcp_client.GetStream().Read(data, 0, ava > data.Length ? data.Length : ava);
                        //string txt = System.Text.Encoding.GetEncoding(1251).GetString(data, 0, rd);
                        //string[] lines = txt.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        //foreach (string line in lines)
                        //    do_incoming(line);
                    };
                }
                catch
                {
                    tcp_client.Close();
                    tcp_client = new TcpClient();
                    Thread.Sleep(1000);
                    continue;
                };
                // -- READ APRS-IS -- //                        

                // ++ PING MESSAGE ++ //
                try
                {
                    if (120 == reping)
                    {
                        reping = 0;
                        SendCommand(this.message_ping_text);                        
                    };
                }
                catch
                {
                    tcp_client.Close();
                    tcp_client = new TcpClient();
                    Thread.Sleep(1000);
                    continue;
                };
                // -- PING MESSAGE -- //

                Thread.Sleep(500);
                reping++;
            };
        }        

        private void do_incoming(string line)
        {                        
            // PARSE PACKET TYPE //
            // :! - Position without timestamp [OK]
            // :" :( :- :\ :] :^ - Unused
            // :$ - RAW GPS [OK] 
            // :) - Item
            // :/ - Position with Timestamp [OK]
            // :: - Message
            // :; - Object
            // := - Position without timestamp + APRS message [OK]
            // :> - Status
            // :? - query
            // :@ - Position with timestamp + APRS message [OK]
            // :T - Telemetry
            // :[ - grid locator beacon
            // :_ - wheather report (page 19, 62) [OK]
            // :} - traffic

            Console.WriteLine("{i:tcp:#} " + line);
            bool isComment = line.IndexOf("#") == 0;
            if (!isComment)
            {
                APRSParser parser = new APRSParser();
                parser.Parse(line);
                // On Good Packet
                if ((parser.Callsign != "") && (parser.Callsign != "Unknown"))
                    OnPacket(line, parser);
                // IF LOCATION PACKET //
                if ((parser.PacketType == "Location") || (parser.PacketType == "GPGGA"))
                    OnLocation(line, parser);
                // IF WEATHER PACKET //
                if (parser.PacketType == "Weather Report")
                    OnWeather(line, parser);
            };
        }

        public void SendToServer(string line)
        {
            if (Connected)
            {
                string txt2send = line + "\r\n";
                byte[] arr = System.Text.Encoding.ASCII.GetBytes(txt2send);
                try { tcp_client.GetStream().Write(arr, 0, arr.Length); } catch { };
            };
        }        

        #region OnIncomingPacket
        public void OnWeather(string line, APRSParser data)
        {
         
        }

        public void OnLocation(string line, APRSParser data)
        {
         
        }
        
        public void OnPacket(string line, APRSParser data)
        {

        }
        #endregion

        private void SendCommand(string cmd)
        {
            SendCommand(cmd, callsign);
        }
        private void SendCommand(string cmd, string fromCallsign)
        {
            if (Connected)
            {
                string txt2send = fromCallsign + ">" + cmd + "\r\n";
                byte[] arr = System.Text.Encoding.GetEncoding(1251).GetBytes(txt2send);
                try { tcp_client.GetStream().Write(arr, 0, arr.Length); } catch { };
            };
        }

        #region static
        public static int CallsignChecksum(string callsign)
        {
            int stophere = callsign.IndexOf("-");
            if (stophere > 0) callsign = callsign.Substring(0, stophere);
            string realcall = callsign.ToUpper();
            while (realcall.Length < 10) realcall += " ";

            // initialize hash 
            int hash = 0x73e2;
            int i = 0;
            int len = realcall.Length;

            // hash callsign two bytes at a time 
            while (i < len)
            {
                hash ^= (int)(realcall.Substring(i, 1))[0] << 8;
                hash ^= (int)(realcall.Substring(i + 1, 1))[0];
                i += 2;
            }
            // mask off the high bit so number is always positive 
            return hash & 0x7fff;
        }

        // http://www.aprs-is.net/SendOnlyPorts.aspx
        public static void SendUDP(string host, int port, string data)
        {
            UdpClient udp = new UdpClient();
            udp.Connect(host, port);
            byte[] dt = System.Text.Encoding.GetEncoding(1251).GetBytes(data);
            udp.Send(dt, dt.Length);
            udp.Close();
        }

        // http://www.aprs-is.net/SendOnlyPorts.aspx
        public static void SendHTTP(string host, int port, string data)
        {
            HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create("http://" + host + ":" + port.ToString());
            wreq.Method = "POST";
            wreq.Accept = "text/plain";
            wreq.ContentType = "application/octet-stream";
            wreq.KeepAlive = false;
            byte[] ba = System.Text.Encoding.GetEncoding(1251).GetBytes(data);
            wreq.ContentLength = ba.Length;
            wreq.GetRequestStream().Write(ba, 0, ba.Length);
            HttpWebResponse wres = (HttpWebResponse)wreq.GetResponse();
            wres.Close();
        }

        public static string SendHTTP(string url)
        {
            HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse wres = (HttpWebResponse)wreq.GetResponse();
            StreamReader sr = new StreamReader(wres.GetResponseStream());
            string res = sr.ReadToEnd();
            sr.Close();
            wres.Close();
            return res;
        }
        #endregion
    }
}
