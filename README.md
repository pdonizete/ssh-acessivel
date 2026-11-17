# SSH Acessível

[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Windows 10 e 11, 64 bits](https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078D6)
![Feito para o NVDA](https://img.shields.io/badge/leitor%20de%20tela-NVDA-orange)

Cliente SSH para Windows feito para pessoas cegas que usam o leitor de tela [NVDA](https://www.nvaccess.org/).

Em terminais comuns, o leitor de tela costuma reler a tela inteira, falar códigos de cor e anunciar cada atualização de uma barra de progresso. O SSH Acessível mostra a saída do servidor num campo de texto comum do Windows e fala só o que é novo, em blocos, sem o eco do comando que você acabou de digitar.

## Recursos

- **Fala pensada para terminal:** junta os pedaços de saída antes de falar, resume saídas grandes ("342 linhas recebidas") e não repete o comando digitado.
- **Saída limpa:** remove sequências ANSI (cores, cursor, título da janela) e guarda só a versão final de barras de progresso, falando a porcentagem de tempos em tempos.
- **Acentos corretos:** UTF-8 em tudo, inclusive caracteres divididos entre dois pacotes de rede.
- **Prompts de senha:** detecta `sudo` e outros pedidos de senha, avisa "Senha solicitada" e esconde o que você digita.
- **Gerenciador de conexões:** login por senha ou por chave (OpenSSH e PEM; ed25519, RSA e ECDSA), com grupos, importação e exportação.
- **Segurança:** senhas e passphrases cifradas com o DPAPI do Windows, verificação da chave do servidor com bloqueio quando ela muda, e nada é falado com o Windows bloqueado.
- **Teclado em primeiro lugar:** atalhos configuráveis, histórico de comandos por servidor e leitura rápida das últimas linhas.
- **Interface nativa:** só controles padrão do Windows, todos com nome acessível, rótulos com tecla de acesso e diálogos que fecham com Esc.
- **Funciona sem fala:** se o NVDA não estiver aberto, o app continua funcionando e avisa na barra de status.

## Requisitos

- Windows 10 ou 11, 64 bits.
- NVDA, para ter fala.
- Para compilar: [SDK do .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0). Não é preciso ter o Visual Studio.

A versão publicada roda sem o .NET instalado.

## Instalação

Ainda não há versão pronta para baixar na página de Releases. Para gerar a sua, siga a seção abaixo e rode `publish.bat`. A pasta `publish\` resultante pode ser copiada para qualquer PC com Windows 64 bits. Abra `ssh-acessivel.exe`.

## Compilar a partir do código

No Prompt de Comando:

```bat
git clone https://github.com/pdonizete/ssh-acessivel.git
cd ssh-acessivel
build.bat
```

| Script | O que faz |
|---|---|
| build.bat | Compila a solução em Release |
| test.bat | Roda os testes unitários (xUnit) |
| run.bat | Compila, se preciso, e abre o app |
| publish.bat | Gera publish\ssh-acessivel.exe, self-contained para win-x64 |

Os scripts procuram o dotnet no PATH e, se não acharem, usam C:\Program Files\dotnet\dotnet.exe.

### DLL do NVDA

A DLL lib\nvdaControllerClient64.dll já vem no repositório, e o build a copia para a pasta do executável. Para trocar por uma versão mais nova, baixe o controllerClient.zip na [página de versões do NVDA](https://www.nvaccess.org/files/nvda/releases/) e copie o arquivo da pasta x64. O app aceita os nomes nvdaControllerClient64.dll e nvdaControllerClient.dll.

## Primeiros passos

1. Abra o app. O foco começa no campo **Comando**. Para saber se o NVDA foi encontrado, leia a barra de status com NVDA+End (layout desktop) ou NVDA+Shift+End (layout laptop).
2. Tecle **Ctrl+N** para abrir o gerenciador de conexões e use **Adicionar** para cadastrar o servidor.
3. Escolha a conexão na lista e tecle **Enter**. Na primeira conexão, confira a impressão digital da chave do servidor e escolha **Aceitar e salvar**.
4. Digite um comando e tecle **Enter**. A resposta é falada. Tecle **F6** para ir ao campo **Saída** e ler com as setas, como num documento.

## Atalhos de teclado

Todos podem ser trocados em **Ajuda > Atalhos de teclado**.

| Atalho | Ação |
|---|---|
| F6 | Alterna o foco entre o campo de comando e o campo de saída |
| Seta acima / Seta abaixo | Comando anterior / próximo do histórico (no campo de comando) |
| Ctrl+C | Interrompe o comando no servidor, quando não há texto selecionado no campo de comando |
| Tab | Envia Tab ao servidor para autocompletar, se ativado nas configurações. Por padrão, move o foco |
| Esc | Interrompe a fala |
| Ctrl+Shift+A | Liga ou desliga a leitura automática da saída |
| Ctrl+Shift+R | Relê a última resposta completa |
| Ctrl+Shift+C | Copia a última resposta para a área de transferência |
| Alt+1 a Alt+9 | Lê a enésima linha mais recente da saída |
| Ctrl+L | Limpa o campo de saída |
| Ctrl+N | Abre o gerenciador de conexões |
| Ctrl+R | Reconecta com os dados da última conexão |

Nenhum desses atalhos conflita com os comandos globais do NVDA. Ctrl sozinho interrompe a fala do NVDA antes de o atalho agir; esse é o comportamento normal do NVDA.

## Dados e privacidade

Tudo fica em %APPDATA%\ssh-acessivel\:

| Arquivo ou pasta | Conteúdo |
|---|---|
| connections.json | Conexões salvas. Senhas e passphrases são cifradas com o DPAPI e só abrem com o mesmo usuário, no mesmo computador |
| known_hosts | Chaves de servidor aceitas com "Aceitar e salvar" |
| settings.json | Configurações e atalhos |
| historico\ | Histórico de comandos, um arquivo por servidor |
| logs\ | Log de erros, um arquivo por dia (Ferramentas > Abrir a pasta de log de erros) |

- A exportação da lista de servidores nunca leva senhas.
- Senhas digitadas depois de "Senha solicitada" não vão para o histórico nem para nenhum log.
- O log da sessão é opcional (Ferramentas > Log da sessão) e grava em .txt o que aparece no campo de saída.
- Se a chave de um servidor mudar, a conexão é bloqueada e o app avisa por fala e por diálogo.

## Testar com um servidor

O jeito mais simples é usar qualquer servidor Linux com openssh-server a que você tenha acesso.

Como alternativa, o repositório traz um servidor de teste em Docker, que escuta só em 127.0.0.1:2222 (usuário teste, senha teste123):

```bat
docker compose up -d
```

Para testar login por chave nesse servidor, gere um par de chaves e coloque a pública em docker\chaves\. A pasta docker\chaves-cliente\ fica fora do Git, para chaves privadas não irem para o repositório:

```bat
ssh-keygen -t ed25519 -f docker\chaves-cliente\minha_chave
move docker\chaves-cliente\minha_chave.pub docker\chaves\
docker compose down
docker compose up -d
```

Sem Docker, também dá para usar o OpenSSH Server do Windows (Configurações > Sistema > Recursos opcionais) ou o openssh-server numa distribuição do WSL.

## Limitações conhecidas

- Programas de tela cheia (vim, nano, htop, less) não são suportados. O app avisa por fala quando detecta um deles.
- No primeiro prompt, o app envia export PAGER=cat GIT_PAGER=cat para man e git log funcionarem. Isso só acontece se o prompt terminar em $, #, > ou %.
- Uma sessão por vez. Abas, SFTP, port forwarding e outros leitores de tela (JAWS, Narrador) estão fora da versão 1.
- Para aceitar uma chave de servidor que mudou de propósito, é preciso apagar a linha do servidor no known_hosts.
- Chaves no formato do PuTTY (.ppk) não foram testadas.
- Com o Tab enviando ao servidor, a linha completada aparece no campo de saída, não no campo de comando.

## Estrutura do projeto

```text
SshAcessivel.slnx
src/
  SshAcessivel.Core/    Lógica sem interface
    Speech/             IScreenReader, NvdaScreenReader, fila de fala
    Output/             ANSI, barras de progresso, UTF-8, prompts de senha
    Ssh/                Sessão SSH (SSH.NET), chaves privadas
    Persistence/        Conexões, DPAPI, known_hosts, histórico, logs
  SshAcessivel.App/     Interface Windows Forms (gera ssh-acessivel.exe)
tests/
  SshAcessivel.Tests/   Testes unitários (xUnit)
lib/                    DLL do controllerClient do NVDA
docker/                 Servidor SSH de teste (opcional)
```

O resto do app só conhece a interface IScreenReader. Suporte a outro leitor de tela entra como uma nova implementação dela.

## Contribuindo

Relatos de problemas e sugestões são bem-vindos na aba [Issues](https://github.com/pdonizete/ssh-acessivel/issues). Ao relatar um problema, descreva o que o NVDA falou (ou deixou de falar) e, se possível, anexe o log de erros do dia.

Para mudanças no código:

- Acessibilidade vem primeiro. Uma mudança que atrapalhe a leitura pelo NVDA não entra.
- Use só controles nativos do Windows, com nome acessível e rótulo com tecla de acesso.
- Textos da interface ficam em src/SshAcessivel.App/Resources/Strings.resx, nunca no código.
- Rode test.bat antes de abrir um pull request.

As regras completas estão em [CLAUDE.md](CLAUDE.md).

## Licença

O código do SSH Acessível está sob a [licença MIT](LICENSE).

A DLL lib/nvdaControllerClient64.dll é distribuída pela NV Access sob a GNU LGPL 2.1. O texto da licença está em [lib/nvdaControllerClient-LICENSE.txt](lib/nvdaControllerClient-LICENSE.txt).

## Créditos

- [NV Access](https://www.nvaccess.org/), pelo NVDA e pela biblioteca controllerClient.
- [SSH.NET](https://github.com/sshnet/SSH.NET), a biblioteca SSH usada pelo app (licença MIT).
