# GameBoost – Plano Completo do Projeto

*App em C# para melhorar o desempenho de jogos em PCs fracos. Nome provisório. Versão 1.0 do plano.*

## 1. Visão geral

O GameBoost é um aplicativo para Windows que ajuda quem tem um computador modesto (principalmente com placa de vídeo onboard) a jogar melhor. Ele aplica um **"modo jogo"** por jogo: fecha o que não é necessário, ajusta o Windows e a rede, mostra o desempenho em tempo real e **desfaz tudo com um clique**.

**Princípios do projeto**

- **Reversível:** toda ação tem desfazer. Nada fica alterado para sempre.
- **Transparente:** o usuário vê o que cada ajuste faz antes de ativar.
- **Seguro:** nunca mexe em processos ou serviços críticos, nem em componentes de anti-cheat.
- **Honesto:** não promete "milagres". O ganho é medido (antes vs. depois), não prometido.

## 2. Funcionalidades principais

1. Área de **perfis de jogo** (configuração por jogo).
2. **Motor de otimização** do Windows (processos, serviços, energia, visual, resolução).
3. **Otimização de rede** (DNS, latência, downloads em segundo plano).
4. **Ponto de restauração + snapshot próprio**, criados a cada ativação, com botão "Restaurar tudo".
5. **Monitor em tempo real:** CPU, GPU, RAM, disco, FPS, ping, jitter e perda de pacotes.
6. **Benchmark antes/depois** com relatório do ganho real.

## 3. Stack tecnológica

| Área | Tecnologia |
| --- | --- |
| Linguagem / framework | C# com .NET 8 |
| Interface | WPF com padrão MVVM |
| Gráficos | LiveCharts2 (ou OxyPlot) |
| Sensores de CPU/GPU | LibreHardwareMonitor (ver cuidado na seção 8) |
| FPS e frametime | PresentMon (Intel, open source) via ETW |
| RAM, disco, rede | PerformanceCounter e WMI |
| Ping / perda de pacotes | `System.Net.NetworkInformation.Ping` |
| Dados e perfis | SQLite ou JSON |
| Execução | Manifesto exigindo administrador |
| Instalador | MSIX ou Inno Setup, com assinatura digital |

## 4. Arquitetura

```
GameBoost.UI            -> WPF/MVVM (telas, gráficos, overlay)
GameBoost.Core          -> orquestrador, perfis, detecção do jogo
GameBoost.Optimizers    -> ações IOptimization (Apply / Revert / Estimate)
GameBoost.Monitoring    -> hardware, FPS, rede
GameBoost.Restore       -> snapshots e ponto de restauração
GameBoost.Data          -> SQLite/JSON, configurações, logs
```

Cada otimização implementa a mesma interface:

```csharp
public interface IOptimization
{
    string Name { get; }
    RiskLevel Risk { get; }          // Verde, Amarelo
    bool SafeWithAntiCheat { get; }  // define se roda em "modo seguro"
    OptimizationEstimate Estimate(); // estimativa qualitativa antes de aplicar
    void Apply(Snapshot snapshot);   // salva o valor original e aplica
    void Revert(Snapshot snapshot);  // restaura exatamente o valor original
}
```

## 5. Módulos detalhados

### 5.1 Perfis de jogo

- Cadastro: nome, executável (.exe), ações ativas, resolução alvo, prioridade, lista extra de processos a fechar.
- Detecção automática de jogos instalados (Steam, Epic, GOG) para facilitar o cadastro.
- Cada perfil tem a flag **"Jogo com anti-cheat"**. Quando ativa, o app entra em **Modo Seguro** (seção 8).
- Fase futura: banco comunitário de presets por jogo (resolução, texturas, sombras sugeridas).
- Dicas dentro do jogo: reduzir resolução ou usar escala de renderização/FSR, baixar texturas, sombras e anti-aliasing, limitar o FPS para estabilizar.

### 5.2 Motor de otimização

Executa as ações do catálogo da seção 6 na ordem: **snapshot, ponto de restauração, aplicar ações, iniciar monitor**. Ao fechar o jogo, reverte automaticamente (opção configurável).

### 5.3 Otimização de rede

- **DNS:** testa a latência de Cloudflare (1.1.1.1), Google (8.8.8.8) e outros, e sugere o mais rápido. Troca só se o usuário aprovar.
- **Nagle e ACK:** ajustes de `TcpAckFrequency` e `TCPNoDelay` na interface de rede ativa.
- **Throttling de rede:** `NetworkThrottlingIndex` e `SystemResponsiveness` (perfil multimídia do Windows).
- **Downloads em segundo plano:** pausa Windows Update, Delivery Optimization e sincronização de nuvem durante o jogo.
- **Limpeza:** `ipconfig /flushdns`.
- **QoS:** política de prioridade para o executável do jogo (via política de QoS do Windows, sem tocar no processo).
- **Dicas ao usuário:** usar cabo em vez de Wi-Fi, ficar perto do roteador, evitar downloads no mesmo computador ou na rede.

### 5.4 Ponto de restauração e reversão

A cada ativação do modo jogo:

1. Cria um **ponto de restauração do Windows** (`Checkpoint-Computer`). Observação: o Windows limita a criação a 1 por 24 horas por padrão, então o app avisa e confia no snapshot próprio nesses casos.
2. Salva um **snapshot próprio** com os valores originais: chaves de registro alteradas, plano de energia ativo, DNS, resolução, estado dos serviços e lista de processos fechados.
3. Botão **"Restaurar tudo"** reverte via snapshot (rápido e preciso). O ponto de restauração do Windows fica como rede de segurança.
4. **Histórico de ativações** com data, jogo, ações aplicadas e resultado.
5. **Proteção contra falhas:** se o app ou o PC fechar de forma inesperada, na próxima abertura o app detecta um snapshot pendente e oferece restaurar.

### 5.5 Monitor em tempo real

- **Gráfico de linhas:** CPU e GPU (uso % e temperatura), atualizado a cada 1 segundo.
- **Cards:** RAM usada/total, disco (leitura, escrita, uso), FPS médio, 1% low e frametime.
- **Rede:** ping, jitter, perda de pacotes e uso de banda. O ping vai para o servidor do jogo (quando conhecido) ou para 1.1.1.1.
- **Overlay opcional:** janela transparente própria, "sempre no topo", **sem injetar nada no jogo** (ver seção 8).

### 5.6 Benchmark e ganho estimado

Promessas de ganho antes de medir seriam enganosas. Por isso:

- **Antes de ativar:** estimativas qualitativas por ação (ex.: "libera cerca de X MB de RAM", "reduz processos em segundo plano").
- **Medição real:** 60 s sem otimização e 60 s com otimização, comparando FPS médio, 1% low, RAM livre, uso de CPU e ping.
- **Relatório final**, por exemplo: "+12% de FPS médio, +900 MB de RAM livre, -3 ms de ping".

## 6. Catálogo de recursos que o app pode parar ou ajustar

Classificação usada:

- **Verde:** seguro, reversível, não afeta o sistema nem o anti-cheat. Ativo por padrão.
- **Amarelo:** seguro, mas depende do uso do PC ou exige aviso. Opcional, desativado por padrão.
- **Vermelho:** o app nunca toca (seção 7).

### 6.1 Aplicativos em segundo plano (fechar ou pausar)

O app mostra a lista, o usuário confirma, e **nunca fecha sem avisar** se o programa pode ter trabalho não salvo.

| Recurso | Nível | Observação |
| --- | --- | --- |
| Navegadores (Chrome, Edge, Firefox) | Amarelo | Avisar sobre abas e downloads abertos |
| OneDrive, Dropbox, Google Drive (sincronização) | Verde | Pausar a sincronização; retoma ao reverter |
| Microsoft Teams, Skype, Zoom (fora de chamada) | Verde | Só se não houver chamada ativa |
| Spotify e players de música | Amarelo | Depende da preferência do usuário |
| Discord | Amarelo | Muita gente joga com ele; fica desativado por padrão |
| Adobe Creative Cloud, atualizadores (Google Update, Java, Adobe) | Verde | Só os processos de atualização |
| Lançadores que o jogo não usa (Epic, EA App, Ubisoft Connect, Battle.net) | Amarelo | Fechar só se o jogo não depende deles |
| Aplicativos de inicialização pesados | Verde | Lista de sugestão; o app não desativa a inicialização sem permissão |

### 6.2 Serviços do Windows (pausa temporária)

O app apenas **para o serviço durante o jogo e o religa na reversão**. Não altera o tipo de inicialização de forma permanente.

| Serviço | Nível | Por que pausar |
| --- | --- | --- |
| Windows Update (`wuauserv`) | Verde | Evita download e instalação durante o jogo |
| Delivery Optimization (`DoSvc`) | Verde | Evita usar a banda para compartilhar atualizações |
| BITS (`BITS`) | Verde | Downloads em segundo plano |
| Windows Search (`WSearch`) | Verde | Evita indexação consumindo disco e CPU |
| Telemetria (`DiagTrack`, Connected User Experiences) | Verde | Reduz atividade de fundo |
| Relatório de erros (`WerSvc`) | Verde | Reduz atividade de fundo |
| Spooler de impressão (`Spooler`) | Amarelo | Só se não houver impressão em andamento |
| SysMain (Superfetch) | Amarelo | Ajuda em HDD, atrapalha pouco em SSD; o app decide pelo tipo de disco |
| Serviços de Bluetooth | Amarelo | Só se o usuário não usa controle ou fone Bluetooth |
| Mapas baixados (`MapsBroker`) | Verde | Sem impacto no uso normal |

### 6.3 Configurações do Windows

| Ajuste | Nível | Observação |
| --- | --- | --- |
| Plano de energia Alto Desempenho (`powercfg`) | Verde | Volta ao plano original na reversão |
| Modo de Jogo do Windows | Verde | Chave de registro oficial |
| Desativar gravação em segundo plano (Game Bar/Game DVR) | Verde | Reduz uso de CPU, GPU e disco |
| Reduzir efeitos visuais e transparência | Verde | Ajuda principalmente em iGPU |
| Modo Foco/Não perturbe (notificações) | Verde | Evita pop-ups e quedas de foco |
| Apps em segundo plano (permissão do Windows) | Verde | Via configurações oficiais |
| Preferência de GPU por aplicativo (iGPU/dedicada) | Verde | Configuração oficial do Windows, sem tocar no jogo |
| Agendamento de GPU acelerado por hardware | Amarelo | Pode ajudar ou piorar; exige reinício, então só como dica |
| Trocar resolução (`ChangeDisplaySettings`) | Verde | Restaura a resolução original ao sair |
| Limpar arquivos temporários (Temp) | Verde | Só arquivos não bloqueados |
| Limpar RAM em standby | Amarelo | Ganho discutível, pode causar travadas; vem desativado |

### 6.4 Rede

| Ajuste | Nível | Observação |
| --- | --- | --- |
| Limpar cache DNS | Verde | Sem risco |
| Trocar DNS | Amarelo | Pede confirmação e mostra o teste de latência |
| Nagle / `TcpAckFrequency` | Amarelo | Ajuda em alguns jogos online; reverte pelo snapshot |
| Throttling de rede (multimídia) | Verde | Reverte pelo snapshot |
| Pausar sincronização de nuvem e atualizações | Verde | Já coberto nas seções 6.1 e 6.2 |
| Política de QoS para o executável do jogo | Verde | Não toca no processo |

### 6.5 Hardware onboard (iGPU)

O app **orienta** onde o controle é limitado por driver e hardware:

- Memória em **dual channel** (maior ganho em iGPU).
- Memória de vídeo dedicada na BIOS, quando disponível.
- Drivers de vídeo atualizados.
- Painel Intel/AMD em modo de desempenho.

## 7. Lista de proteção (o app nunca toca)

- **Processos e serviços críticos do Windows:** `System`, `smss`, `csrss`, `wininit`, `winlogon`, `services`, `lsass`, `svchost` (com cuidado: o app não encerra `svchost`), `dwm`, `explorer`, `audiodg`, drivers e serviços de rede essenciais (DHCP, DNS Client, rede e áudio).
- **Segurança do Windows:** Windows Defender, Firewall e Central de Segurança. O app **não** desativa antivírus nem cria exclusões automáticas.
- **Anti-cheat e seus serviços:** EasyAntiCheat, BattlEye (`BEService`), Vanguard (`vgc`, `vgk`), FACEIT, Ricochet, nProtect, XIGNCODE, Denuvo e similares. O app **nunca** encerra, pausa ou altera esses itens.
- **Serviços de Xbox e Microsoft Store:** necessários para Game Pass e para vários jogos; ficam intocados.
- **Lançadores do próprio jogo atual:** se o perfil depende do Steam, Epic etc., o app não fecha.
- **Drivers de vídeo e áudio:** nenhuma alteração.
- **Overclock, tensão e BIOS:** fora do escopo, por segurança.
- **"Otimizadores" agressivos:** sem limpeza de registro, sem desativar atualizações de segurança de forma permanente, sem desativar o UAC.

## 8. Compatibilidade com anti-cheat

Nenhuma ferramenta pode garantir 100% de compatibilidade, porque cada anti-cheat muda suas regras com o tempo. O projeto reduz o risco assim:

**Regras de design**

1. **Nada de injeção, hooks ou leitura de memória** do processo do jogo.
2. **Nada de abrir handles no processo do jogo** com permissões amplas. Por isso, a prioridade "Alta" do jogo **não** é feita com `SetPriorityClass` em um processo já aberto. Alternativas: iniciar o jogo pelo GameBoost já com a prioridade desejada, ou usar a chave oficial de desempenho por executável do Windows. Ambas vêm **desativadas** em jogos com anti-cheat de kernel até serem testadas.
3. **Overlay próprio**, em janela separada, sem hook em DirectX/Vulkan. Overlays que injetam DLL no jogo são justamente o que anti-cheats costumam bloquear.
4. **Nada de tocar nos processos do anti-cheat**, nem para "economizar RAM".
5. **FPS via PresentMon/ETW**, que lê eventos do sistema e não interfere no jogo.

**Cuidado com sensores de hardware**

O LibreHardwareMonitor pode carregar um driver de kernel para ler sensores. Drivers desse tipo (como o antigo WinRing0) entraram em listas de bloqueio do Windows e já causaram bloqueios em anti-cheats como o Vanguard. Antes de adotar a biblioteca, **verificar a versão atual e qual driver ela usa**. Como proteção extra, no Modo Seguro o app lê apenas contadores do Windows (uso de CPU, RAM, disco, rede) e deixa a leitura de sensores avançados desativada.

**Modo Seguro (jogos com anti-cheat)**

| Permitido | Desativado |
| --- | --- |
| Plano de energia, Modo de Jogo, efeitos visuais | Alterar prioridade do processo do jogo |
| Pausar serviços de atualização e telemetria | Leitura de sensores via driver |
| Fechar apps de segundo plano que não são do jogo | Qualquer ação sobre o processo do jogo |
| Rede (DNS, QoS, throttling) | Overlay (opcional, só se testado) |
| Monitor por contadores do Windows e PresentMon | Limpeza de RAM em standby |
| Trocar resolução |  |

**Processo de validação**

- Manter uma **lista de compatibilidade** de jogos testados (versão do jogo, anti-cheat, data do teste, resultado).
- Testar com contas descartáveis antes de liberar cada perfil.
- Mostrar um aviso claro na primeira vez que o usuário ativa um perfil com anti-cheat.
- Acompanhar comunicados dos anti-cheats e reagir a mudanças.

## 9. Segurança e confiança do usuário

- Executável **assinado digitalmente** para reduzir falsos positivos de antivírus (o app mexe em registro e serviços, o que costuma gerar alertas).
- Pedido de administrador explicado de forma transparente.
- **Confirmação** na primeira vez que cada ação é aplicada.
- Logs locais de tudo que foi feito, visíveis ao usuário.
- Código das ações com testes automatizados, usando mocks do registro e dos serviços.
- Sem telemetria obrigatória; qualquer envio de dados é opcional e informado.

## 10. Telas do aplicativo

1. **Início:** botão "Ativar modo jogo", status do PC e último relatório.
2. **Meus jogos:** lista de perfis, botão de adicionar jogo, detecção automática.
3. **Editar perfil:** ações marcadas por nível (verde/amarelo), resolução, processos extras.
4. **Monitor:** gráficos em tempo real e cards de RAM, disco, FPS e rede.
5. **Rede:** ping, jitter, perda de pacotes, teste de DNS e ajustes.
6. **Restauração:** botão "Restaurar tudo", histórico e pontos de restauração.
7. **Benchmark:** antes vs. depois, com relatório exportável.
8. **Configurações:** reversão automática, idioma, tema, logs.

## 11. Cronograma

| Fase | Duração | Entregas |
| --- | --- | --- |
| 1. MVP | 2–3 semanas | UI básica, 4–5 otimizações verdes, snapshot e botão Restaurar |
| 2. Monitoramento | 2 semanas | Gráficos de CPU/GPU, RAM, disco |
| 3. FPS e rede | 2 semanas | PresentMon, ping, jitter, perda de pacotes, ajustes de rede |
| 4. Perfis e benchmark | 2 semanas | Perfis por jogo, antes/depois, relatório |
| 5. Modo Seguro e testes | 2 semanas | Flag de anti-cheat, lista de compatibilidade, testes em PCs fracos |
| 6. Polimento | 2 semanas | Overlay, instalador assinado, logs, documentação |

## 12. Riscos e mitigação

| Risco | Mitigação |
| --- | --- |
| Anti-cheat sinalizar o app | Modo Seguro, sem injeção, lista de compatibilidade testada |
| Antivírus gerar falso positivo | Assinatura digital e código aberto ou auditável |
| Fechar algo que o usuário precisava | Lista de proteção, confirmação e aviso de trabalho não salvo |
| Ganho menor que o esperado | Benchmark real e mensagens honestas; foco em RAM dual channel e configurações do jogo |
| Falha no meio da otimização | Snapshot pendente detectado na próxima abertura |
| Mudanças do Windows em atualizações | Testes por versão e ações isoladas em classes separadas |
| Limite do ponto de restauração (1 por 24 h) | Snapshot próprio como principal mecanismo |

## 13. Próximos passos

1. Validar este plano e decidir o nome do projeto.
2. Criar a solução .NET 8 com os projetos da seção 4.
3. Implementar a interface `IOptimization` e as primeiras ações verdes: plano de energia, Modo de Jogo, Game DVR, pausa de Windows Update e Search.
4. Implementar o snapshot e o botão **Restaurar tudo**.
5. Montar o monitor básico (CPU, RAM, disco) para o primeiro teste de ganho.
