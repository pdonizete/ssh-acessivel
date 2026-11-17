# AcessivelSsh: regras do projeto

Cliente SSH para Windows feito para usuários do NVDA. A especificação completa está em `PROMPT.MD`, e o plano aprovado está em `C:\Users\paulo\.claude\plans\pasted-content-id-ec79-projeto-cheeky-bumblebee.md`.

## Prioridade
Acessibilidade vem primeiro. Se uma decisão técnica atrapalha a leitura pelo NVDA, ela está errada.

## Usuário/desenvolvedor
- Sabe Python e pouco de C#. Explique em poucas linhas cada conceito de C# sem equivalente direto, comparando com Python.
- Usa só a `dotnet` CLI, sem Visual Studio. Toda instrução vem com o comando exato.
- Comentários no código em português, só nos pontos não óbvios.
- Erro de compilação colado: explique a causa antes de corrigir.
- Trabalho em fases. Pare ao fim de cada fase e espere o OK.

## Ambiente desta máquina
- `dotnet` não está no PATH. Use `C:\Program Files\dotnet\dotnet.exe` (os .bat já tratam isso).
- O NuGet.Config global não tem fontes. O `nuget.config` da raiz define o nuget.org. Não o remova.

## Stack
- .NET 10, WinForms, `net10.0-windows`, x64 (definido em `Directory.Build.props`).
- SSH.NET 2026.0.1 (as versões até a 2025.1.0 têm vulnerabilidades altas).
- Solução `AcessivelSsh.slnx`: `src/AcessivelSsh.Core` (sem UI), `src/AcessivelSsh.App` (WinForms, gera AcessivelSsh.exe), `tests/AcessivelSsh.Tests` (xUnit).
- Dados em `%APPDATA%\AcessivelSsh\`.
- `ShellStream.Read` bloqueia e ignora CancellationToken: a leitura roda numa thread de fundo dedicada; o envio passa por um `Channel<string>` consumido numa Task. Cada conexão tem um número de geração para threads antigas não anunciarem eventos falsos.
- Host key: `HostKeyForm` (Aceitar uma vez / Aceitar e salvar / Cancelar; Enter e Esc cancelam). `known_hosts` próprio, chave por host+porta; impressão digital diferente bloqueia, mesmo com outro tipo de chave.
- Conexões em `connections.json`; segredos só como DPAPI base64 em `ProtectedSecret`. Exportar/importar nunca leva segredos. Arquivo ilegível: cópia `.corrompido-*` antes de sobrescrever.
- Ctrl+R reconecta com o último pedido, guardado só na memória (inclusive a senha digitada).
- `docker-compose.yml`: servidor de teste em 127.0.0.1:2222 (usuário teste, senha teste123; chaves públicas de `docker/chaves/`). Só para desenvolvimento.
- Nos .resx, `&` precisa ser `&`. As ferramentas de edição convertem o escape de volta: use script PowerShell montando `'&' + 'amp;'`.

## NVDA
- DLL em `lib/nvdaControllerClient64.dll`, copiada pelo App.csproj no build e no publish. Nomes aceitos: `nvdaControllerClient64.dll` e `nvdaControllerClient.dll`, carregados só da pasta do executável.
- Funções `__stdcall`, strings UTF-16, retorno 0 = sucesso. `[LibraryImport]` com `StringMarshalling.Utf16`.
- `speakSsml` e `getProcessId` são opcionais (detectadas com `NativeLibrary.TryGetExport`). Nunca dependa delas.
- Nenhuma exceção da DLL pode sair de `NvdaScreenReader`. Sem DLL ou sem NVDA, o app funciona sem fala e avisa uma vez na barra de status.
- Não falar nada com o Windows bloqueado (recomendação do readme do NVDA).
- O resto do app usa só `IScreenReader`.

## Regras de interface
- Só controles nativos (TextBox, ListView, Button, CheckBox, StatusStrip). Nada desenhado à mão.
- Todo controle tem AccessibleName e ordem de Tab lógica.
- Todo campo tem Label com tecla de acesso (&), com TabIndex logo antes do campo.
- Todo diálogo fecha com Esc (CancelButton) e tem botão padrão no Enter (AcceptButton).
- Nenhuma informação só por cor ou ícone.
- Textos em `src/AcessivelSsh.App/Resources/Strings.resx` (pt-BR), nunca literais no código da UI.
- Anexar saída nunca move o foco nem o cursor do usuário.
- A thread da UI nunca faz I/O de rede. Atualizações da UI só via `BeginInvoke`/SynchronizationContext.
- Mudanças de estado da conexão são faladas.

## Fala
- Falar só o texto novo, nunca o buffer inteiro. Não falar o eco do comando digitado.
- Fila com debounce (padrão de 150 ms). Saída grande vira resumo ("N linhas recebidas").

## Segurança
- Segredos com DPAPI (CurrentUser). Nunca em texto puro, no log ou no histórico.
- Host key: SHA256, known_hosts próprio. Se o fingerprint mudar, bloqueie a conexão.

## Comandos
- `build.bat`, `test.bat`, `run.bat`, `publish.bat` (self-contained win-x64 em `publish\`).
