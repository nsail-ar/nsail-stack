// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NSail.Storage.Tests;

// An HTTP server on a loopback port of its own, so two of them are two origins. git is a process
// of its own and speaks to a real socket: a stubbed HttpMessageHandler would never see it. It
// records what every request carried, answers with what the test says, or with no answer at all
// holds the connection open forever.
sealed class Loopback : IDisposable
{
    readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    readonly Func<Request, string>? _answer;
    readonly ConcurrentBag<TcpClient> _connections = [];
    readonly CancellationTokenSource _stop = new();

    public Loopback(Func<Request, string>? answer)
    {
        _answer = answer;
        _listener.Start();
        Origin = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

        _ = Task.Run(Accept);
    }

    public string Origin { get; }

    public ConcurrentQueue<Request> Requests { get; } = new();

    // An empty repository as git's smart HTTP protocol, version 2, serves one: a capability
    // advertisement, then a ref listing that holds no ref.
    public static string EmptyRepository(Request request)
    {
        return request.Method == "GET"
            ? Answer("200 OK", "application/x-git-upload-pack-advertisement", "000eversion 2\n000cls-refs\n0000")
            : Answer("200 OK", "application/x-git-upload-pack-result", "0000");
    }

    public static string Redirect(string location)
    {
        return $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
    }

    static string Answer(string status, string contentType, string body)
    {
        return $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n{body}";
    }

    async Task Accept()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient connection;

            try
            {
                connection = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception)
            {
                return;
            }

            _connections.Add(connection);
            _ = Task.Run(() => Serve(connection));
        }
    }

    async Task Serve(TcpClient connection)
    {
        try
        {
            var stream = connection.GetStream();
            var head = await ReadHead(stream);
            var lines = head.Split("\r\n");
            var start = lines[0].Split(' ');
            var headers = lines.Skip(1)
                .Where(line => line.Contains(':'))
                .ToLookup(line => line[..line.IndexOf(':')].Trim(), line => line[(line.IndexOf(':') + 1)..].Trim(), StringComparer.OrdinalIgnoreCase);

            var request = new Request(start[0], start[1], headers["Authorization"].FirstOrDefault());

            Requests.Enqueue(request);

            if (_answer is null)
            {
                return;
            }

            await ReadBody(stream, headers);

            await stream.WriteAsync(Encoding.ASCII.GetBytes(_answer(request)), _stop.Token);
            await stream.FlushAsync(_stop.Token);
            connection.Close();
        }
        catch (Exception)
        {
            connection.Close();
        }
    }

    async Task<string> ReadHead(NetworkStream stream)
    {
        var head = new List<byte>();
        var one = new byte[1];

        while (head.Count < 4 || head[^4] != '\r' || head[^3] != '\n' || head[^2] != '\r' || head[^1] != '\n')
        {
            if (await stream.ReadAsync(one, _stop.Token) == 0)
            {
                break;
            }

            head.Add(one[0]);
        }

        return Encoding.ASCII.GetString(head.ToArray());
    }

    // Read whole before answering: a socket closed on unread bytes resets, and git would read
    // the reset instead of the answer.
    async Task ReadBody(NetworkStream stream, ILookup<string, string> headers)
    {
        if (headers["Content-Length"].FirstOrDefault() is { } length)
        {
            var body = new byte[int.Parse(length)];

            await stream.ReadExactlyAsync(body, _stop.Token);
        }
        else if (headers["Transfer-Encoding"].Any(value => value.Contains("chunked", StringComparison.OrdinalIgnoreCase)))
        {
            var tail = new List<byte>();
            var one = new byte[1];

            while (!Encoding.ASCII.GetString(tail.ToArray()).EndsWith("\r\n0\r\n\r\n", StringComparison.Ordinal)
                && !(tail.Count == 5 && Encoding.ASCII.GetString(tail.ToArray()) == "0\r\n\r\n"))
            {
                if (await stream.ReadAsync(one, _stop.Token) == 0)
                {
                    return;
                }

                tail.Add(one[0]);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();

        foreach (var connection in _connections)
        {
            connection.Dispose();
        }
    }

    public sealed record Request(string Method, string Path, string? Authorization);
}
