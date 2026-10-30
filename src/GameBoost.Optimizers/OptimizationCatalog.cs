using System.Runtime.Versioning;
using GameBoost.Core;
namespace GameBoost.Optimizers;

[SupportedOSPlatform("windows")]
public static class OptimizationCatalog
{
    public static IReadOnlyList<IOptimization> Create(IPowerShell shell, Func<Task<bool>>? confirmResolution = null) => [
        new PowerPlanOptimization(),
        new RegistryOptimization(new("game-mode", "Modo de Jogo do Windows", "Ativa a preferência de Modo de Jogo do usuário.", RiskLevel.Low, true), @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1),
        new RegistryOptimization(new("game-dvr", "Desativar captura em segundo plano", "Desativa a captura de jogos; restaura o valor anterior.", RiskLevel.Optional, true), @"System\GameConfigStore", "GameDVR_Enabled", 0),
        new RegistryOptimization(new("app-capture", "Desativar captura de aplicativos", "Desativa a captura do Game DVR para o usuário atual.", RiskLevel.Optional, true), @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0),
        new RegistryOptimization(new("transparency", "Reduzir transparência", "Desativa a preferência de transparência da interface. Algumas janelas só atualizam após reabrir.", RiskLevel.Optional, true), @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0),
        new VisualEffectsOptimization(), new ResolutionOptimization(confirmResolution), new GpuPreferenceOptimization(),
        new DnsOptimization(shell), new QosOptimization(shell),
        new InterfaceRegistryOptimization("tcp-no-delay", "TCPNoDelay"), new InterfaceRegistryOptimization("tcp-ack", "TcpAckFrequency"),
        new RegistryOptimization(new("network-throttling", "Throttling multimídia (experimental)", "Define NetworkThrottlingIndex como desativado. Efeito depende do Windows e pode exigir reinício. Registro restaurável; sem promessa de ganho.", RiskLevel.Optional, true), @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1, true),
        new RegistryOptimization(new("system-responsiveness", "Responsividade multimídia (experimental)", "Define SystemResponsiveness como 20; não aplica valores abaixo do intervalo documentado. Afeta o agendador multimídia e pode exigir reinício.", RiskLevel.Optional, true), @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20, true),
        .. ProtectionPolicy.ManagedServices.Select(name => new ServiceOptimization(shell, name, ServiceDescription(name)))
    ];
    private static string ServiceDescription(string name) => name switch
    {
        "wuauserv" or "DoSvc" or "BITS" => "Pausa downloads/atualizações. Pode interromper tarefas em andamento; o Windows pode reiniciar o serviço automaticamente.",
        "Spooler" => "Pausa impressão apenas sem trabalhos pendentes.",
        "SysMain" => "Pode piorar carregamentos em HDD e não oferece ganho garantido.",
        "WSearch" => "Pausa indexação e pode prejudicar buscas durante a sessão.",
        _ => "Pausa temporariamente este serviço opcional durante a sessão."
    };
}
