using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Factos.Tests;

/// The smallest SMTP server MailKit will talk to: accepts one session without TLS or
/// auth and keeps the raw DATA of each message.
public sealed class SmtpSink : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Task _loop;
    public List<string> Messages { get; } = [];
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public SmtpSink()
    {
        _listener.Start();
        _loop = Task.Run(AcceptAsync);
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                await SessionAsync(client);
            }
        }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private async Task SessionAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
        await writer.WriteLineAsync("220 sink ready");
        while (await reader.ReadLineAsync() is { } line)
        {
            var command = line.Split(' ')[0].ToUpperInvariant();
            if (command is "EHLO" or "HELO")
                await writer.WriteLineAsync("250 sink");
            else if (command == "DATA")
            {
                await writer.WriteLineAsync("354 go ahead");
                var data = new StringBuilder();
                while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    data.AppendLine(dataLine);
                Messages.Add(data.ToString());
                await writer.WriteLineAsync("250 queued");
            }
            else if (command == "QUIT")
            {
                await writer.WriteLineAsync("221 bye");
                return;
            }
            else
                await writer.WriteLineAsync("250 ok");
        }
    }

    public void Dispose() => _listener.Stop();
}
