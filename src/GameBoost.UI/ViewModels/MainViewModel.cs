using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameBoost.Core;
using GameBoost.Data;
using GameBoost.Monitoring;
using GameBoost.Optimizers;
using GameBoost.Restore;
using Microsoft.Win32;
using GameBoost.Windows;
namespace GameBoost.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IProfileStore profiles;
    private readonly ISnapshotStore snapshots;
    private readonly OptimizationSession session;
    private readonly NetworkProbe probe = new();
    private bool busy;
    private bool storageReady;
    private bool pending;
    public bool HasPending { get => pending; private set => Set(ref pending, value); }
    private string status = "Carregando perfis e verificando sessões pendentes…";
    public string Status { get => status; private set => Set(ref status, value); }
    private string gameName = "";
    public string GameName { get => gameName; set => Set(ref gameName, value); }
    private string executable = "";
    public string Executable { get => executable; set => Set(ref executable, value); }
    private bool antiCheat = true;
    public bool AntiCheat { get => antiCheat; set => Set(ref antiCheat, value); }
    private string ping = "Ainda não medido";
    public string Ping { get => ping; private set => Set(ref ping, value); }
    private GameProfile? selectedProfile;
    public GameProfile? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            Set(ref selectedProfile, value);
            if (value is null) return;
            GameName = value.Name; Executable = value.ExecutablePath; AntiCheat = value.HasAntiCheat;
            foreach (var item in Options) item.Selected = value.OptimizationIds.Contains(item.Info.Id);
            MonitorExecutable = value.MonitorExecutablePath; Arguments = value.Arguments;
            AutoRestore = value.AutoRestore; CreateRestorePoint = value.CreateRestorePoint;
            InterfaceId = value.NetworkInterfaceId; DnsPrimary = value.DnsPrimary; DnsSecondary = value.DnsSecondary; ResolutionWidth = value.ResolutionWidth; ResolutionHeight = value.ResolutionHeight;
        }
    }
    public ObservableCollection<GameProfile> Profiles { get; } = [];
    public ObservableCollection<OptimizationOption> Options { get; }
    public ObservableCollection<string> History { get; } = [];
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand NewCommand { get; }
    public AsyncCommand BrowseCommand { get; }
    public AsyncCommand ApplyCommand { get; }
    public AsyncCommand RestoreCommand { get; }
    public AsyncCommand PingCommand { get; }
    private readonly List<AsyncCommand> commands;

    public MainViewModel()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameBoost");
        profiles = new JsonProfileStore(directory);
        snapshots = new JsonSnapshotStore(Path.Combine(directory, "snapshots"));
        var catalog = OptimizationCatalog.Create(shell, GameBoost.UI.Services.ResolutionConfirmation.AskAsync);
        session = new(catalog, snapshots, new WindowsRestorePointService(shell));
        gameSession = new(session, new WindowsGameRuntime(shell));
        Options = new(catalog.Select(x => new OptimizationOption(x.Info)));
        SaveCommand = new(() => RunAsync(SaveAsync), () => !busy && storageReady);
        NewCommand = new(() => RunAsync(() => { SelectedProfile = null; GameName = ""; Executable = ""; AntiCheat = true; MonitorExecutable = ""; Arguments = ""; AutoRestore = true; CreateRestorePoint = true; ResolutionWidth = 1280; ResolutionHeight = 720; foreach (var item in Options) item.Selected = false; return Task.CompletedTask; }), () => !busy && storageReady);
        BrowseCommand = new(() => RunAsync(() =>
        {
            var dialog = new OpenFileDialog { Filter = "Executável do jogo (*.exe)|*.exe", CheckFileExists = true };
            if (dialog.ShowDialog() == true) { Executable = dialog.FileName; if (string.IsNullOrWhiteSpace(GameName)) GameName = Path.GetFileNameWithoutExtension(dialog.FileName); }
            return Task.CompletedTask;
        }), () => !busy && storageReady);
        ApplyCommand = new(() => RunAsync(ApplyAsync), () => !busy && storageReady && !HasPending && !GameActive);
        RestoreCommand = new(() => RunAsync(async () => { await session.RestoreAllAsync(); Status = "Todos os ajustes pendentes foram restaurados."; }), () => !busy && storageReady && HasPending);
        PingCommand = new(() => RunAsync(async () =>
        {
            var sample = await probe.MeasureAsync();
            Ping = sample.Milliseconds is long ms ? $"{ms} ms até {sample.Target}" : $"Sem resposta: {sample.Status}";
        }), () => !busy && storageReady);
        commands = [SaveCommand, NewCommand, BrowseCommand, ApplyCommand, RestoreCommand, PingCommand];
        InitializeFeatures();
    }
    public Task InitializeAsync() => RunAsync(async () =>
    {
        foreach (var profile in await profiles.LoadAsync()) Profiles.Add(profile);
        await RefreshAsync();
        storageReady = true;
        if (Profiles.Count > 0) SelectedProfile = Profiles[0];
        Status = HasPending ? "Há ajustes pendentes de uma sessão anterior. Use Restaurar tudo antes de continuar." : "Pronto. Cadastre um jogo e escolha os ajustes na aba Sistema.";
    });
    private GameProfile ReadProfile()
    {
        if (string.IsNullOrWhiteSpace(GameName)) throw new InvalidOperationException("Informe o nome do jogo.");
        if (!File.Exists(Executable) || !string.Equals(Path.GetExtension(Executable), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selecione um executável .exe existente.");
        return new()
        {
            Id = SelectedProfile?.Id ?? Guid.NewGuid(),
            Name = GameName.Trim(),
            ExecutablePath = Path.GetFullPath(Executable),
            HasAntiCheat = AntiCheat,
            OptimizationIds = Options.Where(x => x.Selected).Select(x => x.Info.Id).ToList(),
            MonitorExecutablePath = MonitorExecutable,
            Arguments = Arguments,
            AutoRestore = AutoRestore,
            CreateRestorePoint = CreateRestorePoint,
            NetworkInterfaceId = InterfaceId,
            DnsPrimary = DnsPrimary,
            DnsSecondary = DnsSecondary,
            ResolutionWidth = ResolutionWidth,
            ResolutionHeight = ResolutionHeight
        };
    }
    private async Task SaveAsync()
    {
        var profile = ReadProfile();
        var updated = Profiles.Where(x => x.Id != profile.Id).Append(profile).ToList();
        await profiles.SaveAsync(updated);
        Profiles.Clear(); foreach (var item in updated) Profiles.Add(item);
        SelectedProfile = profile;
        Status = "Perfil salvo. Os ajustes ainda não foram aplicados.";
    }
    private async Task ApplyAsync()
    {
        var profile = ReadProfile();
        if (profile.OptimizationIds.Count == 0) throw new InvalidOperationException("Selecione ao menos um ajuste.");
        if (!ConfirmActions(profile, false)) return;
        Status = "Preparando proteção e aplicando ajustes. O ponto de restauração pode levar alguns minutos…";
        var snapshot = await session.ApplyAsync(profile);
        Status = "Ajustes aplicados. " + snapshot.RestorePointMessage + " Use Restaurar tudo ao terminar.";
    }
    private bool ConfirmActions(GameProfile profile, bool launch)
    {
        var description = string.Join("\n\n", Options.Where(x => x.Selected).Select(x => $"• {x.Info.Name}: {x.Info.Description}"));
        return MessageBox.Show($"{(launch ? "Iniciar" : "Aplicar ajustes para")} {profile.Name}?\n\n{description}\n\nSnapshot salvo antes de cada ajuste. Ponto de restauração: {(profile.CreateRestorePoint ? "tentar criar; se indisponível, usar snapshot próprio" : "não solicitado")}.\nRestauração ao fechar o jogo: {(launch && profile.AutoRestore ? "automática enquanto o GameBoost estiver aberto" : "manual")}. Nenhum ganho de desempenho é garantido.", "Revisar sessão", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK;
    }
    private async Task RefreshAsync()
    {
        var all = await snapshots.LoadAsync();
        HasPending = all.Any(x => x.Status == SnapshotStatus.Pending);
        History.Clear();
        foreach (var item in all) History.Add($"{item.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm} · {item.GameName} · {(item.Status == SnapshotStatus.Restored ? "Restaurado" : "Pendente")} · {item.Entries.Count} ajuste(s) · {item.RestorePointMessage}");
    }
    public bool IsBusy => busy;
    private async Task RunAsync(Func<Task> operation)
    {
        busy = true; foreach (var command in commands) command.Refresh();
        try { await operation(); }
        catch (Exception ex) { Status = $"Não foi possível concluir: {ex.GetBaseException().Message}"; MessageBox.Show(ex.Message, "GameBoost", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally
        {
            try { await RefreshAsync(); }
            catch (Exception ex) { storageReady = false; Status = $"Falha ao ler snapshots. Alterações bloqueadas: {ex.Message}"; }
            busy = false; foreach (var command in commands) command.Refresh();
        }
    }
}
