using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
namespace GameBoost.Monitoring;

public sealed record NetworkSummary(int Sent, int Received, double? AverageMs, double? JitterMs, double LossPercent);
public static class NetworkStatistics
{
    public static NetworkSummary Calculate(IReadOnlyList<long?> samples)
    {
        var replies = samples.Where(x => x.HasValue).Select(x => (double)x!.Value).ToList();
        var differences = samples.Zip(samples.Skip(1)).Where(x => x.First.HasValue && x.Second.HasValue).Select(x => Math.Abs((double)x.First!.Value - x.Second!.Value)).ToList();
        return new(samples.Count, replies.Count, replies.Count > 0 ? replies.Average() : null, differences.Count > 0 ? differences.Average() : null, samples.Count > 0 ? 100.0 * (samples.Count - replies.Count) / samples.Count : 0);
    }
}
public sealed class NetworkDiagnostics
{
    public async Task<NetworkSummary> MeasureAsync(string target, int count = 20, CancellationToken token = default)
    {
        if (!IPAddress.TryParse(target, out _)) throw new ArgumentException("Informe um endereço IP para o teste de ping.");
        var samples = new List<long?>(); var probe = new NetworkProbe();
        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            samples.Add((await probe.MeasureAsync(target)).Milliseconds);
            if (i + 1 < count) await Task.Delay(250, token);
        }
        return NetworkStatistics.Calculate(samples);
    }
}
public sealed record DnsTestResult(string Server, double? Milliseconds, string Status);
public sealed class DnsBenchmark
{
    // Queries the resolver directly over UDP, without modifying Windows or its cache.
    public async Task<DnsTestResult> MeasureAsync(string server, CancellationToken token = default)
    {
        if (!IPAddress.TryParse(server, out var address)) throw new ArgumentException("DNS inválido.");
        var timings = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            using var client = new UdpClient(address.AddressFamily);
            client.Connect(address, 53);
            var id = RandomNumberGenerator.GetInt32(65536);
            var request = new List<byte> { (byte)(id >> 8), (byte)id, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var label in "example.com".Split('.')) { request.Add((byte)label.Length); request.AddRange(Encoding.ASCII.GetBytes(label)); }
            request.AddRange(new byte[] { 0, 0, 1, 0, 1 });
            var watch = Stopwatch.StartNew();
            try
            {
                await client.SendAsync(request.ToArray(), token);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
                var reply = await client.ReceiveAsync(timeout.Token);
                var data = reply.Buffer;
                if (data.Length < 12 || data[0] != (byte)(id >> 8) || data[1] != (byte)id || (data[2] & 0x80) == 0 || (data[2] & 0x02) != 0 || (data[3] & 0x0f) != 0 || (data[6] == 0 && data[7] == 0))
                    return new(server, null, "Resposta DNS inválida, truncada ou sem resposta A.");
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(server, null, "Timeout DNS (UDP/53)"); }
            catch (SocketException ex) { return new(server, null, ex.Message); }
        }
        return new(server, timings.OrderBy(x => x).ElementAt(1), "Mediana de 3 consultas A para example.com; pode refletir cache do resolvedor.");
    }
}
