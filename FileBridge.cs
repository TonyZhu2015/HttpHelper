using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

//File Bridge
public class FileBridge
{
    public int Port { get; set; }

    private string delimiter = "!@#$%^&*()";

    #region Server

    public void Start(int port = 8205)
    {
        this.Port = port;
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
                        using (var networkStream = tcpClient.GetStream())
                        {
                            ProcessRequest(networkStream);
                        }
                    }
                    catch (Exception ex)
                    {
                        NLogHelper.Error(ex);
                    }
                });
            }
        });
    }

    private void ProcessRequest(NetworkStream inputStream)
    {
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
                    var command = Encoding.UTF8.GetString(byteBag.ToArray());
                    var headerBytes = buffer1.Skip(index + delimiterBytes.Length).ToArray();
                    NLogHelper.Info(command.Replace(delimiter, " "));
                    if (string.Equals(command, "list"))
                    {
                        var bytes = Encoding.UTF8.GetBytes(string.Join("|", from l in new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles() select l.Name));
                        inputStream.Write(bytes, 0, bytes.Length);
                    }
                    else if (command.StartsWith("put"))
                    {
                        var commandStrings = command.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                        var fileName = commandStrings[1];
                        var chunk = int.Parse(commandStrings[2]);
                        var offset = int.Parse(commandStrings[3]);
                        var length = int.Parse(commandStrings[4]);
                        var fileInfo = new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}-{chunk}.temp");
                        using (var fileStream = !fileInfo.Exists ? fileInfo.Create() : File.OpenWrite(fileInfo.FullName))
                        {
                            fileStream.Seek(offset, SeekOrigin.Begin);
                            length -= headerBytes.Length;
                            fileStream.Write(headerBytes, 0, headerBytes.Length);
                            do
                            {
                                count = inputStream.Read(buffer, 0, buffer.Length);
                                length -= count;
                                fileStream.Write(buffer, 0, count);
                            } while (length > 0 && count > 0);
                        }
                    }
                    else if (command.StartsWith("get"))
                    {
                        var commandStrings = command.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                        var fileName = commandStrings[1];
                        var offset = int.Parse(commandStrings[2]);
                        var length = int.Parse(commandStrings[3]);
                        var fileInfo = new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}");
                        if (fileInfo.Exists)
                        {
                            using (var fileStream = File.OpenRead(fileInfo.FullName))
                            {
                                fileStream.Seek(offset, SeekOrigin.Begin);
                                do
                                {
                                    count = fileStream.Read(buffer, 0, buffer.Length);
                                    inputStream.Write(buffer, 0, Math.Min(count, length));
                                    length -= count;
                                }
                                while (length > 0 && count > 0);
                            }
                        }
                    }
                    else if (command.StartsWith("length"))
                    {
                        var commandStrings = command.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                        var fileName = commandStrings[1];
                        var chunk = commandStrings[2];
                        var fileInfo = chunk == "-1" ? new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}") : new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}-{chunk}.temp");
                        var length = fileInfo.Exists ? fileInfo.Length : 0;
                        var bytes = UTF8Encoding.UTF8.GetBytes(length.ToString());
                        inputStream.Write(bytes, 0, bytes.Length);
                    }
                    else if (command.StartsWith("splice"))
                    {
                        var commandStrings = command.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
                        var fileName = commandStrings[1];
                        var chunk = int.Parse(commandStrings[2]);
                        var fileInfo = new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}");
                        using (var fileStream = !fileInfo.Exists ? fileInfo.Create() : File.OpenWrite(fileInfo.FullName))
                        {
                            var offset = 0;
                            for (var i = 1; i < chunk + 1; i++)
                            {
                                fileStream.Seek(offset, SeekOrigin.Begin);
                                var tempFile = new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}-{i}.temp");
                                if (tempFile.Exists)
                                {
                                    using (var tempStream = File.OpenRead(tempFile.FullName))
                                    {
                                        count = tempStream.Read(buffer, 0, buffer.Length);
                                        offset += count;
                                        fileStream.Write(buffer, 0, count);
                                    }

                                    tempFile.Delete();
                                }
                            }
                        }
                    }
                }
            }
        }
        while (count != 0 && index == -1);
    }

    #endregion

    #region Client

    public async Task Download(string fileName)
    {
        var length = Length(fileName, -1);
        var folder = Path.IsPathRooted(fileName) ? Path.GetDirectoryName(fileName) : AppDomain.CurrentDomain.BaseDirectory;
        fileName = Path.GetFileName(fileName);
        var fileInfo = new FileInfo($@"{folder}\{fileName}");
        if (!fileInfo.Exists || length != fileInfo.Length)
        {
            var tasks = new List<Task>();
            var chunk = 4;
            var size = length / chunk;
            var outstanding = length - size * chunk;
            var segments = chunk;
            for (var i = 1; i < chunk; i++)
            {
                fileInfo = new FileInfo($@"{folder}\{fileName}-{i}.temp");
                var x = fileInfo.Exists ? fileInfo.Length : 0;
                if (size != x)
                {
                    tasks.Add(Get($@"{folder}\{fileName}", i, (int)((i - 1) * size + x), (int)x, (int)(size - x)));
                }
                else
                {
                    segments--;
                }
            }

            var tail = size + outstanding;
            fileInfo = new FileInfo($@"{folder}\{fileName}-{chunk}.temp");
            var y = fileInfo.Exists ? fileInfo.Length : 0;
            if (tail != y)
            {
                tasks.Add(Get($@"{folder}\{fileName}", chunk, (int)(length / chunk * (chunk - 1) + y), (int)y, (int)(tail - y)));
            }
            else
            {
                segments--;
            }

            await Task.WhenAll(tasks.ToArray());
            segments -= tasks.Count;
            if (segments == 0)
            {
                var buffer = new byte[1024 * 1024];
                var count = int.MaxValue;
                fileInfo = new FileInfo($@"{folder}\{fileName}");
                using (var fileStream = !fileInfo.Exists ? fileInfo.Create() : File.OpenWrite(fileInfo.FullName))
                {
                    var files = new List<FileInfo>();
                    for (var i = 1; i < chunk + 1; i++)
                    {
                        fileStream.Seek(size * (i - 1), SeekOrigin.Begin);
                        var tempFile = new FileInfo($@"{folder}\{fileName}-{i}.temp");
                        if (tempFile.Exists && tempFile.Length == (i == chunk ? tail : size))
                        {
                            using (var tempStream = File.OpenRead(tempFile.FullName))
                            {
                                do
                                {
                                    count = tempStream.Read(buffer, 0, buffer.Length);
                                    fileStream.Write(buffer, 0, count);
                                }
                                while (count > 0);
                            }

                            files.Add(tempFile);
                        }
                    }

                    files.ForEach(f => f.Delete());
                }
            }
        }
    }

    public async Task Upload(string fileName)
    {
        var fileInfo = Path.IsPathRooted(fileName) ? new FileInfo(fileName) : new FileInfo($"{AppDomain.CurrentDomain.BaseDirectory}{fileName}");
        if (fileInfo.Exists)
        {
            var length = Length(fileName, -1);
            if (length != fileInfo.Length)
            {
                var tasks = new List<Task>();
                var chunk = 4;
                var size = fileInfo.Length / chunk;
                var outstanding = fileInfo.Length - size * chunk;
                var segments = chunk;
                for (var i = 1; i < chunk; i++)
                {
                    length = Length(fileName, i);
                    if (size != length)
                    {
                        tasks.Add(Put(fileName, i, (int)((i - 1) * size), length, (int)(size - length)));
                    }
                    else
                    {
                        segments--;
                    }
                }

                var tail = size + outstanding;
                length = Length(fileName, chunk);
                if (tail != length)
                {
                    tasks.Add(Put(fileName, chunk, (int)(fileInfo.Length / chunk) * (chunk - 1), length, (int)(tail - length)));
                }
                else
                {
                    segments--;
                }

                await Task.WhenAll(tasks.ToArray());
                segments -= tasks.Count;
                if (segments == 0)
                {
                    Splice(fileName, chunk);
                }
            }
        }
    }

    private async Task Get(string fileName, int chunk, int start, int offset, int length)
    {
        var folder = Path.GetDirectoryName(fileName);
        fileName = Path.IsPathRooted(fileName) ? Path.GetFileName(fileName) : fileName;
        await Task.Run(() =>
        {
            using (var tcpClient = GetTcpClient())
            {
                var stream = tcpClient.GetStream();
                var bytes = UTF8Encoding.UTF8.GetBytes($"get{delimiter}{fileName}{delimiter}{start}{delimiter}{length}" + Environment.NewLine + Environment.NewLine);
                stream.Write(bytes, 0, bytes.Length);
                var count = 0;
                var buffer = new byte[4096];
                using (var fileStream = File.OpenWrite($@"{folder}\{fileName}-{chunk}.temp"))
                {
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    do
                    {
                        count = stream.Read(buffer, 0, buffer.Length);
                        fileStream.Write(buffer, 0, count);
                    }
                    while (count > 0);
                }
            }
        });
    }

    private async Task Put(string fileName, int chunk, int start, long offset, int length)
    {
        await Task.Run(() =>
        {
            using (var tcpClient = GetTcpClient())
            {
                var stream = tcpClient.GetStream();
                using (var fileStream = File.OpenRead(Path.IsPathRooted(fileName) ? fileName : $"{AppDomain.CurrentDomain.BaseDirectory}{fileName}"))
                {
                    fileName = Path.IsPathRooted(fileName) ? Path.GetFileName(fileName) : fileName;
                    fileStream.Seek(start + offset, SeekOrigin.Begin);
                    var bytes = UTF8Encoding.UTF8.GetBytes($"put{delimiter}{fileName}{delimiter}{delimiter}{chunk}{delimiter}{offset}{delimiter}{length}" + Environment.NewLine + Environment.NewLine);
                    stream.Write(bytes, 0, bytes.Length);
                    var buffer = new byte[1024];
                    var count = 0;
                    do
                    {
                        count = fileStream.Read(buffer, 0, buffer.Length);
                        stream.Write(buffer, 0, Math.Min(count, length));
                        length -= count;
                    } while (count > 0 && length > 0);
                }
            }
        });
    }

    public long Length(string fileName, int chunk)
    {
        fileName = Path.IsPathRooted(fileName) ? Path.GetFileName(fileName) : fileName;
        var result = 0;
        var stringBuilder = new StringBuilder();
        using (var tcpClient = GetTcpClient())
        {
            var stream = tcpClient.GetStream();
            var bytes = UTF8Encoding.UTF8.GetBytes($"length{delimiter}{fileName}{delimiter}{chunk}" + Environment.NewLine + Environment.NewLine);
            stream.Write(bytes, 0, bytes.Length);
            var buffer = new byte[1024];
            var count = 0;
            do
            {
                count = stream.Read(buffer, 0, buffer.Length);
                stringBuilder.Append(UTF8Encoding.UTF8.GetString(buffer, 0, count));
            }
            while (count > 0);
        }

        int.TryParse(stringBuilder.ToString(), out result);
        return result;
    }

    private void Splice(string fileName, int chunk)
    {
        fileName = Path.IsPathRooted(fileName) ? Path.GetFileName(fileName) : fileName;
        using (var tcpClient = GetTcpClient())
        {
            var stream = tcpClient.GetStream();
            var bytes = UTF8Encoding.UTF8.GetBytes($"splice{delimiter}{fileName}{delimiter}{chunk}" + Environment.NewLine + Environment.NewLine);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    public string[] List()
    {
        using (var tcpClient = GetTcpClient())
        {
            var stream = tcpClient.GetStream();
            var bytes = UTF8Encoding.UTF8.GetBytes("list" + Environment.NewLine + Environment.NewLine);
            stream.Write(bytes, 0, bytes.Length);
            var stringBuilder = new StringBuilder();
            var buffer = new byte[1024];
            var count = 0;
            do
            {
                count = stream.Read(buffer, 0, buffer.Length);
                stringBuilder.Append(UTF8Encoding.UTF8.GetString(buffer, 0, count));
            }
            while (count > 0);

            return stringBuilder.ToString().Split('|');
        }
    }

    public virtual TcpClient GetTcpClient()
    {
        return new TcpClient("127.0.0.1", 8205);
    }

    #endregion
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
