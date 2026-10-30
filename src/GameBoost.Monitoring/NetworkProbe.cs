using System.Net.NetworkInformation;
namespace GameBoost.Monitoring;

public sealed record PingSample(string Target, long? Milliseconds, string Status);
public sealed class NetworkProbe
{
    public async Task<PingSample> MeasureAsync(string target = "1.1.1.1")
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(target, 2000);
            return new(target, reply.Status == IPStatus.Success ? reply.RoundtripTime : null, reply.Status.ToString());
        }
        catch (PingException ex) { return new(target, null, ex.GetBaseException().Message); }
    }
}
