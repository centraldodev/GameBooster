# GameBoost

Aplicativo Windows em C#/.NET 8 e WPF/MVVM para ajustes por jogo, com revisão explícita e restauração dos valores anteriores. O [plano original](<GameBoost – Plano Completo do Projeto.md>) descreve o produto completo; este README registra a implementação atual.

## Executar

Em Windows 10/11, com SDK .NET 8 ou superior:

```powershell
dotnet build GameBoost.sln
dotnet run --project src/GameBoost.UI
```

O manifesto solicita administrador para os ajustes. O binário ainda não possui assinatura digital. O jogo é iniciado pela shell do Explorer para evitar herdar a elevação do GameBoost. A inicialização é recusada se a shell de desktop não estiver disponível; não há fallback para executar o jogo como administrador.

Publicação independente do runtime:

```powershell
dotnet publish src/GameBoost.UI -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

No macOS/Linux, é possível compilar para Windows e executar testes das regras, cálculos e adaptadores simulados. WPF, PowerShell e ações Windows exigem validação no Windows:

```sh
dotnet build GameBoost.sln
dotnet test tests/GameBoost.Tests
```

Os testes usam .NET 8 com roll-forward para runtimes mais recentes.

## Funcionalidades integradas

| Área | Implementação |
| --- | --- |
| Jogos | Perfis JSON; descoberta local de instalações Steam/Epic/GOG; escolha explícita do `.exe`; argumentos; executável real separado do launcher |
| Sessão | Inicia pela shell; acompanha caminho do executável por CIM; restaura ao encerrar se habilitado; tenta rollback se inicialização/acompanhamento falhar |
| Aplicativos | Lista restrita de janelas elegíveis; confirmação individual; solicitação normal de fechamento, sem `Kill` |
| Serviços | Consulta de estado; nove serviços opcionais na lista permitida; snapshot do estado; recusa de dependentes ativos; restauração sem mudar inicialização |
| Rede | 20 amostras ICMP, ping médio, jitter e perda; teste DNS por consulta UDP real; DNS IPv4 reversível; QoS temporário; ajustes TCP/multimídia opcionais |
| Benchmark | Coletas antes/depois de 60 s, CPU total, RAM disponível e rede; importação de FPS/1% low de CSV PresentMon; exportação Markdown e JSON |
| Proteção | Snapshot antes de cada alteração; tentativa verificada de ponto de restauração; recuperação de pendências; histórico e eventos locais por snapshot |
| Sistema | Energia, Modo de Jogo, duas preferências de captura, transparência, efeitos visuais, resolução e preferência de GPU por executável |

O catálogo possui **23 ações opcionais**, organizadas por aba. Todas começam desmarcadas. Marcar não aplica: o usuário revisa os efeitos antes da ativação.

## Usar uma sessão

1. Em **Jogos**, crie um perfil ou detecte instalações. A descoberta encontra pastas; selecione o executável correto, evitando instaladores e desinstaladores.
2. Se o executável inicial for um launcher, indique também o **executável real para monitorar**. Sem isso, a saída de um launcher curto pode ser confundida com o fim do jogo.
3. Selecione ajustes nas abas **Sistema**, **Aplicativos** e **Rede**. Para DNS/TCP, carregue as interfaces e escolha explicitamente uma delas.
4. Salve o perfil. Clique em **Revisar e iniciar jogo** e confirme as alterações.
5. Mantenha o GameBoost aberto. Após detectar o processo e sua ausência por quatro verificações, a sessão termina e a restauração segue a opção do perfil.
6. Use **Restaurar tudo** a qualquer momento fora de operações em andamento. Se a execução for interrompida, abra novamente o app para recuperar snapshots pendentes.

O processo tem até 2 minutos para aparecer, com intervalo de 2 s mais o tempo de consulta CIM. Quatro ausências consecutivas confirmam o encerramento. O app não segue automaticamente processos filhos com outros nomes: configure o caminho real. Não é possível abrir uma segunda sessão controlada simultaneamente.

Falhas de inicialização/acompanhamento tentam restaurar os ajustes mesmo se a restauração automática normal estiver desmarcada. **Só aplicar ajustes** cria uma sessão manual; sem acompanhamento do jogo, restaure explicitamente.

A mudança de resolução tem confirmação de **15 segundos**; sem confirmação, a sessão é revertida. Modos não aceitos pelo monitor são recusados. O monitor original precisa estar conectado para restaurar sua resolução.

## Aplicativos e serviços

A lista de proteção funciona por **lista positiva**. Somente nomes explicitamente admitidos podem ser gerenciados. Sistema, Defender, firewall, áudio, DHCP, anti-cheats, Xbox e launchers não estão na lista permitida.

Aplicativos elegíveis nesta fase: Chrome, Edge, Firefox, Spotify, Discord e Bloco de Notas, apenas com janela na sessão atual. O executável monitorado é excluído. O usuário confirma cada solicitação. O aplicativo pode pedir para salvar ou recusar fechar. **Não há reabertura automática nem recuperação de trabalho**; essa ação é separada da sessão reversível.

Serviços opcionais: WSearch, WerSvc, MapsBroker, SysMain, Spooler, wuauserv, DoSvc, BITS e DiagTrack. A parada não usa força e é recusada com dependentes ativos ou estado em transição. Spooler exige fila de impressão vazia. Serviços originalmente parados não são iniciados ao restaurar; originalmente ativos recebem solicitação de início. O Windows pode reiniciá-los antes do fim da sessão. Não pause atualizações ou impressão durante tarefas importantes.

## Rede

O ping exige IP explícito; ICMP não equivale ao protocolo do jogo. Jitter é a média da diferença absoluta entre respostas consecutivas válidas. Timeout pode significar filtragem ICMP.

O teste de DNS consulta `example.com` diretamente em 1.1.1.1, 8.8.8.8 e 9.9.9.9, por UDP/53, três vezes. Exibe a mediana apenas se todas as consultas responderem. Não altera DNS automaticamente; o resultado pode refletir cache do resolvedor. Respostas truncadas são recusadas nesta etapa, sem fallback TCP.

A troca de DNS atua apenas no IPv4 da interface selecionada e verifica os servidores após aplicar. O snapshot distingue DNS automático de estático: a reversão retorna a DHCP ou aos endereços anteriores. IPv6 não é alvo da ação. Redes corporativas, VPNs e políticas de grupo exigem teste específico antes de uso.

QoS cria uma política DSCP 46 com nome exclusivo em `ActiveStore`; a reversão remove somente essa política. Isso não reserva banda nem garante prioridade em roteadores. Ajustes Nagle/ACK e multimídia são experimentais, opcionais e podem depender de novas conexões/reinício. **Limpar cache DNS** é imediato, confirmado separadamente e não reversível; o Windows reconstrói o cache.

## Benchmark

1. Salve o perfil e inicie o jogo sem ajustes. Execute **Coletar antes** e repita uma cena por 60 s. Feche o jogo.
2. Selecione os ajustes, salve e inicie outra sessão do mesmo perfil. Repita a cena com **Coletar depois**.
3. Mantenha o mesmo executável e destino IP. O app verifica o jogo no início/fim e rejeita mudanças de sessão de otimização durante a coleta.
4. Opcionalmente importe CSV PresentMon de cada etapa: colunas `Application` e `MsBetweenPresents`, mínimo de 100 frametimes positivos. Se houver `ProcessID`/`SwapChainAddress`, o arquivo deve conter uma única combinação do jogo; filtre previamente.
5. Exporte Markdown e JSON. As coletas ficam em memória até exportar; uma nova base descarta a comparação anterior.

CPU e RAM são do sistema inteiro. FPS médio é `1000 / frametime médio`; 1% low usa a média dos 1% frametimes mais lentos. CSVs são importados manualmente e **não sincronizados automaticamente** com as coletas. Sem CSV, não há FPS no relatório. PresentMon não é instalado nem executado pelo app nesta etapa. Não há garantia de equivalência de cena/carga, nem promessa de ganho.

## Restauração e dados

Dados em `%LOCALAPPDATA%\GameBoost`: `profiles.json` e `snapshots\<id>.json`. Snapshots contêm originais, resultado do ponto de restauração e eventos de aplicação/falhas/reversão. Não apague pendências antes de restaurar. Erros de leitura bloqueiam novas alterações.

O ponto de restauração é proteção adicional: falha, proteção do sistema desabilitada ou limite de frequência são registrados e o app prossegue com snapshot próprio, conforme informado na revisão. **Restaurar tudo usa o snapshot**, não dispara uma restauração completa do Windows. Não alteramos configurações globais para contornar o limite de criação.

Há bloqueio de segunda instância na mesma sessão Windows. Uso simultâneo entre sessões/usuários diferentes ainda não é suportado. Interrupção do app/PC impede reversão automática imediata; a recuperação fica pendente para a próxima abertura.

## Organização e próximos passos

- [Arquitetura](docs/architecture.md): camadas, contratos e inclusão de ações.
- [Validação no Windows](docs/windows-validation.md): testes manuais ainda necessários.
- [Roadmap](docs/roadmap.md): funcionalidades pendentes.

A interface de monitor contínuo, captura automática PresentMon/ETW, sensores, overlay, configurações gerais, instalador e assinatura ainda estão pendentes. Nenhuma ação carrega drivers, injeta DLLs ou lê memória do jogo. A marcação de anti-cheat expressa uma política de projeto; compatibilidade real precisa de validação por jogo.
