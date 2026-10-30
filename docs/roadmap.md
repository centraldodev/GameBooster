# Estado da implementação

## Seis blocos integrados nesta etapa

- Descoberta local Steam/Epic/GOG, início pela shell e acompanhamento por caminho do executável, com reversão automática opcional.
- Fechamento solicitado de janelas elegíveis, inspeção e pausa reversível de serviços, com lista positiva de proteção.
- Diagnóstico de ping/jitter/perda, consultas DNS reais, troca reversível IPv4 e QoS temporário; ajustes avançados opcionais.
- Benchmark antes/depois com CPU/RAM/rede, importação PresentMon CSV e relatório Markdown/JSON.
- Tentativa verificada de ponto de restauração do Windows e registro do fallback para snapshot.
- Catálogo expandido: transparência, efeitos visuais, resolução com confirmação temporizada e GPU por executável.

O código está integrado e coberto por testes com adaptadores simulados. Esses itens **ainda não foram validados em Windows real neste ambiente**. O [roteiro de validação](windows-validation.md) é a próxima etapa antes de distribuir.

## Complementos ainda pendentes dos seis blocos

- Captura automática de FPS/frametime por PresentMon/ETW; o benchmark atual usa CSV externo opcional.
- Integração específica de launchers/protocolos e identificação automática do processo real. A pasta é detectada, mas o usuário confirma o `.exe`.
- Pausa nativa de sincronizadores e detecção de chamada ativa. Nesta fase não são encerrados automaticamente.
- Ajustes de Foco/notificações e permissões de apps de fundo, com APIs e restauração validadas por versão Windows.
- Tratamento específico de redes gerenciadas, VPN, DoH e políticas de grupo. Troca de IPv6 fora do escopo atual.
- Janela de configuração geral, retenção de histórico, localização e exportação da auditoria pela UI.

## Fora do catálogo reversível atual

Limpeza de temporários e cache DNS não recupera os dados anteriores. Apenas flush DNS foi exposto como ação imediata separada e confirmada. Limpeza de standby RAM, alteração de prioridade do processo e pausa de Bluetooth continuam indisponíveis. Agendamento de GPU é orientação para ajuste manual com reinício.

## Próximas fases do plano original

- Monitor contínuo com gráficos, disco, sensores e leitura de FPS automática.
- Compatibilidade registrada por jogo, anti-cheat e versão Windows.
- Overlay separado opcional, sem injeção.
- Instalador, assinatura digital e testes em máquinas modestas.
