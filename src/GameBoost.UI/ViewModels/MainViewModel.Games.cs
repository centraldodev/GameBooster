using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameBoost.Core;
using GameBoost.Windows;
using Microsoft.Win32;
namespace GameBoost.UI.ViewModels;

public sealed partial class MainViewModel
{
    private readonly IPowerShell shell = new PowerShellRunner();
    private readonly BackgroundApps backgroundApps = new();
    private readonly GameSession gameSession;
    public bool GameActive => gameSession.Active;
    public ObservableCollection<InstalledGame> DiscoveredGames { get; } = [];
    public ObservableCollection<ServiceListing> Services { get; } = [];
    public ObservableCollection<BackgroundApp> Apps { get; } = [];
    public InstalledGame? SelectedDiscoveredGame { get; set; }
    public BackgroundApp? SelectedApp { get; set; }
    public IEnumerable<OptimizationOption> SystemOptions => Options.Where(x => !x.Info.Id.StartsWith("service:") && !NetworkIds.Contains(x.Info.Id));
    public IEnumerable<OptimizationOption> ServiceOptions => Options.Where(x => x.Info.Id.StartsWith("service:"));
    public IEnumerable<OptimizationOption> NetworkOptions => Options.Where(x => NetworkIds.Contains(x.Info.Id));
    private static readonly string[] NetworkIds = ["dns", "qos", "tcp-no-delay", "tcp-ack", "network-throttling", "system-responsiveness"];
    private string monitorExecutable = "", arguments = "", interfaceId = "", dnsPrimary = "1.1.1.1", dnsSecondary = "1.0.0.1";
    private int resolutionWidth = 1280, resolutionHeight = 720;
    public int ResolutionWidth { get => resolutionWidth; set => Set(ref resolutionWidth, value); }
    public int ResolutionHeight { get => resolutionHeight; set => Set(ref resolutionHeight, value); }
    private bool autoRestore = true, createRestorePoint = true;
    public string MonitorExecutable { get => monitorExecutable; set => Set(ref monitorExecutable, value); }
    public string Arguments { get => arguments; set => Set(ref arguments, value); }
    public bool AutoRestore { get => autoRestore; set => Set(ref autoRestore, value); }
    public bool CreateRestorePoint { get => createRestorePoint; set => Set(ref createRestorePoint, value); }
    public string InterfaceId { get => interfaceId; set { if (value is not null) Set(ref interfaceId, value); } }
    public string DnsPrimary { get => dnsPrimary; set => Set(ref dnsPrimary, value); }
    public string DnsSecondary { get => dnsSecondary; set => Set(ref dnsSecondary, value); }
    public AsyncCommand LaunchCommand { get; private set; } = null!;
    public AsyncCommand DiscoverCommand { get; private set; } = null!;
    public AsyncCommand ImportGameCommand { get; private set; } = null!;
    public AsyncCommand RefreshAppsCommand { get; private set; } = null!;
    public AsyncCommand RefreshServicesCommand { get; private set; } = null!;
    public AsyncCommand CloseAppCommand { get; private set; } = null!;
    public AsyncCommand BrowseMonitorCommand { get; private set; } = null!;
    private void InitializeFeatures()
    {
        LaunchCommand = Feature(BeginGameAsync, () => !HasPending && !GameActive);
        DiscoverCommand = Feature(async () =>
        {
            DiscoveredGames.Clear();
            foreach (var game in await new GameDiscovery(shell).DiscoverAsync()) DiscoveredGames.Add(game);
            Status = $"{DiscoveredGames.Count} instalação(ões) encontrada(s). Selecione e confirme o executável real.";
        });
        ImportGameCommand = Feature(() =>
        {
            var game = SelectedDiscoveredGame ?? throw new InvalidOperationException("Selecione um jogo detectado.");
            var picker = new OpenFileDialog { InitialDirectory = game.Directory, Filter = "Executável (*.exe)|*.exe", CheckFileExists = true };
            if (picker.ShowDialog() == true)
            {
                SelectedProfile = null; GameName = game.Name; Executable = picker.FileName;
                MonitorExecutable = ""; Arguments = ""; AntiCheat = true; AutoRestore = true; CreateRestorePoint = true;
                foreach (var option in Options) option.Selected = false;
            }
            return Task.CompletedTask;
        });
        BrowseMonitorCommand = Feature(() =>
        {
            var picker = new OpenFileDialog { Filter = "Processo real do jogo (*.exe)|*.exe", CheckFileExists = true };
            if (picker.ShowDialog() == true) MonitorExecutable = picker.FileName;
            return Task.CompletedTask;
        });
        RefreshAppsCommand = Feature(() =>
        {
            Apps.Clear(); foreach (var app in backgroundApps.List(GameActive ? gameSession.Executable : EffectiveExecutable)) Apps.Add(app);
            return Task.CompletedTask;
        });
        RefreshServicesCommand = Feature(async () =>
        {
            Services.Clear(); foreach (var item in await new ServiceInspection(shell).ListAsync()) Services.Add(item);
        });
        CloseAppCommand = Feature(() =>
        {
            var app = SelectedApp ?? throw new InvalidOperationException("Selecione uma janela na lista.");
            if (MessageBox.Show($"Solicitar fechamento de {app.Name}: {app.Title}?\nSalve documentos e conclua chamadas/downloads antes de continuar. O fechamento não é desfeito pelo snapshot. O GameBoost não forçará o encerramento nem reabrirá o aplicativo.", "Revisar fechamento", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            {
                backgroundApps.RequestClose(app, GameActive ? gameSession.Executable : EffectiveExecutable);
                Status = "Fechamento solicitado. Resolva qualquer confirmação exibida pelo aplicativo.";
                Apps.Remove(app);
            }
            return Task.CompletedTask;
        });
        InitializeNetwork(); InitializeBenchmark();
    }
    private string EffectiveExecutable
    {
        get
        {
            var path = string.IsNullOrWhiteSpace(MonitorExecutable) ? Executable : MonitorExecutable;
            return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);
        }
    }
    private AsyncCommand Feature(Func<Task> action, Func<bool>? condition = null)
    {
        var command = new AsyncCommand(() => RunAsync(action), () => !busy && storageReady && (condition?.Invoke() ?? true));
        commands.Add(command); return command;
    }
    private async Task BeginGameAsync()
    {
        var profile = ReadProfile();
        if (!string.IsNullOrWhiteSpace(profile.MonitorExecutablePath) && !File.Exists(profile.MonitorExecutablePath)) throw new InvalidOperationException("O executável monitorado não existe.");
        if (!ConfirmActions(profile, true)) return;
        Status = "Preparando a sessão e o ponto de restauração; aguarde…";
        await gameSession.StartAsync(profile);
        Changed(nameof(GameActive));
        Status = "Jogo iniciado. Aguardando o processo real; mantenha o GameBoost aberto para restauração automática.";
        _ = ObserveGameAsync();
    }
    private async Task ObserveGameAsync()
    {
        try { await gameSession.Completion; Status = "Jogo encerrado. A sessão foi finalizada conforme a opção de restauração do perfil."; }
        catch (Exception ex) { Status = "Falha ao acompanhar/restaurar o jogo: " + ex.Message; MessageBox.Show(Status, "Sessão", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally
        {
            Changed(nameof(GameActive));
            try { await RefreshAsync(); } catch (Exception ex) { storageReady = false; Status = ex.Message; }
            foreach (var command in commands) command.Refresh();
        }
    }
}
