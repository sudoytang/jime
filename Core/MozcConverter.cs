using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using Google.Protobuf;
using Mozc.Commands;

namespace Jime;

public sealed class MozcConverter
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Process? _server;
    private ulong _sessionId;

    public async Task<IReadOnlyList<string>> GetCandidatesAsync(string romaji) =>
        (await GetConversionAsync(romaji)).Candidates;

    public async Task<MozcConversion> GetConversionAsync(string romaji)
    {
        if (romaji.Length == 0) return MozcConversion.Empty;
        await _lock.WaitAsync();
        try
        {
            EnsureServer();
            try
            {
                return await ConvertOnceAsync(romaji);
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
            {
                ResetConnection();
                EnsureServer();
                return await ConvertOnceAsync(romaji);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<MozcConversion> ConvertOnceAsync(string romaji)
    {
        await EnsureSessionAsync();
        var id = _sessionId;
        await CallAsync(new Input
        {
            Id = id,
            Type = Input.Types.CommandType.SendCommand,
            Command = new SessionCommand { Type = SessionCommand.Types.CommandType.ResetContext }
        });
        foreach (var character in romaji)
        {
            await CallAsync(new Input
            {
                Id = id,
                Type = Input.Types.CommandType.SendKey,
                Key = new KeyEvent { KeyCode = character }
            });
        }
        var output = await CallAsync(new Input
        {
            Id = id,
            Type = Input.Types.CommandType.SendKey,
            Key = new KeyEvent { SpecialKey = KeyEvent.Types.SpecialKey.Space }
        });
        var segments = output.Preedit?.Segment;
        var candidates = segments is { Count: > 1 }
            ? new List<string> { string.Concat(segments.Select(s => s.Value)) }
            : output.AllCandidateWords?.Candidates.Select(c => c.Value).ToList()
                ?? output.CandidateWindow?.Candidate.Select(c => c.Value).ToList()
                ?? new List<string>();
        var reading = segments is { Count: > 0 }
            ? string.Concat(segments.Select(s => s.Key))
            : output.Preedit?.Segment.FirstOrDefault()?.Key ?? "";
        if (candidates.Count == 0 && reading.Length > 0) candidates.Add(reading);
        var distinctCandidates = candidates.Distinct().ToArray();
        var katakana = distinctCandidates.FirstOrDefault(IsKatakana) ?? "";
        return new MozcConversion(
            distinctCandidates,
            reading,
            katakana,
            segments?.Select(s => s.Key).ToArray() ?? Array.Empty<string>(),
            segments?.Select(s => s.Value).ToArray() ?? Array.Empty<string>());
    }

    private static bool IsKatakana(string value) =>
        value.Length > 0 && value.All(c => c is >= 'ァ' and <= 'ヺ' or 'ー' or '・' or ' ');

    private void EnsureServer()
    {
        if (_server is { HasExited: false }) return;
        var directory = Path.Combine(AppContext.BaseDirectory, "mozc");
        var executable = Path.Combine(directory, "mozc_server.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("找不到随附的 Mozc 转换引擎", executable);
        _server = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Mozc 转换引擎启动失败");
        ResetConnection();
    }

    private async Task EnsureSessionAsync()
    {
        if (_sessionId != 0) return;
        var session = await CallAsync(new Input { Type = Input.Types.CommandType.CreateSession });
        _sessionId = session.Id;
        await CallAsync(new Input
        {
            Id = _sessionId,
            Type = Input.Types.CommandType.SendKey,
            Key = new KeyEvent { SpecialKey = KeyEvent.Types.SpecialKey.On }
        });
    }

    private static async Task<Output> CallAsync(Input input)
    {
        using var pipe = new NamedPipeClientStream(".", $"mozc.{GetPipeKey()}.session",
            PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000);
        pipe.ReadMode = PipeTransmissionMode.Message;
        await pipe.WriteAsync(input.ToByteArray());
        await pipe.FlushAsync();
        using var response = new MemoryStream();
        var buffer = new byte[262144];
        int read;
        do
        {
            read = await pipe.ReadAsync(buffer);
            if (read > 0) response.Write(buffer, 0, read);
        } while (read > 0 && !pipe.IsMessageComplete);
        if (response.Length == 0) throw new IOException("Mozc 管道已断开");
        var output = Output.Parser.ParseFrom(response.ToArray());
        if (output.ErrorCode == Output.Types.ErrorCode.SessionFailure)
            throw new InvalidOperationException("Mozc 无法处理这次转换");
        return output;
    }

    private void ResetConnection()
    {
        _sessionId = 0;
    }

    private static string GetPipeKey()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        return Convert.ToHexString(SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(sid))).ToLowerInvariant();
    }
}

public sealed record MozcConversion(
    IReadOnlyList<string> Candidates,
    string Reading,
    string Katakana,
    IReadOnlyList<string> SegmentReadings,
    IReadOnlyList<string> SegmentValues)
{
    public static readonly MozcConversion Empty = new([], "", "", [], []);
}
