/*********************************************/
/*                                           */
/*                                           */
/*  Author Milok Zbrozek (milokz@gmail.com)  */
/*                                           */
/*                                           */
/*     Simple TCP/UDP/HTTP Servers Class     */
/*        last moidified 04.08.2021          */
/*              version 0.2B                 */
/*                                           */
/*                                           */
/* Supports:                                 */
/*                                           */
/*  - Browsing files and directories         */
/*  - index.html                             */
/*  - CGI-BIN                                */
/*                                           */
/*********************************************/


using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using System.Security;
using System.Security.Cryptography;

namespace TCPListener
{
    public enum ServerState
    {
        ssStopped = 0,
        ssStarting = 1,
        ssRunning = 2,
        ssStopping = 3
    }

    /// <summary>
    ///     Базовые методы серверов
    /// </summary>
    public interface IServer
    {        
        void Start();
        void Stop();
        ServerState GetState();
        int GetServerPort();
        Exception GetLastError();
    }

    /// <summary>
    ///     Базовый класс для TCP серверов
    /// </summary>
    public abstract class TTCPServer : IServer, IDisposable
    {
        protected Thread mainThread = null;
        protected TcpListener mainListener = null;

        /// <summary>
        ///     Listen IP Address
        /// </summary>
        protected IPAddress ListenIP = IPAddress.Any;
        /// <summary>
        ///     Listen IP Address
        /// </summary>
        public IPAddress ServerIP { get { return ListenIP; } }

        /// <summary>
        ///     Listen Port Number
        /// </summary>
        protected int ListenPort = 5000;
        /// <summary>
        ///     Listen Port Number
        /// </summary>
        public int ServerPort { get { return ListenPort; } set { if (isRunning) throw new Exception("Server is running"); ListenPort = value; } }
        /// <summary>
        ///     Listen Port Number
        /// </summary>
        /// <returns></returns>
        public int GetServerPort() { return ListenPort; }

        /// <summary>
        ///     Server is running or not
        /// </summary>
        protected bool isRunning = false;
        /// <summary>
        ///     Server is running or not
        /// </summary>
        public bool Running { get { return isRunning; } }
        /// <summary>
        ///     Get Servers State
        /// </summary>
        /// <returns></returns>
        public ServerState GetState() { if (isRunning) return ServerState.ssRunning; else return ServerState.ssStopped; }

        /// <summary>
        ///     Last Thread Error
        /// </summary>
        protected Exception LastError = null;
        /// <summary>
        ///     Last Thread Error
        /// </summary>
        public Exception GetLastError() { return LastError; }

        /// <summary>
        ///     Last Error Time
        /// </summary>
        protected DateTime LastErrTime = DateTime.MaxValue;
        /// <summary>
        ///     Last Error Time
        /// </summary>
        public DateTime LastErrorTime { get { return LastErrTime; } }

        /// <summary>
        ///     Get Total Error Count
        /// </summary>
        protected uint ErrorsCounter = 0;
        /// <summary>
        ///     Get Total Error Count
        /// </summary>
        public uint GetErrorsCount { get { return ErrorsCounter; } }

        /// <summary>
        ///     Total Clients Counter
        /// </summary>
        protected ulong counter = 0;
        /// <summary>
        ///     Total Clients Counter
        /// </summary>
        public ulong ClientsCounter { get { return counter; } }

        /// <summary>
        ///     Connected clients counter
        /// </summary>
        protected ulong alive = 0;
        /// <summary>
        ///     Connected clients counter
        /// </summary>
        public ulong ClientsAlive { get { return alive; } }

        /// <summary>
        ///     Client Read Timeout in seconds, default 10
        /// </summary>
        protected int readTimeout = 10; // 10 sec
        /// <summary>
        ///     Client Read Timeout in seconds, default 10
        /// </summary>
        public int ReadTimeout { get { return readTimeout; } set { readTimeout = value; } }

        public TTCPServer() { }
        public TTCPServer(int Port) { this.ListenPort = Port; }
        public TTCPServer(IPAddress IP, int Port) { this.ListenIP = IP; this.ListenPort = Port; }

        /// <summary>
        ///     Start Server
        /// </summary>
        public virtual void Start() { }

        /// <summary>
        ///     Stop Server
        /// </summary>
        public virtual void Stop() { }

        /// <summary>
        ///     Stop Server and Dipose
        /// </summary>
        public virtual void Dispose() { this.Stop(); }

        /// <summary>
        ///     Accept TCP Client
        /// </summary>
        /// <param name="client"></param>
        /// <returns>true - connect, false - ignore</returns>
        protected virtual bool AcceptClient(TcpClient client) { return true; }

        /// <summary>
        ///     Get Client Connection
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="clientID">Client Number</param>
        protected virtual void GetClient(TcpClient Client, ulong clientID) { }

        /// <summary>
        ///     On Error
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="clientID">Client Number</param>
        /// <param name="error"></param>
        protected virtual void onError(TcpClient Client, ulong clientID, Exception error) { throw error; }

        /// <summary>
        ///     Is Connection Alive?
        /// </summary>
        /// <param name="Client"></param>
        /// <returns></returns>
        public static bool IsConnected(TcpClient Client)
        {
            if (!Client.Connected) return false;
            if (Client.Client.Poll(0, SelectMode.SelectRead))
            {
                byte[] buff = new byte[1];
                try
                {
                    if (Client.Client.Receive(buff, SocketFlags.Peek) == 0)
                        return false;
                }
                catch
                {
                    return false;
                };
            };
            return true;
        }

        /// <summary>
        ///     Get Client HTTP Headers
        /// </summary>
        /// <param name="Header">client header</param>
        /// <returns>header list</returns>
        public static IDictionary<string, string> GetClientHeaders(string Header)
        {
            if (String.IsNullOrEmpty(Header)) return null;

            Dictionary<string, string> clHeaders = new Dictionary<string, string>();
            Regex rx = new Regex(@"([\w-]+): (.*)", RegexOptions.IgnoreCase);
            try
            {
                MatchCollection mc = rx.Matches(Header);
                foreach (Match mx in mc)
                {
                    string val = mx.Groups[2].Value.Trim();
                    if (!clHeaders.ContainsKey(mx.Groups[1].Value))
                        clHeaders.Add(mx.Groups[1].Value, val);
                    else
                        clHeaders[mx.Groups[1].Value] += val;
                };
            }
            catch { };
            return clHeaders;
        }

        /// <summary>
        ///     Get client HTTP query
        /// </summary>
        /// <param name="Header">header</param>
        /// <param name="host">server:ip</param>
        /// <param name="page">server page</param>
        /// <param name="sParameters">string params</param>
        /// <param name="lParameters">list params</param>
        /// <returns>query string</returns>
        public static string GetClientQuery(string Header, out string host, out string page, out string sParameters, out IDictionary<string, string> lParameters)
        {
            host = null;
            Regex rx = new Regex(@"Host: (.*)", RegexOptions.IgnoreCase);
            Match mx = rx.Match(Header);
            if (mx.Success) host = mx.Groups[1].Value.Trim();

            page = null;
            lParameters = null;
            sParameters = null;
            if (String.IsNullOrEmpty(Header)) return null;

            string query = "";
            rx = new Regex("^(?:PUT|POST|GET) (.*) HTTP", RegexOptions.IgnoreCase); // "^(?:PUT|POST|GET) (.*) HTTP" or @"^(?:PUT|POST|GET) (\/?[\w\.?=%&=\-@/$,]*) HTTP"
            mx = rx.Match(Header);
            if (mx.Success) query = UrlUnescape(mx.Groups[1].Value);
            if (query != null)
            {
                rx = new Regex(@"^(?<page>[\[\]+!_\(\)\s\w\.=%=\-@/$,]*)?", RegexOptions.None);
                mx = rx.Match(query);
                if (mx.Success) page = mx.Groups["page"].Value;

                rx = new Regex(@"(?:[\?&](.*))", RegexOptions.None);
                mx = rx.Match(query);
                if (mx.Success)  sParameters = mx.Groups[1].Value;
                
                rx = new Regex(@"([\?&]((?<name>[^&=]+)=(?<value>[^&=]+)))", RegexOptions.None);
                MatchCollection mc = rx.Matches(query);
                if (mc.Count > 0)
                {
                    lParameters = new Dictionary<string, string>();
                    foreach (Match m in mc)
                    {
                        string n = m.Groups["name"].Value;
                        string v = m.Groups["value"].Value;
                        if (lParameters.ContainsKey(n))
                            lParameters[n] += "," + v;
                        else
                            lParameters.Add(n, v);
                    };
                };
            };
            return query;
        }

        /// <summary>
        ///     Get client GET/POST Query Parameters
        /// </summary>
        /// <param name="Query"></param>
        /// <returns></returns>
        public static IDictionary<string, string> GetClientParams(string query)
        {
            if (String.IsNullOrEmpty(query)) return null;
            Dictionary<string, string> lParameters = new Dictionary<string, string>();

            Regex rx = new Regex(@"([\?&]*((?<name>[^&=]+)=(?<value>[^&=]+)))", RegexOptions.None);
            MatchCollection mc = rx.Matches(query);
            if (mc.Count > 0)
            {
                lParameters = new Dictionary<string, string>();
                foreach (Match m in mc)
                {
                    string n = UrlUnescape(m.Groups["name"].Value);
                    string v = UrlUnescape(m.Groups["value"].Value);
                    if (lParameters.ContainsKey(n))
                        lParameters[n] += "," + v;
                    else
                        lParameters.Add(n, v);
                };
            };
            return lParameters;
        }

        /// <summary>
        ///     Dictionary has cas-ignored key
        /// </summary>
        /// <param name="dict">dictionary</param>
        /// <param name="key">key to find</param>
        /// <returns>has key</returns>
        public static bool DictHasKeyIgnoreCase(IDictionary<string, string> dict, string key)
        {
            if (dict == null) return false;
            if (dict.Count == 0) return false;
            foreach (string k in dict.Keys)
                if (string.Compare(k, key, true) == 0)
                    return true;
            return false;
        }

        /// <summary>
        ///     Get Dictionary Value by Key ignoring case
        /// </summary>
        /// <param name="dict">dictionary</param>
        /// <param name="key">key to find</param>
        /// <returns>key value</returns>
        public static string DictGetKeyIgnoreCase(IDictionary<string, string> dict, string key)
        {
            if (dict == null) return null;
            if (dict.Count == 0) return null;
            foreach (string k in dict.Keys)
                if (string.Compare(k, key, true) == 0)
                    return dict[k];
            return null;
        }

        /// <summary>
        ///     Encode string to base64
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        ///     Decode base64 to string
        /// </summary>
        /// <param name="base64EncodedData"></param>
        /// <returns></returns>
        public static string Base64Decode(string base64EncodedData)
        {
            byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        ///     Code string with key
        /// </summary>
        /// <param name="clearText"></param>
        /// <param name="EncryptionKey"></param>
        /// <returns></returns>
        public static string CodeInString(string clearText, string EncryptionKey)
        {
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (System.Security.Cryptography.SymmetricAlgorithm encryptor = System.Security.Cryptography.SymmetricAlgorithm.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        /// <summary>
        ///     decode string with key
        /// </summary>
        /// <param name="cipherText"></param>
        /// <param name="EncryptionKey"></param>
        /// <returns></returns>
        public static string CodeOutString(string cipherText, string EncryptionKey)
        {
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (System.Security.Cryptography.SymmetricAlgorithm encryptor = System.Security.Cryptography.SymmetricAlgorithm.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(EncryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    cipherText = Encoding.Unicode.GetString(ms.ToArray());
                }
            }
            return cipherText;
        }

        public static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value /
                        Math.Pow(1024, i)) +
                        " " + suffixes[i];
                };
            };
            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
        }

        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
            {
                // No digits after the decimal.
                return value.ToString("0,0");
            }
            else if (value >= 10)
            {
                // One digit after the decimal.
                return value.ToString("0.0");
            }
            else
            {
                // Two digits after the decimal.
                return value.ToString("0.00");
            }
        }

        protected static char Get1251Char(byte b)
        {
            return (System.Text.Encoding.GetEncoding(1251).GetString(new byte[] { b }, 0, 1))[0];
        }

        public static string UrlEscape(string str)
        {
            return System.Uri.EscapeDataString(str.Replace("+", "%2B"));
        }

        public static string UrlUnescape(string str)
        {
            return System.Uri.UnescapeDataString(str).Replace("%2B", "+");
        }

        /// <summary>
        ///     Получение папки, из которой запущено приложение
        /// </summary>
        /// <returns>Полный путь к папки с \ на конце</returns>
        public static string GetCurrentDir()
        {
            string fname = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase.ToString();
            fname = fname.Replace("file:///", "");
            fname = fname.Replace("/", @"\");
            fname = fname.Substring(0, fname.LastIndexOf(@"\") + 1);
            return fname;
        }

        public static string GetMemeType(string fileExt)
        {
            // https://snipp.ru/handbk/mime-list
            switch (fileExt)
            {
                case ".pdf": return "application/pdf";
                case ".djvu": return "image/vnd.djvu";
                case ".zip": return "application/zip";
                case ".doc": return "application/msword";
                case ".docx": return "application/msword";
                case ".mp3": return "audio/mpeg";
                case ".m3u": return "audio/x-mpegurl";
                case ".wav": return "audio/x-wav";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".psd": return "image/vnd.adobe.photoshop";
                case ".jpg": return "image/jpeg";
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".svg": return "image/svg";
                case ".tiff": return "image/tiff";
                case ".css": return "text/css";
                case ".csv": return "text/csv";
                case ".html": return "text/html";
                case ".htmlx": return "text/html";
                case ".dhtml": return "text/html";
                case ".xhtml": return "text/html";
                case ".js": return "application/javascript";
                case ".json": return "application/json";
                case ".txt": return "text/plain";
                case ".md": return "text/plain";
                case ".php": return "text/php";
                case ".xml": return "text/xml";
                case ".mpg": return "video/mpeg";
                case ".mpeg": return "video/mpeg";
                case ".mp4": return "video/mp4";
                case ".ogg": return "video/ogg";
                case ".avi": return "video/x-msvideo";
                case ".rar": return "application/x-rar-compresse";
                default: return "application/octet-stream";
            };
        }
    }

    /// <summary>
    ///     Простейший однопоточный TCP-сервер
    /// </summary>
    public class SingledTCPServer: TTCPServer
    {
        /// <summary>
        ///     Close connection when GetClient method completes
        /// </summary>
        protected bool _closeOnGetClientCompleted = true;

        /// <summary>
        ///     Close connection when GetClient method completes
        /// </summary>
        public bool CloseConnectionOnGetClientCompleted { get { return _closeOnGetClientCompleted; } set { _closeOnGetClientCompleted = value; } }

        public SingledTCPServer() { }
        public SingledTCPServer(int Port) { this.ListenPort = Port; }
        public SingledTCPServer(IPAddress IP, int Port) { this.ListenIP = IP; this.ListenPort = Port; }
        ~SingledTCPServer() { this.Dispose(); }

        public override void Start()
        {
            if (isRunning) throw new Exception("Server Already Running!");

            try
            {
                mainListener = new TcpListener(this.ListenIP, this.ListenPort);
                mainListener.Start();
            }
            catch (Exception ex)
            {
                LastError = ex;
                ErrorsCounter++;
                throw ex;
            };

            mainThread = new Thread(MainThread);
            mainThread.Start();
        }

        public override void Stop()
        {
            if (!isRunning) return;

            isRunning = false;

            if (mainListener != null) mainListener.Stop();
            mainListener = null;

            mainThread.Join();
            mainThread = null;
        }           
                
        private void MainThread()
        {
            isRunning = true;            
            while (isRunning)
            {
                try
                {
                    TcpClient client = mainListener.AcceptTcpClient();
                    if (client == null) continue;

                    if (!AcceptClient(client))
                    {
                        client.Client.Close();
                        client.Close();
                        continue;
                    };
                    
                    ulong id = 0;
                    try 
                    {
                        alive++;
                        client.GetStream().ReadTimeout = this.readTimeout * 1000;                        
                        GetClient(client, id = this.counter++); 
                    } 
                    catch (Exception ex) 
                    {
                        try
                        {
                            ErrorsCounter++;
                            LastError = ex;
                            onError(client, id, ex);
                        }
                        catch { };
                    };
                    if(_closeOnGetClientCompleted)
                        CloseClient(client, id);                   
                }
                catch (Exception ex) 
                {
                    LastError = ex;
                    ErrorsCounter++;
                };
                Thread.Sleep(1);
            };
        }

        protected override void GetClient(TcpClient Client, ulong clientID) 
        { 
            //
            // do something
            //

            if (!this._closeOnGetClientCompleted) CloseClient(Client, clientID);
        }

        protected void CloseClient(TcpClient Client, ulong clientID)
        {
            try 
            {
                alive--;
                Client.Client.Close();
                Client.Close(); 
            }
            catch { };      
        }
    }       

    /// <summary>
    ///     Простейший однопоточный TCP-сервер, который принимает текст
    /// </summary>
    public class SingledTextTCPServer : SingledTCPServer
    {
        public SingledTextTCPServer() : base() { }
        public SingledTextTCPServer(int Port) : base(Port) { }
        public SingledTextTCPServer(IPAddress IP, int Port) : base(IP, Port) { }
        ~SingledTextTCPServer() { this.Dispose(); }

        protected bool _OnlyHTTP = false;
        public virtual bool OnlyHTTPClients { get { return _OnlyHTTP; } set { _OnlyHTTP = value; } }
        protected uint _MaxHeaderSize = 4096;
        public uint MaxClientHeaderSize { get { return _MaxHeaderSize; } set { _MaxHeaderSize = value; } }
        protected uint _MaxBodySize = 65536;
        public uint MaxClientBodySize { get { return _MaxBodySize; } set { _MaxBodySize = value; } }
        protected Encoding _responseEnc = Encoding.GetEncoding(1251);
        public Encoding ResponseEncoding { get { return _responseEnc; } set { _responseEnc = value; } }
        protected Encoding _requestEnc = Encoding.GetEncoding(1251);
        public Encoding RequestEncoding { get { return _requestEnc; } set { _requestEnc = value; } }
        
        protected override void GetClient(TcpClient Client, ulong clientID)
        {
            Regex CR = new Regex(@"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            string Request = "";
            string Header = null;
            List<byte> Body = new List<byte>();

            int bRead = -1;
            int posCRLF = -1;
            int receivedBytes = 0;
            int contentLength = 0;
            
            // Get Header
            //while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) // doesn't work correct
            while ((bRead = Client.GetStream().ReadByte()) >= 0)
            {
                receivedBytes++;
                Body.Add((byte)bRead);

                // Check GET or POST
                if (_OnlyHTTP && (receivedBytes == 1))
                    if ((bRead != 0x47) && (bRead != 0x50))
                    {
                        if (!this._closeOnGetClientCompleted) CloseClient(Client, clientID);
                        return;
                    };
                
                Request += (char)bRead; // standard symbol
                if (bRead == 0x0A) posCRLF = Request.IndexOf("\r\n\r\n"); // get body start index
                if (posCRLF >= 0 || Request.Length > _MaxHeaderSize) { break; }; // GET ONLY                    
            };

            bool valid = (posCRLF > 0);
            if ((!valid) && _OnlyHTTP)
            {
                if (!this._closeOnGetClientCompleted) CloseClient(Client, clientID);
                return;
            };

            // Get Body
            if(valid)
            {
                Body.Clear();
                Header = Request;                
                Match mx = CR.Match(Request);
                if (mx.Success) contentLength = int.Parse(mx.Groups[1].Value);
                int total2read = posCRLF + 4 + contentLength;
                while ((receivedBytes < total2read) && ((bRead = Client.GetStream().ReadByte()) >= 0))
                {
                    receivedBytes++;
                    Body.Add((byte)bRead);

                    string rcvd = _requestEnc.GetString(new byte[] { (byte)bRead }, 0, 1);
                    Request += rcvd;
                    if (Request.Length > _MaxBodySize) { break; };
                };
            };

            GetClientRequest(Client, clientID, Request, Header, Body.ToArray());
        }

        /// <summary>
        ///     Get Client with Request Text Data
        /// </summary>
        /// <param name="Client">socket</param>
        /// <param name="clientID">number</param>
        /// <param name="Request">Request</param>
        /// <param name="Header">Header</param>        
        /// <param name="Body">Body</param>
        /// <param name="BodyData">Body</param>
        protected virtual void GetClientRequest(TcpClient Client, ulong clientID, string Request, string Header, byte[] Body)
        {
            string proto = "tcp://" + Client.Client.RemoteEndPoint.ToString() + "/text/";

            //
            // do something
            //

            if (!this._closeOnGetClientCompleted) CloseClient(Client, clientID);
        }
    }    
    
    /// <summary>
    ///     Простейший HTTP-сервер
    /// </summary>
    public class SingledHttpServer : SingledTextTCPServer
    {
        public SingledHttpServer() : base(80) { this._closeOnGetClientCompleted = true; this._OnlyHTTP = true; }
        public SingledHttpServer(int Port) : base(Port) { this._closeOnGetClientCompleted = true; this._OnlyHTTP = true; }
        public SingledHttpServer(IPAddress IP, int Port) : base(IP, Port) { this._closeOnGetClientCompleted = true; this._OnlyHTTP = true; }
        ~SingledHttpServer() { this.Dispose(); }

        protected Mutex _h_mutex = new Mutex();
        protected Dictionary<string, string> _headers = new Dictionary<string, string>();
        public Dictionary<string, string> Headers
        {
            get
            {
                _h_mutex.WaitOne();
                Dictionary<string, string> res = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kvp in _headers)
                    res.Add(kvp.Key, kvp.Value);
                _h_mutex.ReleaseMutex();
                return res;
            }
            set
            {
                _h_mutex.WaitOne();
                _headers.Clear();
                foreach (KeyValuePair<string, string> kvp in value)
                    _headers.Add(kvp.Key, kvp.Value);
                _h_mutex.ReleaseMutex();
            }
        }        

        public virtual void HttpClientSendError(TcpClient Client, int Code, Dictionary<string, string> dopHeaders)
        {
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            // Код простой HTML-странички
            string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string Str = "HTTP/1.1 " + CodeStr + "\r\n";
            this._h_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._h_mutex.ReleaseMutex();
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            Str += "Content-type: text/html\r\nContent-Length: " + Html.Length.ToString() + "\r\n\r\n" + Html;
            // Приведем строку к виду массива байт
            byte[] Buffer = Encoding.GetEncoding(1251).GetBytes(Str);
            // Отправим его клиенту
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.GetStream().Flush();
            Client.Client.Close();
            Client.Close();
        }
        public virtual void HttpClientSendError(TcpClient Client, int Code)
        {
            HttpClientSendError(Client, Code, null);
        }
        public virtual void HttpClientSendText(TcpClient Client, string Text, Dictionary<string, string> dopHeaders)
        {
            // Код простой HTML-странички
            string body = "<html><body>" + Text + "</body></html>";
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string header = "HTTP/1.1 200\r\n";

            this._h_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._h_mutex.ReleaseMutex();

            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            
            byte[] bData = _responseEnc.GetBytes(body);
            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-type"))
                header += "Content-type: text/html\r\n";
            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-Length"))
                header += "Content-Length: " + bData.Length.ToString() + "\r\n";
            header += "\r\n";

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.GetEncoding(1251).GetBytes(header));
            response.AddRange(bData);

            Client.GetStream().Write(response.ToArray(), 0, response.Count);
            Client.GetStream().Flush();
            Client.Client.Close();
            Client.Close();
        }
        public virtual void HttpClientSendText(TcpClient Client, string Text)
        {
            HttpClientSendText(Client, Text, null);
        }

        protected override void GetClientRequest(TcpClient Client, ulong clientID, string Request, string Header, byte[] Body)
        {
            IDictionary<string, string> clHeaders = GetClientHeaders(Header);
            string page, host, inline;
            IDictionary<string, string> parameters;
            string query = GetClientQuery(Header, out host, out page, out inline, out parameters);
            HttpClientSendError(Client, 501);
            if (!this._closeOnGetClientCompleted) CloseClient(Client, clientID);
        }
    }

    #region SimpleUDP
    /// <summary>
    ///     Простейший UDP-сервер
    /// </summary>
    public class SimpleUDPServer : IServer, IDisposable
    {
        private Thread mainThread = null;
        private Socket udpSocket = null;
        private IPAddress ListenIP = IPAddress.Any;
        private int ListenPort = 5000;
        private bool isRunning = false;
        private int _bufferSize = 4096;
        protected Exception LastError = null;
        protected uint ErrorsCounter = 0;

        public SimpleUDPServer() { }
        public SimpleUDPServer(int Port) { this.ListenPort = Port; }
        public SimpleUDPServer(IPAddress IP, int Port) { this.ListenIP = IP; this.ListenPort = Port; }
        ~SimpleUDPServer() { Dispose(); }        

        public bool Running { get { return isRunning; } }
        public ServerState GetState() { if (isRunning) return ServerState.ssRunning; else return ServerState.ssStopped; }
        public IPAddress ServerIP { get { return ListenIP; } }
        public int ServerPort { get { return ListenPort; } }
        public int GetServerPort() { return ListenPort; }
        public int BufferSize { get { return _bufferSize; } set { _bufferSize = value; } }
        public Exception GetLastError() { return LastError; }
        public uint GetErrorsCount { get { return ErrorsCounter; } }

        public void MainThread()
        {
            isRunning = true;

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint Remote = (EndPoint)(sender);

            //byte[] data1 = Encoding.GetEncoding(1251).GetBytes("Hello");
            //udpSocket.SendTo(data1, data1.Length, SocketFlags.None, new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ListenPort));

            while (isRunning)
            {
                try
                {
                    byte[] data = new byte[_bufferSize];
                    int recv = udpSocket.ReceiveFrom(data, ref Remote);
                    if (recv > 0) ReceiveBuff(Remote, data, recv);
                }
                catch (Exception ex)
                {
                    try 
                    {
                        ErrorsCounter++;
                        LastError = ex; 
                        onError(ex); 
                    } 
                    catch { };
                };
                Thread.Sleep(1);
            };
        }

        public virtual void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            udpSocket.Close();
            mainThread.Join();

            udpSocket = null;
            mainThread = null;
        }

        public virtual void Start()
        {
            if (isRunning) throw new Exception("Server Already Running!");
            
            try
            {
                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint ipep = new IPEndPoint(this.ListenIP, this.ListenPort);
                udpSocket.Bind(ipep);
            }
            catch (Exception ex)
            {
                ErrorsCounter++;
                LastError = ex;
                throw ex;
            };

            mainThread = new Thread(MainThread);
            mainThread.Start();
        }

        public virtual void Dispose() { Stop(); } 

        protected virtual void onError(Exception ex)
        {

        }

        protected virtual void ReceiveBuff(EndPoint Client, byte[] data, int length)
        {
            // do anything
        }
    }

    /// <summary>
    ///     Простейший UDP-сервер, который принимает текст
    /// </summary>
    public class SimpleTextUDPServer : SimpleUDPServer
    {
        public SimpleTextUDPServer() : base() { }
        public SimpleTextUDPServer(int Port) : base(Port) { }
        public SimpleTextUDPServer(IPAddress IP, int Port) : base(IP, Port) { }
        ~SimpleTextUDPServer() { this.Dispose(); }

        protected override void ReceiveBuff(EndPoint Client, byte[] data, int length)
        {
            string Request = System.Text.Encoding.GetEncoding(1251).GetString(data, 0, length);
            ReceiveData(Client, Request);
        }

        protected virtual void ReceiveData(EndPoint Client, string Request)
        {
            string proto = "udp://" + Client.ToString() + "/text/";
            // do anything
        }
    }
    #endregion    

    /// <summary>
    ///     Многопоточный TCP-сервер
    /// </summary>
    public class ThreadedTCPServer : TTCPServer
    {
        private class ClientTCPSInfo
        {
            public ulong id;
            public TcpClient client;
            public Thread thread;

            public ClientTCPSInfo(TcpClient client, Thread thread)
            {
                this.client = client;
                this.thread = thread;
            }
        }

        /// <summary>
        ///     Working Mode
        /// </summary>
        public enum Mode: byte
        {
            /// <summary>
            ///     Allow all connections
            /// </summary>
            NoRules = 0,
            /// <summary>
            ///     Allow only specified connections
            /// </summary>
            AllowWhiteList = 1,
            /// <summary>
            ///     Allow all but black list
            /// </summary>
            DenyBlackList = 2
        }
        
        /// <summary>
        ///     Started
        /// </summary>
        private DateTime _started = DateTime.MinValue;
        /// <summary>
        ///     Started
        /// </summary>
        public DateTime Started { get { return isRunning ? _started : DateTime.MinValue; } }

        /// <summary>
        ///     Started
        /// </summary>
        private DateTime _stopped = DateTime.MaxValue;
        /// <summary>
        ///     Started
        /// </summary>
        public DateTime Stopped { get { return isRunning ? DateTime.MaxValue : _stopped; } }

        /// <summary>
        ///     Server Mode
        /// </summary>
        private Mode ipmode = Mode.NoRules;
        /// <summary>
        ///     Server Access by IP Rules Mode
        /// </summary>
        public Mode ListenIPMode { get { return ipmode; } set { ipmode = value; } }

        private Mutex iplistmutex = new Mutex();
        /// <summary>
        ///     IP white list (supporting *: 192.168.10.*)
        /// </summary>
        private List<string> ipwhitelist = new List<string>();
        /// <summary>
        ///     IP white List (supporting *: 192.168.10.*)
        /// </summary>
        public string[] ListenIPAllow 
        { 
            get 
            { 
                iplistmutex.WaitOne();
                string[] res = ipwhitelist.ToArray();
                iplistmutex.ReleaseMutex();
                return res;
            }
            set 
            {
                iplistmutex.WaitOne();
                ipwhitelist.Clear();
                if (value != null)
                    ipwhitelist.AddRange(value);
                iplistmutex.ReleaseMutex();
            }
        }

        /// <summary>
        ///     IP black list (supporting *: 192.168.10.*)
        /// </summary>
        private List<string> ipblacklist = new List<string>();
        /// <summary>
        ///     IP black list (supporting *: 192.168.10.*)
        /// </summary>
        public string[] ListenIPDeny
        {
            get
            {
                iplistmutex.WaitOne();
                string[] res = ipblacklist.ToArray();
                iplistmutex.ReleaseMutex();
                return res;
            }
            set
            {
                iplistmutex.WaitOne();
                ipblacklist.Clear();
                if (value != null)
                    ipblacklist.AddRange(value);
                iplistmutex.ReleaseMutex();
            }
        }
        
        /// <summary>
        ///     Max Clients Count
        /// </summary>
        private ushort maxClients = 50;
        /// <summary>
        ///     Max connected clients count
        /// </summary>
        public ushort MaxClients { get { return maxClients; } set { maxClients = value; } }

        /// <summary>
        ///     Abort client connection on stop
        /// </summary>
        private bool abortOnStop = false;
        /// <summary>
        ///     Abort client connections on stop
        /// </summary>
        public bool AbortOnStop { get { return abortOnStop; } set { abortOnStop = value; } }

        /// <summary>
        ///     Mutex for client dictionary
        /// </summary>
        private Mutex stack = new Mutex();
        /// <summary>
        ///     Client dictionary
        /// </summary>
        private Dictionary<ulong, ClientTCPSInfo> clients = new Dictionary<ulong, ClientTCPSInfo>();
        /// <summary>
        ///     Currect connected clients
        /// </summary>
        public KeyValuePair<ulong, TcpClient>[] Clients
        {
            get
            {
                this.stack.WaitOne();
                List<KeyValuePair<ulong, TcpClient>> res = new List<KeyValuePair<ulong, TcpClient>>();
                foreach (KeyValuePair<ulong, ClientTCPSInfo> kvp in this.clients)
                    res.Add(new KeyValuePair<ulong, TcpClient>(kvp.Key, kvp.Value.client));
                this.stack.ReleaseMutex();
                return res.ToArray();
            }
        }       

        public ThreadedTCPServer() { }
        public ThreadedTCPServer(int Port) { this.ListenPort = Port; }
        public ThreadedTCPServer(IPAddress IP, int Port) { this.ListenIP = IP; this.ListenPort = Port; }
        ~ThreadedTCPServer() { Dispose(); }        
        
        private bool AllowedByIPRules(TcpClient client)
        {
            if (ipmode != Mode.NoRules)
            {
                string remoteIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                iplistmutex.WaitOne();
                if (ipmode == Mode.AllowWhiteList)
                {
                    if ((ipwhitelist != null) && (ipwhitelist.Count > 0))
                    {
                        foreach(string ip in ipwhitelist)
                            if (ip.Contains("*"))
                            {
                                string nip = ip.Replace(".", @"\.").Replace("*",@"\d");
                                Regex ex = new Regex(nip, RegexOptions.None);
                                if (ex.Match(remoteIP).Success) 
                                    return true;
                            };
                    };
                    if ((ipwhitelist == null) || (ipwhitelist.Count == 0) || (!ipwhitelist.Contains(remoteIP)))
                    {
                        iplistmutex.ReleaseMutex();
                        return false;
                    };                    
                }
                else
                {
                    if ((ipblacklist != null) && (ipblacklist.Count > 0) && ipblacklist.Contains(remoteIP))
                    {
                        iplistmutex.ReleaseMutex();
                        return false;
                    };
                    if ((ipblacklist != null) && (ipblacklist.Count > 0))
                    {
                        foreach (string ip in ipblacklist)
                            if (ip.Contains("*"))
                            {
                                string nip = ip.Replace(".", @"\.").Replace("*", @"\d");
                                Regex ex = new Regex(nip, RegexOptions.None);
                                if (ex.Match(remoteIP).Success)
                                    return false;
                            };
                    };
                };
                iplistmutex.ReleaseMutex();
            };
            return true;
        }

        /// <summary>
        ///     Calls at server main listening thread not for each client
        ///     -- connection will be closed after return --
        /// </summary>
        /// <param name="Client"></param>
        protected virtual void GetBlockedClient(TcpClient Client)
        {
            // do nothing
            // close after return
        }

        private void MainThread()
        {            
            _started = DateTime.Now;
            isRunning = true;
            while (isRunning)
            {
                try
                {
                    bool allowed = false;
                    TcpClient client = mainListener.AcceptTcpClient();
                    if (!AcceptClient(client))
                    {
                        client.Client.Close();
                        client.Close();
                        continue;
                    };

                    allowed = AllowedByIPRules(client);
                    if (!allowed)
                    {
                        try
                        {
                            GetBlockedClient(client);
                            client.Client.Close();
                            client.Close();
                        }
                        catch { };
                        continue;
                    };

                    if (this.maxClients < 2) // single-threaded
                    {
                        RunClientThread(new ClientTCPSInfo(client, null));
                    }
                    else // multi-threaded
                    {
                        while ((this.alive >= this.maxClients) && isRunning) // wait for any closed thread
                            System.Threading.Thread.Sleep(5);
                        if (isRunning)
                        {
                            Thread thr = new Thread(RunThreaded);
                            thr.Start(new ClientTCPSInfo(client, thr));
                        };
                    };
                }
                catch { };
                Thread.Sleep(1);
            };
        }
        
        private void RunThreaded(object client)
        {
            RunClientThread((ClientTCPSInfo)client);
        }

        private void RunClientThread(ClientTCPSInfo Client)
        {
            this.alive++;
            Client.id = this.counter++;
            this.stack.WaitOne();
            this.clients.Add(Client.id, Client);
            this.stack.ReleaseMutex();
            try
            {
                Client.client.GetStream().ReadTimeout = this.readTimeout * 1000;
                GetClient(Client.client, Client.id);                
            }
            catch (Exception ex)
            {
                LastError = ex;
                LastErrTime = DateTime.Now;
                ErrorsCounter++;
                onError(Client.client, Client.id, ex);
            } 
            finally
            {
                try { Client.client.GetStream().Flush(); } catch { };
                try
                {                    
                    Client.client.Client.Close();
                    Client.client.Close();
                }
                catch { };
            };

            this.stack.WaitOne();
            if (this.clients.ContainsKey(Client.id))
                this.clients.Remove(Client.id);
            this.stack.ReleaseMutex();
            this.alive--;
        }

        /// <summary>
        ///  Start Server
        /// </summary>
        public override void Start()
        {
            if (isRunning) throw new Exception("Server Already Running!");

            try
            {
                mainListener = new TcpListener(this.ListenIP, this.ListenPort);
                mainListener.Start();
            }
            catch (Exception ex)
            {
                LastError = ex;
                LastErrTime = DateTime.Now;
                ErrorsCounter++;
                throw ex;
            };

            mainThread = new Thread(MainThread);
            mainThread.Start();
        }

        /// <summary>
        ///     Stop Server
        /// </summary>
        public override void Stop()
        {
            if (!isRunning) return;

            isRunning = false;

            if (this.abortOnStop)
            {
                this.stack.WaitOne();
                try
                {
                    foreach (KeyValuePair<ulong, ClientTCPSInfo> kvp in this.clients)
                    {
                        try { if (kvp.Value.thread != null) kvp.Value.thread.Abort(); }
                        catch { };
                        try { kvp.Value.client.Client.Close(); }
                        catch { };
                        try { kvp.Value.client.Close(); }
                        catch { };
                    };
                    this.clients.Clear();
                }
                catch { };
                this.stack.ReleaseMutex();
            };

            _stopped = DateTime.Now;

            if (mainListener != null) mainListener.Stop();
            mainListener = null;

            mainThread.Join();
            mainThread = null;
        }

        /// <summary>
        ///     Get Client, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        protected override void GetClient(TcpClient Client, ulong id)
        {
            // loop something  
            // connection will be close after return
        }                      
    }

    public class ClientExample4Bytes
    {
        public ClientExample4Bytes()
        {
            System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();
            client.Connect("127.0.0.1", 8011);

            // write
            List<byte> buff = new List<byte>();
            buff.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes("PROTOBUF+4"));
            buff.Add(0); buff.Add(0); buff.Add(0); buff.Add(0);
            client.GetStream().Write(buff.ToArray(), 0, buff.Count);
            client.GetStream().Flush();

            // read
            byte[] incb = new byte[14];
            int count = client.GetStream().Read(incb, 0, incb.Length);
            string prefix = System.Text.Encoding.GetEncoding(1251).GetString(incb, 0, 10);
            if (prefix != "PROTOBUF+4")
            {
                int length = System.BitConverter.ToInt32(incb, 10);
                incb = new byte[length];
                client.GetStream().Read(incb, 0, incb.Length);
            };
        }
    }    

    /// <summary>
    ///    Protobuf-сервер
    /// </summary>
    public class Threaded4BytesTCPServer : ThreadedTCPServer
    {
        //[ProtoContract]
        //public class Config
        //{
        //    [ProtoMember(1)]
        //    public string inputFileName = "";
        //    [ProtoMember(2)]
        //    public string outputFileName = AppDomain.CurrentDomain.BaseDirectory.Trim('\\') + @"\SHAPES\default.dbf";
        //    [ProtoMember(3)]
        //    public byte selector = 1;
        //    [ProtoMember(4)]
        //    public bool onlyHasName = false;
        //    [ProtoMember(5)]
        //    public Dictionary<string, string> onlyWithTags = new Dictionary<string, string>();
        //    [ProtoMember(7)]
        //    public DateTime onlyMdfAfter = DateTime.MinValue;
        //    [ProtoMember(10)]
        //    public List<int> onlyVersion = new List<int>();
        //};
        // ProtoBuf.Serializer.Serialize(Stream, object);
        // ProtoBuf.Serializer.Deserialize<BlobHeader>(new MemoryStream(data));

        public Threaded4BytesTCPServer() : base() { }
        public Threaded4BytesTCPServer(int Port) : base(Port) { }
        public Threaded4BytesTCPServer(IPAddress IP, int Port) : base(IP, Port) { }
        ~Threaded4BytesTCPServer() { this.Dispose(); }

        protected string _prefix = "PROTOBUF+4";
        public string MessagePrefix { get { return _prefix; } set { _prefix = value; } }

        /// <summary>
        ///     Get Client, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        protected override void GetClient(TcpClient Client, ulong id)
        {
            try
            {                
                // PROTOBUF + 0x00 0x00 0x00 0x00 // 4 bytes length of 1-st block data (BigEndian) // 0x04000000 means 4; 0x00000004 means 67108864                

                byte[] buff = new byte[this._prefix.Length + 4];
                Client.GetStream().Read(buff, 0, buff.Length);
                string prefix = System.Text.Encoding.GetEncoding(1251).GetString(buff, 0, this._prefix.Length);
                if (prefix != this._prefix) return;

                int length = System.BitConverter.ToInt32(buff, this._prefix.Length);
                buff = new byte[length];
                Client.GetStream().Read(buff, 0, buff.Length);
                GetClientData(Client, id, buff);
            }
            catch (Exception ex)
            {
                onError(Client, id, ex);
            };
        }

        /// <summary>
        ///     Get Client with 1st block of data
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        protected virtual void GetClientData(TcpClient Client, ulong id, byte[] data)
        {
            // Write No Data To Client

            byte[] prfx = System.Text.Encoding.GetEncoding(1251).GetBytes(this._prefix);
            Client.GetStream().Write(prfx, 0, prfx.Length);
            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("Hello " + DateTime.Now.ToString());
            Client.GetStream().Write(BitConverter.GetBytes(buff.Length), 0, 4);
            Client.GetStream().Write(buff, 0, buff.Length);
            Client.GetStream().Flush();

            // connection will be close after return       
        }
        
        protected override void onError(TcpClient Client, ulong id, Exception error)
        {

        }      

        private static void sample()
        {
            TCPListener.Threaded4BytesTCPServer srv = new TCPListener.Threaded4BytesTCPServer(8011);
            srv.ReadTimeout = 30;
            srv.Start();
            //
            System.Threading.Thread.Sleep(10000);
            //
            srv.Stop();
            srv.Dispose();
        }
    }

    /// <summary>
    ///     Многопоточный TCP-сервер, который принимает текст
    /// </summary>
    public class ThreadedTextTCPServer : ThreadedTCPServer
    {
        public ThreadedTextTCPServer() : base() { }
        public ThreadedTextTCPServer(int Port) : base(Port) { }
        public ThreadedTextTCPServer(IPAddress IP, int Port) : base(IP, Port) { }
        ~ThreadedTextTCPServer() { this.Dispose(); }

        protected bool _OnlyHTTP = false;
        public virtual bool OnlyHTTPClients { get { return _OnlyHTTP; } set { _OnlyHTTP = value; } }
        protected ushort _MaxHeaderSize = 4096;
        public ushort MaxClientHeaderSize { get { return _MaxHeaderSize; } set { _MaxHeaderSize = value; } }
        protected uint _MaxBodySize = 65536;
        public uint MaxClientBodySize { get { return _MaxBodySize; } set { _MaxBodySize = value; } }
        protected Encoding _responseEnc = Encoding.GetEncoding(1251);
        public Encoding ResponseEncoding { get { return _responseEnc; } set { _responseEnc = value; } }
        protected Encoding _requestEnc = Encoding.GetEncoding(1251);
        public Encoding RequestEncoding { get { return _requestEnc; } set { _requestEnc = value; } }

        /// <summary>
        ///     Get Client, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="clientID"></param>
        protected override void GetClient(TcpClient Client, ulong clientID)
        {
            Regex CR = new Regex(@"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            string Request = "";
            string Header = null;
            List<byte> Body = new List<byte>();

            int bRead = -1;
            int posCRLF = -1;
            int receivedBytes = 0;
            int contentLength = 0;

            // Get Header
            //while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) // doesn't work correct
            while ((bRead = Client.GetStream().ReadByte()) >= 0)
            {
                receivedBytes++;
                Body.Add((byte)bRead);

                // Check GET or POST
                if (_OnlyHTTP && (receivedBytes == 1))
                    if ((bRead != 0x47) && (bRead != 0x50))
                        return;

                Request += (char)bRead; // standard symbol
                if (bRead == 0x0A) posCRLF = Request.IndexOf("\r\n\r\n"); // get body start index
                if (posCRLF >= 0 || Request.Length > _MaxHeaderSize) { break; }; // GET ONLY                    
            };

            bool valid = (posCRLF > 0);
            if ((!valid) && _OnlyHTTP) return;

            // Get Body
            if (valid)
            {
                Body.Clear();
                Header = Request;
                Match mx = CR.Match(Request);
                if (mx.Success) contentLength = int.Parse(mx.Groups[1].Value);
                int total2read = posCRLF + 4 + contentLength;
                while ((receivedBytes < total2read) && ((bRead = Client.GetStream().ReadByte()) >= 0))
                {
                    receivedBytes++;
                    Body.Add((byte)bRead);

                    string rcvd = _requestEnc.GetString(new byte[] { (byte)bRead }, 0, 1);
                    Request += rcvd;
                    if (Request.Length > _MaxBodySize) { break; };
                };
            };

            GetClientRequest(Client, clientID, Request, Header, Body.ToArray());
        }

        /// <summary>
        ///     Get Client with Request Text Data
        /// </summary>
        /// <param name="Client">socket</param>
        /// <param name="clientID">number</param>
        /// <param name="Request">Request</param>
        /// <param name="Header">Header</param>        
        /// <param name="Body">Body</param>
        /// <param name="BodyData">Body</param>
        protected virtual void GetClientRequest(TcpClient Client, ulong clientID, string Request, string Header, byte[] Body)
        {
            //
            // loop something  
            // connection will be close after return   
            //         
        }             
    }

    /// <summary>
    ///     Многопоточный HTTP-сервер
    /// </summary>
    public class ThreadedHttpServer : ThreadedTCPServer
    {
        public ThreadedHttpServer() : base(80) { Init(); }
        public ThreadedHttpServer(int Port) : base(Port) { Init(); }
        public ThreadedHttpServer(IPAddress IP, int Port) : base(IP, Port) { Init(); }
        ~ThreadedHttpServer() { this.Dispose(); }        

        protected void Init() 
        { 
            _headers.Add("Server-Name", _serverName);
            //_headers.Add("Connection", "close"); 
        }        

        /// <summary>
        ///     Skip Files Extentions to Browse and download
        /// </summary>
        public string[] AllowNotFileExt = new string[] { ".exe", ".dll", ".cmd", ".bat", ".lib", ".crypt", };

        /// <summary>
        ///     Server Name
        /// </summary>
        public string ServerName
        {
            get { return _serverName; }
            set
            {
                _serverName = value;
                _headers_mutex.WaitOne();
                if (_headers.ContainsKey("Server-Name"))
                    _headers["Server-Name"] = _serverName;
                else
                    _headers.Add("Server-Name", _serverName);
                _headers_mutex.ReleaseMutex();
            }
        }
        protected string _serverName = "SimpleServersPBAuth Basic HttpServer v0.2B";

        /// <summary>
        ///     Allow to connect only HTTP clients
        /// </summary>
        public virtual bool OnlyHTTPClients { get { return _OnlyHTTP; } set { _OnlyHTTP = value; } }
        protected bool _OnlyHTTP = true;

        /// <summary>
        ///     Max Http Header Client Size
        /// </summary>
        public uint MaxClientHeaderSize { get { return _MaxHeaderSize; } set { _MaxHeaderSize = value; } }
        protected uint _MaxHeaderSize = 4096;

        /// <summary>
        ///     Max Http Body Client Size
        /// </summary>
        public uint MaxClientBodySize { get { return _MaxBodySize; } set { _MaxBodySize = value; } }
        protected uint _MaxBodySize = 65536;        

        /// <summary>
        ///     Max File Size to Download
        /// </summary>
        public long AllowBrowseDownloadMaxSize { get { return _MaxFileDownloadSize; } set { _MaxFileDownloadSize = value; } }
        protected long _MaxFileDownloadSize = 1024 * 1024 * 40; // 40 MB        

        /// <summary>
        ///    Default Server Response Encoding
        /// </summary>
        public Encoding ResponseEncoding { get { return _responseEnc; } set { _responseEnc = value; } }
        protected Encoding _responseEnc = Encoding.GetEncoding(1251);

        /// <summary>
        ///     Default Client Request Encoding
        /// </summary>
        public Encoding RequestEncoding { get { return _requestEnc; } set { _requestEnc = value; } }
        protected Encoding _requestEnc = Encoding.GetEncoding(1251);

        /// <summary>
        ///     Send Error to Denied IP Clients
        /// </summary>
        public bool ListenIPDeniedSendError { get { return _sendlockedError; } set { _sendlockedError = value; } }
        protected bool _sendlockedError = false;

        /// <summary>
        ///     Error Code for Denied IP Clients
        /// </summary>
        public int ListenIPDeniedErrorCode { get { return _sendlockedErrCode; } set { _sendlockedErrCode = value; } }
        protected int _sendlockedErrCode = 423; // Locked        

        /// <summary>
        ///     Server Response Main Headers
        /// </summary>
        public Dictionary<string, string> Headers
        {
            get
            {
                _headers_mutex.WaitOne();
                Dictionary<string, string> res = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kvp in _headers)
                    res.Add(kvp.Key, kvp.Value);
                _headers_mutex.ReleaseMutex();
                return res;
            }
            set
            {
                _headers_mutex.WaitOne();
                _headers.Clear();
                foreach (KeyValuePair<string, string> kvp in value)
                    _headers.Add(kvp.Key, kvp.Value);
                if (_headers.ContainsKey("Server-Name"))
                    _headers["Server-Name"] = _serverName;
                else
                    _headers.Add("Server-Name", _serverName);
                _headers_mutex.ReleaseMutex();
            }
        }
        protected Dictionary<string, string> _headers = new Dictionary<string, string>();
        protected Mutex _headers_mutex = new Mutex();
        
        /// <summary>
        ///     Home Directory For File Listing
        /// </summary>
        public string HomeDirectory { get { return _baseDir; } set { _baseDir = value; } }
        protected string _baseDir = null;
        
        /// <summary>
        ///     Allow File Download
        /// </summary>
        public bool AllowBrowseDownloads { get { return _allowGetFiles; } set { _allowGetFiles = value; } }
        protected bool _allowGetFiles = false;
        
        /// <summary>
        ///     Allow Browse Directory for Files
        /// </summary>
        public bool AllowBrowseFiles { get { return _allowGetDirs; } set { if (value) _allowGetFiles = true; _allowGetDirs = value; } }
        protected bool _allowGetDirs = false;

        /// <summary>
        ///     Allow Browse Directory for Big Files (over AllowBrowseDownloadMaxSize)
        /// </summary>
        public bool AllowBrowseBigFiles { get { return _allowGetDirs; } set { if (value) _allowGetFiles = true; _allowGetDirs = value; } }
        protected bool _allowListBigFiles = true;
        
        /// <summary>
        ///     Allow Browse Directory for Directories
        /// </summary>
        public bool AllowBrowseDirectories { get { return _allowListDirs; } set { if (value) _allowGetFiles = true; _allowListDirs = value; } }
        protected bool _allowListDirs = false;                
                
        /// <summary>
        ///     User:password for Authorizated users
        /// </summary>
        public Dictionary<string, string> AuthentificationCredintals = new Dictionary<string, string>();
        /// <summary>
        ///     Server Requires Authorization
        /// </summary>
        public bool AuthentificationRequired { get { return _authRequired; } set { _authRequired = value; } }
        private bool _authRequired = false;
        
        /// <summary>
        ///     Get Client, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="clientID"></param>
        protected override void GetClient(TcpClient Client, ulong clientID)
        {
            Regex CR = new Regex(@"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            string Request = "";
            string Header = null;
            List<byte> Body = new List<byte>();

            int bRead = -1;
            int posCRLF = -1;
            int receivedBytes = 0;
            int contentLength = 0;

            try
            {
                // Get Header
                //while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) // doesn't work correct
                while ((bRead = Client.GetStream().ReadByte()) >= 0)
                {
                    receivedBytes++;
                    Body.Add((byte)bRead);

                    // Check GET or POST
                    if (_OnlyHTTP && (receivedBytes == 1))
                        if ((bRead != 0x47) && (bRead != 0x50))
                        {
                            onBadClient(Client, clientID, Body.ToArray());
                            return;
                        };

                    Request += (char)bRead; // standard symbol
                    if (bRead == 0x0A) posCRLF = Request.IndexOf("\r\n\r\n"); // get body start index
                    if (posCRLF >= 0 || Request.Length > _MaxHeaderSize) { break; }; // GET ONLY                    
                };

                if (Request.Length > _MaxHeaderSize)
                {
                    // Header too long
                    HttpClientSendError(Client, 414, "414 Header Too Long");
                    return;
                };

                bool valid = (posCRLF > 0);
                if ((!valid) && _OnlyHTTP) 
                {
                    onBadClient(Client, clientID, Body.ToArray());
                    return;
                };

                if (_authRequired && (AuthentificationCredintals.Count > 0))
                {
                    bool accept = false;
                    string sa = "Authorization:";
                    if (Request.IndexOf(sa) > 0)
                    {
                        int iofcl = Request.IndexOf(sa);
                        sa = Request.Substring(iofcl + sa.Length, Request.IndexOf("\r", iofcl + sa.Length) - iofcl - sa.Length).Trim();
                        if (sa.StartsWith("Basic"))
                        {
                            sa = Base64Decode(sa.Substring(6));
                            string[] up = sa.Split(new char[] { ':' }, 2);
                            if (AuthentificationCredintals.ContainsKey(up[0]) && AuthentificationCredintals[up[0]] == up[1])
                                accept = true;
                        };
                    };
                    if (!accept)
                    {
                        Dictionary<string, string> dh = new Dictionary<string, string>();
                        dh.Add("WWW-Authenticate", "Basic realm=\"Authentification required\"");
                        HttpClientSendError(Client, 401, dh); // 401 Unauthorized
                        return;
                    };
                };

                // Get Body
                if (valid)
                {
                    Body.Clear();
                    Header = Request;
                    Match mx = CR.Match(Request);
                    if (mx.Success) contentLength = int.Parse(mx.Groups[1].Value);
                    int total2read = posCRLF + 4 + contentLength;
                    while ((receivedBytes < total2read) && ((bRead = Client.GetStream().ReadByte()) >= 0))
                    {
                        receivedBytes++;
                        Body.Add((byte)bRead);

                        string rcvd = _requestEnc.GetString(new byte[] { (byte)bRead }, 0, 1);
                        Request += rcvd;
                        if (Request.Length > _MaxBodySize) 
                        {
                            // Body too long
                            HttpClientSendError(Client, 413, "413 Payload Too Large");
                            return;
                        };
                    };
                };

                GetClientRequest(Client, clientID, Request, Header, Body.ToArray());
            }
            catch (Exception ex)
            {
                LastError = ex;
                LastErrTime = DateTime.Now;
                ErrorsCounter++;
                onError(Client, clientID, ex);
            };
        }

        /// <summary>
        ///     Send Response with Error Code To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="Code">Status Code</param>
        /// <param name="dopHeaders">Response Headers</param>
        protected virtual void HttpClientSendError(TcpClient Client, int Code, Dictionary<string, string> dopHeaders)
        {
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            string body = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            HttpClientSendData(Client, _responseEnc.GetBytes(body), dopHeaders, Code, "text/html");
        }
        /// <summary>
        ///     Send Response with Error Code To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="Code">Status Code</param>
        protected virtual void HttpClientSendError(TcpClient Client, int Code)
        {
            HttpClientSendError(Client, Code, (Dictionary<string, string>)null);
        }
        /// <summary>
        ///     Send Response with Error Code To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="Code">Status Code</param>
        /// <param name="Text">Body Text</param>
        protected virtual void HttpClientSendError(TcpClient Client, int Code, string Text)
        {
            HttpClientSendData(Client, _responseEnc.GetBytes(Text), null, Code, "text/html");
        }
        /// <summary>
        ///     Send Response Text Status 200 To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="Text">Body Text</param>
        /// <param name="dopHeaders">Response Headers</param>
        protected virtual void HttpClientSendText(TcpClient Client, string Text, IDictionary<string, string> dopHeaders)
        {
            string body = "<html><body>" + Text + "</body></html>";
            HttpClientSendData(Client, _responseEnc.GetBytes(body), dopHeaders, 200, "text/html");
        }
        /// <summary>
        ///     Send Response Text Status 200 To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="Text">Body Text</param>
        protected virtual void HttpClientSendText(TcpClient Client, string Text)
        {
            HttpClientSendText(Client, Text, null);
        }

        /// <summary>
        ///     Send Response Data Status 200 To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        protected virtual void HttpClientSendData(TcpClient Client, byte[] body)
        {
            HttpClientSendData(Client, body, null, 200, "text/html");
        }
        /// <summary>
        ///     Send Response Data Status 200 To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        /// <param name="dopHeaders">Response Headers</param>
        protected virtual void HttpClientSendData(TcpClient Client, byte[] body, IDictionary<string, string> dopHeaders)
        {
            HttpClientSendData(Client, body, dopHeaders, 200, "text/html");
        }
        /// <summary>
        ///     Send Response Data To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        /// <param name="dopHeaders">Response Headers</param>
        /// <param name="ResponseCode">Status Code</param>
        protected virtual void HttpClientSendData(TcpClient Client, byte[] body, IDictionary<string, string> dopHeaders, int ResponseCode)
        {
            HttpClientSendData(Client, body, dopHeaders, ResponseCode, "text/html");
        }
        /// <summary>
        ///     Send Response Data To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        /// <param name="dopHeaders">Response Headers</param>
        /// <param name="ContentType">Content Type</param>
        protected virtual void HttpClientSendData(TcpClient Client, byte[] body, IDictionary<string, string> dopHeaders, string ContentType)
        {
            HttpClientSendData(Client, body, dopHeaders, 200, ContentType);
        }
        /// <summary>
        ///     Send Response Data To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="body"></param>
        /// <param name="dopHeaders">Response Headers</param>
        /// <param name="ResponseCode">Status Code</param>
        /// <param name="ContentType">Content Type</param>
        protected virtual void HttpClientSendData(TcpClient Client, byte[] body, IDictionary<string, string> dopHeaders, int ResponseCode, string ContentType)
        {
            string header = "HTTP/1.1 " + ResponseCode.ToString() + "\r\n";

            string val = null;
            if ((val = DictGetKeyIgnoreCase(dopHeaders, "Status")) != null) header = "HTTP/1.1 " + val + "\r\n";

            // Main Headers
            this._headers_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._headers_mutex.ReleaseMutex();

            // Dop Headers
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);

            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-type"))
                header += "Content-type: " + ContentType + "\r\n";
            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-Length"))
                header += "Content-Length: " + body.Length.ToString() + "\r\n";
            header += "\r\n";

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.GetEncoding(1251).GetBytes(header));
            response.AddRange(body);

            Client.GetStream().Write(response.ToArray(), 0, response.Count);
            Client.GetStream().Flush();
            //Client.Client.Close();
            //Client.Close();
        }
        /// <summary>
        ///     Send File To Client
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="fileName">File Full Path</param>
        /// <param name="dopHeaders">Response Headers</param>
        /// <param name="ResponseCode">Status Code</param>
        /// <param name="ContentType">Content Type</param>
        protected virtual void HttpClientSendFile(TcpClient Client, string fileName, Dictionary<string, string> dopHeaders, int ResponseCode, string ContentType)
        {
            FileInfo fi = new FileInfo(fileName);

            string header = "HTTP/1.1 " + ResponseCode.ToString() + "\r\n";

            // Main Headers
            this._headers_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._headers_mutex.ReleaseMutex();

            // Dop Headers
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    header += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);

            if (String.IsNullOrEmpty(ContentType))
                ContentType = GetMemeType(fi.Extension.ToLower());
            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-type"))
                header += "Content-type: " + ContentType + "\r\n";
            if (!DictHasKeyIgnoreCase(dopHeaders, "Content-Length"))
                header += "Content-Length: " + fi.Length.ToString() + "\r\n";
            header += "\r\n";

            List<byte> response = new List<byte>();
            response.AddRange(Encoding.GetEncoding(1251).GetBytes(header));
            Client.GetStream().Write(response.ToArray(), 0, response.Count);

            // copy
            byte[] buff = new byte[65536];
            int bRead = 0;
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            while (fs.Position < fs.Length)
            {
                bRead = fs.Read(buff, 0, buff.Length);
                Client.GetStream().Write(buff, 0, bRead);
            };
            fs.Close();
            //

            Client.GetStream().Flush();
            //Client.Client.Close();
            //Client.Close();
        }

        /// <summary>
        ///     Get HTTP Client Request, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="clientID"></param>
        /// <param name="Request"></param>
        /// <param name="Header"></param>
        /// <param name="Body"></param>
        protected virtual void GetClientRequest(TcpClient Client, ulong clientID, string Request, string Header, byte[] Body)
        {            
            IDictionary<string, string> clHeaders = GetClientHeaders(Header);
            string page, host, inline, query;
            IDictionary<string, string> parameters;
            ClientRequest cl = ClientRequest.FromServer(this);

            try
            {
                query = GetClientQuery(Header, out host, out page, out inline, out parameters);

                cl.Client = Client;
                cl.clientID = clientID;
                cl.OriginRequest = Request;
                cl.OriginHeader = Header;
                cl.BodyData = Body;
                cl.Query = query;
                cl.Page = page;
                cl.Host = host;
                cl.QueryParams = parameters;
                cl.QueryInline = inline;
                cl.Headers = clHeaders;
            }
            catch
            {
                
            };

            GetClientRequest(cl);
            // connection will be close after return   
        }
        /// <summary>
        ///     Get HTTP Client Request, threaded
        /// </summary>
        /// <param name="Request"></param>
        protected virtual void GetClientRequest(ClientRequest Request)
        {
            HttpClientSendError(Request.Client, 501);
            // connection will be close after return   
        }
        /// <summary>
        ///     Call on error in GetClient & GetClientRequest
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        /// <param name="error"></param>
        protected override void onError(TcpClient Client, ulong id, Exception error)
        {
            // no base.onError - no throw exception
        }
        /// <summary>
        ///     Call on Bad Client (Invalid HTTP Request)
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        /// <param name="Request"></param>
        protected virtual void onBadClient(TcpClient Client, ulong id, byte[] Request)
        {
            // do nothing
        }

        /// <summary>
        ///     Pass File or Browse Folder(s) to HTTP Client by Request
        /// </summary>
        /// <param name="Request"></param>
        protected virtual void PassFileToClientByRequest(ClientRequest Request)
        {
            PassFileToClientByRequest(Request, _baseDir, null); 
        }
        /// <summary>
        ///     Pass File or Browse Folder(s) to HTTP Client by Request
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="HomeDirectory">Home Directory with files to Browse</param>
        protected virtual void PassFileToClientByRequest(ClientRequest Request, string HomeDirectory)
        {
            PassFileToClientByRequest(Request, HomeDirectory, null); 
        }
        /// <summary>
        ///     Pass File or Browse Folder(s) to HTTP Client by Request
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="HomeDirectory">Home Directory with files to Browse</param>
        /// <param name="subPath">Sub Path to Extract from URL Path</param>
        protected virtual void PassFileToClientByRequest(ClientRequest Request, string HomeDirectory, string subPath)
        {
            if (String.IsNullOrEmpty(HomeDirectory)) { HttpClientSendError(Request.Client, 403); return; };
            if (!_allowGetFiles) { HttpClientSendError(Request.Client, 403); return; };
            if (String.IsNullOrEmpty(Request.Query)) { HttpClientSendError(Request.Client, 400); return; };
            if (String.IsNullOrEmpty(Request.Page)) { HttpClientSendError(Request.Client, 403); return; };
            if ((Request.QueryParams != null) && (Request.QueryParams.Count > 0)) { HttpClientSendError(Request.Client, 400); return; };

            string path = Request.Page;
            if (!String.IsNullOrEmpty(subPath))
            {
                int i = path.IndexOf(subPath);
                if (i >= 0) path = path.Remove(i, subPath.Length);
            };
            path = path.Replace("/", @"\");            
            if (path.IndexOf("/./") >= 0) { HttpClientSendError(Request.Client, 400); return; };
            if (path.IndexOf("/../") >= 0) { HttpClientSendError(Request.Client, 400); return; };
            if (path.IndexOf("/.../") >= 0) { HttpClientSendError(Request.Client, 400); return; };
            path = HomeDirectory + @"\" + path;
            while (path.IndexOf(@"\\") > 0) path = path.Replace(@"\\", @"\");
            string fName = System.IO.Path.GetFileName(path);
            string dName = System.IO.Path.GetDirectoryName(path);
            if ((String.IsNullOrEmpty(dName)) && (String.IsNullOrEmpty(fName)) && (path.EndsWith(@":\")) && (Path.IsPathRooted(path))) dName = path;
            if (!String.IsNullOrEmpty(fName))
            {
                if (!File.Exists(path))
                {
                    HttpClientSendError(Request.Client, 404);
                    return;
                }
                else
                {
                    List<string> disallowExt = new List<string>(AllowNotFileExt);
                    FileInfo fi = new FileInfo(path);
                    string fExt = fi.Extension.ToLower();
                    if (disallowExt.Contains(fExt))
                    {
                        HttpClientSendError(Request.Client, 403);
                        return;
                    }
                    else
                    {
                        if (fi.Length > _MaxFileDownloadSize)
                            HttpClientSendError(Request.Client, 509, String.Format("509 File is too big - {0}, limit - {1}", ToFileSize(fi.Length), ToFileSize(_MaxFileDownloadSize)));
                        else
                            HttpClientSendFile(Request.Client, path, null, 200, null);
                        return;
                    };
                };
            }
            else if (!String.IsNullOrEmpty(dName))
            {
                if (!Directory.Exists(path))
                {
                    HttpClientSendError(Request.Client, 404);
                    return;
                }
                else
                {
                    // load default file
                    {
                        List<string> files = new List<string>(Directory.GetFiles(path, "index.*", SearchOption.TopDirectoryOnly));
                        foreach (string file in files)
                        {
                            string fExt = Path.GetExtension(file);
                            if (fExt == ".html")  { HttpClientSendFile(Request.Client, file, null, 200, null); return; };
                            if (fExt == ".dhtml") { HttpClientSendFile(Request.Client, file, null, 200, null); return; };
                            if (fExt == ".htmlx") { HttpClientSendFile(Request.Client, file, null, 200, null); return; };
                            if (fExt == ".xhtml") { HttpClientSendFile(Request.Client, file, null, 200, null); return; };
                            if (fExt == ".txt")   { HttpClientSendFile(Request.Client, file, null, 200, null); return; };
                        };
                    };
                    if (!_allowGetDirs)
                    {
                        HttpClientSendError(Request.Client, 403);
                        return;
                    }
                    else
                    {
                        string html = "<html><body>";
                        if (_allowListDirs)
                        {
                            html += String.Format("<a href=\"{0}/\"><b> {0} </b></a><br/>\n\r", "..");
                            string[] dirs = Directory.GetDirectories(path);
                            if (dirs != null) Array.Sort<string>(dirs);
                            foreach (string dir in dirs)
                            {
                                DirectoryInfo di = new DirectoryInfo(dir);
                                if ((di.Attributes & FileAttributes.Hidden) > 0) continue;
                                string sPath = dir.Substring(dir.LastIndexOf(@"\") + 1);
                                html += String.Format("<a href=\"{1}/\"><b>{0}</b></a><br/>\n\r", sPath, UrlEscape(sPath));
                            };
                        };
                        {
                            List<string> disallowExt = new List<string>(AllowNotFileExt);
                            string[] files = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly);
                            if (files != null) Array.Sort<string>(files);
                            foreach (string file in files)
                            {
                                FileInfo fi = new FileInfo(file);
                                if (disallowExt.Contains(fi.Extension.ToLower())) continue;
                                if ((fi.Attributes & FileAttributes.Hidden) > 0) continue;
                                if ((!_allowListBigFiles) && (fi.Length > _MaxFileDownloadSize)) continue;
                                string sPath = Path.GetFileName(file);
                                html += String.Format("<a href=\"{1}\">{0}</a> - <span style=\"color:gray;\">{2}</span>, <span style=\"color:silver;\">MDF: {3}</span><br/>\n\r", sPath, UrlEscape(sPath), ToFileSize(fi.Length), fi.LastWriteTime);
                            };
                        };
                        html += "</body></html>";
                        HttpClientSendText(Request.Client, html);
                        return;
                    };
                };
            };
            HttpClientSendError(Request.Client, 400);
        }        

        /// <summary>
        ///     Call CGI-BIN file and Send Response to HTTP Client
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="exeFile">CGI-BIN File Full Path</param>
        /// <param name="cmdLineArgs">Command Line Arguments</param>
        protected virtual void PassCGIBinResultToClientByRequest(ClientRequest Request, string exeFile, string cmdLineArgs)
        {
            string pVal = null;
            Dictionary<string, string> parameters = new Dictionary<string, string>();                        
            if ((pVal = DictGetKeyIgnoreCase(Request.Headers, "content-type")) != null) parameters.Add("CONTENT_TYPE", pVal);
            if ((pVal = DictGetKeyIgnoreCase(Request.Headers, "content-length")) != null) 
                parameters.Add("CONTENT_LENGTH", pVal);
            else
                parameters.Add("CONTENT_LENGTH", Request.BodyData == null ? "0" : Request.BodyData.Length.ToString());
            parameters.Add("SERVER_PORT", ServerPort.ToString());
            parameters.Add("PATH_INFO", Request.Page);
            parameters.Add("REQUEST_URI", Request.Query);
            if (!String.IsNullOrEmpty(Request.QueryInline)) parameters.Add("QUERY_STRING", Request.QueryInline);
            parameters.Add("REMOTE_HOST", Request.RemoteIP);
            parameters.Add("REMOTE_ADDR", Request.RemoteIP);
            if (!String.IsNullOrEmpty(Request.Authorization)) parameters.Add("AUTH_TYPE", "Basic");
            parameters.Add("REMOTE_USER", Request.User);
            if ((pVal = DictGetKeyIgnoreCase(Request.Headers, "accept")) != null) parameters.Add("HTTP_ACCEPT", pVal);
            parameters.Add("HTTP_USER_AGENT", Request.UserAgent);
            parameters.Add("HTTP_REFERER", Request.Referer);
            if ((pVal = DictGetKeyIgnoreCase(Request.Headers, "cookie")) != null) parameters.Add("HTTP_COOKIE", Request.User);
            parameters.Add("SERVER_NAME", _serverName);

            CGIBINCaller.Response resp = CGIBINCaller.Call(exeFile, Request.BodyData, parameters, cmdLineArgs);
            if (resp == null)
                HttpClientSendError(Request.Client, 523, "523 Origin Is Unreachable");
            else
                HttpClientSendData(Request.Client, resp.Content, String.IsNullOrEmpty(resp.Header) ? null : GetClientHeaders(resp.Header));
        }

        /// <summary>
        ///     Calls on Blocked by IP Client
        /// </summary>
        /// <param name="Client"></param>
        protected override void GetBlockedClient(TcpClient Client)
        {
            if (_sendlockedError)
            {
                if ((_sendlockedErrCode == 0) || ((_sendlockedErrCode == 423)))
                    HttpClientSendError(Client, 423, "423 Locked");
                else
                    HttpClientSendError(Client, _sendlockedErrCode);
            };
        }

        /// <summary>
        ///     Initialize Web Socket Connection
        /// </summary>
        /// <param name="clientRequest"></param>
        /// <returns>true if ok</returns>
        protected virtual bool HttpClientWebSocketInit(ClientRequest clientRequest, bool sendErrorIfFail)
        {
            try
            {
                string swk = Regex.Match(clientRequest.OriginRequest, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                if(string.IsNullOrEmpty(swk))
                {
                    if(sendErrorIfFail)
                        HttpClientSendError(clientRequest.Client, 417, "417 Expectation Failed");
                    return false;
                };

                string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                string swkaSha1Base64 = Convert.ToBase64String(swkaSha1);

                byte[] response = Encoding.UTF8.GetBytes(
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");
                clientRequest.Client.GetStream().Write(response, 0, response.Length);
                clientRequest.Client.GetStream().Flush();
            }
            catch
            {
                if (sendErrorIfFail)
                    HttpClientSendError(clientRequest.Client, 417, "417 Expectation Failed");
                return false;
            };

            GetClientWebSocket(clientRequest);
            return true;
        }

        /// <summary>
        ///     Get WebSocket Connection
        /// </summary>
        /// <param name="clientRequest"></param>
        protected virtual void GetClientWebSocket(ClientRequest clientRequest)
        {
            try { OnWebSocketClientConnected(clientRequest); } catch { };
            try { if (!clientRequest.Client.Connected) return; } catch { return; };

            int rxCount = 0;
            int rxAvailable = 0;
            byte[] rxBuffer = new byte[65536];
            List<byte> rx8Buffer = new List<byte>();
            bool loop = true;
            int rCounter = 0;
            while (loop)
            {                
                try { rxAvailable = clientRequest.Client.Available; }
                catch { break; };

                // Read Incoming Data
                while (rxAvailable > 0)
                {
                    try { rxAvailable -= (rxCount = clientRequest.Client.GetStream().Read(rxBuffer, 0, rxBuffer.Length > rxAvailable ? rxAvailable : rxBuffer.Length)); }
                    catch { break; };
                    if (rxCount > 0)
                    {
                        byte[] b2a = new byte[rxCount];
                        Array.Copy(rxBuffer, b2a, rxCount);
                        rx8Buffer.AddRange(b2a);
                    };
                };

                if (rx8Buffer.Count > 0)
                {
                    OnWebSocketClientData(clientRequest, rx8Buffer.ToArray());
                    rx8Buffer.Clear();
                    rCounter = 0;
                };
                
                if (!isRunning) loop = false;
                if (rCounter >= 200) // 10s
                {
                    try
                    {
                        if (!IsConnected(clientRequest.Client)) loop = false;
                        rCounter = 0;
                    }
                    catch { loop = false; };
                };
                System.Threading.Thread.Sleep(50);
                rCounter++;
            };
            // loop //

            try { OnWebSocketClientDisconnected(clientRequest); } catch { };
        }

        /// <summary>
        ///     On WebSocket Client Connected
        ///     -- close connection will stop thread --
        /// </summary>
        /// <param name="clientRequest"></param>
        protected virtual void OnWebSocketClientConnected(ClientRequest clientRequest)
        {
            //clientRequest.Client.Close();
        }

        /// <summary>
        ///     On WebSocket Client Disconnected
        /// </summary>
        /// <param name="clientRequest"></param>
        protected virtual void OnWebSocketClientDisconnected(ClientRequest clientRequest)
        {
            
        }

        /// <summary>
        ///     On WebSocket Client Data Received
        ///     -- close connection will stop thread --
        /// </summary>
        /// <param name="clientRequest"></param>
        /// <param name="data"></param>
        protected virtual void OnWebSocketClientData(ClientRequest clientRequest, byte[] data)
        {
            //clientRequest.Client.Close();
        }

        // https://learn.javascript.ru/websockets
        // http://tools.ietf.org/html/rfc6455
        public static string GetStringFromWebSocketFrame(byte[] buffer, int length)
        {
            if ((buffer == null) || (buffer.Length < 2)) return ""; // throw new Exception("The buffer length couldn't be less than 2 bytes");
            bool FIN = (buffer[0] & 0x80) == 0x80; // last packet?
            int OPCODE = buffer[0] & 0x0F; // packet type: 0 continue, 1 - Text Frame, 2 - Binary frame, 8 - Close, 9 - Ping, 10 - Pong                        
            bool MASKED = (buffer[1] & 0x80) == 0x80; // has mask?
            int dataLength = buffer[1] & 0x7F; // data length

            if (OPCODE != 1) return ""; // Not Text Frame;

            int nextIndex = 0;
            if (dataLength <= 125) // length here
            {
                nextIndex = 2; // [][] (M M M M) byte (no addit bytes length) 2/6
            }
            else if (dataLength == 126) // length next 2 bytes
            {
                if (buffer.Length < 4) return ""; // throw new Exception("The buffer length couldn't be less than 4 bytes");
                dataLength = (int)BitConverter.ToUInt16(new byte[] { buffer[3], buffer[2] }, 0);
                nextIndex = 4; // [][] X X (M M M M) byte (2 addit bytes length) 4/8
            }
            else if (dataLength == 127) // length next 8 bytes
            {
                if (buffer.Length < 10) return ""; // throw new Exception("The buffer length couldn't be less than 10 bytes");
                dataLength = (int)BitConverter.ToUInt64(new byte[] { buffer[9], buffer[8], buffer[7], buffer[6], buffer[5], buffer[4], buffer[3], buffer[2] }, 0);
                nextIndex = 10;// [][] X X X X X X X X (M M M M) byte (8 addit bytes length) 10/14
            };

            int dataFrom = MASKED ? nextIndex + 4 : nextIndex;
            if ((dataFrom + dataLength) > length) return ""; //throw new Exception("The buffer length is smaller than the data length");
            if (MASKED)
            {
                byte[] mask = new byte[] { buffer[nextIndex], buffer[nextIndex + 1], buffer[nextIndex + 2], buffer[nextIndex + 3] };
                int byteNum = 0;
                int dataTill = dataFrom + dataLength;
                for (int i = dataFrom; i < dataTill; i++)
                    buffer[i] = (byte)(buffer[i] ^ mask[byteNum++ % 4]);
            };

            try
            {
                string res = Encoding.UTF8.GetString(buffer, dataFrom, dataLength);
                return res;
            }
            catch 
            {
                return "";
            };
        }

        // https://learn.javascript.ru/websockets
        // http://tools.ietf.org/html/rfc6455
        public static byte[] GetBytesFromWebSocketFrame(byte[] buffer, int length)
        {
            if ((buffer == null) || (buffer.Length < 2)) return null; // throw new Exception("The buffer length couldn't be less than 2 bytes");
            bool FIN = (buffer[0] & 0x80) == 0x80; // last packet?
            int OPCODE = buffer[0] & 0x0F; // packet type: 0 continue, 1 - Text Frame, 2 - Binary frame, 8 - Close, 9 - Ping, 10 - Pong                        
            bool MASKED = (buffer[1] & 0x80) == 0x80; // has mask?
            int dataLength = buffer[1] & 0x7F; // data length

            if (OPCODE != 1) return null; // Not Text Frame;

            int nextIndex = 0;
            if (dataLength <= 125) // length here
            {
                nextIndex = 2; // [][] (M M M M) byte (no addit bytes length) 2/6
            }
            else if (dataLength == 126) // length next 2 bytes
            {
                if (buffer.Length < 4) return null; // throw new Exception("The buffer length couldn't be less than 4 bytes");
                dataLength = (int)BitConverter.ToUInt16(new byte[] { buffer[3], buffer[2] }, 0);
                nextIndex = 4; // [][] X X (M M M M) byte (2 addit bytes length) 4/8
            }
            else if (dataLength == 127) // length next 8 bytes
            {
                if (buffer.Length < 10) return null; // throw new Exception("The buffer length couldn't be less than 10 bytes");
                dataLength = (int)BitConverter.ToUInt64(new byte[] { buffer[9], buffer[8], buffer[7], buffer[6], buffer[5], buffer[4], buffer[3], buffer[2] }, 0);
                nextIndex = 10;// [][] X X X X X X X X (M M M M) byte (8 addit bytes length) 10/14
            };

            int dataFrom = MASKED ? nextIndex + 4 : nextIndex;
            if ((dataFrom + dataLength) > length) return null; //throw new Exception("The buffer length is smaller than the data length");
            if (MASKED)
            {
                byte[] mask = new byte[] { buffer[nextIndex], buffer[nextIndex + 1], buffer[nextIndex + 2], buffer[nextIndex + 3] };
                int byteNum = 0;
                int dataTill = dataFrom + dataLength;
                for (int i = dataFrom; i < dataTill; i++)
                    buffer[i] = (byte)(buffer[i] ^ mask[byteNum++ % 4]);
            };

            try
            {
                byte[] res = new byte[dataLength];
                Array.Copy(buffer,dataFrom,res,0,dataLength);
                return res;
            }
            catch
            {
                return null;
            };
        }

        // https://learn.javascript.ru/websockets
        // http://tools.ietf.org/html/rfc6455
        public static byte[] GetWebSocketFrameFromString(string Message)
        {
            if (String.IsNullOrEmpty(Message)) return null;

            Random rnd = new Random();
            byte[] BODY = Encoding.UTF8.GetBytes(Message);
            byte[] MASK = new byte[0]; // no mask
            // byte[] MASK = new byte[4] { (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255) }; // new byte[0]
            int OPCODE = 1; // 1 - Text, 2 - Binary
            byte[] FRAME = null;

            int nextIndex = 0;
            if (BODY.Length < 126)
            {
                nextIndex = 2;
                FRAME = new byte[2 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + BODY.Length);
            }
            else if (BODY.Length <= short.MaxValue)
            {
                nextIndex = 4;
                FRAME = new byte[4 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + 126);
                FRAME[2] = (byte)((BODY.Length >> 8) & 255);
                FRAME[3] = (byte)(BODY.Length & 255);
            }
            else
            {
                nextIndex = 10;
                FRAME = new byte[10 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + 127);
                ulong blen = (ulong)BODY.Length;
                FRAME[2] = (byte)((blen >> 56) & 255);
                FRAME[3] = (byte)((blen >> 48) & 255);
                FRAME[4] = (byte)((blen >> 40) & 255);
                FRAME[5] = (byte)((blen >> 32) & 255);
                FRAME[6] = (byte)((blen >> 24) & 255);
                FRAME[7] = (byte)((blen >> 16) & 255);
                FRAME[8] = (byte)((blen >> 08) & 255);
                FRAME[9] = (byte)(blen & 255);
            };
            FRAME[0] = (byte)(0x80 + OPCODE); // FIN + OPCODE
            if (MASK.Length == 4)
            {
                for (int mi = 0; mi < MASK.Length; mi++)
                    FRAME[nextIndex + mi] = MASK[mi];
                nextIndex += MASK.Length;
            };
            for (int bi = 0; bi < BODY.Length; bi++)
                FRAME[nextIndex + bi] = MASK.Length == 4 ? (byte)(BODY[bi] ^ MASK[bi % 4]) : BODY[bi];

            return FRAME;
        }

        // https://learn.javascript.ru/websockets
        // http://tools.ietf.org/html/rfc6455
        public static byte[] GetWebSocketFrameFromBytes(byte[] BODY)
        {
            if ((BODY == null) || (BODY.Length == 0)) return null;

            Random rnd = new Random();
            byte[] MASK = new byte[0]; // no mask
            // byte[] MASK = new byte[4] { (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255), (byte)rnd.Next(0, 255) }; // new byte[0]
            int OPCODE = 1; // 1 - Text, 2 - Binary
            byte[] FRAME = null;

            int nextIndex = 0;
            if (BODY.Length < 126)
            {
                nextIndex = 2;
                FRAME = new byte[2 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + BODY.Length);
            }
            else if (BODY.Length <= short.MaxValue)
            {
                nextIndex = 4;
                FRAME = new byte[4 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + 126);
                FRAME[2] = (byte)((BODY.Length >> 8) & 255);
                FRAME[3] = (byte)(BODY.Length & 255);
            }
            else
            {
                nextIndex = 10;
                FRAME = new byte[10 + MASK.Length + BODY.Length];
                FRAME[1] = (byte)((MASK.Length == 4 ? 0x80 : 0) + 127);
                ulong blen = (ulong)BODY.Length;
                FRAME[2] = (byte)((blen >> 56) & 255);
                FRAME[3] = (byte)((blen >> 48) & 255);
                FRAME[4] = (byte)((blen >> 40) & 255);
                FRAME[5] = (byte)((blen >> 32) & 255);
                FRAME[6] = (byte)((blen >> 24) & 255);
                FRAME[7] = (byte)((blen >> 16) & 255);
                FRAME[8] = (byte)((blen >> 08) & 255);
                FRAME[9] = (byte)(blen & 255);
            };
            FRAME[0] = (byte)(0x80 + OPCODE); // FIN + OPCODE
            if (MASK.Length == 4)
            {
                for (int mi = 0; mi < MASK.Length; mi++)
                    FRAME[nextIndex + mi] = MASK[mi];
                nextIndex += MASK.Length;
            };
            for (int bi = 0; bi < BODY.Length; bi++)
                FRAME[nextIndex + bi] = MASK.Length == 4 ? (byte)(BODY[bi] ^ MASK[bi % 4]) : BODY[bi];

            return FRAME;
        }

        public class ClientRequest
        {
            private ThreadedHttpServer server; 

            /// <summary>
            ///     Client
            /// </summary>
            public TcpClient Client;
            /// <summary>
            ///     CLient ID
            /// </summary>
            public ulong clientID;
            /// <summary>
            ///     Original HTTP Request Client Request
            /// </summary>
            public string OriginRequest;
            /// <summary>
            ///     Original Http Request Client Header
            /// </summary>
            public string OriginHeader;
            /// <summary>
            ///     Http Client Request Body Data
            /// </summary>
            public byte[] BodyData;
            /// <summary>
            ///     Http Client Request Full Query
            /// </summary>
            public string Query;
            /// <summary>
            ///     Http Client Request Query Path/Page
            /// </summary>
            public string Page;
            /// <summary>
            ///     Http Client Request Host
            /// </summary>
            public string Host;
            /// <summary>
            ///     Http Client Request Query/Get Parameters
            /// </summary>
            public string QueryInline;
            /// <summary>
            ///     Http Client Request Headers
            /// </summary>
            public IDictionary<string, string> Headers;
            /// <summary>
            ///     Http Client Request Query/Get Parameters
            /// </summary>
            public IDictionary<string, string> QueryParams;

            internal ClientRequest() { }
            internal static ClientRequest FromServer(ThreadedHttpServer server)
            {
                ClientRequest res = new ClientRequest();
                res.server = server;
                return res;
            }

            /// <summary>
            ///     Http Client Request Accept Header
            /// </summary>
            public string Accept { get { return DictGetKeyIgnoreCase(Headers, "Accept"); } }
            /// <summary>
            ///     Http Client Request Accept-Language Header
            /// </summary>
            public string AcceptLanguage { get { return DictGetKeyIgnoreCase(Headers, "Accept-Language"); } }
            /// <summary>
            ///     Http Client Request Accept-Language Header
            /// </summary>
            public string AcceptEncoding { get { return DictGetKeyIgnoreCase(Headers, "Accept-Encoding"); } }
            /// <summary>
            ///     Http Client Request Authorization Header
            /// </summary>
            public string Authorization { get { return DictGetKeyIgnoreCase(Headers, "Authorization"); } }
            /// <summary>
            ///     Http Client Request Body as Text
            /// </summary>
            public string BodyText { get { if((BodyData == null) || (BodyData.Length == 0)) return null; else return (server == null ? Encoding.ASCII.GetString(BodyData) : server._requestEnc.GetString(BodyData));  }}
            /// <summary>
            ///     Http Client Request Cache-Control Header
            /// </summary>
            public string CacheControl { get { return DictGetKeyIgnoreCase(Headers, "Cache-Control"); } }            
            /// <summary>
            ///     Http Client Request Cookie Header
            /// </summary>
            public string Cookie { get { return DictGetKeyIgnoreCase(Headers, "Cookie"); } }            
            /// <summary>
            ///     Http Client Request Content-Encoding Header
            /// </summary>
            public string ContentEncoding { get { return DictGetKeyIgnoreCase(Headers, "Content-Encoding"); } }
            /// <summary>
            ///     Http Client Request Content-Length Header
            /// </summary>
            public string ContentLength { get { return DictGetKeyIgnoreCase(Headers, "Content-Length"); } }
            /// <summary>
            ///     Http Client Request Content-Type Header
            /// </summary>
            public string ContentType { get { return DictGetKeyIgnoreCase(Headers, "Content-Type"); } }
            /// <summary>
            ///     Http Client Request Query/Get Parameters
            /// </summary>
            public IDictionary<string, string> GetParams { get { return QueryParams; } }
            /// <summary>
            ///     Http Client Request Origin Header
            /// </summary>
            public string Origin { get { return DictGetKeyIgnoreCase(Headers, "Origin"); } }
            /// <summary>
            ///     Http Client Request Post Data
            /// </summary>
            public string PostData { get { if ((BodyData == null) || (BodyData.Length == 0)) return null; else return (server == null ? UrlUnescape(Encoding.ASCII.GetString(BodyData)) : UrlUnescape(server._requestEnc.GetString(BodyData))); } }
            /// <summary>
            ///     Http Client Request Post Parameters
            /// </summary>
            public IDictionary<string, string> PostParams  { get {  if ((BodyData == null) || (BodyData.Length == 0)) return null; else return GetClientParams(server._requestEnc.GetString(BodyData)); } }
            /// <summary>
            ///     Http Client Request Referer Header
            /// </summary>
            public string Referer { get { return DictGetKeyIgnoreCase(Headers, "Referer"); } }
            /// <summary>
            ///     Http Client Remote IP Address
            /// </summary>
            public string RemoteIP { get { return ((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString(); } }
            /// <summary>
            ///     Http Client Request User-Agent Header
            /// </summary>
            public string UserAgent { get { return DictGetKeyIgnoreCase(Headers, "User-Agent"); } }
            /// <summary>
            ///     Http Client Authentificated User
            /// </summary>
            public string User { get {
                string auth = DictGetKeyIgnoreCase(Headers, "Authorization");
                if (String.IsNullOrEmpty(auth)) return null;
                if (auth.StartsWith("Basic"))
                {
                    string sp = Base64Decode(auth.Substring(6));
                    string[] up = sp.Split(new char[] { ':' }, 2);
                    return up[0];
                };
                return "Unknown";
            } }
            /// <summary>
            ///     Http Client Request Query/Get or Post Parameters
            /// </summary>
            /// <param name="value">parameter name</param>
            /// <returns></returns>
            public string this[string value]
            {
                get
                {
                    string res = null;
                    res = DictGetKeyIgnoreCase(QueryParams, value);
                    if (!String.IsNullOrEmpty(res)) return res;
                    res = DictGetKeyIgnoreCase(PostParams, value);
                    return res;
                }
            }

            /// <summary>
            ///     Get Header Parameter, Ignore Case
            /// </summary>
            /// <param name="value">parameter name</param>
            /// <returns></returns>
            public string GetHeaderParam(string value) { return DictGetKeyIgnoreCase(Headers, value); }
            /// <summary>
            ///     Get Query/Get Parameter, Ignore Case
            /// </summary>
            /// <param name="value">parameter name</param>
            /// <returns></returns>
            public string GetQueryParam(string value) { return DictGetKeyIgnoreCase(QueryParams,value); }
            /// <summary>
            ///     Get Post Parameter, Ignore Case
            /// </summary>
            /// <param name="value">parameter name</param>
            /// <returns></returns>
            public string GetPostParam(string value) { return DictGetKeyIgnoreCase(PostParams,value); }
        }        
    }    

    /// <summary>
    ///     Protobuf HTTP sever
    ///     curl http://127.0.0.1:8011/PROTOBUF+4/ --upload-file C:\xxx.bin
    //      curl http://127.0.0.1:8011/PROTOBUF+4/ --upload-file C:\xxx.bin --output c:\yyy.bin
    /// </summary>
    public class Threaded4BytesHttpServer : Threaded4BytesTCPServer
    {
        public Threaded4BytesHttpServer() : base(80) { }
        public Threaded4BytesHttpServer(int Port) : base(Port) { }
        public Threaded4BytesHttpServer(IPAddress IP, int Port) : base(IP, Port) { }
        ~Threaded4BytesHttpServer() { this.Dispose(); }

        protected ushort _MaxHeaderSize = 4096;
        public ushort MaxClientHeaderSize { get { return _MaxHeaderSize; } set { _MaxHeaderSize = value; } }

        private Mutex _h_mutex = new Mutex();
        private Dictionary<string, string> _headers = new Dictionary<string, string>();
        public Dictionary<string, string> Headers
        {
            get
            {
                _h_mutex.WaitOne();
                Dictionary<string, string> res = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kvp in _headers)
                    res.Add(kvp.Key, kvp.Value);
                _h_mutex.ReleaseMutex();
                return res;
            }
            set
            {
                _h_mutex.WaitOne();
                _headers.Clear();
                foreach (KeyValuePair<string, string> kvp in value)
                    _headers.Add(kvp.Key, kvp.Value);
                _h_mutex.ReleaseMutex();
            }
        }

        private bool _authRequired = false;
        public Dictionary<string, string> AuthentificationCredintals = new Dictionary<string, string>();
        public bool AuthentificationRequired { get { return _authRequired; } set { _authRequired = value; } }

        // Отправка данных
        public virtual void HttpClientSendData(TcpClient Client, byte[] data, Dictionary<string, string> dopHeaders)
        {
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            int Code = 200;
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string Str = "HTTP/1.1 " + CodeStr + "\r\n";
            this._h_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._h_mutex.ReleaseMutex();
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            Str += "Content-type: application/" + this._prefix + "\r\nContent-Length: " + data.Length.ToString() + "\r\n\r\n";
            // Приведем строку к виду массива байт
            byte[] Buffer = Encoding.GetEncoding(1251).GetBytes(Str);
            // Отправим его клиенту
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.GetStream().Write(data, 0, data.Length);
            Client.GetStream().Flush();
            // Закроем соединение
            //Client.Client.Close();
            //Client.Close();
        }
        public virtual void HttpClientSendData(TcpClient Client, byte[] data)
        {
            HttpClientSendData(Client, data, null);
        }

        // Отправка страницы с ошибкой
        public virtual void HttpClientSendError(TcpClient Client, int Code, Dictionary<string, string> dopHeaders)
        {
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
            // Код простой HTML-странички
            string Html = "<html><body><h1>" + CodeStr + "</h1></body></html>";
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string Str = "HTTP/1.1 " + CodeStr + "\r\n";
            this._h_mutex.WaitOne();
            foreach (KeyValuePair<string, string> kvp in this._headers)
                Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            this._h_mutex.ReleaseMutex();
            if (dopHeaders != null)
                foreach (KeyValuePair<string, string> kvp in dopHeaders)
                    Str += String.Format("{0}: {1}\r\n", kvp.Key, kvp.Value);
            Str += "Content-type: text/html\r\nContent-Length: " + Html.Length.ToString() + "\r\n\r\n" + Html;
            // Приведем строку к виду массива байт
            byte[] Buffer = Encoding.GetEncoding(1251).GetBytes(Str);
            // Отправим его клиенту
            Client.GetStream().Write(Buffer, 0, Buffer.Length);
            Client.GetStream().Flush();
            // Закроем соединение
            //Client.Client.Close();
            //Client.Close();
        }
        public virtual void HttpClientSendError(TcpClient Client, int Code)
        {
            HttpClientSendError(Client, Code, null);
        }      
        
        /// <summary>
        ///     Get Client, threaded
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        protected override void GetClient(TcpClient Client, ulong id)
        {
            Regex CR = new Regex(@"Content-Length: (\d+)", RegexOptions.IgnoreCase);

            string s1 = "GET /" + this._prefix + "/";
            string s2 = "POST /" + this._prefix + "/";
            string s3 = "PUT /" + this._prefix + "/";

            string Header = "";
            List<byte> Body = new List<byte>();

            int bRead = -1;
            int posCRLF = -1;
            int receivedBytes = 0;
            int contentLength = 0;

            try
            {
                // Get Header
                //while ((Client.Available > 0) && ((bRead = Client.GetStream().ReadByte()) >= 0)) // doesn't work correct
                while ((bRead = Client.GetStream().ReadByte()) >= 0)
                {
                    receivedBytes++;
                    Header += (char)bRead; // standard symbol
                    if (bRead == 0x0A) posCRLF = Header.IndexOf("\r\n\r\n"); // get body start index
                    if (posCRLF >= 0 || Header.Length > _MaxHeaderSize) { break; }; // GET ONLY                    
                };
                bool valid = (posCRLF > 0) && ((Header.IndexOf("GET") == 0) || (Header.IndexOf("POST") == 0) || (Header.IndexOf("PUT") == 0));
                if (!valid)
                {
                    HttpClientSendError(Client, 400); // 400 Bad Request
                    return;
                };

                if (_authRequired && (AuthentificationCredintals.Count > 0))
                {
                    bool accept = false;
                    string sa = "Authorization:";
                    if (Header.IndexOf(sa) > 0)
                    {
                        int iofcl = Header.IndexOf(sa);
                        sa = Header.Substring(iofcl + sa.Length, Header.IndexOf("\r", iofcl + sa.Length) - iofcl - sa.Length).Trim();
                        if (sa.StartsWith("Basic"))
                        {
                            sa = Base64Decode(sa.Substring(6));
                            string[] up = sa.Split(new char[] { ':' }, 2);
                            if (AuthentificationCredintals.ContainsKey(up[0]) && AuthentificationCredintals[up[0]] == up[1])
                                accept = true;
                        };
                    };
                    if (!accept)
                    {
                        Dictionary<string, string> dh = new Dictionary<string, string>();
                        dh.Add("WWW-Authenticate", "Basic realm=\"Authentification required\"");
                        HttpClientSendError(Client, 401, dh); // 401 Unauthorized
                        return;
                    };
                };

                Match mx = CR.Match(Header);
                if (mx.Success) contentLength = int.Parse(mx.Groups[1].Value);
                if (contentLength == 0)
                {
                    HttpClientSendError(Client, 411); // 411 Length Required
                    return;
                }
                if (contentLength < (this._prefix.Length + 4))
                {
                    HttpClientSendError(Client, 406); // 406 Not Acceptable
                    return;
                };

                byte[] buff = new byte[this._prefix.Length + 4];
                Client.GetStream().Read(buff, 0, buff.Length);
                string prefix = System.Text.Encoding.GetEncoding(1251).GetString(buff, 0, this._prefix.Length);
                if (prefix != this._prefix)
                {
                    HttpClientSendError(Client, 415); // 415 Unsupported Media Type
                    return;
                };

                int length = System.BitConverter.ToInt32(buff, this._prefix.Length);
                if (length > contentLength)
                {
                    HttpClientSendError(Client, 416); // 416 Range Not Satisfiable
                    return;
                };
                buff = new byte[length];
                Client.GetStream().Read(buff, 0, buff.Length);
                GetClientRequestData(Client, id, Header, buff);                
            }
            catch (Exception ex)
            {
                LastError = ex;
                LastErrTime = DateTime.Now;
                ErrorsCounter++;
                onError(Client, id, ex);
            };
        }

        /// <summary>
        ///     Get Client Request with 1st block of data
        /// </summary>
        /// <param name="Client"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public virtual void GetClientRequestData(TcpClient Client, ulong id, string Header, byte[] data)
        {
            // Write No Data To Client
            List<byte> result = new List<byte>();
            result.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(this._prefix));
            byte[] buff = System.Text.Encoding.GetEncoding(1251).GetBytes("Hello " + DateTime.Now.ToString());
            result.AddRange(BitConverter.GetBytes(buff.Length));
            result.AddRange(buff);
            HttpClientSendData(Client, result.ToArray());

            // connection will be close after return   
        }

        protected override void onError(TcpClient Client, ulong id, Exception error)
        {

        }


        private static void sample()
        {
            // curl http://127.0.0.1:8011 --upload-file C:\xxx.bin
            // curl http://127.0.0.1:8011 --upload-file C:\xxx.bin --output c:\yyy.bin
            // curl --user sa:q http://127.0.0.1:8011 --upload-file C:\xxx.bin

            TCPListener.Threaded4BytesHttpServer svr = new TCPListener.Threaded4BytesHttpServer(8011);
            svr.Headers.Add("Server", "Threaded4BytesHttpServer/0.1");
            svr.Headers.Add("Server-Name", "TEST SAMPLE");
            svr.Headers.Add("Server-Owner", "I am");
            svr.AuthentificationCredintals.Add("sa", "q");
            svr.AuthentificationRequired = false;
            svr.ListenIPMode = TCPListener.ThreadedTCPServer.Mode.DenyBlackList;
            svr.ListenIPDeny = new string[] { "127.0.0.2" };
            svr.Start();
            //
            System.Threading.Thread.Sleep(10000);
            //
            svr.Stop();
            svr.Dispose();
        }
    }    

    /// <summary>
    ///     Module for call CGI-BIN executable file
    /// </summary>
    public class CGIBINCaller
    {
        public class Response
        {
            /// <summary>
            ///     Respone Header
            /// </summary>
            public string Header;

            /// <summary>
            ///     Response Body
            /// </summary>
            public string Body
            {
                get
                {
                    if (Content == null) return null;
                    if (Content.Length == 0) return "";
                    int chs = Header.IndexOf("charset=");
                    if (chs < 0)
                        return System.Text.Encoding.UTF8.GetString(Content);
                    else
                    {
                        int lind = Header.IndexOf("\n", chs + 8);
                        if (lind < 0) return System.Text.Encoding.UTF8.GetString(Content);
                        string charset = Header.Substring(chs + 8, lind - (chs + 8)).Trim('\n').Trim('\r').Trim();
                        return System.Text.Encoding.GetEncoding(charset).GetString(Content);
                    };
                }
            }

            /// <summary>
            ///     Response Content
            /// </summary>
            public byte[] Content;

            public Response(string header, byte[] content)
            {
                this.Header = header;
                this.Content = content;
            }
        }

        private static void SetDefaultParams(string path, System.Diagnostics.ProcessStartInfo startInfo)
        {
            startInfo.EnvironmentVariables["CONTENT_TYPE"] = "application/x-www-form-urlencoded";
            startInfo.EnvironmentVariables["CONTENT_LENGTH"] = "0";
            startInfo.EnvironmentVariables["CONTENT_DATA"] = "";
            startInfo.EnvironmentVariables["GATEWAY_INTERFACE"] = "CGI/1.1";
            startInfo.EnvironmentVariables["SERVER_NAME"] = "";
            startInfo.EnvironmentVariables["SERVER_SOFTWARE"] = "SimpleServersPBAuth.ThreadedHttpServer";
            startInfo.EnvironmentVariables["SERVER_PROTOCOL"] = "HTTP/1.1";
            startInfo.EnvironmentVariables["SERVER_PORT"] = "80";
            startInfo.EnvironmentVariables["PATH_INFO"] = "";
            startInfo.EnvironmentVariables["PATH_TRANSLATED"] = path;
            startInfo.EnvironmentVariables["SCRIPT_NAME"] = System.IO.Path.GetFileName(path);
            startInfo.EnvironmentVariables["DOCUMENT_ROOT"] = System.IO.Path.GetFileName(path);
            startInfo.EnvironmentVariables["REQUEST_METHOD"] = "GET";
            startInfo.EnvironmentVariables["REQUEST_URI"] = "";
            startInfo.EnvironmentVariables["QUERY_STRING"] = "";
            startInfo.EnvironmentVariables["REMOTE_HOST"] = "127.0.0.1";
            startInfo.EnvironmentVariables["REMOTE_ADDR"] = "127.0.0.1";
            startInfo.EnvironmentVariables["AUTH_TYPE"] = "";
            startInfo.EnvironmentVariables["REMOTE_USER"] = "";
            startInfo.EnvironmentVariables["HTTP_ACCEPT"] = "text/html,application/xhtml,application/xml";
            startInfo.EnvironmentVariables["HTTP_USER_AGENT"] = "CGIBINCaller";
            startInfo.EnvironmentVariables["HTTP_REFERER"] = "";
            startInfo.EnvironmentVariables["HTTP_COOKIE"] = "";
            startInfo.EnvironmentVariables["HTTPS"] = "";
        }

        private static void SetParams(IDictionary<string, string> pars, System.Diagnostics.ProcessStartInfo startInfo)
        {
            foreach (KeyValuePair<string, string> kv in pars)
                startInfo.EnvironmentVariables[kv.Key] = kv.Value;
        }

        private static void SetParams(IDictionary<string, object> pars, System.Diagnostics.ProcessStartInfo startInfo)
        {
            foreach (KeyValuePair<string, object> kv in pars)
                startInfo.EnvironmentVariables[kv.Key] = kv.Value.ToString();
        }

        private static void SetParams(System.Collections.Specialized.NameValueCollection pars, System.Diagnostics.ProcessStartInfo startInfo)
        {
            foreach (string key in pars.AllKeys)
                startInfo.EnvironmentVariables[key] = pars[key];
        }

        private static Response CallBin(string path, byte[] postBody, IDictionary<string, string> p1, IDictionary<string, object> p2, System.Collections.Specialized.NameValueCollection p3, string cmdLineArgs)
        {
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.FileName = path;
            startInfo.Arguments = cmdLineArgs;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.StandardOutputEncoding = System.Text.Encoding.UTF7;

            SetDefaultParams(path, startInfo);
            if (p1 != null) SetParams(p1, startInfo);
            if (p2 != null) SetParams(p2, startInfo);
            if (p3 != null) SetParams(p3, startInfo);

            // IF POST BODY
            if ((postBody != null) && (postBody.Length > 0))
            {
                startInfo.EnvironmentVariables["CONTENT_LENGTH"] = postBody.Length.ToString();
                startInfo.EnvironmentVariables["REQUEST_METHOD"] = "POST";
            };

            System.Diagnostics.Process proc = System.Diagnostics.Process.Start(startInfo);

            // IF POST BODY
            if ((postBody != null) && (postBody.Length > 0))
            {
                proc.StandardInput.BaseStream.Write(postBody, 0, postBody.Length);
                proc.StandardInput.BaseStream.Flush();
            };
            proc.WaitForExit();

            string header = "";
            // RECEIVE DATA
            byte[] content = new byte[0];
            {
                List<byte> resvd = new List<byte>();
                int hi = 0;
                while (!proc.StandardOutput.EndOfStream)
                {
                    int b = proc.StandardOutput.Read();
                    if ((resvd.Count == 0) && (b == 10)) { hi = 1; }; // no header
                    if ((resvd.Count == 0) && (b == 60)) { resvd.Add(10); hi = 1; }; // no header
                    if (b >= 0)
                    {
                        if (hi == 0)
                        {
                            header += (char)b;
                            if (header.Length > 0)
                            {
                                int hend = header.IndexOf("\n\n");
                                if (hend > 0) { hi = hend + 2; };
                                hend = header.IndexOf("\r\n\r\n");
                                if (hend > 0) { hi = hend + 4; };
                            };
                        };
                        resvd.Add((byte)b);
                    }
                    else
                        break;
                };

                if (resvd.Count > header.Length)
                {
                    content = new byte[resvd.Count - hi];
                    Array.Copy(resvd.ToArray(), hi, content, 0, content.Length);
                };
            };

            return new Response(header, content);
        }

        public static Response Call(string path, byte[] postBody, string cmdLineArgs)
        {
            return CallBin(path, postBody, null, null, null, cmdLineArgs);
        }

        public static Response Call(string path, byte[] postBody, IDictionary<string, string> parameters, string cmdLineArgs)
        {
            return CallBin(path, postBody, parameters, null, null, cmdLineArgs);
        }

        public static Response Call(string path, byte[] postBody, IDictionary<string, object> parameters, string cmdLineArgs)
        {
            return CallBin(path, postBody, null, parameters, null, cmdLineArgs);
        }

        public static Response Call(string path, byte[] postBody, System.Collections.Specialized.NameValueCollection parameters, string cmdLineArgs)
        {
            return CallBin(path, postBody, null, null, parameters, cmdLineArgs);
        }

        public static void Test()
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters.Add("QUERY_STRING",
                "path=none" +
                "&test=empty" +
                "&var=a" +
                "&dd=Яков"
                );

            byte[] post = System.Text.Encoding.UTF8.GetBytes(
                "test=post" +
                "&codepage=utf-8" +
                "&lang=ru" +
                "&name=" + System.Uri.EscapeDataString("Яковинка")
                );

            Response resp = CGIBINCaller.Call(System.Reflection.Assembly.GetExecutingAssembly().Location, post, parameters, null);

            Console.WriteLine("========================================================================");
            Console.Write(resp.Header);
            Console.OutputEncoding = System.Text.Encoding.GetEncoding(866);
            Console.Write(resp.Body);
            Console.WriteLine();
            Console.WriteLine("========================================================================");

            System.IO.FileStream fs = new System.IO.FileStream(System.Reflection.Assembly.GetExecutingAssembly().Location + "_header.txt", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            if (resp.Header.Length > 0)
                fs.Write(System.Text.Encoding.ASCII.GetBytes(resp.Header), 0, resp.Header.Length);
            fs.Close();

            fs = new System.IO.FileStream(System.Reflection.Assembly.GetExecutingAssembly().Location + "_body.txt", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            if ((resp.Content != null) && (resp.Content.Length > 0))
                fs.Write(resp.Content, 0, resp.Content.Length);
            fs.Close();
        }

    }

    /// <summary>
    ///    Module for CGI-BIN implementation of executable file
    /// </summary>
    public class CGIBINModule
    {
        public Dictionary<string, object> Variables = new Dictionary<string, object>();

        public System.Collections.Specialized.NameValueCollection QUERY_PARAMS = new System.Collections.Specialized.NameValueCollection();
        public System.Collections.Specialized.NameValueCollection GET_PARAMS { get { return QUERY_PARAMS; } }

        public System.Collections.Specialized.NameValueCollection CONTENT_PARAMS = new System.Collections.Specialized.NameValueCollection();
        public System.Collections.Specialized.NameValueCollection POST_PARAMS { get { return CONTENT_PARAMS; } }
        public string POST_DATA { get { if ((CONTENT_DATA != null) && (CONTENT_DATA.Length > 0)) return System.Text.Encoding.ASCII.GetString(CONTENT_DATA); else return ""; } }
        public string POST_DATA_W1251 { get { if ((CONTENT_DATA != null) && (CONTENT_DATA.Length > 0)) return System.Text.Encoding.GetEncoding(1251).GetString(CONTENT_DATA); else return ""; } }
        public string POST_DATA_UTF8 { get { if ((CONTENT_DATA != null) && (CONTENT_DATA.Length > 0)) return System.Text.Encoding.UTF8.GetString(CONTENT_DATA); else return ""; } }
        public byte[] POST_DATA_BYTES { get { return CONTENT_DATA; } }
        public int POST_DATA_LENGTH { get { return CONTENT_LENGTH; } }

        public string GATEWAY_INTERFACE;

        public string SERVER_NAME;
        public string SERVER_SOFTWARE;
        public string SERVER_PROTOCOL;
        public int SERVER_PORT = 80;

        public string PATH_INFO;
        public string PATH_TRANSLATED;
        public string SCRIPT_NAME;
        public string DOCUMENT_ROOT;

        public string REQUEST_METHOD;
        public string REQUEST_URI;
        public string QUERY_STRING;
        public string GET_STRING { get { return QUERY_STRING; } }

        public string REMOTE_HOST;
        public string REMOTE_ADDR;

        public string AUTH_TYPE;
        public string REMOTE_USER;

        public string CONTENT_TYPE;
        public byte[] CONTENT_DATA = null;
        public int CONTENT_LENGTH = 0;

        public string HTTP_ACCEPT;
        public string HTTP_USER_AGENT;
        public string HTTP_REFERER;
        public string HTTP_COOKIE;
        public string HTTPS;

        private bool _canwriteheader = true;

        public CGIBINModule()
        {
            ReadVars();
        }

        private void ReadVars()
        {
            // Get Post Data
            Variables.Add("CONTENT_TYPE", CONTENT_TYPE = System.Environment.GetEnvironmentVariable("CONTENT_TYPE"));
            int.TryParse(System.Environment.GetEnvironmentVariable("CONTENT_LENGTH"), out CONTENT_LENGTH);
            Variables.Add("CONTENT_LENGTH", CONTENT_LENGTH);
            if (CONTENT_LENGTH > 0)
            {
                CONTENT_DATA = new byte[CONTENT_LENGTH];
                for (int i = 0; i < CONTENT_LENGTH; i++)
                    CONTENT_DATA[i] = (byte)Console.Read();
            };
            Variables.Add("CONTENT_DATA", CONTENT_DATA);
            Variables.Add("GATEWAY_INTERFACE", GATEWAY_INTERFACE = System.Environment.GetEnvironmentVariable("GATEWAY_INTERFACE"));
            Variables.Add("SERVER_NAME", SERVER_NAME = System.Environment.GetEnvironmentVariable("SERVER_NAME"));
            Variables.Add("SERVER_SOFTWARE", SERVER_SOFTWARE = System.Environment.GetEnvironmentVariable("SERVER_SOFTWARE"));
            Variables.Add("SERVER_PROTOCOL", SERVER_PROTOCOL = System.Environment.GetEnvironmentVariable("SERVER_PROTOCOL"));
            int.TryParse(System.Environment.GetEnvironmentVariable("SERVER_PORT"), out SERVER_PORT);
            Variables.Add("SERVER_PORT", SERVER_PORT);
            Variables.Add("PATH_INFO", PATH_INFO = System.Environment.GetEnvironmentVariable("PATH_INFO"));
            Variables.Add("PATH_TRANSLATED", PATH_TRANSLATED = System.Environment.GetEnvironmentVariable("PATH_TRANSLATED"));
            Variables.Add("SCRIPT_NAME", SCRIPT_NAME = System.Environment.GetEnvironmentVariable("SCRIPT_NAME"));
            Variables.Add("DOCUMENT_ROOT", DOCUMENT_ROOT = System.Environment.GetEnvironmentVariable("DOCUMENT_ROOT"));
            Variables.Add("REQUEST_METHOD", REQUEST_METHOD = System.Environment.GetEnvironmentVariable("REQUEST_METHOD"));
            Variables.Add("REQUEST_URI", REQUEST_URI = System.Environment.GetEnvironmentVariable("REQUEST_URI"));
            Variables.Add("QUERY_STRING", QUERY_STRING = System.Environment.GetEnvironmentVariable("QUERY_STRING"));
            Variables.Add("REMOTE_HOST", REMOTE_HOST = System.Environment.GetEnvironmentVariable("REMOTE_HOST"));
            Variables.Add("REMOTE_ADDR", REMOTE_ADDR = System.Environment.GetEnvironmentVariable("REMOTE_ADDR"));
            Variables.Add("AUTH_TYPE", AUTH_TYPE = System.Environment.GetEnvironmentVariable("AUTH_TYPE"));
            Variables.Add("REMOTE_USER", REMOTE_USER = System.Environment.GetEnvironmentVariable("REMOTE_USER"));
            Variables.Add("HTTP_ACCEPT", HTTP_ACCEPT = System.Environment.GetEnvironmentVariable("HTTP_ACCEPT"));
            Variables.Add("HTTP_USER_AGENT", HTTP_USER_AGENT = System.Environment.GetEnvironmentVariable("HTTP_USER_AGENT"));
            Variables.Add("HTTP_REFERER", HTTP_REFERER = System.Environment.GetEnvironmentVariable("HTTP_REFERER"));
            Variables.Add("HTTP_COOKIE", HTTP_COOKIE = System.Environment.GetEnvironmentVariable("HTTP_COOKIE"));
            Variables.Add("HTTPS", HTTPS = System.Environment.GetEnvironmentVariable("HTTPS"));

            if (QUERY_STRING != null)
                QUERY_PARAMS = HttpUtility.ParseQueryString(QUERY_STRING);

            if ((CONTENT_DATA != null) && (CONTENT_DATA.Length > 0))
            {
                string cd = System.Text.Encoding.UTF8.GetString(CONTENT_DATA);
                try { CONTENT_PARAMS = HttpUtility.ParseQueryString(cd); }
                catch { };
            };
        }

        public string VariableToString(object value)
        {
            if (value == null) return "";
            Type valueType = value.GetType();
            if (valueType.IsArray && (value.ToString() == "System.Byte[]"))
                return System.Text.Encoding.UTF8.GetString((byte[])value);
            return value.ToString();
        }

        public void WriteReponseHeader(string header)
        {
            if (_canwriteheader)
                Console.Out.Write(header + "\n");
            else
                throw new System.IO.EndOfStreamException("Write Headers before GetResponseStream");
        }

        public void WriteReponseHeader(System.Collections.Specialized.NameValueCollection headers)
        {
            if (_canwriteheader)
            {
                if (headers.Count > 0)
                    foreach (string key in headers.AllKeys)
                        Console.Out.Write(key + ": " + headers[key] + "\n");
            }
            else
                throw new System.IO.EndOfStreamException("Write Headers before GetResponseStream");
        }

        public void WriteReponseHeader(IDictionary<string, string> headers)
        {
            if (_canwriteheader)
            {
                if (headers.Count > 0)
                    foreach (KeyValuePair<string, string> nv in headers)
                        Console.Out.Write(nv.Key + ": " + nv.Value + "\n");
            }
            else
                throw new System.IO.EndOfStreamException("Write Headers before GetResponseStream");
        }

        public void WriteReponseHeader(KeyValuePair<string, string> header)
        {
            if (_canwriteheader)
                Console.Out.Write(header.Key + ": " + header.Value + "\n");
            else
                throw new System.IO.EndOfStreamException("Write Headers before GetResponseStream");
        }

        public void WriteReponseHeader(string name, string value)
        {
            if (_canwriteheader)
                Console.Out.Write(name + ": " + value + "\n");
            else
                throw new System.IO.EndOfStreamException("Write Headers before GetResponseStream");
        }

        public System.IO.Stream GetResponseStream()
        {
            if (_canwriteheader) { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            return Console.OpenStandardOutput();
        }

        public System.IO.Stream GetResponseStream(System.Text.Encoding encoding)
        {
            if (_canwriteheader) { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            Console.OutputEncoding = encoding;
            return Console.OpenStandardOutput();
        }

        public System.IO.StreamWriter GetResponseStreamWriter()
        {
            if (_canwriteheader) { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            return new System.IO.StreamWriter(Console.OpenStandardOutput());
        }

        public System.IO.StreamWriter GetResponseStreamWriter(System.Text.Encoding encoding)
        {
            if (_canwriteheader) { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            Console.OutputEncoding = encoding;
            return new System.IO.StreamWriter(Console.OpenStandardOutput(), encoding);
        }

        public void WriteResponse(string response)
        {
            { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            Console.Write(response);
        }

        public void WriteResponse(byte[] data)
        {
            { Console.Out.Write("\n"); Console.Out.Flush(); };
            _canwriteheader = false;
            Console.OpenStandardOutput().Write(data, 0, data.Length);
        }

        public void CloseResponse()
        {
            if (_canwriteheader)
                Console.Out.Write("\n");
            Console.Out.Close();
            _canwriteheader = false;
        }

        /// <summary>
        ///     Use this method to simple test cgi-bin executebale appilcation -- 
        ///     static void Main(string[] args) { SimpleServersPBAuth.CGIBINModule.Test(); };
        /// </summary>
        public static void Test()
        {
            CGIBINModule cgi = new CGIBINModule();
            
            //
            // WRITE HEADERS FIRST
            //
            cgi.WriteReponseHeader("Status: 201 Created");
            cgi.WriteReponseHeader("CGI-Script: SimpleServersPBAuth.CGIBINModule C# CGI-bin Test");
            cgi.WriteReponseHeader("Content-Type: text/html; charset=utf-8");

            // WRITE BODY NEXT

            System.IO.StreamWriter response = cgi.GetResponseStreamWriter(System.Text.Encoding.UTF8);

            response.Write("<html><head><title>CGI in C#</title></head><body>CGI Environment Variables<br />");
            response.Write("<table border=\"1\">");
            // LIST Environment Variables
            {                 
                int del = 1;
                foreach (KeyValuePair<string, object> kv in cgi.Variables)
                    response.Write("<tr><td>" + (del++).ToString("00") + "</td><td>" + kv.Key + "</td><td>" + cgi.VariableToString(kv.Value) + "</td></tr>");
            };
            // LIST GET QUERY
            {                 
                if (cgi.GET_PARAMS.Count > 0)
                    foreach (string q in cgi.GET_PARAMS.AllKeys)
                        response.Write("<tr><td>GET</td><td>" + q + "</td><td>" + cgi.GET_PARAMS[q] + "</td></tr>");
            };
            // LIST POST QUERY
            {                 
                if (cgi.POST_PARAMS.Count > 0)
                    foreach (string q in cgi.POST_PARAMS.AllKeys)
                        response.Write("<tr><td>POST</td><td>" + q + "</td><td>" + cgi.POST_PARAMS[q] + "</td></tr>");
            };
            response.Write("<tr><td colspan=\"3\"><form method=\"post\"><input type=\"text\" name=\"param1\"/><input type=\"submit\"/></form></td></tr>");
            response.Write("</table></body></html>");

            response.Close();
            cgi.CloseResponse();

            // Exit Environment
            Environment.Exit(0);
        }
    }
}
