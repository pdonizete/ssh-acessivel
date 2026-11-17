# SSH Acessível

Cliente SSH para Windows feito para quem usa o leitor de tela NVDA. A saída do servidor é falada em blocos, sem o eco do comando digitado, sem sequências de cor e sem cada atualização de barra de progresso.

## Requisitos

- Windows 10 ou 11, 64 bits.
- NVDA instalado e em execução, para ter fala. Sem o NVDA, o app funciona sem fala e avisa isso na barra de status.
- Para compilar: SDK do .NET 10 (`dotnet --list-sdks` deve mostrar 10.x). Não é preciso ter o Visual Studio.
- A versão publicada (pasta `publish\`) roda sem o .NET instalado.

## Onde colocar a DLL do NVDA

Copie a versão x64 da DLL do controllerClient do NVDA para:


lib\nvdaControllerClient64.dll


O arquivo pode se chamar nvdaControllerClient64.dll ou nvdaControllerClient.dll. Baixe o controllerClient.zip na página de versões do NVDA e use o arquivo da pasta x64. O build copia a DLL para a pasta do executável e para a pasta de publicação. Sem a DLL, o build funciona e o app roda sem fala.

## Como compilar, testar e executar

No Prompt de Comando:


cd /d d:\projetos\accessible-ssh
build.bat      (compila em Release)
test.bat       (roda os testes unitários)
run.bat        (abre o app)
publish.bat    (gera publish\ssh-acessivel.exe, self-contained win-x64)


Os scripts usam C:\Program Files\dotnet\dotnet.exe quando o dotnet não está no PATH.

## Servidor de teste

Com Docker:


docker compose up -d


O servidor escuta só em 127.0.0.1, porta 2222. Usuário teste, senha teste123. As chaves de teste ficam em docker\chaves-cliente\:

| Arquivo | Tipo | Formato | Passphrase |
|---|---|---|---|
| teste_ed25519 | ed25519 | OpenSSH | nenhuma |
| teste_rsa_pem | RSA 3072 | PEM | teste123 |
| teste_ecdsa | ECDSA 256 | OpenSSH | teste123 |

Essas chaves servem só para o servidor de teste. Não use nenhuma delas em servidores reais.

Sem Docker, há duas opções: ativar o OpenSSH Server do Windows (Configurações > Sistema > Recursos opcionais > Servidor OpenSSH) ou instalar o openssh-server numa distribuição do WSL.

## Atalhos de teclado

Todos podem ser trocados em Ajuda > Atalhos de teclado.

| Atalho | Ação |
|---|---|
| F6 | Alterna o foco entre o campo de comando e o campo de saída |
| Seta acima / Seta abaixo | Comando anterior / próximo do histórico, no campo de comando. O histórico é separado por servidor |
| Ctrl+C | Envia Ctrl+C ao servidor, quando o campo de comando não tem texto selecionado |
| Tab | Envia Tab ao servidor para autocompletar, só se a opção estiver ligada nas configurações. Por padrão, Tab move o foco |
| Esc | Interrompe a fala |
| Ctrl+Shift+A | Liga ou desliga a leitura automática da saída |
| Ctrl+Shift+R | Relê a última resposta completa |
| Ctrl+Shift+C | Copia a última resposta para a área de transferência |
| Alt+1 a Alt+9 | Lê a enésima linha mais recente da saída |
| Ctrl+L | Limpa o campo de saída |
| Ctrl+N | Abre o gerenciador de conexões |
| Ctrl+R | Reconecta com os dados da última conexão |

Nenhum desses atalhos conflita com os comandos globais do NVDA. Ctrl sozinho interrompe a fala do NVDA antes de o atalho agir. Esse é o comportamento normal do NVDA.

## Onde ficam os dados

Tudo fica em %APPDATA%\ssh-acessivel\:

- connections.json: conexões salvas. Senhas e passphrases são cifradas com o DPAPI do Windows e só podem ser abertas pelo mesmo usuário, no mesmo computador.
- known_hosts: chaves de servidor aceitas com "Aceitar e salvar".
- settings.json: configurações e atalhos.
- historico\: histórico de comandos, um arquivo por servidor.
- logs\: log de erros do app, um arquivo por dia. Envie esses arquivos quando algo falhar (Ferramentas > Abrir a pasta de log de erros).

O log da sessão é opcional (Ferramentas > Log da sessão) e grava o que aparece no campo de saída. Senhas digitadas depois de "Senha solicitada" não vão para o histórico nem para o log.

## Limitações conhecidas

- Programas de tela cheia (vim, nano, htop, less) não são suportados. O app avisa por fala quando detecta um deles.
- O app envia export PAGER=cat GIT_PAGER=cat no primeiro prompt. Isso só acontece se o prompt terminar em $, #, > ou %.
- Uma sessão por vez. Abas, SFTP, port forwarding e outros leitores de tela (JAWS, Narrador) ficaram fora da v1.
- Para aceitar uma chave de servidor que mudou de propósito, é preciso apagar a linha do servidor no known_hosts.
- Chaves no formato do PuTTY (.ppk) não foram testadas.
- Com Tab enviando ao servidor, o texto digitado vai junto com o Tab, e a linha completada aparece no campo de saída, não no campo de comando.
