namespace HummingBird
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Dynamic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Web;
    using Microsoft.CSharp;

    /*
    1.MaxConnPerIP 10
     */

    public class HttpServer
    {
        private Dictionary<string, IHttpHandler> httpHandlers = new Dictionary<string, IHttpHandler>();

        private Dictionary<string, List<HttpConnection>> httpSessions = new Dictionary<string, List<HttpConnection>>();

        public int Port { get; set; }

        public void Add(IHttpHandler handler)
        {
            var type = handler.GetType();
            httpHandlers[type.Name] = handler;
        }

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var listener = new TcpListener(IPAddress.Any, this.Port);
                listener.Start();
                while (true)
                {
                    var tcpClient = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            var socket = tcpClient.Client;
                            var visitor = socket.RemoteEndPoint;
                            var connection = new HttpConnection() { Socket = socket };
                            if (!httpSessions.ContainsKey(visitor.ToString()))
                            {
                                httpSessions.Add(visitor.ToString(), new List<HttpConnection>());
                            }

                            if (httpSessions[visitor.ToString()].Count > 10)
                            {
                                //return busy message
                            }
                            else
                            {
                                httpSessions[visitor.ToString()].Add(connection);
                                using (var networkStream = tcpClient.GetStream())
                                {
                                    //var endTime = connection.TimeStamp.AddMilliseconds(connection.ReadTimeout);
                                    //networkStream.ReadTimeout = (int)(endTime-DateTime.Now).TotalMilliseconds;        
                                    ProcessRequest(connection, networkStream, null);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            NLogHelper.Error(ex);
                        }
                    });
                }
            });

            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(1000);
                if (httpSessions.Count > 100)
                {
                    for (var i = httpSessions.Count - 1; i >= 0; i--)
                    {
                        var collection = httpSessions.Values.ElementAt(i);
                        if (!collection.Any())
                        {
                            httpSessions.Remove(httpSessions.Keys.ElementAt(i));
                        }
                    }
                }
            });
        }

        public HttpServer()
        {
            this.Port = 8052;
        }

        private void ProcessRequest(HttpConnection connection, NetworkStream networkStream, byte[] prefix)
        {
            //interlock
            connection.Count += 1;
            if (connection.Count < 20)
            {
                var httpRequest = new HttpRequest() { Connection = connection };
                var handlerTuple = GetHttpHandler(networkStream, prefix, httpRequest);
                if (handlerTuple != default(IHttpHandler))
                {
                    var httpHandler = handlerTuple.Item1;
                    var actionMethod = handlerTuple.Item2;
                    var httpResponse = new HttpResponse() { OutputStream = networkStream };
                    using (var outputStream = new MemoryStream())
                    {
                        httpHandler.Request = httpRequest;
                        httpHandler.Response = httpResponse;
                        if (httpRequest.Header.ContainsKey("Accept-Encoding") && httpRequest.Header["Accept-Encoding"].Contains("gzip"))
                        {
                            httpResponse.ContentEncoding = "gzip";
                        }

                        if (httpHandler is IHttpByteHandler)
                        {
                            var httpStringHandler = httpHandler as IHttpByteHandler;
                            httpResponse.ETag = httpStringHandler.ETag;
                            var header = httpRequest.Header;
                            if (header.ContainsKey("If-None-Match") && header["If-None-Match"] == httpStringHandler.ETag)
                            {
                                httpResponse.StatusCode = "304 Not Modified";
                                using (var streamWriter = new StreamWriter(outputStream))
                                {
                                    var headerString = httpResponse.Header();
                                    streamWriter.Write(headerString);
                                    streamWriter.Flush();
                                    var bytes = outputStream.ToArray();
                                    networkStream.Write(bytes, 0, bytes.Length);
                                }
                            }
                            else
                            {
                                var buffer = httpStringHandler.Handle(actionMethod);
                                using (var streamWriter = new StreamWriter(outputStream))
                                {
                                    var headerString = httpResponse.Header();
                                    streamWriter.Write(headerString);
                                    streamWriter.Flush();
                                    if (buffer != default(byte[]) && buffer.Any())
                                    {
                                        if (httpResponse.ContentEncoding == "gzip")
                                        {
                                            using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress, false))
                                            {
                                                gZipStream.Write(buffer, 0, buffer.Length);
                                            }
                                        }
                                        else
                                        {
                                            outputStream.Write(buffer, 0, buffer.Length);
                                            streamWriter.Flush();
                                        }
                                    }

                                    var bytes = outputStream.ToArray();
                                    networkStream.Write(bytes, 0, bytes.Length);
                                }
                            }
                        }
                        else if (httpHandler is IHttpRawHandler)
                        {
                            var httpRawHandler = httpHandler as IHttpRawHandler;
                            httpRawHandler.Handle();
                        }
                    }
                }
            }
            else
            {
                //too many requests per one connection; disconnect the socket and try again.
            }
        }

        private Tuple<IHttpHandler, string> GetHttpHandler(Stream inputStream, byte[] prefix, HttpRequest request)
        {
            var handle = default(Tuple<IHttpHandler, string>);
            var buffer = new byte[1024 * 1024];
            var byteBag = new List<byte>();
            var delimiterBytes = Encoding.UTF8.GetBytes(Environment.NewLine + Environment.NewLine);
            var count = int.MaxValue;
            var index = -1;
            do
            {
                count = 0;
                SystemHelper.InvokeSilently(delegate { count = inputStream.Read(buffer, 0, buffer.Length); });
                if (count > 0)
                {
                    var buffer1 = buffer.Take(count).ToArray();
                    index = buffer1.IndexOf(delimiterBytes);
                    if (index == -1)
                    {
                        byteBag.AddRange(buffer1);
                        NLogHelper.Info(Encoding.UTF8.GetString(byteBag.ToArray()));
                    }
                    else
                    {
                        byteBag.AddRange(buffer1.Take(index));
                        NLogHelper.Info(Encoding.UTF8.GetString(byteBag.ToArray()));
                        handle = GetHttpHandler(inputStream, request, byteBag.ToArray(), buffer1.Skip(index + delimiterBytes.Length).ToArray());
                    }
                }
            }
            while (count != 0 && index == -1);
            return handle;
        }

        private Tuple<IHttpHandler, string> GetHttpHandler(Stream inputStream, HttpRequest request, byte[] headerBytes, byte[] partialBody)
        {
            var result = default(Tuple<IHttpHandler, string>);
            var headerStrings = Encoding.UTF8.GetString(headerBytes).Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            var firstString = headerStrings.FirstOrDefault();
            if (firstString != default(string))
            {
                var itemStrings = firstString.Split(' ');
                if (itemStrings.Length > 1)
                {
                    request.Header["Method"] = itemStrings[0];
                    request.Header["Target"] = itemStrings[1];
                    foreach (var headerString in headerStrings.Skip(1))
                    {
                        itemStrings = headerString.Split(':');
                        if (itemStrings.Length > 1)
                        {
                            request.Header[itemStrings[0]] = itemStrings[1].Trim();
                        }
                    }

                    var method = request.Header["Method"];
                    var target = request.Header["Target"];
                    request.Method = method;
                    if (request.Header.ContainsKey("Cookie"))
                    {
                        foreach (var cookieString in request.Header["Cookie"].Split(';').Where(s => !string.IsNullOrEmpty(s)))
                        {
                            var cookieItems = cookieString.Split('=');
                            if (cookieItems.Length > 1)
                            {
                                request.Cookies.Add(new HttpCookie(cookieItems[0].Trim(), cookieItems[1]));
                            }
                        }
                    }

                    var httpHandler = default(IHttpHandler);
                    var actionMethod = string.Empty;
                    if (target == "/")
                    {
                        httpHandler = httpHandlers.Values.FirstOrDefault(l => typeof(DefaultHandler).IsAssignableFrom(l.GetType()));
                    }
                    else
                    {
                        if (target.IndexOf(".") != -1)
                        {
                            httpHandler = httpHandlers.Values.FirstOrDefault(l => typeof(StaticFileHandler).IsAssignableFrom(l.GetType()));
                        }
                        else
                        {
                            var handlerPrefix = target.Trim('/');
                            if (handlerPrefix.IndexOf("/") != -1)
                            {
                                handlerPrefix = handlerPrefix.Substring(0, handlerPrefix.IndexOf("/"));
                            }

                            httpHandler = httpHandlers.Values.FirstOrDefault(l => l.Name.ToUpper() == string.Format("{0}HANDLER", handlerPrefix.ToUpper()));
                        }
                    }

                    if (httpHandler == default(IHttpHandler))
                    {
                        httpHandler = httpHandlers.Values.FirstOrDefault(h => typeof(DefaultHandler).IsAssignableFrom(h.GetType()));
                    }

                    if (httpHandler != default(IHttpHandler))
                    {
                        if (method == "POST")
                        {
                            var delimiterString = Environment.NewLine + Environment.NewLine;
                            var bodyBytes = partialBody;
                            if (request.Header["Content-Type"].IndexOf("boundary=") != -1)
                            {
                                httpHandler.Session = Guid.NewGuid().ToString();
                                var boundaryString = request.Header["Content-Type"].Substring(request.Header["Content-Type"].IndexOf("boundary=") + "boundary=".Length);
                                if (request.Header.ContainsKey("Content-Length"))
                                {
                                    var contentLength = long.Parse(request.Header["Content-Length"]);
                                    var memoryMappedFile = MemoryMappedFile.CreateNew(httpHandler.Session, contentLength);
                                    using (var stream = memoryMappedFile.CreateViewStream())
                                    {
                                        ReadInputStream(inputStream, bodyBytes, contentLength, stream);
                                        ProcessStream(request, stream, boundaryString, httpHandler.Session);
                                    }
                                }
                            }
                            else
                            {
                                var buffer = new byte[16 * 1024];
                                using (var memoryStream = new MemoryStream())
                                {
                                    if (bodyBytes.Length > 0)
                                    {
                                        memoryStream.Write(bodyBytes, 0, bodyBytes.Length);
                                    }

                                    if (request.Header.ContainsKey("Content-Length"))
                                    {
                                        var receivedLength = bodyBytes.Length;
                                        var contentLength = int.Parse(request.Header["Content-Length"]);
                                        while (receivedLength < contentLength)
                                        {
                                            var count = inputStream.Read(buffer, 0, buffer.Length);
                                            receivedLength += count;
                                            if (receivedLength > contentLength)
                                            {

                                            }

                                            memoryStream.Write(buffer, 0, count);
                                        }
                                    }

                                    bodyBytes = memoryStream.ToArray();
                                }

                                if (bodyBytes.Length > 0)
                                {
                                    var bodyString = Encoding.UTF8.GetString(bodyBytes, 0, bodyBytes.Length);
                                    var delimiterString2 = "=";
                                    foreach (var itemString in bodyString.Split('&'))
                                    {
                                        var delimiterIndex2 = itemString.IndexOf(delimiterString2);
                                        var nameString = itemString.Substring(0, delimiterIndex2);
                                        var valueString = itemString.Substring(delimiterIndex2 + delimiterString2.Length);
                                        request.Form[nameString] = HttpUtility.UrlDecode(valueString);
                                    }
                                }
                            }
                        }
                    }

                    target = target.ToLower().Trim('/');
                    var device = target.StartsWith("m") ? Device.Mobile : (target.StartsWith("d") ? Device.Desktop : Device.Unknown);
                    if (device != Device.Unknown)
                    {
                        target = target.Substring(1);
                    }

                    httpHandler.Device = device;
                    request.Target = target;
                    var targetSegments = target.Trim('/').Split('/');
                    if (targetSegments.Length >= 2)
                    {
                        actionMethod = targetSegments[1];
                    }

                    result = new Tuple<IHttpHandler, string>(httpHandler, actionMethod);
                }
            }

            return result;
        }

        public static void ReadInputStream(Stream inputStream, byte[] bodyBytes, long contentLength, Stream targetStream)
        {
            var receivedLength = bodyBytes.Length;
            if (bodyBytes.Length > 0)
            {
                targetStream.Write(bodyBytes, 0, bodyBytes.Length);
            }

            var buffer = new byte[1024 * 1024];
            while (receivedLength < contentLength)
            {
                var count = inputStream.Read(buffer, 0, buffer.Length);
                receivedLength += count;
                if (receivedLength > contentLength)
                {
                    //count -= receivedLength - (int)contentLength; 
                }

                targetStream.Write(buffer, 0, count);
            }
        }

        public static void ProcessStream(HttpRequest httpRequest, Stream stream, string boundaryString, string session)
        {
            var headerBytes = Encoding.UTF8.GetBytes("--" + boundaryString + Environment.NewLine);
            var trailerBytes = Encoding.UTF8.GetBytes(Environment.NewLine + boundaryString + "--" + Environment.NewLine);
            var boundaryBytes = Encoding.UTF8.GetBytes(Environment.NewLine + "--" + boundaryString + Environment.NewLine);
            var delimiterBytes = Encoding.UTF8.GetBytes(Environment.NewLine + "--" + boundaryString);
            stream.Seek(headerBytes.Length, SeekOrigin.Begin);
            using (var binaryReader = new BinaryReader(stream))
            {
                var contentLength = stream.Length;
                while (stream.Position < contentLength - trailerBytes.Length)
                {
                    var dataString = DelimiterHelper.Read(stream);
                    if (dataString.StartsWith("Content-Disposition: form-data;"))
                    {
                        var dataSegments = dataString.Split(';');
                        if (dataSegments.Length > 1)
                        {
                            var name = string.Empty;
                            var stringPair = dataSegments[1].Split('=');
                            if (stringPair.Length == 2 && string.Compare(stringPair[0], "name") != 0)
                            {
                                name = stringPair[1];
                            }

                            if (dataSegments.Length > 2)
                            {
                                stringPair = dataSegments[2].Split('=');
                                if (stringPair.Length == 2 && string.Compare(stringPair[0], "filename") != 0)
                                {
                                    var httpFile = new HttpFile();
                                    httpFile.Name = name;
                                    httpFile.FileName = stringPair[1].Trim('"');
                                    dataString = DelimiterHelper.Read(stream);
                                    if (dataString.StartsWith("Content-Type:"))
                                    {
                                        httpFile.ContentType = dataString.Substring("Content-Type:".Length + 1);
                                        httpRequest.Files.Add(httpFile);
                                    }

                                    //read binary data;
                                    if (DelimiterHelper.Next(stream))
                                    {
                                        var startIndex = stream.Position;
                                        var boyerMoore = new BoyerMoore(boundaryBytes);
                                        do
                                        {
                                            var buffer2 = binaryReader.ReadBytes(1024 * 1024);
                                            var searchIndex = boyerMoore.IndexOf(buffer2);
                                            if (searchIndex == -1)
                                            {
                                                stream.Seek(1 - boundaryBytes.Length, SeekOrigin.Current);
                                            }
                                            else
                                            {
                                                if (searchIndex > 0)
                                                {
                                                    var memoryMappedFile = MemoryMappedFile.OpenExisting(session);
                                                    httpFile.Stream = memoryMappedFile.CreateViewStream(startIndex, searchIndex);
                                                }

                                                stream.Seek(startIndex + searchIndex + boundaryBytes.Length, SeekOrigin.Begin);
                                                break;
                                            }

                                        } while (stream.Position < contentLength - trailerBytes.Length);
                                    }
                                }
                            }
                            else if (dataSegments.Length == 2)
                            {
                                if (dataSegments[1].Trim().StartsWith("name=\""))
                                {
                                    name = dataSegments[1].TrimEnd('"').Substring(" name=\"".Length);
                                    if (DelimiterHelper.Next(stream))
                                    {
                                        httpRequest.Form[name] = DelimiterHelper.Read(stream, delimiterBytes);
                                        stream.Seek(2, SeekOrigin.Current);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void LoadHandlerAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(IHttpHandler).IsAssignableFrom(t));
                foreach (var type in types)
                {
                    var httpHandler = Activator.CreateInstance(type) as IHttpHandler;
                    this.Add(httpHandler);
                }

                if (types.All(t => !typeof(StaticFileHandler).IsAssignableFrom(t)))
                {
                    this.Add(new StaticFileHandler());
                }
            }
        }
    }

    public class BoyerMoore
    {
        private static int ALPHABET_SIZE = 256;
        private byte[] pattern;
        private int[] last;
        private int[] match;
        private int[] suffix;

        public BoyerMoore(byte[] pattern)
        {
            this.pattern = pattern;
            last = new int[ALPHABET_SIZE];
            match = new int[pattern.Length];
            suffix = new int[pattern.Length];
            ComputeLast();
            ComputeMatch();
        }

        public int IndexOf(byte[] text)
        {
            int i = pattern.Length - 1;
            int j = pattern.Length - 1;
            while (i < text.Length)
            {
                if (pattern[j] == text[i])
                {
                    if (j == 0)
                    {
                        return i;
                    }
                    j--;
                    i--;
                }
                else
                {
                    i += pattern.Length - j - 1 + Math.Max(j - last[text[i]], match[j]);
                    j = pattern.Length - 1;
                }
            }
            return -1;
        }

        private void ComputeLast()
        {
            for (int k = 0; k < last.Length; k++)
            {
                last[k] = -1;
            }
            for (int j = pattern.Length - 1; j >= 0; j--)
            {
                if (last[pattern[j]] < 0)
                {
                    last[pattern[j]] = j;
                }
            }
        }


        private void ComputeMatch()
        {
            for (int j = 0; j < match.Length; j++)
            {
                match[j] = match.Length;
            }

            ComputeSuffix();
            for (int i = 0; i < match.Length - 1; i++)
            {
                int j = suffix[i + 1] - 1;
                if (suffix[i] > j)
                {
                    match[j] = j - i;
                }
                else
                {
                    match[j] = Math.Min(j - i + match[i], match[j]);
                }
            }

            if (suffix[0] < pattern.Length)
            {
                for (int j = suffix[0] - 1; j >= 0; j--)
                {
                    if (suffix[0] < match[j]) { match[j] = suffix[0]; }
                }
                {
                    int j = suffix[0];
                    for (int k = suffix[j]; k < pattern.Length; k = suffix[k])
                    {
                        while (j < k)
                        {
                            if (match[j] > k)
                            {
                                match[j] = k;
                            }
                            j++;
                        }
                    }
                }
            }
        }

        private void ComputeSuffix()
        {
            suffix[suffix.Length - 1] = suffix.Length;
            int j = suffix.Length - 1;
            for (int i = suffix.Length - 2; i >= 0; i--)
            {
                while (j < suffix.Length - 1 && !pattern[j].Equals(pattern[i]))
                {
                    j = suffix[j + 1] - 1;
                }
                if (pattern[j] == pattern[i])
                {
                    j--;
                }
                suffix[i] = j + 1;
            }
        }

    }

    public class DelimiterHelper
    {
        private static byte[] newLineBytes;

        public static byte[] NewLineBytes
        {
            get { return newLineBytes; }
        }

        private static byte[] buffer;

        static DelimiterHelper()
        {
            newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
            buffer = new byte[newLineBytes.Length];
        }

        public static string Read(Stream stream, byte[] delimiterBytes)
        {
            var dataString = string.Empty;
            var byteBag = new List<byte>();
            do
            {
                var boyerMoore = new BoyerMoore(delimiterBytes);
                var startIndex = stream.Position;
                var buffer4 = new byte[1024 * 1024];
                var count = stream.Read(buffer4, 0, buffer4.Length);
                if (count >= delimiterBytes.Length)
                {
                    var searchIndex = boyerMoore.IndexOf(buffer4.Take(count).ToArray());
                    if (searchIndex == -1)
                    {
                        byteBag.AddRange(buffer4.Take(count - (delimiterBytes.Length - 1)));
                        stream.Seek(1 - delimiterBytes.Length, SeekOrigin.Current);
                    }
                    else
                    {
                        if (byteBag.Count > 0)
                        {
                            dataString = Encoding.UTF8.GetString(byteBag.ToArray());
                            byteBag.Clear();
                        }

                        dataString += Encoding.UTF8.GetString(buffer4.Take(searchIndex).ToArray());
                        stream.Seek(startIndex + searchIndex + delimiterBytes.Length, SeekOrigin.Begin);
                        break;
                    }
                }
            }
            while (stream.Position < stream.Length);
            return dataString;
        }

        public static string Read(Stream stream)
        {
            return Read(stream, DelimiterHelper.newLineBytes);
        }

        public static bool Next(Stream stream)
        {
            var count = stream.Read(buffer, 0, newLineBytes.Length);
            return count == newLineBytes.Length && newLineBytes.SequenceEqual<byte>(buffer);
        }
    }

    public class HttpCookie
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public DateTime Expire { get; set; }

        public string Path { get; set; }

        public string Domain { get; set; }

        public bool Secure { get; set; }

        public bool HttpOnly { get; set; }

        public HttpCookie(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        public override string ToString()
        {
            return string.Format("{0}={1}", Name, Value);
        }
    }

    public class HttpFile
    {
        public string Temp { get; set; }

        public string Name { get; set; }

        public string ContentType { get; set; }

        public string FileName { get; set; }

        public Stream Stream { get; set; }

        public void Save(string filePath)
        {
            File.Copy(Temp, filePath, true);
        }
    }

    public class HttpRequest
    {
        public HttpConnection Connection { get; set; }

        private Dictionary<string, string> header = new Dictionary<string, string>();
        public Dictionary<string, string> Header
        {
            get { return header; }
        }

        private Dictionary<string, string> form = new Dictionary<string, string>();
        public Dictionary<string, string> Form
        {
            get { return form; }
        }

        private IList<HttpCookie> cookies = new List<HttpCookie>();
        public IList<HttpCookie> Cookies
        {
            get { return cookies; }
        }

        private IList<HttpFile> files = new List<HttpFile>();
        public IList<HttpFile> Files
        {
            get { return files; }
        }

        public string Target { get; set; }

        public string Method { get; set; }
    }

    public class HttpResponse
    {
        private IList<HttpCookie> cookies = new List<HttpCookie>();

        public IList<HttpCookie> Cookies
        {
            get { return cookies; }
        }

        private string contentType = "text/html";
        public string ContentType
        {
            get { return contentType; }
            set { contentType = value; }
        }

        public string Location { get; set; }

        public string StatusCode { get; set; }

        public long ContentLength { get; set; }

        public string ContentDisposition { get; set; }

        public string ContentEncoding { get; set; }

        public string Connection { get; set; }

        public string ETag { get; set; }

        public NetworkStream OutputStream { get; set; }

        public HttpResponse()
        {
            StatusCode = "200 OK";
            Connection = "Close";
        }

        public void Redirect(string location)
        {
            this.StatusCode = "302 Found";
            this.Location = location;
        }

        public string Header()
        {
            var contentType = string.Format("Content-Type: {0};charset=utf-8", this.ContentType);
            var responseHeader = new StringBuilder();
            responseHeader.AppendLine(string.Format("HTTP/1.1 {0}", StatusCode));
            if (!string.IsNullOrEmpty(this.Location))
            {
                responseHeader.AppendLine(string.Format("Location:{0}", this.Location));
            }

            foreach (var cookie in this.Cookies)
            {
                responseHeader.AppendLine(string.Format("Set-Cookie: {0};", cookie.ToString()));
            }

            if (!string.IsNullOrEmpty(ContentDisposition))
            {
                responseHeader.AppendLine(string.Format("Content-Disposition:{0}", ContentDisposition));
            }

            if (!string.IsNullOrEmpty(ContentEncoding))
            {
                responseHeader.AppendLine(string.Format("Content-Encoding:{0}", ContentEncoding));
            }

            if (!string.IsNullOrEmpty(ETag))
            {
                responseHeader.AppendLine(string.Format("ETag:{0}", ETag));
            }

            if (ContentLength != 0)
            {
                responseHeader.AppendLine(string.Format("Content-Length:{0}", ContentLength));
            }

            if (!string.IsNullOrEmpty(Connection))
            {
                responseHeader.AppendLine(string.Format("Connection:{0}", Connection));
            }

            responseHeader.AppendLine(contentType);
            responseHeader.Append(Environment.NewLine);
            return responseHeader.ToString();
        }
    }

    public class HttpConnection
    {
        public Socket Socket { get; set; }

        public DateTime TimeStamp { get; set; }

        public HttpConnection()
        {
            this.TimeStamp = DateTime.Now;
        }

        public int ReadTimeout { get; set; }

        public int Count { get; set; }
    }

    public interface IHttpHandler
    {
        string Name { get; }

        HttpRequest Request { get; set; }

        HttpResponse Response { get; set; }

        string Session { get; set; }

        string ETag { get; }

        string ContentType { get; }

        Device Device { get; set; }
    }

    public interface IHttpByteHandler : IHttpHandler
    {
        byte[] Handle(string actionMethod);
    }

    public interface IHttpRawHandler : IHttpHandler
    {
        void Handle();
    }

    public class HttpHandler
    {
        public virtual string Name { get { return this.GetType().Name; } }

        public virtual string ContentType
        {
            get { return "text/html"; }
        }

        public Device Device { get; set; }

        public virtual string ETag
        {
            get { return string.Empty; }
        }

        public HttpRequest Request { get; set; }

        public HttpResponse Response { get; set; }

        public string Session { get; set; }
    }

    public abstract class ByteHttpHandler : HttpHandler, IHttpByteHandler
    {
        public virtual byte[] Handle(string actionMethod)
        {
            var result = default(byte[]);
            try
            {
                var methodInfos = this.GetType().GetMethods();
                var methodInfo = default(MethodInfo);
                if (!string.IsNullOrEmpty(actionMethod))
                {
                    methodInfo = methodInfos.FirstOrDefault(m => m.Name.ToUpper() == actionMethod.ToUpper());
                }

                if (methodInfo == null)
                {
                    methodInfo = methodInfos.FirstOrDefault(m => m.Name.ToUpper() == "INDEX");
                }

                if (methodInfo != null)
                {
                    var parameterObjects = new List<object>();
                    var targetString = HttpUtility.UrlDecode(this.Request.Target.Trim('/').Replace("+", "%2B"));
                    var targetSegments = targetString.Split('/');
                    var parameterInfos = methodInfo.GetParameters().Where(p => p.GetCustomAttributes(false).Length == 0);
                    for (var i = 0; i < parameterInfos.Count(); i++)
                    {
                        var parameterInfo = parameterInfos.ElementAt(i);
                        var parameterType = parameterInfo.ParameterType;
                        var parameterObject = parameterType.IsValueType ? Activator.CreateInstance(parameterInfo.ParameterType) : null;
                        if (targetSegments.Length >= i + 1)
                        {
                            var targetSegment = targetSegments[i];
                            if (parameterType == typeof(int))
                            {
                                var integerValue = -1;
                                int.TryParse(targetSegment, out integerValue);
                                parameterObject = integerValue;
                            }
                            else if (parameterType == typeof(string))
                            {
                                parameterObject = targetSegment;
                            }
                        }

                        parameterObjects.Add(parameterObject);
                    }

                    result = methodInfo.Invoke(this, parameterObjects.ToArray()) as byte[];
                }
            }
            catch (Exception ex)
            {
                result = Encoding.UTF8.GetBytes(ex.ToString());
            }

            return result;
        }
    }

    public class RawHttpHandler : HttpHandler, IHttpRawHandler
    {
        public virtual void Handle()
        {
        }
    }

    public class UnknownHandler : ByteHttpHandler
    {
        public override byte[] Handle(string actionName)
        {
            return Encoding.UTF8.GetBytes(@"Page Not Found");
        }
    }

    public class StaticFileHandler : ByteHttpHandler
    {
        public override string ETag
        {
            get { return "EDA2B9B0282Ds4CAA00FE56AEB"; }
        }

        public override string ContentType
        {
            get
            {
                var result = "text/html";
                var target = Request.Target;
                if (target.EndsWith(".png"))
                {
                    result = "image/png";
                }
                else if (target.EndsWith(".ico"))
                {
                    result = "image/ico";
                }
                else if (target.EndsWith(".jpg"))
                {
                    result = "image/jpg";
                }
                else if (target.EndsWith(".css"))
                {
                    result = "text/css";
                }
                else if (target.EndsWith(".js"))
                {
                    result = "application/javascript";
                }

                return result;
            }
        }

        public override byte[] Handle(string actionName)
        {
            var result = default(byte[]);
            var fileInfo = new FileInfo(Assembly.GetEntryAssembly().Location);
            Response.ContentType = ContentType;
            var location = Path.Combine(fileInfo.DirectoryName, HttpUtility.HtmlDecode(Request.Target.TrimStart('/').Replace("/", "\\")));
            if (File.Exists(location))
            {
                result = File.ReadAllBytes(location);
            }

            return result;
        }
    }

    public abstract class DefaultHandler : ByteHttpHandler
    {
    }

    [AttributeUsageAttribute(AttributeTargets.Parameter)]
    public class FromUri : Attribute
    {

    }

    [AttributeUsageAttribute(AttributeTargets.Parameter)]
    public class FromQueryString : Attribute
    {

    }

    [AttributeUsageAttribute(AttributeTargets.Parameter)]
    public class FromBody : Attribute
    {

    }

    public static class SystemHelper
    {
        public static int IndexOf(this byte[] source, byte[] pattern)
        {
            var result = -1;
            var buffer = new byte[pattern.Length];
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        break;
                    }
                    else if (j == pattern.Length - 1)
                    {
                        return i;
                    }
                }
            }

            return result;
        }

        public delegate void MethodInvoker();

        public static void InvokeSilently(MethodInvoker ethodInvoker)
        {
            try
            {
                ethodInvoker.Invoke();
            }
            catch
            {
            }
        }
    }

    public class PositionTagged<T>
    {
        public int Position { get; private set; }

        public T Value { get; private set; }

        public PositionTagged(T value, int offset)
        {
            Position = offset;
            Value = value;
        }

        public static implicit operator PositionTagged<T>(Tuple<T, int> value)
        {
            return new PositionTagged<T>(value.Item1, value.Item2);
        }
    }

    public class AttributeValue
    {
        public PositionTagged<string> Prefix { get; private set; }

        public PositionTagged<object> Value { get; private set; }

        public bool Literal { get; private set; }

        public AttributeValue(PositionTagged<string> prefix, PositionTagged<object> value, bool literal)
        {
            Prefix = prefix;
            Value = value;
            Literal = literal;
        }

        public static implicit operator AttributeValue(Tuple<Tuple<string, int>, Tuple<object, int>, bool> value)
        {
            return new AttributeValue(value.Item1, value.Item2, value.Item3);
        }

        public static implicit operator AttributeValue(Tuple<Tuple<string, int>, Tuple<string, int>, bool> value)
        {
            return new AttributeValue(value.Item1, new PositionTagged<object>(value.Item2.Item1, value.Item2.Item2), value.Item3);
        }
    }

    public abstract class TemplateBase
    {
        [Browsable(false)]
        public StringBuilder Buffer { get; set; }

        public virtual dynamic Model { get; set; }

        [Browsable(false)]
        public StringWriter Writer { get; set; }

        public TemplateBase()
        {
            Buffer = new StringBuilder();
            Writer = new StringWriter(Buffer);
        }

        public abstract void Execute();

        public virtual void Write(object value)
        {
            WriteLiteral(value);
        }

        protected void WriteAttribute(string attribute, PositionTagged<string> prefix, PositionTagged<string> suffix, params AttributeValue[] values)
        {
            Buffer.Append(prefix.Value);
            if (values != null)
            {
                foreach (var attributeValue in values)
                {
                    Buffer.Append(attributeValue.Prefix.Value);

                    var value = attributeValue.Value.Value;
                    if (value != null)
                    {
                        Buffer.Append(value);
                    }
                }
            }

            Buffer.Append(suffix.Value);
        }
        public virtual void WriteLiteral(object value)
        {
            Buffer.Append(value);
        }
    }

    /*public class RazorEngine
    {
        static readonly object _locker = new object();
        private static Dictionary<string, TemplateBase> viewObjects = new Dictionary<string, TemplateBase>();
        public static string View(string htmlUrl, object model)
        {
            var result = string.Empty;
            var fileInfo = new FileInfo(Assembly.GetEntryAssembly().Location);
            var location = Path.Combine(fileInfo.DirectoryName, htmlUrl);

            var viewTemplate = default(TemplateBase);
            if (!viewObjects.ContainsKey(htmlUrl))
            {
                viewTemplate = RazorEngine.Parse(File.ReadAllText(location));
                viewObjects[htmlUrl] = viewTemplate;
            }
            else
            {
                viewTemplate = viewObjects[htmlUrl];
            }

            lock (_locker)
            {
                viewTemplate.Model = !ReferenceEquals(null, model) && model.GetType().Name.StartsWith("<>f__AnonymousType") ? CreateExpandoObject(model) : model;
                viewTemplate.Execute();
                result = viewTemplate.Buffer.ToString();
                viewTemplate.Buffer.Clear();
            }

            return result;
        }

        public static TemplateBase Parse(string razorScript)
        {
            var razorEngineHost = new RazorEngineHost(new CSharpRazorCodeLanguage());
            razorEngineHost.DefaultBaseClass = typeof(TemplateBase).FullName;
            razorEngineHost.DefaultNamespace = "RazorOutput";
            razorEngineHost.DefaultClassName = "Template";
            razorEngineHost.NamespaceImports.Add("System");
            razorEngineHost.NamespaceImports.Add("System.Collections.Generic");
            razorEngineHost.NamespaceImports.Add("System.Linq");

            var razorTemplateEngine = new RazorTemplateEngine(razorEngineHost);
            var razorResult = default(GeneratorResults);
            using (var stringReader = new StringReader(razorScript))
            {
                razorResult = razorTemplateEngine.GenerateCode(stringReader);
            }

            var loaded = typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly != null;
            var codeProvider = new CSharpCodeProvider();
            var builder = new StringBuilder();
            var sourceCode = string.Empty;
            using (var writer = new StringWriter(builder, CultureInfo.InvariantCulture))
            {
                codeProvider.GenerateCodeFromCompileUnit(razorResult.GeneratedCode, writer, new CodeGeneratorOptions());
                sourceCode = builder.ToString();
            }

            var parameters = CreateComplilerParameters();
            var compileResult = codeProvider.CompileAssemblyFromDom(parameters, razorResult.GeneratedCode);
            if (compileResult.Errors != null && compileResult.Errors.HasErrors) throw new Exception(compileResult.Errors[0].ToString());
            var type = compileResult.CompiledAssembly.GetType(string.Format("{0}.{1}", razorEngineHost.DefaultNamespace, razorEngineHost.DefaultClassName));
            var viewTemplate = Activator.CreateInstance(type) as TemplateBase;
            return viewTemplate;
        }

        private static CompilerParameters CreateComplilerParameters()
        {
            var parameters = new CompilerParameters { GenerateInMemory = true, GenerateExecutable = false, IncludeDebugInformation = false, CompilerOptions = "/target:library /optimize", };
            parameters.ReferencedAssemblies.AddRange(GetLoadedAssemblies());
            return parameters;
        }

        private static ExpandoObject CreateExpandoObject(object anonymousObject)
        {
            var expandoObject = anonymousObject as ExpandoObject;
            if (expandoObject != null) return expandoObject;

            var anonymousDictionary = new Dictionary<string, object>();
            if (anonymousObject != null)
            {
                foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(anonymousObject))
                {
                    anonymousDictionary.Add(property.Name, property.GetValue(anonymousObject));
                }
            }

            IDictionary<string, object> expando = new ExpandoObject();
            foreach (var item in anonymousDictionary)
            {
                expando.Add(item);
            }

            return (ExpandoObject)expando;
        }

        private static string[] GetLoadedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).GroupBy(a => a.FullName).Select(grp => grp.First()).Select(a => a.Location).Where(a => !String.IsNullOrWhiteSpace(a)).ToArray();
        }
    }*/

    public class NLogHelper
    {
        public static void Error(Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        internal static void Info(string p)
        {
            Console.WriteLine(p);
        }
    }

    public enum Device
    {
        Desktop,
        Mobile,
        Unknown,
    }
}
