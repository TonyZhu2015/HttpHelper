using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Xml.Serialization;

/*
command line ftp
OPTS UTF8 ON
550 Unknown command
NLST
550 Unknown command
XPWD
550 Unknown command
*/
public class FileBridge
{
    public int Port { get; private set; }

    private static readonly byte[] delimiter = Encoding.UTF8.GetBytes(Environment.NewLine + Environment.NewLine);

    private static readonly BoyerMoore boyerMoore = new BoyerMoore(delimiter);

    public void Start(int port = 8205)
    {
        this.Port = port;
        for (var i = 0; i < 1; i++)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                var listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
                listener.Start(port);
                if (true)
                {
                    var socket = listener.Accept();
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        var processingQueue = new BlockingCollection<IProtocalProcessor>();
                        //send greeting package if necessary, for instance: mysql server
                        ThreadPool.QueueUserWorkItem(delegate { ParseRequests(socket, processingQueue); });
                        ThreadPool.QueueUserWorkItem(delegate { ProcessRequests(socket, processingQueue); });
                    });
                }
            });
        }
    }

    private void ProcessRequests(Socket socket, BlockingCollection<IProtocalProcessor> processingQueue)
    {
        try
        {
            using (processingQueue)
            {
                foreach (var protocalProcessor in processingQueue.GetConsumingEnumerable())
                {
                    if (socket.Connected && !protocalProcessor.Process(socket))
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                }
            }
        }
        finally
        {
            socket.Close();
        }
    }

    private void ParseRequests(Socket socket, BlockingCollection<IProtocalProcessor> processingQueue)
    {
        try
        {
            var buffer = new byte[5];
            var process = true;
            var protocalParser = GetProtocalParser();
            do
            {
                var count = socket.Receive(buffer);
                process = count > 0;
                if (process)
                {
                    var processor = protocalParser.Parse(buffer, count, socket, ref process);
                    processingQueue.InsertOrIgnore(processor);
                }
            }
            while (process);
            MessageBox.Show("connection closed");
        }
        finally
        {
            processingQueue.CompleteAdding();
        }
    }

    public interface IProtocalProcessor
    {
        bool Process(Socket socket);
    }

    public class HttpProcessor : IProtocalProcessor
    {
        private readonly string Request;

        private readonly Stream Stream;

        public HttpProcessor(string request, Stream stream)
        {
            this.Request = request;
            this.Stream = stream;
        }

        public bool Process(Socket socket)
        {
            var result = true;
            var httpHeader = this.Request;
            using (var stream = this.Stream)
            {
                if (stream?.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(memoryStream);
                        MessageBox.Show(string.Format("body:{0}{1}", Environment.NewLine, Encoding.UTF8.GetString(memoryStream.ToArray())));
                    }
                }

                MessageBox.Show(httpHeader);
                var html = string.Empty;
                using (var outputStream = new OutputStream(socket))
                {
                    //string,byte[],object,list,stream
                    html += "<!DOCTYPE html><html><body>hello <img src='/s1.jpg'/>";
                    html += "hello <form method='POST' enctype='multipart/form-data'><input type='text' value='jjj++++++' name='firstname'/><button type='submit'>submit</button>";
                    html += "<input type='file' name='fileToUpload' id='fileToUpload'>";
                    html += "</form>hello <a href='/sys.php'>sys.php</a></body></html>";
                    var body = Encoding.UTF8.GetBytes(html);
                    var header = Encoding.UTF8.GetBytes(ResponseHeader(body.Length));
                    outputStream.Write(header);
                    outputStream.Flush();
                    outputStream.Write(body);
                    //send header to client first.
                }

                result = socket.Connected;
            }

            return result;
        }

        private string ResponseHeader(int ContentLength)
        {
            var contentType = string.Format("Content-Type: {0};charset=utf-8", "text/html");
            var responseHeader = new StringBuilder();
            responseHeader.AppendLine(string.Format("HTTP/1.1 {0}", "200 OK"));

            if (ContentLength != 0)
            {
                responseHeader.AppendLine(string.Format("Content-Length:{0}", ContentLength));
            }

            //responseHeader.AppendLine(string.Format("Connection:{0}", "Close"));          

            responseHeader.AppendLine(contentType);
            responseHeader.Append(Environment.NewLine);
            return responseHeader.ToString();
        }
    }

    private IProtocalParser GetProtocalParser()
    {
        return new HttpParser();
    }

    public interface IProtocalParser
    {
        IProtocalProcessor Parse(byte[] buffer, int count, Socket socket, ref bool process);
    }

    private class HttpParser : IProtocalParser
    {
        private readonly List<byte> byteBag = new List<byte>();

        public IProtocalProcessor Parse(byte[] buffer, int count, Socket socket, ref bool process)
        {
            var result = default(IProtocalProcessor);
            if (count > 0)
            {
                var offset = byteBag.Count < delimiter.Length ? byteBag.Count : byteBag.Count - delimiter.Length + 1;
                var index = boyerMoore.IndexOf(byteBag, offset, buffer, count);
                if (index == -1)
                {
                    byteBag.AddRange(buffer, count);
                }
                else
                {
                    var bytes = new byte[index];
                    if (index > byteBag.Count)
                    {
                        byteBag.CopyTo(bytes);
                        Array.Copy(buffer, 0, bytes, byteBag.Count, index - byteBag.Count);
                    }
                    else
                    {
                        byteBag.CopyTo(0, bytes, 0, index);
                    }

                    var contentLength = 0;
                    var request = Encoding.UTF8.GetString(bytes);
                    var nameString = "Content-Length:";
                    var contentLengthString = request.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(l => l.StartsWith(nameString));
                    if (!string.IsNullOrEmpty(contentLengthString))
                    {
                        if (!int.TryParse(contentLengthString.Substring(contentLengthString.IndexOf(nameString) + nameString.Length).Trim(), out contentLength))
                        {
                            contentLength = 0;
                        }
                    }

                    var headerLength = count - (delimiter.Length + index - byteBag.Count);
                    byteBag.Clear();
                    var inputStream = GetInputStream(buffer, count - headerLength, headerLength, socket, contentLength - headerLength);
                    result = new HttpProcessor(request, inputStream.GetStream());
                }
            }

            return result;
        }

        private IInputStream GetInputStream(byte[] buffer, int offset, int headerLength, Socket socket, int remaining)
        {
            var contentLength = headerLength + remaining;
            var result = contentLength > 100 ? new FileInputStream(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())) as IInputStream : new MemoryInputStream();
            if (contentLength > 0)
            {
                if (headerLength > 0)
                {
                    result.Write(buffer, offset, headerLength);
                }

                if (remaining > 0)
                {
                    var count2 = 0;
                    do
                    {
                        if (socket.Connected)
                        {
                            count2 = socket.Receive(buffer);
                            result.Write(buffer, 0, count2);
                            remaining -= count2;
                        }
                        else
                        {
                            remaining = 0;
                        }
                    } while (count2 > 0 && remaining > 0);
                }
            }

            return result;
        }
    }
}

public interface IInputStream
{
    Stream GetStream();

    void Write(byte[] buffer, int offset, int count);
}

public class OutputStream : MemoryStream
{
    public bool BufferOutput { get; set; } = true;

    public override bool CanRead
    {
        get { return false; }
    }

    private readonly Socket socket;

    public OutputStream(Socket socket)
    {
        this.socket = socket;
    }

    public override void Flush()
    {
        this.Flush(socket);
    }

    public void Write(byte[] buffer)
    {
        base.Write(buffer, 0, buffer.Length);
        if (!this.BufferOutput)
        {
            this.Flush(socket);
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            this.Flush(socket);
        }
        catch { }
        base.Dispose(disposing);
    }
}

public class MemoryInputStream : MemoryStream, IInputStream
{
    private bool readOnly { get; set; } = false;

    public override bool CanWrite
    {
        get { return !readOnly; }
    }

    public Stream GetStream()
    {
        this.readOnly = true;
        return this;
    }
}

public class FileInputStream : FileStream, IInputStream
{
    private bool readOnly { get; set; } = false;

    private string FilePath { get; set; }

    public FileInputStream(string filePath) : base(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite)
    {
        FilePath = filePath;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            File.Delete(FilePath);
        }
        catch
        {
        }
    }

    public override bool CanWrite
    {
        get { return !readOnly; }
    }

    public Stream GetStream()
    {
        this.readOnly = true;
        return this;
    }
}

public class BoyerMoore
{
    private static int ALPHABET_SIZE = 256;
    private readonly byte[] pattern;
    private readonly int[] last;
    private readonly int[] match;
    private readonly int[] suffix;

    public BoyerMoore(byte[] pattern)
    {
        this.pattern = pattern;
        last = new int[ALPHABET_SIZE];
        match = new int[pattern.Length];
        suffix = new int[pattern.Length];
        ComputeLast();
        ComputeMatch();
    }

    public int IndexOf(IList<byte> prefix, int index1, byte[] text, int length)
    {
        int i = pattern.Length - 1;
        int j = pattern.Length - 1;
        while (i < prefix.Count - index1 + Math.Min(text.Length, length))
        {
            var @byte = i < prefix.Count - index1 ? prefix[i + index1] : text[i - (prefix.Count - index1)];
            if (pattern[j] == @byte)
            {
                if (j == 0)
                {
                    return i + index1;
                }
                j--;
                i--;
            }
            else
            {
                i += pattern.Length - j - 1 + Math.Max(j - last[@byte], match[j]);
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

public static class Extensions
{
    public static string ToHtmlEncodedString(this string s)
    {
        if (String.IsNullOrEmpty(s))
            return s;
        return HttpUtility.HtmlEncode(s);
    }

    public static void Raise(this EventHandler handler, object sender, EventArgs e)
    {
        if (handler != null)
        {
            handler(sender, e);
        }
    }

    public static void Raise<T>(this EventHandler<T> handler, object sender, T e) where T : EventArgs
    {
        if (handler != null)
        {
            handler(sender, e);
        }
    }

    public static string Shorten(this string str, int toLength, string cutOffReplacement = " ...")
    {
        if (string.IsNullOrEmpty(str) || str.Length <= toLength)
            return str;
        else
            return str.Remove(toLength) + cutOffReplacement;
    }

    public static string GetMemberName<T, TResult>(this T anyObject, Expression<Func<T, TResult>> expression)
    {
        return ((MemberExpression)expression.Body).Member.Name;
    }

    public static Color GetForegroundColor(this Color input)
    {
        // Math taken from one of the replies to
        // http://stackoverflow.com/questions/2241447/make-foregroundcolor-black-or-white-depending-on-background
        if (Math.Sqrt(input.R * input.R * .241 + input.G * input.G * .691 + input.B * input.B * .068) > 128)
            return Color.Black;
        else
            return Color.White;
    }

    // Converts a given Color to gray
    public static Color ToGray(this Color input)
    {
        int g = (int)(input.R * .299) + (int)(input.G * .587) + (int)(input.B * .114);
        return Color.FromArgb(input.A, g, g, g);
    }

    public static bool IsNullOrEmpty(this ICollection obj)
    {
        return (obj == null || obj.Count == 0);
    }

    public static T Deserialize<T>(this string xmlString)
    {
        var serializer = new XmlSerializer(typeof(T));
        using (var reader = new StringReader(xmlString))
        {
            return (T)serializer.Deserialize(reader);
        }
    }

    public static string Serialize<T>(this T obj)
    {
        var serializer = new XmlSerializer(obj.GetType());
        using (var writer = new StringWriter())
        {
            serializer.Serialize(writer, obj);
            return writer.ToString();
        }
    }

    public static bool EqualsIgnoreCase(this string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static string Substring(this string source, string delimiter)
    {
        var result = source;
        if (source.IndexOf(delimiter, StringComparison.Ordinal) != -1)
        {
            result = source.Substring(0, source.IndexOf(delimiter, StringComparison.Ordinal));
        }

        return result;
    }

    public static string Replace(this string source, IEnumerable<Tuple<string, string>> replacements)
    {
        var result = source;
        foreach (var replacement in replacements)
        {
            result = result.Replace(replacement.Item1, replacement.Item2);
        }

        return result;
    }

    public static string Truncate(this string source, int startIndex, int endIndex)
    {
        if (source != null)
        {
            var length = endIndex - startIndex + 1;
            if (length >= 0)
            {
                source = source.Substring(startIndex, endIndex - startIndex + 1);
            }
        }

        return source;
    }

    public static string SplitUpperCase(this string source)
    {
        if (source == null)
        {
            return null;
        }

        if (source.Length == 0)
        {
            return string.Empty;
        }

        var words = new StringCollection();
        var wordStartIndex = 0;

        var letters = source.ToCharArray();
        var previousChar = char.MinValue;

        // Skip the first letter. we don't care what case it is
        for (var i = 1; i < letters.Length; i++)
        {
            if (char.IsUpper(letters[i]) && !char.IsWhiteSpace(previousChar))
            {
                // Ignore exceptions
                if (previousChar == 'I' && letters[i] == 'D')
                {
                    continue;
                }

                // Grab everything before the current character
                words.Add(new string(letters, wordStartIndex, i - wordStartIndex));
                wordStartIndex = i;
            }

            previousChar = letters[i];
        }

        // We need to have the last word
        words.Add(new string(letters, wordStartIndex, letters.Length - wordStartIndex));

        var lowerWords = new[] { "and", "by" };

        var j = 0;
        var wordArray = new string[words.Count];
        foreach (var word in words)
        {
            if (lowerWords.Contains(word.ToLower()))
            {
                wordArray[j] = word.ToLower();
            }
            else
            {
                wordArray[j] = word;
            }

            j++;
        }

        return string.Join(" ", wordArray);
    }

    public static string ConvertHtmlToPlainText(this string source)
    {
        if (source == null)
        {
            return null;
        }

        if (source.Length == 0)
        {
            return string.Empty;
        }

        source = source.Replace("\n", " ");
        source = source.Replace("\t", " ");
        source = Regex.Replace(source, "\\s+", " ");
        source = Regex.Replace(source, "<head.*?</head>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        source = Regex.Replace(source, "<script.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var stringBuilder = new StringBuilder(source);
        string[] oldWords = { "&nbsp;", "&amp;", "&quot;", "&lt;", "&gt;", "&reg;", "&copy;", "&bull;", "&trade;" };
        string[] newWords = { " ", "&", "\"", "<", ">", "®", "©", "•", "™" };
        for (var i = 0; i < oldWords.Length; i++)
        {
            stringBuilder.Replace(oldWords[i], newWords[i]);
        }

        stringBuilder.Replace("<BR", "<br");
        stringBuilder.Replace("<br>", string.Format("{0}<br>", Environment.NewLine));
        stringBuilder.Replace("<br ", string.Format("{0}<br ", Environment.NewLine));
        stringBuilder.Replace("<P", "<p");
        stringBuilder.Replace("<p>", string.Format("{0}{0}<p>", Environment.NewLine));
        stringBuilder.Replace("<p ", string.Format("{0}{0}<p ", Environment.NewLine));

        var plainText = Regex.Replace(stringBuilder.ToString(), "<[^>]*>", string.Empty);
        plainText = string.Join(Environment.NewLine, plainText.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Select(s => s.Trim()));

        while (plainText.Contains(Environment.NewLine + Environment.NewLine + Environment.NewLine))
        {
            plainText = plainText.Replace(Environment.NewLine + Environment.NewLine + Environment.NewLine, Environment.NewLine + Environment.NewLine);
        }

        return plainText.Trim().Trim(Environment.NewLine);
    }

    public static string Trim(this string source, string trimString)
    {
        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(trimString))
        {
            source = TrimStart(source, trimString);
            source = TrimEnd(source, trimString);
        }

        return source;
    }

    public static string TrimStart(this string source, string trimString)
    {
        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(trimString))
        {
            while (source.StartsWith(trimString))
            {
                source = source.Substring(trimString.Length);
            }
        }

        return source;
    }

    public static string TrimEnd(this string source, string trimString)
    {
        if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(trimString))
        {
            while (source.EndsWith(trimString))
            {
                source = source.Substring(0, source.Length - trimString.Length);
            }
        }

        return source;
    }

    public static string Join(this IEnumerable<string> enumerableObject, string separator)
    {
        return string.Join(separator, enumerableObject.ToArray());
    }

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var line in source)
        {
            action(line);
        }
    }

    public static async Task ForEach<T>(this IEnumerable<T> source, Func<T, Task> action)
    {
        foreach (var line in source)
        {
            await action(line);
        }
    }

    static public IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
    {
        if (source == null) throw new ArgumentNullException("source");

        T[] array = source.ToArray();
        Random rnd = new Random();
        for (int n = array.Length; n > 1;)
        {
            int k = rnd.Next(n--); // 0 <= k < n

            //Swap items
            if (n != k)
            {
                T tmp = array[k];
                array[k] = array[n];
                array[n] = tmp;
            }
        }

        foreach (var item in array) yield return item;
    }

    public static string ToString(this byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return "";

        // Ansi as default
        Encoding encoding = Encoding.Default;

        /*
    		EF BB BF	UTF-8 
    		FF FE UTF-16	little endian 
    		FE FF UTF-16	big endian 
    		FF FE 00 00	UTF-32, little endian 
    		00 00 FE FF	UTF-32, big-endian 
    	 */

        if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
            encoding = Encoding.UTF8;
        else if (buffer[0] == 0xfe && buffer[1] == 0xff)
            encoding = Encoding.Unicode;
        else if (buffer[0] == 0xfe && buffer[1] == 0xff)
            encoding = Encoding.BigEndianUnicode; // utf-16be
        else if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
            encoding = Encoding.UTF32;
        else if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
            encoding = Encoding.UTF7;

        using (var stream = new MemoryStream())
        {
            stream.Write(buffer, 0, buffer.Length);
            stream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(stream, encoding))
            {
                return reader.ReadToEnd();
            }
        }
    }


    public static byte[] XOR(this byte[] arr1, byte[] arr2)
    {
        if (arr1.Length != arr2.Length)
        {
            throw new ArgumentException("arr1 and arr2 are not the same length");
        }

        var result = new byte[arr1.Length];
        for (var i = 0; i < arr1.Length; ++i)
        {
            result[i] = (byte)(arr1[i] ^ arr2[i]);
        }

        return result;
    }

    public static byte[] Concat(this byte[] a1, byte[] a2)
    {
        var result = new byte[a1.Length + a2.Length];
        Buffer.BlockCopy(a1, 0, result, 0, a1.Length);
        Buffer.BlockCopy(a2, 0, result, a1.Length, a2.Length);
        return result;
    }

    public static void Start(this Socket socket, int port)
    {
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Listen(100);
    }

    public static void Flush(this Stream stream, Socket socket)
    {
        if (socket.Connected)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[2048];
            var count = int.MaxValue;
            while (count > 0 && socket.Connected)
            {
                count = stream.Read(buffer, 0, buffer.Length);
                if (socket.Connected)
                {
                    socket.Send(buffer, count, SocketFlags.None);
                }
            }

            stream.SetLength(0);
        }
    }

    public static void AddRange<T>(this IList<T> items, T[] array, int count)
    {
        for (var i = 0; i < array.Length && i < count; i++)
        {
            items.Add(array[i]);
        }
    }

    public static Type GetPropertyType(this Type baseType, string propertyString)
    {
        var propertyStrings = propertyString.Split('.');
        var propertyQueue = new Queue<string>();
        foreach (var property in propertyStrings)
        {
            propertyQueue.Enqueue(property);
        }

        return GetPropertyType(baseType, propertyQueue);
    }

    private static Type GetPropertyType(Type type, Queue<string> propertyStrings)
    {
        var propertyName = propertyStrings.Dequeue();
        var propertyInfo = type.GetProperty(propertyName);
        if (propertyInfo != null)
        {
            if (propertyStrings.Count == 0)
            {
                return propertyInfo.PropertyType;
            }

            return GetPropertyType(propertyInfo.PropertyType, propertyStrings);
        }

        return null;
    }

    public static T GetAttribute<T>(this PropertyInfo propertyInfo)
    {
        return (T)propertyInfo.GetCustomAttributes(typeof(T), false).FirstOrDefault();
    }

    public static T GetAttribute<T>(this FieldInfo fieldInfo)
    {
        return (T)fieldInfo.GetCustomAttributes(typeof(T), false).FirstOrDefault();
    }

    public static void CaptureScreen(string storageUrl, System.Drawing.Point location, System.Drawing.Size size)
    {
        /*var screenshot = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        var screenShotGraphics = Graphics.FromImage(screenshot);
        screenShotGraphics.CopyFromScreen(location.X, location.Y, 0, 0, size, CopyPixelOperation.SourceCopy);
        screenShotGraphics.Dispose();

        var qualityParam = new EncoderParameter(Encoder.Quality, (long)Constants.JpgCompressionPercentage);
        var jpegCodec = GetEncoderInfo("image/jpeg");
        var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = qualityParam;
        screenshot.Save(storageUrl, jpegCodec, encoderParameters);*/
    }

    public static byte[] ToByteArray(this Image image)
    {
        var stream = new MemoryStream();
        try
        {
            image.Save(stream, image.RawFormat);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public static Image ToImage(this byte[] bytes)
    {
        Image convertedImage = null;
        using (var memoryStream = new MemoryStream(bytes))
        {
            try
            {
                convertedImage = Image.FromStream(memoryStream);
            }
            catch (ArgumentException ex)
            {
            }
        }

        return convertedImage;
    }

    public class GenericComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, object> _uniqueCheckerMethod;

        public GenericComparer(Func<T, object> uniqueCheckerMethod)
        {
            this._uniqueCheckerMethod = uniqueCheckerMethod;
        }

        bool IEqualityComparer<T>.Equals(T x, T y)
        {
            return this._uniqueCheckerMethod(x).Equals(this._uniqueCheckerMethod(y));
        }

        int IEqualityComparer<T>.GetHashCode(T obj)
        {
            return this._uniqueCheckerMethod(obj).GetHashCode();
        }
    }

    public static IEnumerable<T> Distinct<T>(this IEnumerable<T> source, Func<T, object> uniqueCheckerMethod)
    {
        return source.Distinct(new GenericComparer<T>(uniqueCheckerMethod));
    }

    public static IEnumerable<T> Pop<T>(this Stack<T> stack, int count)
    {
        var result = new List<T>();
        for (var i = 0; i < count; i++)
        {
            if (stack.Any())
            {
                result.Add(stack.Pop());
            }
        }

        return result;
    }

    public static object GetPropertyValue(this object baseObject, string propertyString)
    {
        var propertyStrings = propertyString.Split('.');
        var propertyQueue = new Queue<string>();
        foreach (var property in propertyStrings)
        {
            propertyQueue.Enqueue(property);
        }

        return GetPropertyValue(baseObject, propertyQueue);
    }

    private static object GetPropertyValue(object currentObject, Queue<string> propertyStrings)
    {
        object result = null;
        if (currentObject != null)
        {
            if (propertyStrings.Count > 0)
            {
                var propertyName = propertyStrings.Dequeue();
                var propertyInfo = currentObject.GetType().GetProperty(propertyName);
                if (propertyInfo != null)
                {
                    var subObject = propertyInfo.GetValue(currentObject, null);
                    if (propertyStrings.Count == 0)
                    {
                        return subObject;
                    }

                    return GetPropertyValue(subObject, propertyStrings);
                }
            }
            else
            {
                result = currentObject;
            }
        }

        return result;
    }

    public static bool InsertOrIgnore<T>(this BlockingCollection<T> items, T item)
    {
        var result = false;
        if (item != null)
        {
            items.Add(item);
            result = true;
        }

        return result;
    }

    public static string HttpPost(string url, IList<string> files, string paramName, NameValueCollection formData)
    {
        var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
        var boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

        var webRequest = (HttpWebRequest)WebRequest.Create(url);
        webRequest.ContentType = "multipart/form-data; boundary=" + boundary;
        webRequest.Method = "POST";
        webRequest.KeepAlive = true;
        webRequest.Credentials = CredentialCache.DefaultCredentials;

        using (var memoryStream = new System.IO.MemoryStream())
        {
            var formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in formData.Keys)
            {
                memoryStream.Write(boundarybytes, 0, boundarybytes.Length);
                var formitem = string.Format(formdataTemplate, key, formData[key]);
                var formitembytes = Encoding.UTF8.GetBytes(formitem);
                memoryStream.Write(formitembytes, 0, formitembytes.Length);
            }

            memoryStream.Write(boundarybytes, 0, boundarybytes.Length);

            var headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: application/octet-stream\r\n\r\n";
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var header = string.Format(headerTemplate, paramName, file);
                var headerbytes = Encoding.UTF8.GetBytes(header);
                memoryStream.Write(headerbytes, 0, headerbytes.Length);
                using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[4096];
                    var bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        memoryStream.Write(buffer, 0, bytesRead);
                    }
                }

                if (i != files.Count - 1)
                {
                    memoryStream.Write(boundarybytes, 0, boundarybytes.Length);
                }
            }

            var trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            memoryStream.Write(trailer, 0, trailer.Length);

            memoryStream.Position = 0;
            var tempBuffer = new byte[memoryStream.Length];
            memoryStream.Read(tempBuffer, 0, tempBuffer.Length);
            memoryStream.Close();
            var requestStream = webRequest.GetRequestStream();
            requestStream.Write(tempBuffer, 0, tempBuffer.Length);
            requestStream.Close();
        }

        using (var reader = new StreamReader(webRequest.GetResponse().GetResponseStream()))
        {
            return reader.ReadToEnd();
        }
    }

    public static TcpClient Accept(this TcpListener tcpListener)
    {
        tcpListener.Start();
        return tcpListener.AcceptTcpClient();
    }

    public static void WriteLine(this NetworkStream stream, string message)
    {
        stream.Write($"{message}{Environment.NewLine}");
    }

    public static void Write(this NetworkStream stream, string message)
    {
        Log(message);
        stream.Write(Encoding.UTF8.GetBytes(message));
    }

    public static void Write(this NetworkStream stream, byte[] buffer)
    {
        stream.Write(buffer, 0, buffer.Length);
    }

    public static void Read(this Stream stream, byte[] buffer)
    {
        stream.Read(buffer, 0, buffer.Length);
    }

    public static void Log(string message)
    {
        File.AppendAllText($@"{AppDomain.CurrentDomain.BaseDirectory}\log.txt", $"{ message}{Environment.NewLine}");
    }

    public static void Log(Exception ex)
    {
        Log(ex.ToString());
    }

    public static async Task FtpProtocol()
    {
        var listener = new TcpListener(21);
        listener.Start();
        while (true)
        {
            var clientSocket = await listener.AcceptTcpClientAsync();
#pragma warning disable CS4014
            FtpCommandProtocol(clientSocket);
#pragma warning restore CS4014 
        }
    }

    public static async Task FtpCommandProtocol(TcpClient commandSocket)
    {
        using (commandSocket)
        {
            try
            {
                using (var networkStream = commandSocket.GetStream())
                {
                    /*
                    530 Not logged in.
                    530 Login authentication failed.
                    530 Password rejected.
                    */
                    networkStream.WriteLine($"220 WELCOME");
                    var userName = string.Empty;
                    var buffer = new byte[50000];
                    var authenticated = false;
                    var rootDirectory = new DirectoryInfo(@"C:/Users/rong/Documents");
                    var offset = 0;
                    var beningRenaming = false;
                    File.AppendAllText($@"{AppDomain.CurrentDomain.BaseDirectory}\log.txt", string.Empty);
                    var namePrefix = "/";
                    var count = 0;
                    var passiveListner = default(TcpListener);
                    var dataSocket = default(TcpClient);
                    do
                    {
                        count = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (count > 0)
                        {
                            var command = Encoding.UTF8.GetString(buffer, 0, count).Trim(Environment.NewLine);
                            Log($"{command}{Environment.NewLine}");
                            if (command.StartsWith("USER"))
                            {
                                beningRenaming = false;
                                authenticated = false;
                                userName = command.Substring(command.IndexOf(' ') + 1);
                                networkStream.WriteLine($"331 User {userName} logged in, needs password");
                            }
                            else if (command.StartsWith("PASS"))
                            {
                                beningRenaming = false;
                                if (!string.IsNullOrEmpty(userName))
                                {
                                    var password = command.Substring(command.IndexOf(' ') + 1);
                                    if (true)
                                    {
                                        authenticated = true;
                                        networkStream.WriteLine($"220 Password ok, FTP server ready");
                                    }
                                    else
                                    {
                                        networkStream.WriteLine($"530 Username or password incorrect");
                                    }
                                }
                                else
                                {
                                    Authenticate(networkStream, false);
                                }
                            }
                            else if (command.StartsWith("SYST"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.Write($"215 UNIX Type: L8{Environment.NewLine}");
                                }
                            }
                            else if (command.StartsWith("PWD"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.WriteLine($"257 \"/{namePrefix.TrimStart("/")}\" is current directory.");
                                }
                            }
                            else if (command.StartsWith("CWD"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    try
                                    {
                                        var pathName = command.Substring(command.IndexOf(' ') + 1);
                                        if (!pathName.StartsWith("/"))
                                        {
                                            pathName = GetPath(namePrefix, pathName);
                                        }

                                        var directory = GetPath(rootDirectory.FullName, pathName);
                                        if (Directory.Exists(directory))//&& must be subdirectory of root directory
                                        {
                                            namePrefix = pathName.Replace('\\', '/');
                                            networkStream.Write($"250 Okay.{Environment.NewLine}");
                                        }
                                        else
                                        {
                                            networkStream.WriteLine($"550 Not a valid directory.");
                                        }
                                    }
                                    catch
                                    {
                                        networkStream.WriteLine($"550 Not a valid directory.");
                                    }
                                }
                            }
                            else if (command.StartsWith("TYPE"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    var type = command.Substring(command.IndexOf(' ') + 1);
                                    if (type == "A")
                                    {
                                        networkStream.Write($"200 ASCII transfer mode active.{Environment.NewLine}");
                                    }
                                    else if (type == "I")
                                    {
                                        networkStream.Write($"200 Binary transfer mode active.{Environment.NewLine}");
                                    }
                                    else
                                    {
                                        networkStream.Write($"550 Error - unknown binary mode {type}{Environment.NewLine}");
                                    }
                                }
                            }
                            else if (command.StartsWith("PASV"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    passiveListner = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
                                    passiveListner.Start();
                                    var m_nPort = ((IPEndPoint)passiveListner.LocalEndpoint).Port;
                                    var sIpAddress = $"127,0,0,1,{(int)(m_nPort / 256)},{(m_nPort % 256)}";
                                    networkStream.WriteLine($"227 Entering Passive Mode ({sIpAddress})");
                                    dataSocket = passiveListner.AcceptTcpClient();
                                }
                            }
                            else if (command.StartsWith("LIST") || command.StartsWith("NLIST"))
                            {
                                /*-h displays hidden files
                                -a does not include the '.' and '..' directories in the listing
                                -F adds file characterizations to the listing. Directories are terminated with a '/' and executable files are terminated with a '*'.
                                -A displays All files.
                                -T when used with - l, displays the full month, day, year, hour, minute, and second for the file date/time.
                                */
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.WriteLine($"150 Opening data connection for LIST.");
                                    var stringBuilder = new StringBuilder();
                                    foreach (var info in new DirectoryInfo(GetPath(rootDirectory.FullName, namePrefix)).GetFileSystemInfos())
                                    {
                                        var sAttributes = info.GetAttributeString();
                                        stringBuilder.Append($"{sAttributes} 1 owner group");
                                        if (info.IsDirectory())
                                        {
                                            stringBuilder.Append("            0 ");
                                        }
                                        else
                                        {
                                            var sFileSize = ((FileInfo)info).Length.ToString();
                                            stringBuilder.Append(sFileSize.RightAlignString(13, ' '));
                                            stringBuilder.Append(" ");
                                        }

                                        var fileDate = info.LastWriteTime;
                                        var sDay = fileDate.Day.ToString();
                                        stringBuilder.Append(fileDate.Month.Month());
                                        stringBuilder.Append(" ");
                                        if (sDay.Length == 1)
                                        {
                                            stringBuilder.Append(" ");
                                        }

                                        stringBuilder.Append($"{sDay} {fileDate:hh}:{fileDate:mm} {info.Name}");
                                        stringBuilder.Append(Environment.NewLine);
                                    }

                                    using (dataSocket)
                                    {
                                        dataSocket.GetStream().Write(stringBuilder.ToString());
                                    }

                                    networkStream.WriteLine($"226 LIST successful.");
                                }
                            }
                            else if (command.StartsWith("REST"))
                            {
                                beningRenaming = false;
                                var position = 0;
                                if (int.TryParse(command.Substring(command.IndexOf(' ') + 1), out position))
                                {
                                    offset = position;
                                    networkStream.WriteLine($"350 REST successful");
                                }
                                else
                                {
                                    networkStream.WriteLine($"550 Error - REST");
                                }
                            }
                            else if (command.StartsWith("RETR"))
                            {
                                /*accepts the RETR request with code 226 if the entire file was successfully written to the server's TCP buffers
                                rejects the RETR request with code 425 if no TCP connection was established
                                rejects the RETR request with code 426 if the TCP connection was established but then broken by the client or by network failure; or
                                rejects the RETR request with code 451 or 551 if the server had trouble reading the file from disk.*/
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.WriteLine($"150 Opening data connection for RETR.");
                                    var fileName = command.Substring(command.IndexOf(' ') + 1);
                                    var filePath = GetPath(rootDirectory.FullName, namePrefix, fileName);
                                    if (File.Exists(filePath))
                                    {
                                        if (dataSocket != null && dataSocket.Connected)
                                        {
                                            using (dataSocket)
                                            {
                                                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                                                {
                                                    fileStream.Seek(offset, SeekOrigin.Begin);
                                                    var bytes = new byte[fileStream.Length - offset];
                                                    fileStream.Read(bytes);
                                                    dataSocket.GetStream().Write(bytes);
                                                }
                                            }

                                            offset = 0;
                                            networkStream.WriteLine($"226 RETR successful.");
                                        }
                                        else
                                        {
                                            networkStream.WriteLine($"425 No TCP connection was established.");
                                        }
                                    }
                                    else
                                    {
                                        networkStream.WriteLine($"550 Error - RETR file does not exist");
                                    }
                                }
                            }
                            else if (command.StartsWith("STOR"))
                            {
                                /*If the server is willing to create a new file under that name, or replace an existing file under that name, it responds with a mark using code 150. It then stops accepting new connections, attempts to read the contents of the file from the data connection, and closes the data connection. Finally it
                                accepts the STOR request with code 226 if the entire file was successfully received and stored
                                rejects the STOR request with code 425 if no TCP connection was established
                                rejects the STOR request with code 426 if the TCP connection was established but then broken by the client or by network failure; or
                                rejects the STOR request with code 451, 452, or 552 if the server had trouble saving the file to disk.*/
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.WriteLine($"150 Opening data connection for STOR.");
                                    var fileName = command.Substring(command.IndexOf(' ') + 1);
                                    var filePath = GetPath(rootDirectory.FullName, namePrefix, fileName);
                                    using (dataSocket)
                                    {
                                        if (dataSocket != null && dataSocket.Connected)
                                        {
                                            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate))
                                            {
                                                fileStream.Seek(offset, SeekOrigin.Begin);
                                                using (dataSocket)
                                                {
                                                    var dataStream = dataSocket.GetStream();
                                                    var count2 = 0;
                                                    var buffer2 = new byte[1024];
                                                    do
                                                    {
                                                        count2 = dataStream.Read(buffer2, 0, buffer2.Length);
                                                        fileStream.Write(buffer2, 0, count2);
                                                    } while (count2 > 0);
                                                }
                                            }

                                            offset = 0;
                                            networkStream.WriteLine($"226 RETR successful.");
                                        }
                                        else
                                        {
                                            networkStream.WriteLine($"425 No TCP connection was established.");
                                        }
                                    }
                                }
                            }
                            else if (command.StartsWith("APPE"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    networkStream.WriteLine($"150 Opening data connection for STOR.");
                                    var fileName = command.Substring(command.IndexOf(' ') + 1);
                                    var filePath = GetPath(rootDirectory.FullName, namePrefix, fileName);
                                    using (dataSocket)
                                    {
                                        if (dataSocket != null && dataSocket.Connected)
                                        {
                                            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate))
                                            {
                                                fileStream.Seek(0, SeekOrigin.End);
                                                using (dataSocket)
                                                {
                                                    var dataStream = dataSocket.GetStream();
                                                    var count2 = 0;
                                                    var buffer2 = new byte[1024];
                                                    do
                                                    {
                                                        count2 = dataStream.Read(buffer2, 0, buffer2.Length);
                                                        fileStream.Write(buffer2, 0, count2);
                                                    } while (count2 > 0);
                                                }
                                            }

                                            offset = 0;
                                            networkStream.WriteLine($"226 RETR successful.");
                                        }
                                        else
                                        {
                                            networkStream.WriteLine($"425 No TCP connection was established.");
                                        }
                                    }
                                }
                            }
                            else if (command.StartsWith("DELE"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    //A typical server accepts DELE with code 250 if the file was successfully removed, or rejects DELE with code 450 or 550 if the removal failed.
                                    var fileName = command.Substring(command.IndexOf(' ') + 1);
                                    var filePath = GetPath(rootDirectory.FullName, namePrefix, fileName);
                                    if (File.Exists(filePath))
                                    {
                                        try
                                        {
                                            File.Delete(filePath);
                                        }
                                        catch
                                        {
                                            networkStream.WriteLine($"550  XXXXXXXXXXXXXX");
                                        }

                                        if (!File.Exists(filePath))
                                        {
                                            networkStream.WriteLine($"250 DELE {fileName} successful.");
                                        }
                                    }
                                    else
                                    {
                                        networkStream.WriteLine($"550 Dele failed");
                                    }
                                }
                            }
                            else if (command.StartsWith("PORT"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    //PORT 127,0,0,1,214,80
                                    var addressString = command.Substring(command.IndexOf(' ') + 1);
                                    var y = addressString.Split(',');
                                    var tcpClient = new TcpClient();
                                    var port = 256 * int.Parse(y[4]) + int.Parse(y[5]);
                                    tcpClient.Connect(IPAddress.Parse(string.Join(".", new[] { y[0], y[1], y[2], y[3] })), port);
                                    dataSocket = tcpClient;
                                    networkStream.WriteLine($"200 PORT successful.");
                                }
                            }
                            else if (command.StartsWith("QUIT"))
                            {
                                networkStream.WriteLine($"221 Bye.");
                                commandSocket?.Close();
                                dataSocket?.Close();
                                passiveListner?.Stop();
                                break;
                            }
                            else if (command.StartsWith("SIZE"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    var pathName = command.Substring(command.IndexOf(' ') + 1).Trim('/');
                                    var path = Path.GetFullPath(Path.Combine(rootDirectory.FullName, namePrefix, pathName));
                                    var size = File.Exists(path) ? (new FileInfo(path).Length) : 0;
                                    networkStream.WriteLine($"213 {size}");
                                }
                            }
                            else if (command.StartsWith("CDUP"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    if (namePrefix.IndexOf('/') != -1)
                                    {
                                        namePrefix = namePrefix.Substring(0, namePrefix.LastIndexOf('/'));
                                    }

                                    networkStream.WriteLine($"250 CWD command successful.");
                                }
                            }
                            else if (command.StartsWith("RNFR"))
                            {
                                beningRenaming = true;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    //A typical server accepts RNFR with code 350 if the file exists, or rejects RNFR with code 450 or 550 otherwise.
                                    networkStream.WriteLine($"550 Unknown command");
                                }
                            }
                            else if (command.StartsWith("RNFR"))
                            {
                                if (beningRenaming)
                                {
                                    if (Authenticate(networkStream, authenticated))
                                    {
                                        //A typical server accepts RNTO with code 250 if the file was renamed successfully, or rejects RNTO with code 550 or 553 otherwise.
                                        networkStream.WriteLine($"550 Unknown command");
                                    }
                                }
                                else
                                {
                                    networkStream.WriteLine($"503 XXXXXXXXXXX");
                                    //RNTO must come immediately after RNFR; otherwise the server may reject RNTO with code .
                                }
                            }
                            else if (command.StartsWith("RMD") || command.StartsWith("XRMD"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    //A typical server accepts RMD with code 250 if the directory was successfully removed, or rejects RMD with code 550 if the removal failed.
                                    networkStream.WriteLine($"550 Unknown command");
                                }
                            }
                            else if (command.StartsWith("MKD") || command.StartsWith("XMKD"))
                            {
                                beningRenaming = false;
                                if (Authenticate(networkStream, authenticated))
                                {
                                    /*If the server accepts MKD (required code 257), its response includes the pathname of the directory, in the same format used for responses to PWD.
                                    A typical server accepts MKD with code 250 if the directory was successfully created, or rejects MKD with code 550 if the creation failed.*/
                                    networkStream.WriteLine($"550 Unknown command");
                                }
                            }
                            /*The ALLO verb

ALLO is obsolete. The server should accept any ALLO request with code 202.*/
                            /*else if (command.StartsWith("RNFR"))
                            {

                            }*/
                            /*else if (command.StartsWith("APPE"))
                            {
                            }*/
                            /*else if (command.StartsWith("REST"))
                            {
                            }*/
                            /*else if (command.StartsWith("FEAT"))
                            {
                                networkStream.Write($"211- Features:");
                                networkStream.Write($" UTF8");
                                networkStream.Write($"211 END");
                        }*/
                            else
                            {
                                networkStream.WriteLine($"550 Unknown command");
                            }
                        }
                    } while (count > 0);
                }
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }
    }

    private static bool Authenticate(NetworkStream networkStream, bool authenticated)
    {
        if (!authenticated)
        {
            networkStream.WriteLine($"550 Login required");
        }

        return authenticated;
    }

    private static string GetPath(string path1, string namePrefix)
    {
        return Path.Combine(path1, namePrefix.TrimStart("/"));
    }

    private static string GetPath(string path1, string namePrefix, string fileName)
    {
        return Path.Combine(GetPath(path1, namePrefix), fileName.TrimStart("/"));
    }

    static public string Month(this int nMonth)
    {
        switch (nMonth)
        {
            case 1:
                return "Jan";
            case 2:
                return "Feb";
            case 3:
                return "Mar";
            case 4:
                return "Apr";
            case 5:
                return "May";
            case 6:
                return "Jun";
            case 7:
                return "Jul";
            case 8:
                return "Aug";
            case 9:
                return "Sep";
            case 10:
                return "Oct";
            case 11:
                return "Nov";
            case 12:
                return "Dec";
            default:
                System.Diagnostics.Debug.Assert(false);
                return "";
        }
    }

    public static string RightAlignString(this string sString, int nWidth, char cDelimiter)
    {
        System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();

        for (int nCharacter = 0; nCharacter < nWidth - sString.Length; nCharacter++)
        {
            stringBuilder.Append(cDelimiter);
        }

        stringBuilder.Append(sString);
        return stringBuilder.ToString();
    }

    public static bool IsDirectory(this FileSystemInfo m_theInfo)
    {
        return (m_theInfo.Attributes & System.IO.FileAttributes.Directory) != 0;
    }

    public static string GetAttributeString(this FileSystemInfo m_theInfo)
    {
        bool fDirectory = (m_theInfo.Attributes & System.IO.FileAttributes.Directory) != 0;
        bool fReadOnly = (m_theInfo.Attributes & System.IO.FileAttributes.ReadOnly) != 0;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        if (fDirectory)
        {
            builder.Append("d");
        }
        else
        {
            builder.Append("-");
        }

        builder.Append("r");

        if (fReadOnly)
        {
            builder.Append("-");
        }
        else
        {
            builder.Append("w");
        }

        if (fDirectory)
        {
            builder.Append("x");
        }
        else
        {
            builder.Append("-");
        }

        if (fDirectory)
        {
            builder.Append("r-xr-x");
        }
        else
        {
            builder.Append("r--r--");
        }

        return builder.ToString();
    }

    public static void FtpDataProtocol(TcpListener listener2)
    {
        listener2.Start();
        ThreadPool.QueueUserWorkItem(delegate
        {
            var PasvSocket = listener2.AcceptTcpClient();
            using (var networkStream = PasvSocket.GetStream())
            {
                var buffer = new byte[50000];
                var count = 0;
                do
                {
                    count = networkStream.Read(buffer, 0, buffer.Length);
                    if (count > 0)
                    {
                        var s = Encoding.UTF8.GetString(buffer, 0, count).Trim(Environment.NewLine);

                    }
                }
                while (count > 0);
            }

            listener2.Stop();
        });
    }

    public static string GetRelativePath(string filespec, string folder)
    {
        Uri pathUri = new Uri(filespec);
        // Folders must end in a slash
        if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            folder += Path.DirectorySeparatorChar;
        }
        Uri folderUri = new Uri(folder);
        return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
    }
}