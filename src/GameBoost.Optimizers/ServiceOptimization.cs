using System.Text.Json;
using GameBoost.Core;
namespace GameBoost.Optimizers;

public sealed record ServiceState(string Name, string Status);
public sealed class ServiceOptimization(IPowerShell shell, string name, string description) : IOptimization
{
    public OptimizationInfo Info => new("service:" + name, "Pausar " + name, description + " Só para sem dependentes ativos. O tipo de inicialização não é alterado.", RiskLevel.Optional, true);
    private bool wasRunning;
    private void Validate() { if (!ProtectionPolicy.CanManageService(name)) throw new InvalidOperationException("Serviço protegido ou fora da lista permitida."); }
    public async Task<OriginalValue> CaptureAsync()
    {
        Validate();
        var output = await shell.RunAsync($"$s=Get-Service -Name {PowerShellLiteral.Quote(name)}; if($s.Status -notin @('Running','Stopped')){{throw 'Serviço em transição.'}}; if(@($s.DependentServices | Where-Object {{$_.Status -ne 'Stopped'}}).Count){{throw 'Há serviços dependentes ativos.'}}; if($s.Status -eq 'Running' -and -not $s.CanStop){{throw 'Serviço não aceita parada.'}}; " +
            (name == "Spooler" ? "if(@(Get-Printer -ErrorAction Stop | Get-PrintJob -ErrorAction Stop).Count){throw 'Há impressões pendentes.'}; " : "") + "$s.Status.ToString()");
        if (output is not ("Running" or "Stopped")) throw new InvalidDataException("Estado de serviço inesperado.");
        wasRunning = output == "Running";
        return new("Service", JsonSerializer.Serialize(new ServiceState(name, output)));
    }
    public async Task ApplyAsync()
    {
        Validate(); if (!wasRunning) return;
        await shell.RunAsync($"$s=Get-Service {PowerShellLiteral.Quote(name)}; if(@($s.DependentServices | Where-Object {{$_.Status -ne 'Stopped'}}).Count){{throw 'Há dependentes ativos.'}}; {(name == "Spooler" ? "if(@(Get-Printer | Get-PrintJob).Count){throw 'Há impressões pendentes.'}; " : "")}Stop-Service -Name {PowerShellLiteral.Quote(name)} -ErrorAction Stop; (Get-Service {PowerShellLiteral.Quote(name)}).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(15))");
    }
    public async Task RevertAsync(OriginalValue original)
    {
        Validate();
        var state = JsonSerializer.Deserialize<ServiceState>(original.Value!) ?? throw new InvalidDataException("Snapshot de serviço inválido.");
        if (state.Name != name || original.Kind != "Service" || state.Status is not ("Running" or "Stopped")) throw new InvalidDataException("Serviço do snapshot diverge do catálogo.");
        if (state.Status == "Running") await shell.RunAsync($"Start-Service -Name {PowerShellLiteral.Quote(name)}; (Get-Service {PowerShellLiteral.Quote(name)}).WaitForStatus('Running',[TimeSpan]::FromSeconds(15))");
    }
}
