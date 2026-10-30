# Validação pendente em Windows

Este roteiro verifica comportamento real. Os testes automatizados usam simuladores para não alterar a máquina do desenvolvedor; compilação cruzada no macOS não comprova execução de WPF, CIM, COM, cmdlets ou APIs nativas.

## Ambiente

Use uma instalação Windows de teste com dados descartáveis. Registre versão/edição, rede, monitor, plano de energia, permissões, jogo e launcher. Execute `scripts/Check-WindowsPrerequisites.ps1` em Windows PowerShell para consultar recursos disponíveis, sem alterar configurações.

## Critérios por módulo

| Módulo | Casos necessários | Resultado esperado |
| --- | --- | --- |
| Perfil | Salvar, reabrir, editar seleção em cada aba, migrar perfil antigo | Dados preservados; novos campos com valores padrão |
| Detecção | Steam em várias bibliotecas, Epic, GOG e launchers ausentes | Pastas corretas; nenhum executável escolhido sem confirmação |
| Inicialização | `.exe` direto, argumentos com espaços/apóstrofos, launcher com processo distinto | Execução pela shell; caminho monitorado correto; não herda elevação do app |
| Sessão | Jogo já aberto, não aparece em 2 min, fecha normalmente, restauração desmarcada | Recusa de duplicação; rollback no erro; restauração configurável no encerramento |
| Recuperação | Encerrar app abruptamente após aplicar um ajuste | Snapshot pendente na próxima abertura; valores originais restauráveis |
| Aplicativos | Documento não salvo; janela recusa fechar; PID reutilizado; jogo com nome permitido | Sem força; confirmação do aplicativo respeitada; identidade revalidada; jogo protegido |
| Serviços | Inicialmente ativo/parado; dependentes ativos; fila de impressão; serviço ausente; acesso negado | Estado preservado; transições/dependentes recusados; startup inalterado; erro visível |
| DNS automático | Registrar DNS IPv4/IPv6, trocar IPv4, restaurar; reconectar adaptador | Volta ao modo automático IPv4, não congela o endereço do lease; IPv6 permanece igual |
| DNS estático | Registrar ordem, trocar e restaurar; testar falha parcial e interface removida | Endereços originais e ordem restaurados; interface indisponível mantém pendência |
| QoS | Criar sessão, inspecionar ActiveStore, restaurar e reiniciar PC | Política exclusiva removida; políticas externas intactas; não persiste após reboot |
| Diagnóstico | Servidor responde, ICMP bloqueado, UDP/53 bloqueado, VPN | Métricas reais; indisponibilidade sem inventar 0 ms; sem alteração automática de DNS |
| Registro | Valores existentes/ausentes, tipo inesperado, políticas negando escrita | Tipo preservado; valores ausentes removidos ao reverter; falhas provocam rollback |
| Efeitos | Preferências ligadas/desligadas, restauração após reinício do app | Estado anterior restaurado |
| Resolução | Modo suportado/inválido, não confirmar, desconectar monitor | Teste de modo; reversão após 15 s sem confirmação; monitor ausente mantém pendência |
| GPU | Valor ausente/existente com outras preferências; jogo em múltiplas GPUs | Preferência por caminho, preservando demais campos; valor original restaurado |
| Ponto de restauração | Proteção habilitada/desabilitada, criação repetida | Só diz criado após verificar novo ponto; fallback registrado sem contornar limites |
| Benchmark | Mesma cena antes/depois; cancelar; jogo fecha; sessão muda | Dados reais, descarte de parcial/inconsistente, sem FPS quando não importado |
| CSV | Jogo correto/incorreto, duas swapchains, frametimes inválidos | Filtragem por app, mínimo de 100 frames, recusa de múltiplos streams detectáveis |
| Relatório | Exportar e conferir cálculos contra JSON bruto | Métricas, duração e ações correspondem às coletas; ressalva de captura externa |
| Interface | Abas, teclado, DPI 100/150/200%, resolução baixa | Conteúdo acessível por rolagem, controles legíveis e confirmações visíveis |

## Registro de resultados

Não marque um jogo como compatível com anti-cheat sem registrar teste específico (data, versão, anti-cheat e resultado). Nunca teste interrompendo processos protegidos. Nenhum teste local simulado demonstra ganho de FPS ou compatibilidade com anti-cheat.

## Referências das integrações

- [Checkpoint-Computer](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/checkpoint-computer?view=powershell-5.1): criação de ponto e limitação de frequência.
- [Set-DnsClientServerAddress](https://learn.microsoft.com/en-us/powershell/module/dnsclient/set-dnsclientserveraddress): definir servidores e restaurar automático.
- [New-NetQosPolicy](https://learn.microsoft.com/en-us/powershell/module/netqos/new-netqospolicy): política por aplicação em ActiveStore.
- [SystemParametersInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow): leitura e alteração de efeitos visuais.
