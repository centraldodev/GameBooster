using GameBoost.Core;
namespace GameBoost.Restore;

public sealed class WindowsRestorePointService(IPowerShell shell) : IRestorePointService
{
    public async Task<RestorePointResult> CreateAsync(string description)
    {
        try
        {
            // Verify the new point: Checkpoint-Computer can emit a warning without creating one.
            var output = await shell.RunAsync($"$before=@(Get-ComputerRestorePoint | Select-Object -ExpandProperty SequenceNumber); Checkpoint-Computer -Description {PowerShellLiteral.Quote(description)} -RestorePointType MODIFY_SETTINGS -WarningAction SilentlyContinue; $new=@(Get-ComputerRestorePoint | Where-Object {{$_.SequenceNumber -notin $before}}); if($new.Count -gt 0){{'CREATED'}}else{{'NOT_CREATED'}}", 180);
            return output == "CREATED" ? new(true, "Ponto de restauração criado.") : new(false, "Windows não criou novo ponto (possível limite de 24 h). Snapshot próprio disponível.");
        }
        catch (Exception ex) { return new(false, "Ponto de restauração indisponível: " + ex.Message + " Snapshot próprio disponível."); }
    }
}
