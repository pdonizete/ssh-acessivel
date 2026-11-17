\# Projeto: Cliente SSH acessível para usuários de leitor de tela (NVDA)



\## Contexto

Quero um cliente SSH para Windows feito para pessoas cegas que usam o NVDA.

Eu sou o usuário final e uso NVDA no dia a dia. Acessibilidade é o requisito

principal. Se uma decisão técnica atrapalhar a leitura pelo NVDA, ela está errada.



\## Sobre mim como desenvolvedor

\- Conheço bem Python e pouco de C#/.NET. Quando usar um conceito de C# que não

&#x20; existe em Python (ex.: interfaces, async/await, P/Invoke, eventos, LINQ),

&#x20; explique em poucas linhas, comparando com o equivalente em Python.

\- Uso apenas a linha de comando (`dotnet` CLI). Não tenho Visual Studio. Toda

&#x20; instrução de compilar, testar ou executar deve vir com o comando exato.

\- Crie scripts `build.bat`, `run.bat` e `test.bat` na raiz do projeto.

\- Comente o código nos pontos não óbvios, em português.

\- Mensagens de erro de compilação: quando eu colar uma, explique a causa antes

&#x20; de corrigir.



\## Stack

\- C# / .NET 10 (LTS), Windows Forms, `TargetFramework` = `net10.0-windows`,

&#x20; `UseWindowsForms` = true.

\- Use controles nativos (TextBox, ListView, Button), que o NVDA lê sem

&#x20; configuração extra. Não use controles desenhados à mão.

\- SSH: biblioteca SSH.NET (Renci.SshNet), via NuGet, versão estável mais recente.

\- Compilar como \*\*x64\*\* (`PlatformTarget` = x64, não AnyCPU), porque a DLL do

&#x20; NVDA é de 64 bits.

\- A DLL `nvdaControllerClient64.dll` já está em `lib/`. Configure o .csproj para

&#x20; copiá-la para a pasta de saída no build e no publish.

\- Publicação: `dotnet publish` self-contained, win-x64, para gerar uma pasta que

&#x20; rode em PC sem .NET instalado.

\- Se você tiver um motivo técnico forte para outra stack, apresente o argumento

&#x20; antes de começar e espere minha decisão.



\## Integração com o NVDA

Referência: https://github.com/nvaccess/nvda (pasta `extras/controllerClient`).

Leia o header e os exemplos do repositório antes de escrever o P/Invoke.

Funções principais (strings em UTF-16 / wchar\_t\*, retorno 0 = sucesso):

\- `nvdaController\_testIfRunning()`

\- `nvdaController\_speakText(const wchar\_t\*)`

\- `nvdaController\_cancelSpeech()`

\- `nvdaController\_brailleMessage(const wchar\_t\*)`

Versões recentes também têm `nvdaController\_speakSsml` e

`nvdaController\_getProcessId`. Use essas funções só se existirem na DLL

(verifique a exportação) e não dependa delas.



Requisitos:

\- P/Invoke com strings Unicode (UTF-16). Confirme a convenção de chamada no header.

\- Encapsule tudo numa classe `NvdaScreenReader` que implementa a interface

&#x20; `IScreenReader`, para que dê para trocar por outro leitor ou por um mock

&#x20; nos testes.

\- Se o NVDA não estiver rodando ou a DLL não for encontrada, o app funciona

&#x20; normalmente sem fala. Avise isso uma vez na barra de status. Nunca deixe uma

&#x20; exceção da DLL derrubar o app.

\- Fila de fala com agrupamento (debounce configurável, padrão \~150 ms). A saída

&#x20; do SSH chega em pedaços, e falar cada pedaço separado fica picotado.

\- Saída muito grande (ex.: `cat` de arquivo enorme): fale um resumo

&#x20; ("342 linhas recebidas") em vez de ler tudo. Configure o limite em linhas.

\- O NVDA fala só o texto NOVO, nunca o buffer inteiro de novo.

\- Não fale o eco do comando que o usuário acabou de digitar.



\## Interface (janela principal)

1\. \*\*Campo de comando\*\*: TextBox de uma linha, com AccessibleName "Comando".

&#x20;  Enter envia. O foco fica aqui por padrão.

2\. \*\*Campo de saída\*\*: TextBox multilinha, somente leitura, com AccessibleName

&#x20;  "Saída". O usuário pode entrar nele e navegar com as setas como num

&#x20;  documento. Adicionar texto ao campo de saída não pode tirar o foco do campo

&#x20;  de comando nem mover o cursor do usuário se ele estiver lendo a saída.

3\. \*\*Barra de status\*\*: estado da conexão (conectado a X, desconectado, erro).



Toda mudança de estado (conectando, conectado, desconectado, erro de

autenticação, conexão caiu) também é falada pelo NVDA.



Limite o tamanho do campo de saída (ex.: últimas 5.000 linhas, configurável),

descartando as mais antigas, para a interface não ficar lenta.



\### Atalhos de teclado (todos configuráveis, listados em Ajuda > Atalhos)

\- F6: alternar foco entre comando e saída

\- Seta cima/baixo no campo de comando: histórico de comandos (salvo por servidor)

\- Ctrl+C no campo de comando, com o campo vazio ou sem texto selecionado: envia

&#x20; SIGINT (\\x03) ao servidor

\- Esc: interrompe a fala atual (cancelSpeech)

\- Ctrl+Shift+A: liga/desliga a leitura automática da saída

\- Ctrl+Shift+R: relê a última resposta completa

\- Alt+1 a Alt+9: lê a enésima linha mais recente da saída

\- Ctrl+L: limpa o campo de saída

\- Ctrl+Shift+C: copia a última resposta para a área de transferência

\- Ctrl+N: abre o gerenciador de conexões

\- Ctrl+R: reconectar

\- Tab: envia Tab ao servidor para autocompletar, só se a opção estiver ativada

&#x20; nas configurações (por padrão, Tab move o foco, como no Windows)



Antes de definir os atalhos, verifique se algum conflita com atalhos padrão do

NVDA e me avise.



\## Tratamento da saída do terminal

\- Abra um shell com PTY (`ShellStream` do SSH.NET) e `TERM=dumb`, para reduzir

&#x20; sequências de controle.

\- Remova sequências ANSI/VT100 (cores, cursor, título da janela) antes de exibir

&#x20; e de falar. Implemente isso como função pura, com testes unitários.

\- Trate `\\r` sem `\\n` (barras de progresso, ex.: apt, wget): mantenha só a

&#x20; versão final da linha e não fale cada atualização. No máximo, fale a

&#x20; porcentagem a cada N segundos (configurável).

\- UTF-8 em tudo. Acentos em português precisam funcionar. Cuidado com

&#x20; caracteres UTF-8 multibyte divididos entre dois pacotes (use um Decoder com

&#x20; estado, não Encoding.GetString por pedaço).

\- Detecte prompts de senha (`\[sudo] password for`, `Password:`, `Senha`). Nesses

&#x20; casos, fale "Senha solicitada" e mascare o campo de comando até o próximo

&#x20; Enter. A senha não vai para o histórico nem para o log.

\- Programas de tela cheia (vim, nano, htop, less) não são suportados na v1.

&#x20; Se um deles for detectado, avise por fala. Configure no shell remoto

&#x20; `PAGER=cat` e `GIT\_PAGER=cat` para que `man`, `git log` etc. funcionem.



\## Gerenciador de conexões

Diálogo acessível com lista de servidores (ListView) e botões Adicionar, Editar,

Remover, Duplicar e Conectar. Enter na lista conecta. Delete na lista remove,

com confirmação.



Campos de cada conexão: nome, host, porta (padrão 22), usuário, tipo de

autenticação (senha | chave), caminho da chave privada (com botão Procurar),

senha ou passphrase (opcional, com opção "salvar"), grupo/pasta (opcional).

Campos que não se aplicam ao tipo de autenticação escolhido ficam desabilitados.



\- Chaves: OpenSSH e PEM; ed25519, RSA e ECDSA. Se a chave tiver passphrase

&#x20; e ela não estiver salva, peça a passphrase num diálogo.

\- Segredos: nunca em texto puro. Use Windows DPAPI (`ProtectedData`, escopo do

&#x20; usuário) ou o Windows Credential Manager.

\- Configurações em JSON (System.Text.Json) em `%APPDATA%\\<NomeDoApp>\\`.

\- Importar/exportar a lista de servidores, sem os segredos.

\- \*\*Verificação de host key\*\*: na primeira conexão, mostre um diálogo acessível

&#x20; com o fingerprint SHA256 e as opções Aceitar uma vez, Aceitar e salvar,

&#x20; Cancelar. Se o fingerprint mudar depois, bloqueie a conexão e alerte em

&#x20; destaque, por fala e por diálogo. Salve num arquivo de known\_hosts próprio.

\- Timeout de conexão e keep-alive configuráveis. Quando a conexão cair, avise

&#x20; por fala e ofereça reconectar (Ctrl+R).



\## Outros requisitos

\- Log da sessão opcional, salvo em arquivo .txt, sem senhas.

\- Log de erros do app em `%APPDATA%\\<NomeDoApp>\\logs\\`, para eu enviar quando

&#x20; algo falhar.

\- Interface em português do Brasil, com textos em arquivo de recursos (.resx)

&#x20; para permitir tradução depois.

\- Todos os controles com AccessibleName e ordem de Tab lógica. Todo diálogo

&#x20; fecha com Esc e tem botão padrão no Enter. Todo campo tem um Label associado

&#x20; com tecla de acesso (\&).

\- Nenhuma informação transmitida só por cor ou ícone.



\## Fora do escopo da v1 (não implementar agora)

Múltiplas sessões em abas, SFTP/transferência de arquivos, port forwarding,

suporte a outros leitores de tela (JAWS, Narrador). Estruture o código para que

essas funções possam ser adicionadas depois, sem implementá-las.



\## Arquitetura e qualidade

\- Separe as camadas: UI, sessão SSH, processamento de saída (ANSI, `\\r`,

&#x20; detecção de prompts), fala (IScreenReader + fila) e persistência.

\- Leitura do SSH em tarefa de fundo (async/await). A thread da interface nunca

&#x20; faz I/O de rede. Atualizações da interface só via `Control.BeginInvoke` ou

&#x20; SynchronizationContext. Um app travado deixa o NVDA mudo e o usuário sem

&#x20; saber o que aconteceu.

\- Testes unitários (xUnit) para: remoção de ANSI, tratamento de `\\r`, decodificação

&#x20; UTF-8 em pedaços, detecção de prompt de senha, debounce da fila de fala (com

&#x20; mock de IScreenReader) e criptografia/persistência de conexões.

\- Para testes de integração, um `docker-compose.yml` com um servidor OpenSSH

&#x20; configurado com usuário/senha e com chave. Se eu não tiver Docker, explique

&#x20; uma alternativa.

\- README em português com: requisitos, como compilar, onde colocar a DLL,

&#x20; lista de atalhos e limitações conhecidas.



\## Forma de trabalho

1\. Antes de escrever código, leia `extras/controllerClient` no repositório do

&#x20;  NVDA. Depois me apresente: estrutura de pastas, principais classes e

&#x20;  eventuais dúvidas ou discordâncias com este documento. Espere minha aprovação.

2\. Crie um `CLAUDE.md` com as decisões do projeto e as regras de acessibilidade

&#x20;  acima, para manter a consistência entre sessões.

3\. Implemente em fases e pare ao fim de cada uma para eu testar com o NVDA:

&#x20;  - Fase 1: integração com o NVDA (classe NvdaScreenReader + janela de teste

&#x20;    que fala um texto digitado).

&#x20;  - Fase 2: janela principal + conexão SSH com senha, digitada à mão + leitura

&#x20;    da saída.

&#x20;  - Fase 3: processamento de saída (ANSI, `\\r`, UTF-8, prompts de senha,

&#x20;    debounce, resumo de saída grande).

&#x20;  - Fase 4: gerenciador de conexões, chaves SSH, DPAPI, host keys.

&#x20;  - Fase 5: atalhos restantes, histórico, logs, configurações, publish, README.

4\. Ao fim de cada fase, informe o que foi feito, os comandos exatos para

&#x20;  compilar e testar, o que verificar com o NVDA e o que ficou pendente.

&#x20;  Não avance sem meu OK.





