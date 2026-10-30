# Arquitetura e manutenção

## Camadas

```text
UI → Core, Optimizers, Monitoring, Data, Restore, Windows
Optimizers → Core
Restore → Core, Data
Data → Core
Monitoring → Core
Windows → Core
Core → biblioteca padrão
```

| Camada | Responsabilidade |
| --- | --- |
| Core | Modelos, contratos, proteção, transação de otimização e ciclo da sessão de jogo |
| Optimizers | Uma ação por classe: registro, energia, serviços, DNS, QoS, efeitos, resolução e GPU |
| Windows | Execução de PowerShell, CIM/COM, descoberta de jogos, interfaces, inspeção de serviços e janelas |
| Restore | Persistência de snapshots e adaptador de ponto de restauração |
| Data | Perfis e escrita JSON atômica |
| Monitoring | Amostragem nativa, diagnóstico de rede, benchmark, cálculo de métricas e importação CSV |
| UI | Views por aba, bindings, comandos, tema e confirmações |

Cada aba possui `UserControl` em `UI/Views`. O tema e o template de ajustes ficam em `UI/Resources/Theme.xaml`. O ViewModel principal foi separado em arquivos parciais por responsabilidade (`MainViewModel`, `.Games`, `.Network`, `.Benchmark`), compartilhando estado e bloqueio de operações. Se esses módulos crescerem, migre-os para ViewModels próprios com serviços de aplicação compartilhados.

## Ações e contexto por perfil

`IOptimization` expõe `Info`, `CaptureAsync`, `ApplyAsync` e `RevertAsync`. `IProfileOptimization.Configure` valida/prepara o contexto do perfil antes do ponto de restauração e antes de qualquer mutação. `Configure` nunca deve alterar o sistema.

O dado capturado precisa ser suficiente para **uma nova instância** reverter a ação após reabrir o app. DNS persiste GUID, automático/estático e servidores; QoS persiste o nome exclusivo e executável; GPU persiste o caminho usado como nome de valor; resolução persiste monitor e modo original. Não dependa de campos em memória para reverter.

Para novas ações:

1. Dê um ID estável, descrição de efeito e risco, e política explícita de anti-cheat.
2. Valide contexto sem mutações em `Configure`/`CaptureAsync`.
3. Preserve o valor original, inclusive ausência e tipo.
4. Torne a reversão repetível; uma falha pode ter ocorrido depois do efeito, mas antes do registro de sucesso.
5. Registre no catálogo e na categoria da UI.
6. Cubra falhas/recuperação com adaptadores simulados e depois valide a API no Windows.

Não reutilize IDs nem remova a reversão de uma ação que possa constar em snapshots antigos. ID desconhecido mantém pendência em vez de marcar sucesso. O schema de `OriginalValue` é específico de cada ação; alterações futuras devem manter leitores das versões anteriores.

## Persistência e recuperação

`OptimizationSession` serializa ativação/reversão. Persiste uma sessão pendente antes da proteção adicional e persiste cada original antes da mutação. Após aplicar, grava evento de auditoria. Em falha, tenta reverter em ordem inversa, incluindo a ação que falhou, pois ela pode ter produzido efeito parcial.

A reversão continua após falhas individuais. Entradas bem-sucedidas são salvas; a sessão só fica restaurada quando todas terminam. Falhas mantêm snapshots pendentes para nova tentativa. Eventos com timestamp ficam no próprio arquivo: criação, ponto de restauração, aplicação, falha e restauração. Não são enviados a serviço externo.

`AtomicJson` usa arquivo temporário, flush e substituição do arquivo final. Erros de leitura nunca viram histórico vazio. O mutex impede duas instâncias na mesma sessão Windows; coordenação entre usuários/sessões ainda não existe. Outro programa pode alterar uma preferência durante a sessão; restaurar reescreve o valor capturado, sem uma transação global do Windows.

Pontos de restauração são opcionais e independentes: a criação é verificada consultando novos números de sequência; erro/limite é registrado e a sessão prossegue com snapshot próprio, conforme informado na confirmação. Não habilitamos Proteção do Sistema nem modificamos seu limite de frequência automaticamente. O botão Restaurar usa snapshots, não `Restore-Computer`.

## Ciclo de jogo

`GameSession` aplica ajustes, solicita início pela shell do Explorer e observa o executável real por CIM. Não consulta módulos, memória ou handles do jogo. Há timeout de início e confirmação de saída por ausências consecutivas. `GameWatchOptions` permite ajustar intervalos em testes; a UI usa os padrões de 2 minutos/2 segundos/quatro ausências.

O observador mantém o contexto da sessão capturada, independentemente de edições posteriores no formulário. Erros de inicialização/acompanhamento tentam restaurar; encerramento normal respeita `AutoRestore`. A janela bloqueia fechamento durante operações/acompanhamento para não abandonar inadvertidamente a restauração. Interrupção forçada/queda de energia depende da recuperação na próxima abertura.

Não há rastreamento automático de árvore de processos. Launchers que trocam de executável precisam de `MonitorExecutablePath`. A descoberta de instalações não procura arbitrariamente um `.exe` para executar: o usuário confirma o caminho.

## Limites de segurança das ações

`ProtectionPolicy` usa listas positivas. Nenhum nome fora delas pode virar um serviço gerenciado ou janela fechável. Não há encerramento forçado, manipulação de anti-cheat, alteração de segurança, mudanças permanentes de startup ou drivers adicionais.

Fechamento de janelas e flush DNS são ações imediatas separadas: não alegam reversibilidade. Não reabrimos aplicativos como administrador para simular recuperação. Serviços originalmente ativos são reiniciados; os originalmente parados ficam intocados. Dependentes ativos e filas de impressão bloqueiam a pausa.

O adaptador de PowerShell usa caminho absoluto, script codificado, aspas literais, saída UTF-8 e timeout. Dados de usuário nunca são interpolados como código sem validação/escape. Isso evita injeção por caminhos/argumentos, mas não torna entradas locais um limite de segurança contra o próprio administrador da máquina.

## Benchmark

Coletas de sistema usam `GetSystemTimes`/`GlobalMemoryStatusEx` e ping ICMP, sem sensores por driver. A duração real e todas as amostras são preservadas no JSON. O relatório distingue dados do sistema de FPS do jogo.

A base deve estar sem sessão pendente. A medição depois usa as ações do snapshot ativo do mesmo perfil, não as opções atualmente marcadas. O jogo é verificado no início/fim e a identidade/estado da sessão é revalidada ao concluir. Isso não prova equivalência de cena nem carga durante toda a janela.

CSV PresentMon é integração manual. Frametime inválido é descartado; mínimo de 100 frames; múltiplos processos/swapchains identificáveis são recusados. Não há sincronização temporal automática, instalação ou execução do coletor PresentMon. A UI só publica uma coleta completa; cancelar descarta a parcial.

## Validação

```sh
dotnet build GameBoost.sln
dotnet test tests/GameBoost.Tests
```

Os testes validam regras, cálculos, persistência e protocolos com simuladores. O workflow CI compila e executa esses testes em Windows, mas não executa ajustes reais automaticamente. Consulte [Validação no Windows](windows-validation.md) para critérios funcionais e visuais pendentes.
