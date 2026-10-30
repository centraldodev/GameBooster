using System.Collections.ObjectModel;
using System.Net;
using GameBoost.Monitoring;
using GameBoost.Windows;
namespace GameBoost.UI.ViewModels;

public sealed partial class MainViewModel
{
    public ObservableCollection<NetworkAdapter> Adapters { get; } = [];
    public ObservableCollection<DnsTestResult> DnsResults { get; } = [];
    private string target = "1.1.1.1", networkSummary = "Ainda não medido";
    public string Target { get => target; set => Set(ref target, value); }
    public string NetworkSummary { get => networkSummary; private set => Set(ref networkSummary, value); }
    public AsyncCommand RefreshAdaptersCommand { get; private set; } = null!;
    public AsyncCommand DiagnoseCommand { get; private set; } = null!;
    public AsyncCommand TestDnsCommand { get; private set; } = null!;
    public AsyncCommand FlushDnsCommand { get; private set; } = null!;
    private void InitializeNetwork()
    {
        RefreshAdaptersCommand = Feature(async () =>
        {
            Adapters.Clear(); foreach (var adapter in await new NetworkAdapters(shell).ListAsync()) Adapters.Add(adapter);
        });
        DiagnoseCommand = Feature(async () =>
        {
            Status = "Medindo 20 amostras de ping…";
            var result = await new NetworkDiagnostics().MeasureAsync(Target);
            NetworkSummary = $"Enviados: {result.Sent} · Recebidos: {result.Received} · Ping: {result.AverageMs:F1} ms · Jitter: {result.JitterMs:F1} ms · Perda: {result.LossPercent:F1}%";
            Status = "Diagnóstico concluído. Ausência de resposta ICMP pode indicar bloqueio, não perda do jogo.";
        });
        TestDnsCommand = Feature(async () =>
        {
            DnsResults.Clear(); Status = "Consultando resolvedores DNS diretamente…";
            foreach (var server in new[] { "1.1.1.1", "8.8.8.8", "9.9.9.9" }) DnsResults.Add(await new DnsBenchmark().MeasureAsync(server));
            Status = "Teste de DNS concluído. Copie os endereços desejados; nenhuma troca é automática.";
        });
        FlushDnsCommand = Feature(async () =>
        {
            if (System.Windows.MessageBox.Show("Limpar agora o cache DNS do Windows? As entradas não serão restauradas; novas consultas reconstruirão o cache.", "Limpar cache DNS", System.Windows.MessageBoxButton.OKCancel) != System.Windows.MessageBoxResult.OK) return;
            await shell.RunAsync("Clear-DnsClientCache");
            Status = "Cache DNS limpo. Esta operação não é reversível; o Windows reconstruirá o cache.";
        });
    }
}
