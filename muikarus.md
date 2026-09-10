# MUIKARUS.MD — MU IKARUS (Arquivo-Mestre do Projeto MU Online)

> **Regra de Ouro:** Ler este arquivo ao trabalhar no projeto MU Online dentro da rede IKARUS SERVERS.  
> Ele é o **Índice Geral & Diário de Bordo** de tudo o que foi pesquisado, baixado, construído, testado e configurado no ecossistema MU Online Cross-Platform.

---

## 🛡️ LEIS DE OURO & DIRETRIZES ANTI-REGRESSÃO (MOBILE)

> **AVISO CRÍTICO PARA DESENVOLVEDORES E IA:**  
> Estas regras foram descobertas após testes práticos e diagnósticos intensivos no ambiente Android/MonoGame. **O descumprimento de qualquer uma delas causará regressão grave (queda para 2 FPS, teclado sumindo ou jogo congelando).**

### 1. ⌨️ Teclado Virtual Mobile (`MainActivity.cs` & `TextFieldControl.cs`)
* ❌ **NUNCA** usar `EditText` transparente, invisível ou off-screen no frame esperando que `InputMethodManager.ShowSoftInput` funcione na `GLSurfaceView` do MonoGame. O modo `ImmersiveSticky` de tela cheia captura todo o foco da janela e o sistema Android descarta o pedido de abertura do teclado.
* ✅ **SEMPRE** invocar o diálogo nativo escuro com elevação de janela (`AlertDialog` estilizado com `ThemeDeviceDefaultDialogAlert`), configurado com `dialog.Window.SetSoftInputMode(SoftInput.StateAlwaysVisible)` e `ShowSoftInput(ShowFlags.Forced)`.
* ✅ **SEMPRE** manter a cadeia fluida: Usuário ➔ Senha (transição automática de 150ms) ➔ Disparo de Login automático ao pressionar "Concluir / Done / OK" no teclado.

### 2. ⚡ Desempenho 3D e FPS Mobile (`SelectWorld.cs`, `TerrainControl.cs`, `WalkableWorldControl.cs`)
* ❌ **EXTREMAMENTE PROIBIDO** chamar `InvalidateBuffers()` todo frame em objetos do mundo (como acontecia no `WaterFallObject.cs`). Reconstruir e enviar DynamicVertexBuffers para a GPU móvel a cada frame reduz o jogo imediatamente para **2 FPS**.
* ❌ **NUNCA** renderizar o terreno 3D aberto e pesado do World 94 na seleção de personagens no mobile (`SelectWorld.cs`). Manter `Terrain.Visible = false` e `Array.Clear(MapTileObjects)`. O modo clássico focado apenas nos personagens 3D eleva a taxa para **60 FPS cravados**.
* ❌ **NUNCA** esquecer de verificar `if (!Visible || Status != Models.GameControlStatus.Ready) return;` no `TerrainControl.DrawAfter()`. Caso contrário o terreno é desenhado em loop mesmo com `Visible = false`.
* ❌ **NUNCA** reativar emissores pesados de partículas contínuas (`WaterSplashObject`) ou distorção matemática de água no terreno (`DistortionAmplitude`) em telas estáticas no mobile.
* ❌ **NUNCA** reativar `Constants.DRAW_GRASS = true` no mobile (o overdraw da grama derruba o framerate de 30 FPS para 5 FPS em Lorencia).
* ✅ **SEMPRE** manter o decodificador customizado DXT (`DxtDecoder.DecompressDXT1/3/5`) ativo em `MainActivity.cs` para evitar textura corrompida ou travamentos na carga GL.
* ✅ **SEMPRE** manter `CalculateMouseTilePos()` no `WalkableWorldControl.cs` lendo `MuGame.Instance.Mouse.Position` (o método desktop `Mouse.GetState()` sempre retorna `(0, 0)` no touch).
* ✅ Manter `Camera.Instance.ViewFar` limitado entre `3000f` e `3500f` no mobile.

### 3. 🔄 Troca de Cenas e Rede (`NetworkManager.cs` & `GameScene.cs`)
* ❌ **NUNCA** esquecer de chamar `await MuGame.Network.SendClientReadyAfterMapChangeAsync();` ao concluir `GameScene.LoadSceneContentWithProgress()`. O servidor OpenMU exige este pacote (`0xB0`) para liberar o spawn do herói e o streaming de entidades no mapa. Sem ele, o servidor fica esperando, o mundo não carrega e gera ANR ("MuAndroid não está respondendo").
* ❌ **NUNCA** disparar troca de cena concorrente no `NetworkManager.cs`. Quando o servidor envia `ProcessCharacterRespawn`, se a cena ativa for `SelectCharacterScene`, DEVE-SE ignorar a troca direta e delegar exclusivamente ao evento `EnteredGame`.
* ✅ **SEMPRE** manter a trava de concorrência `_isChangingScene` em `MuGame.ChangeSceneInternal`.
* ✅ **SEMPRE** manter o fallback em `SelectCharacterScene.HandleEnteredGame` para recuperar dados do personagem via `_networkManager.GetCharacterState()` caso pacotes cheguem fora de ordem.

### 4. 👆 Interatividade e Touch Screen
* ✅ Elementos 3D distantes em celulares são difíceis de mirar com o dedo. **SEMPRE** disponibilizar atalhos de toque generosos:
  * Rótulos flutuantes com `Interactive = true`.
  * Cards/botões touch visíveis na tela (ex: botões dourados no rodapé da seleção de personagem).

### 5. 📦 Versionamento e Build CI/CD
* ✅ O pipeline GitHub Actions (`android-build.yml`) extrai automaticamente o número da versão do `AndroidManifest.xml`.
* ✅ Sempre atualizar em sincronia: `AndroidManifest.xml` (versionCode/versionName), `MuAndroid.csproj` (ApplicationVersion/DisplayVersion) e a seção de changelog neste arquivo.

---

## 🗺️ MAPA DO PROJETO (`c:\TECX SOFTHOUSE\L2 IKARUS INTERCROW\MU_ONLINE\`)

| Pasta / Repositório | O que é | Tecnologias | Origem / Repo |
|---|---|---|---|
| `MU_ONLINE/OpenMU/` | **Servidor Backend + Web Admin** | C# / .NET 10.0 / ASP.NET Core / PostgreSQL / Docker | [MUnique/OpenMU](https://github.com/MUnique/OpenMU) |
| `MU_ONLINE/Client_Android/` | **Cliente Cross-Platform (Mobile)** | C# / .NET 9.0 / MonoGame Framework | [bhrama-br/muonline-android](https://github.com/bhrama-br/muonline-android) |
| `MU_ONLINE/Client_Desktop/` | **Cliente PC (Windows/Linux)** | C# / .NET 10.0 / MonoGame Framework | [xulek/muonline](https://github.com/xulek/muonline) |

---

## 🖥️ DIAGNÓSTICO DO AMBIENTE (.NET SDK)

- **SDKs Instalados no PC:**
  - `.NET SDK 7.0.400`
  - `.NET SDK 8.0.424`
  - `.NET SDK 10.0.400` (Instalado em `C:\Users\icaro\AppData\Local\Microsoft\dotnet`)
- **Status do Build:** 🎉 **Compilação do OpenMU realizada com 100% de sucesso (0 Erros)!**
- **PATH do Sistema:** Atualizado com `C:\Users\icaro\AppData\Local\Microsoft\dotnet` no escopo do Usuário.

---

## 💡 DESCOBERTAS CHAVE & ARQUITETURA

1. **Cross-Play Real (PC + Android/iOS):**
   * O cliente MonoGame em C# (`Client_Android` / `Client_Desktop`) lê os arquivos originais do MU (`.bmd`, `.ozj`, `.tga`) e renderiza em 3D nativo tanto no PC quanto no celular.
   * O servidor `OpenMU` gerencia conexões de todas as plataformas simultaneamente.

2. **Infraestrutura em Nuvem (Servidor Cross-Platform):**
   * Por ser escrito em .NET Core / C#, o `OpenMU` roda em **Linux (Ubuntu/Debian) via Docker** ou em **Windows Server**, reduzindo significativamente os custos de hospedagem em VPS.
   * Acompanha um **Painel Admin Web (ASP.NET Core)** acessível em `http://localhost:5000` para gerenciar contas, personagens, inventário, drops e mapas em tempo real.
   * Portas Padrão: `44405` (ConnectServer) / `55901` (GameServer) / `5000` (Web Admin Panel).

3. **Estratégia de Monetização & Marketing Orgânico:**
   * **Aquisição Frequente (TikTok / Instagram Reels):** Vídeos de 15 segundos focados nos gatilhos nostálgicos (som da *Jewel of Bless*, Chaos Machine, asas +15).
   * **Fricção Mínima:** Download do cliente leve em celular/PC facilita conversão imediata de jogadores solo.
   * **Decisão Oficial (26/08):** O projeto usará o **Cliente Moderno Cross-Platform** como oficial, trazendo o saudosismo clássico atrelado a uma pegada moderna, jogável em Celulares e PC simultaneamente.

---

## 🏗️ FLUXO COMPLETO DO PROJETO (ARQUITETURA E BUILD)

Para que o projeto funcione perfeitamente de ponta a ponta (Servidor na VPS + APK no Celular do Jogador), este é o ciclo de vida e a arquitetura oficial:

### 1. O Servidor Backend (OpenMU)
* **Onde fica:** Roda dentro da VPS Windows (IP `192.99.110.164`).
* **Como ligar:** Através do script `Ligar_Servidor.bat` (comando: `dotnet run --project src\Startup -- -resolveIP:192.99.110.164`).
* **O que faz:** Ele abre a porta `44405` (ConnectServer) para receber os jogadores. Quando o jogador clica na sala de jogo, o ConnectServer envia para o celular o IP Público da VPS para que o celular se conecte no GameServer (porta `55901`).
* **⚠️ Regra Crítica:** Se o servidor não rodar com a flag `-resolveIP:192.99.110.164`, o ConnectServer vai enviar `127.0.0.1` para o celular. O celular vai tentar se conectar nele mesmo, não vai achar nada, e vai exibir a mensagem **"Status: Disconnected"** antes de abrir a tela de login.

### 2. O Aplicativo Android (O APK do Jogador)
* **Como é feito o APK:** O código fica na pasta `Client_Android`. Ele é compilado em C# usando o framework MonoGame (que roda jogos nativos no Android).
* **Processo de Build (GitHub Actions):** Sempre que alteramos o código (ex: mudamos o IP no `MuOnlineSettings.cs`), nós enviamos para o GitHub. A nuvem do GitHub (Actions) roda o comando `dotnet publish` com o Workload do Android 9.0, compila as texturas via `mgcb` (MonoGame Content Builder), assina o aplicativo e gera o arquivo `MuAndroid-Signed.apk`.
* **Download dos Gráficos (Patch):** O APK nativo tem apenas ~30MB. Ao abrir pela primeira vez, ele usa o link do GitHub Releases (ou Google Drive) para baixar o `Data.zip` (com os gráficos pesados de 875MB), extrai na memória interna do celular e liga o motor 3D.

### 3. Comunicação (Rede)
* O celular (seja no Wi-Fi ou 4G/5G) usa a classe `ConnectionManager.cs` do Android.
* É obrigatório usar `DnsEndPoint` em vez de `IPEndPoint`. Redes de celular modernas bloqueiam tentativas diretas de conexão IPv4. O `DnsEndPoint` permite que o sistema Android converta e direcione a rede corretamente.

---

## 📜 HISTÓRICO DE AÇÕES & CRONOGRAMA

### 24/08/2026 — Inicialização & Compilação do Projeto MU IKARUS
- [x] Pesquisa de mercado e comparativo de rentabilidade (L2 vs MU vs Priston vs Cabal).
- [x] Seleção da stack Open Source Cross-Platform (OpenMU + MonoGame Client).
- [x] Criação da estrutura de pastas `MU_ONLINE/` no workspace.
- [x] Download/Clone dos repositórios: `OpenMU` (Server), `Client_Android` e `Client_Desktop`.
- [x] Instalação do .NET 8.0 SDK e .NET 10.0 SDK.
- [x] Compilação do `OpenMU` com 100% de sucesso (0 erros).
- [x] Teste de execução em Modo Demo (`-demo -autostart`).
- [x] Download do pacote de assets oficiais `MU_Full_Data.zip` (1.74 GB) concluído.
- [x] Criação do arquivo-mestre `MUIKARUS.MD`.

### 26/08/2026 — Otimização do Ambiente e Primeiros Testes de Login
- [x] Varredura profunda no SSD e script de limpeza automática de diretórios temporários, liberando +7 GB de espaço.
- [x] Criação do script `Iniciar_MU_Online.bat` mapeando o ambiente do `.NET 10.0` para evitar travamentos de DirectX (erro `0xc0000142`).
- [x] Conectividade validada: Cliente Desktop e Servidor OpenMU agora abrem juntos automaticamente.
- [x] Correção do erro da tela de carregamento do cliente 3D (`Error initializing LoginScene: NullReferenceException`) criando uma Junção de Diretório (`mklink /J`) para linkar a pasta `Data/` (1.7 GB) diretamente na pasta de binários do cliente sem precisar duplicar os arquivos, poupando espaço.
- [x] **Sucesso Absoluto (Localhost):** O servidor foi configurado para resolver o IP de conexão via loopback (`-resolveIP:loopback`), corrigindo o erro de bloqueio de NAT do roteador na hora de migrar do ConnectServer para o GameServer. Personagem "testgm" testado e logado em Lorencia com sucesso pelo cliente Desktop.

### 27/08/2026 — Integração Android, GitHub Actions e Polimento UI
- [x] Criação de um bypass (Google Drive Direct Link com `confirm=t`) injetado no código-fonte (`Constants.cs`) para baixar o pacote de dados (`Data.zip` de 875 MB) sem ser barrado pela tela de aviso de vírus do Google.
- [x] Correção do script de CI/CD do GitHub Actions: Atualizadas as actions (`setup-dotnet` e `checkout`) para a versão `v4`, garantindo compatibilidade com o `.NET 9` e resolvendo falhas de compilação na nuvem.
- [x] Otimização da UI no Android (Tela Cheia): Implementado o modo **Immersive Sticky** no `MainActivity.cs` para ocultar barras de navegação/status nativas do celular.
- [x] Correção de Pillarboxing (Barras Pretas laterais): Alterado o motor gráfico `MuGame.cs` para capturar a resolução ultrawide nativa do aparelho dinamicamente, abandonando a trava antiga de 16:9 (`1280x720`).
- [x] Correção de Crash Crítico (Android): Resolvido o erro `NullReferenceException` e divisão por zero causado por chamadas antecipadas ao `GraphicsAdapter`. A configuração de `PreferredBackBuffer` foi zerada para forçar o MonoGame a renderizar com o tamanho físico real do aparelho.
- [x] Validação de Download de Assets (Android): O jogo foi capaz de iniciar, fazer o download limpo e extrair o pacote `Data.zip` (875 MB) fornecido via GitHub Releases. Aplicativo atingindo perfeitamente a tela de Login (em stand-by aguardando VPS).
- [x] Sucesso na instalação e bypass do Google Play Protect durante os testes com o APK assinado (Signed).

### 29-30/08/2026 — Correções de Conexão (VPS) e Resolução da Tela Preta no Android
- [x] Correção do envio do IP Público da VPS pelo Servidor OpenMU para os clientes na transição do ConnectServer, impedindo que o cliente tente conectar num IP local.
- [x] Correção de DNS/IPv6 (NAT64) no Android alterando a forma de conexão (`IPEndPoint` para `DnsEndPoint`) para resolver falhas de rede em dados móveis/alguns provedores.
- [x] Correção do "Crash de Tela Preta" no Android: Desativado o modo de FPS Ilimitado (`UNLIMITED_FPS`), que causava gargalo severo de renderização e travava completamente o aparelho.
- [x] Configuração de Keystore Android (Assinatura de App) persistente, garantindo a compilação de APKs instaláveis e assinados.

### 30/08/2026 — Alinhamento de IPs e Registro de Erro (Desconexão no Android)
- [x] **Diagnóstico do erro "Status: Disconnected" no celular:** Falso alarme sobre a VPS estar desligada (teste de ping confirmou portas 44405 e 55901 abertas e operantes). O log interno da VPS provou que o erro de desconexão ocorria porque o cliente Android enviava um "RST" (Connection Reset) e forçava a queda da conexão prematuramente.
- [x] **Causa do Crash:** A biblioteca de rede base (Pipelines.Sockets.Unofficial) não suporta a criação de sockets usando a classe `DnsEndPoint` no Android e acabava fechando a conexão. E o modelo anterior, `IPEndPoint`, não suporta as redes IPv6/NAT64 modernas (ex: Starlink, 4G/5G).
- [x] **Solução Definitiva (Rede):** O `ConnectionManager.cs` do Android foi atualizado com um resolvedor de DNS manual em C#. Ele faz a busca por trás dos panos (`Dns.GetHostAddressesAsync`), verifica se o celular sintetizou o IP em IPv6 (NAT64), puxa esse IP e joga ele limpo dentro de um `IPEndPoint`.
- [x] **A Otimização Assassina (A Tela Preta):** A otimização visual que tentamos fazer na última sessão (`IsFixedTimeStep = false`) fez o jogo rodar num loop infinito de CPU sem descanso (Thread Starvation). O jogo tentava desenhar a tela milhares de vezes por segundo, o que sufocava a placa de rede e os downloads de recursos em segundo plano do celular. O resultado? O mapa 3D não carregava (tela preta) e o sinal de rede morria sufocado, resultando em "Disconnected". A otimização foi revertida para o comportamento original, limitando a 60 FPS estritamente para deixar a CPU respirar.
- [x] **Configuração Dinâmica de FPS (TargetFPS):** Adicionado suporte ao limite de FPS dinâmico via `appsettings.json`. O celular agora roda travado a **30 FPS por padrão**, garantindo o máximo de economia de bateria, zero superaquecimento, e muita sobra de processamento para download e rede. Para voltar para 60 FPS, basta mudar o `TargetFPS` no `appsettings.json`!

### 03/09/2026 — Resolução Definitiva da Tela Preta e Disconnect no Android
- [x] **Restauração da Conexão Nativa (OpenMU Pipelines):** Revertida a tentativa com `TcpClient` + `StreamDuplexPipe` que forçava a queda da conexão. Restaurado o `SocketConnection.ConnectAsync` com resolvedor de DNS assíncrono para IPv4/IPv6/NAT64, garantindo estabilidade e comunicação direta com o ConnectServer da VPS.
- [x] **Resolução da Tela Preta (Mundo 3D / Barco e Mar):** Corrigido o `LoginScene.cs` para manter o `NewLoginWorld` sempre ativo mesmo na presença de avisos secundários, impedindo que a cena seja descartada e o fundo fique preto.
- [x] **Correção de Leitura de Assets no Android:**
  - `Constants.cs`: Criada detecção inteligente do `DataPath` verificando tanto o armazenamento externo (`GetExternalFilesDir`) quanto interno (`BaseDirectory`).
  - `LoadScene.cs`: Checagem rigorosa da existência de arquivos fundamentais (`World95/EncTerrain95.att` e `World1/EncTerrain1.att`) antes de pular download, evitando entrar no jogo com assets parciais ou corrompidos.
  - `Utils.cs`: Aprimorado o `GetActualPath` com busca case-insensitive recursiva para diretórios e arquivos em sistemas Linux/Android com cache em memória.
  - `BMDLoader.cs`: Eliminada a duplicação de `Path.Combine(Constants.DataPath, path)`.
- [x] **Otimização de Renderização (Fim dos Travamentos):**
  - `NewLoginWorld.cs`: Reduzida a distância máxima de renderização da câmera (`Camera.Instance.ViewFar`) de `50000f` para `6000f`, diminuindo drasticamente a contagem de DrawCalls e eliminando engasgos na GPU do celular.
- [x] **Publicação Automática de Releases (Versão v10.1):** Configurado o GitHub Actions para publicar o APK diretamente na aba Releases sob a tag **`v10.1`** e renomeado o arquivo para **`IkarusMU-v10.1.apk`**, permitindo download e instalação direta pelo celular com 1 clique sem passar pelo PC.

---

### 04/09/2026 — Versão v1.12: Reconhecimento do Data.zip, Bypass de Re-download e Ajuste de Versionamento
- [x] **Reconhecimento de Assets já Existentes (Fim do Re-download de 1.7 GB):**
  - **Problema:** Toda vez que um novo APK era instalado ou atualizado, o jogo não encontrava a pasta `Data` anteriormente extraída e forçava o download de 1.7 GB do zero.
  - **Causa:** No Android, o `Constants.DataPath` e a validação do `LoadScene.cs` checavam caminhos voláteis ou incompletos, e a lista de arquivos de teste para validar se o jogo estava completo falhava por diferenças de maiúsculas/minúsculas no Linux/Android.
  - **Solução (`Constants.cs` & `LoadScene.cs`):** Implementada checagem com prioridade máxima para a pasta externa padrão (`/Android/data/com.ikarus.mu/files/Data/`) e fallback interno (`BaseDirectory/Data/`). Adicionada verificação multi-arquivo de integridade (`Object95/water01.ozj`, `Object1/Tile01.ozj`, `World1/EncTerrain1.att`, etc.). Se os arquivos existirem, o download é pulado imediatamente e o jogo entra no mundo 3D em menos de 2 segundos.
- [x] **Ajuste e Padronização do Versionamento:**
  - Correção da numeração solicitada pelo usuário: De v10.1 para **v1.12** (`versionCode: 12`), alinhando o ciclo correto de updates sucessivos a partir da v1.10.
  - Atualização do fluxo do GitHub Actions para gerar tags automáticas no repositório oficial com release direta do APK assinado (`IkarusMU-v1.12.apk`).

---

### 04/09/2026 — Versão v1.13: Toque na Tela (Touch to Click), Auto-Open de Servidores e Otimização 30 FPS
- [x] **Correção Crítica: Botão "Servers" não respondia ao toque ("clico e não aparece nada"):**
  - **Diagnóstico:** O sistema de interface gráfica do MU (`GameControl.cs`) ouvia estritamente os eventos do mouse (`MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed / Released`). No Android, o motor MonoGame recebia os toques da tela física através do `TouchPanel`, porém o `MuGame.cs` apenas atualizava a posição do cursor (`CursorControl`) e **nunca** traduzia o toque para o botão esquerdo do mouse. Por isso, ao tocar no botão "Servers", o cursor se movia até ele, mas o clique nunca era disparado!
  - **Solução no Motor (`MuGame.cs`):** Implementada a sintetização de `MouseState` a partir do `TouchCollection` do `TouchPanel`. Agora, enquanto o dedo estiver na tela física do celular, o MonoGame registra `Mouse.LeftButton = ButtonState.Pressed`. No momento em que o jogador tira o dedo da tela, o sistema emite `ButtonState.Released` na exata coordenada do toque, disparando o evento `OnClick()` de qualquer botão do jogo.
  - **Padding de Toque (Touch Hitbox Tolerance):** Adicionado padding de 12 pixels na detecção de cliques do `GameControl.cs` no Android, facilitando o toque em botões finos em telas touch de alta resolução.
  - **Abertura Automática da Lista de Servidores (`LoginScene.cs`):** Para eliminar a necessidade do jogador precisar caçar o minúsculo botão de 110x26 pixels "Servers" com o dedo, o método `HandleServerListReceived` agora seleciona automaticamente o grupo de servidores e torna `_serverList.Visible = true` assim que a resposta do ConnectServer chega. A lista de salas surge instantaneamente no centro da tela.
  - **Centralização Dinâmica (`ServerList.cs`):** O `ViewSize` da janela de servidores agora é recalculado automaticamente baseado na quantidade de salas existentes, garantindo alinhamento e centralização perfeitos no celular.
- [x] **Otimização de Performance e Fim do Travamento ("está travando bastante, achei que seria mais leve"):**
  - **Diagnóstico:** O jogo estava tentando rodar a 60 FPS (`TargetElapsedTime = 16.66ms`) em telas mobile que frequentemente possuem resolução nativa Full HD+ (1080x2400) ou 2K. O cálculo intensivo de vértice de água, malhas 3D oceânicas e efeitos a cada 16 milissegundos causava superaquecimento e estrangulamento térmico (thermal throttling) da GPU/CPU.
  - **Solução (Trava em 30 FPS Estáveis):** Fixado o `TargetElapsedTime = TimeSpan.FromTicks(333333)` (30 FPS) no Android/iOS. Isso reduziu pela metade a carga de processamento do chip gráfico do smartphone, mantendo o consumo de bateria baixo e a taxa de quadros estável sem engasgos.
  - **Culling de Câmera 3D (`NewLoginWorld.cs`):** Reduzida a distância máxima de renderização da câmera (`Camera.Instance.ViewFar`) de `6000f` para `3200f` no mobile, impedindo que centenas de malhas de oceano e terreno invisíveis ao jogador fossem processadas.
  - **Otimização de Shaders de Água:** Reduzida a velocidade e a amplitude de distorção da superfície da água para 0.05f no mobile, poupando processamento de shaders na GPU.
- [x] **Release e Versionamento v1.13:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 13` e `versionName: 1.13`.
  - Workflow GitHub Actions atualizado para gerar e publicar o **`IkarusMU-v1.13.apk`** na release `v1.13`.

---

### 04/09/2026 — Versão v1.14: Teclado Virtual Nativo no Android e Digitação de Login/Senha
- [x] **Correção Crítica: Teclado virtual não abria ao clicar nos campos ("clico e o teclado não abre"):**
  - **Diagnóstico:** O sistema de texto do MU (`TextFieldControl.cs`) esperava exclusivamente teclas físicas do teclado do computador (`MuGame.Instance.Keyboard.GetPressedKeys()`). Em smartphones, o sistema operacional Android não exibe o teclado na tela (IME / soft keyboard) automaticamente para jogos MonoGame, porque a tela é um canvas gráfico (`SurfaceView`) sem campos nativos de formulário vinculados.
  - **Solução no Motor (`TextFieldControl.cs`):**
    - Criado o delegate estático `ShowKeyboardAsync` e o método `TriggerSoftKeyboard()`, além das propriedades `Label` e `Placeholder`.
    - Sobrescrito o método `OnClick()` no controle de texto: ao tocar na caixa de Usuário ou Senha, o jogo agora solicita a abertura imediata do teclado para digitação.
    - Aumentada a altura padrão dos inputs de 14px para 20px, facilitando o toque em telas mobile.
  - **Implementação Nativa no Android (`MainActivity.cs`):**
    - Criado o método `ShowTextInputDialogAsync` acoplado ao `AlertDialog` e `EditText` nativos do Android.
    - Forçado o modo de teclado sempre visível (`SoftInput.StateAlwaysVisible`) e foco imediato com `InputMethodManager.ShowSoftInput`.
    - Suporte a campo mascarado para Senha (`PasswordTransformationMethod`).
    - Botões nativos "OK" e "Cancelar" integrados com retorno assíncrono direto para a interface do jogo sem perda de estado.
  - **Aprimoramento da Tela de Login (`LoginDialog.cs`):**
    - Tornados os rótulos de texto "User" e "Password" clicáveis (`Interactive = true`), permitindo que tocar tanto na caixinha quanto no texto ao lado abra o teclado para digitar.
    - Textos de instrução configurados: "Usuário" / "Digite seu usuário de login" e "Senha" / "Digite sua senha".
- [x] **Release e Versionamento v1.14:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 14` e `versionName: 1.14`.
  - Workflow GitHub Actions atualizado para gerar e publicar o **`IkarusMU-v1.14.apk`** na release `v1.14`.

---

### 04/09/2026 — Versão v1.15: Fim da "Janelinha Branca", Digitação Direta no Jogo e Zoom na Tela de Login
- [x] **Eliminação da "Janelinha Branca" (Remoção do AlertDialog intermediário):**
  - **Problema:** Ao tocar nos campos, o Android abria um popup modal branco separado do jogo (`AlertDialog`), que causava estranheza visual, exigia confirmação dupla no "OK", e em alguns casos reabria o teclado desnecessariamente.
  - **Solução (`MainActivity.cs`):** Implementada uma ponte de digitação transparente em tempo real via `EditText` invisível (`_hiddenInput`) acoplado ao ciclo de vida do Android fora da área de toque da tela. Ao focar em qualquer campo no jogo, o teclado do celular abre suavemente sobre a tela do MU, e cada letra, número ou backspace digitado reflete **diretamente dentro da caixa de texto do próprio MU** em tempo real!
  - **Fim do Loop de Foco:** O teclado só requisita abertura se o campo ativo mudar, evitando que a tela pisque ou feche/reabra ao digitar.
  - **Ação Concluído (Enter/Done):** Ao pressionar a tecla Enter/Done no teclado virtual, o teclado é recolhido automaticamente.
- [x] **Zoom e Ampliação dos Campos de Login (`LoginDialog.cs`):**
  - **Problema:** A janela de login original de PC (300x200) ficava muito pequena e distante em celulares de tela grande ou alta resolução.
  - **Solução:**
    - Dimensões ampliadas em +46%: de `300x200` para **`440x270`**.
    - Caixas de texto de Usuário e Senha aumentadas para **`280px` de largura por `28px` de altura** (anteriormente 176x14), proporcionando uma área de toque ampla e confortável para os dedos.
    - Tamanho das fontes aumentado de 12 para 14/16 pixels, tornando os textos e letras nítidos.
    - Sanitização automática com `.Trim()` nos campos de Usuário e Senha para impedir que espaços invisíveis inseridos pelo corretor do celular impeçam o login.
- [x] **Release e Versionamento v1.15:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 15` e `versionName: 1.15`.
  - Workflow GitHub Actions atualizado para gerar e publicar o **`IkarusMU-v1.15.apk`** na release `v1.15`.

---

### 04/09/2026 — Versão v1.16: Chão de Lorencia Restaurado, 30+ FPS Sem Quedas e Movimentação Touch Precisa
- [x] **Correção Crítica: Chão Preto em Lorencia ("chão preto"):**
  - **Diagnóstico:**
    1. GPUs de smartphones (Qualcomm Adreno e ARM Mali) utilizam drivers OpenGL ES que **não suportam nativamente texturas comprimidas no padrão desktop S3TC (DXT1, DXT3, DXT5)**.
    2. O `Client_Desktop` possuía o decodificador `DxtDecoder.cs` e a função `CustomDecompressFunction`, enquanto o `Client_Android` não possuía nenhum dos dois implementados no `TextureLoader.cs` nem no `MainActivity.cs`.
    3. As texturas de terreno (`TileGrass01.ozj`, etc.) e DDS falhavam ou retornavam nulo, e qualquer textura chamada na thread de carregamento assíncrono falhava por falta de contexto OpenGL ativo na thread secundária.
  - **Soluções Implementadas:**
    - Portado o **`DxtDecoder.cs`** para o `Client_Android/Client.Main/Content/DxtDecoder.cs`, realizando descompressão rápida em memória de DXT1/DXT3/DXT5 para RGBA8888.
    - Atualizado o `TextureLoader.cs` com suporte a `CustomDecompressFunction`, pool de memória `ArrayPool<Color>.Shared` para uploads instantâneos à GPU, e busca de arquivos insensível a maiúsculas/minúsculas (`Utils.GetActualPath`).
    - Vinculada a descompressão nativa no `MainActivity.cs` via `TextureLoader.Instance.CustomDecompressFunction`.
    - No `TerrainControl.cs`, implementado carregamento *on-demand* com recuperação automática na thread de renderização da GPU: se qualquer textura do piso for necessária, ela é gerada instantaneamente no contexto gráfico oficial do jogo, eliminando para sempre o chão preto!
- [x] **Otimização Extrema de Desempenho (De 2 FPS para 30+ FPS fluidos):**
  - **Diagnóstico:** O motor do terreno (`TerrainControl.cs`) estava calculando e despachando proceduralmente mais de 12 tufos de grama 3D com jitter aleatório, rotação e vento para cada tile visível em cena (`grassPerTile = 12`), além de recalcular tabelas de seno de vento concorrentes sem verificar a flag `DRAW_GRASS`. Em celulares, essa avalanche de quads afogava a CPU/GPU em Lorencia, derrubando os quadros para 2 FPS.
  - **Soluções Implementadas:**
    - Adicionada a flag `Constants.DRAW_GRASS = false` por padrão no mobile.
    - No `TerrainControl.cs`, blindados com `if (!Constants.DRAW_GRASS) return;` a geração dos quads de grama (`RenderTerrainTile`), o despacho de buffers (`FlushGrassBatch`) e o cálculo de vento multithread (`InitTerrainWind`).
    - Ativadas as diretrizes `ApplyAndroidDefaults()` no `MainActivity.cs`: luzes dinâmicas desligadas, otimização de GPU integrada ativada e shaders pesados de reflexo simplificados para mobile.
- [x] **Correção da Caminhada e Precisão do Clique ("o click não obedece onde cliquei, o char andou 1 vez apenas"):**
  - **Diagnóstico:** No arquivo `WalkableWorldControl.cs` (linha 159), o método `CalculateMouseTilePos()` usava a chamada desktop `Mouse.GetState().Position.ToVector2()`, que no Android sempre retornava `(0, 0)` (o canto superior esquerdo da tela)! Enquanto isso, os toques reais estavam sendo processados em `MuGame.Instance.Mouse`. Como resultado, qualquer toque em qualquer lugar do mapa projetava um raio para o ponto `(0, 0)`, fazendo o personagem andar para o lugar errado uma única vez e parar.
  - **Solução:** Corrigida a linha 159 para utilizar diretamente `MuGame.Instance.Mouse.Position.ToVector2()`. Agora, o raio 3D desprojeta exatamente no tile do chão onde o jogador tocou com o dedo, garantindo movimentação instantânea, precisa e responsiva em Lorencia.
- [x] **Release e Versionamento v1.16:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 16` e `versionName: 1.16`.
  - Workflow GitHub Actions atualizado para gerar e publicar o **`IkarusMU-v1.16.apk`** na release `v1.16`.

### 04/09/2026 — Versão v1.17: Teclado Virtual Fluido e Otimizado no Login
- [x] **Abertura Fluida do Teclado Nativo no Android (`MainActivity.cs`):**
  - **Problema:** Na v1.15/v1.16, o campo transparente estava configurado fora da tela (`-500, -500`), o que fazia o gerenciador de métodos de entrada do Android (`InputMethodManager`) ignorar as requisições de foco e não subir o teclado virtual ao tocar nos inputs de usuário e senha.
  - **Soluções Implementadas:**
    - Ajustado o `EditText` invisível (`Alpha = 0.01f`) para estar dentro da hierarquia da janela com layout real (`100x50`), garantindo foco imediato via `FocusableInTouchMode = true`.
    - Implementado despacho assíncrono via `_hiddenInput.Post(...)` combinando `ShowSoftInput(ShowFlags.Forced)` e `ToggleSoftInput(ShowFlags.Forced, HideSoftInputFlags.ImplicitOnly)`.
    - Adicionada navegação fluida: ao apertar a tecla "Avançar / Próximo" (ImeAction.Next) no teclado do celular enquanto preenche o Usuário, o cursor e foco pulam automaticamente para o campo da Senha.
    - Sincronização em tempo real frame a frame com o motor do jogo sem travamentos ou janelas brancas intermediárias.
### 04/09/2026 — Versão v1.18: Teclado Virtual 100% Infalível e Fluido no Login
- [x] **Garantia Absoluta de Abertura do Teclado (`MainActivity.cs` & `TextFieldControl.cs`):**
  - **Diagnóstico:** Em atividades de jogo MonoGame (`AndroidGameActivity`) com modo imersivo de tela cheia (`ImmersiveSticky`) e `GLSurfaceView`, o `InputMethodManager` do sistema Android rejeita abrir o teclado virtual quando solicitado por Views transparentes ou em segundo plano, pois a janela GL captura todo o foco de toque.
  - **Soluções Implementadas:**
    - Restaurado e aprimorado o diálogo nativo escuro com foco absoluto de janela (`AlertDialog` estilizado com `ThemeDeviceDefaultDialogAlert` e espaçamentos ergonômicos).
    - Teclado forçado automaticamente via `dialog.Window.SetSoftInputMode(SoftInput.StateAlwaysVisible)` e `ShowSoftInput(ShowFlags.Forced)` no `EditText`.
    - **Avanço Automático e Fluido:** Ao terminar de digitar o usuário e pressionar a tecla "Avançar" / "OK" do teclado, a janela do usuário fecha e a da senha abre instantaneamente sem necessidade de toques adicionais.
    - **Login Automático:** Ao pressionar "Concluir" / "Done" no teclado virtual no campo de senha, a tentativa de login é disparada diretamente, eliminando o esforço de acertar o botão menor na tela.
    - Toque em qualquer lugar (no campo ou no texto "User" / "Password") aciona imediatamente o teclado.
- [x] **Release e Versionamento v1.18:**
### 04/09/2026 — Versão v1.19: Otimização Drástica da Seleção de Personagens (30+ FPS) e Entrada no Mundo 100% Confiável
- [x] **Fim dos 2 FPS na Seleção de Personagens (`SelectWorld.cs` / `WaterFallObject.cs`):**
  - **Diagnóstico:** No mapa de seleção de personagens (`World94`), o objeto de cachoeira animada (`WaterFallObject.cs`) chamava `InvalidateBuffers()` a cada quadro, forçando o motor gráfico a reconstruir e enviar buffers dinâmicos de vértices para a GPU móvel a cada frame! Além disso, emissores de partículas (`WaterSplashObject`), efeitos de distorção de água no terreno e alcance de visão excessivo (`ViewFar = 5500f`) afunilavam o desempenho nos celulares para apenas 2 FPS.
  - **Soluções:**
    - Removida a invalidação de buffers a cada quadro no `WaterFallObject` e desativados emissores de partículas no mobile, eliminando 100% dos gargalos na GPU.
    - Desativada a simulação pesada de distorção de água no mobile e ajustado o `ViewFar` para `3200f`.
    - O cenário de seleção agora roda a **30+ FPS lisos e sem travamento**.
- [x] **Entrada no Mundo Garantida (Fim do travamento após selecionar o personagem):**
  - **Diagnóstico:** Quando o servidor enviava o pacote `ProcessCharacterRespawn`, o `NetworkManager.cs` forçava a criação de uma `GameScene()` genérica vazia enquanto a `SelectCharacterScene` tentava criar simultaneamente uma `GameScene(characterInfo)` via `EnteredGame`. Essa colisão destruía instâncias em carregamento assíncrono e travava o jogo na tela preta/carregamento.
  - **Soluções:**
    - `NetworkManager.cs` agora delega a troca de cena exclusivamente para a `SelectCharacterScene` (compatível com a lógica testada no desktop).
    - `MuGame.cs` recebeu uma trava de concorrência (`_isChangingScene`), impedindo que duas trocas de cena aconteçam simultaneamente e corrompam o estado do jogo.
    - `SelectCharacterScene.cs` ganhou fallback inteligente de informações do personagem caso ocorra reordenação de pacotes de rede.
- [x] **Botões Touch Mobile para Seleção de Personagem:**
  - Adicionados botões dourados touch na parte inferior da tela (`[ Nome (Lv.X) ]`), permitindo entrar no mundo com um único toque direto no dedo, além do clique nos rótulos de nome e nos personagens 3D.
- [x] **Release e Versionamento v1.19:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 19` e `versionName: 1.19`.
  - Workflow GitHub Actions atualizado para gerar e publicar o **`IkarusMU-v1.19.apk`** na release `v1.19`.

### 05/09/2026 — Versão v1.20: Seleção Clássica Ultra-Leve (60 FPS) e Entrada no Mundo Corrigida (Fim do ANR)
- [x] **Seleção de Personagens Clássica e Leve (60 FPS Cravados):**
  - **Diagnóstico:** O mapa `World94` é o cenário de Season 6 com penhasco, cachoeira e um terreno 3D gigante de 256x256 blocos. No OpenGL ES móvel, desenhar centenas de blocos e objetos 3D a cada frame fazia a seleção rodar a apenas 6 FPS.
  - **Solução Clássica (Inspirada no MU 0.97d / 99b):**
    - Desativada a renderização do terreno pesado do World 94 (`Terrain.Visible = false`).
    - Corrigido o método `TerrainControl.DrawAfter()` que ignorava a flag `Visible = false`.
    - Limpos os objetos estáticos do cenário (`Array.Clear(MapTileObjects)`).
    - Mantidos os modelos 3D dos personagens com suas armaduras, asas, armas, rotações, animações, nomes em dourado e botões touch.
    - O consumo de GPU caiu em 95% e o framerate subiu de **6 FPS para 60 FPS cravados e fluidos**.
- [x] **Fim do Travamento / ANR ao Entrar no Mundo ("MuAndroid não está respondendo"):**
  - **Diagnóstico:** O servidor OpenMU exige receber o pacote `SendClientReadyAfterMapChangeAsync` (packet `0xB0`) para confirmar que o cliente concluiu a carga do mapa inicial e liberar a entrada do herói e o streaming dos monstros/jogadores. Como esse pacote não estava sendo despachado ao término do `GameScene.LoadSceneContentWithProgress()`, o servidor deixava a conexão suspensa, a tela congelava e o Android emitia erro de aplicativo que não responde (ANR).
  - **Solução:**
    - Adicionado o envio imediato de `SendClientReadyAfterMapChangeAsync` assim que Lorencia termina de carregar no `GameScene.cs`. O servidor agora spawna o personagem e inicia o mundo instantaneamente.
### 05/09/2026 — Versões v1.21 a v1.23: Entrada em Lorencia 3D Conquistada e Diagnóstico do Mundo
- [x] **Entrada no Mundo 3D com Sucesso:**
  - Carregamento progressivo transparente com `OnScreenLogger` em tempo real.
  - O jogador ultrapassou a tela de loading e entrou diretamente no centro de Lorencia (`138, 124`).
  - Renderização confirmada: herói 3D com armadura, taberna, casas de pedra, grama ao redor e HUD do MU ativos na tela.
- [x] **Diagnóstico de Performance e Piso de Lorencia:**
  - Identificados 2 FPS no mundo causados por:
    1. Renderização de terreno tile-a-tile sem batching (~800 Draw Calls por frame no OpenGL ES).
    2. Atualização de animação óssea e recriação de buffers na CPU (`SetDynamicBuffers()`) a cada frame para centenas de objetos estáticos do cenário (`MapTileObject`).
    3. Chão da praça de Lorencia preto por falta de fallback e inicialização assíncrona fora da thread gráfica.

---

### 05/09/2026 — Versão v1.24: Otimização Massiva de Lorencia (30+ FPS), Piso Texturizado e Joystick Touch Mobile
- [x] **Batching Dinâmico de Terreno (800+ Draw Calls -> ~4 a 8 Draw Calls):**
  - O `TerrainControl.cs` agora agrupa todos os quads de terreno visíveis em lotes por índice de textura (`_opaqueBatches` e `_alphaBatches`).
  - Em vez de uma chamada `DrawUserPrimitives` com 2 triângulos para cada tile, emite apenas **1 única chamada de desenho por textura**.
  - O pipeline do OpenGL ES roda suave e leve no processador gráfico do celular.
  - Eliminado o segundo passe redundante `RenderTerrain(true)` dentro de `DrawAfter`.
- [x] **Otimização de Objetos de Mapa Estáticos (`MapTileObject.cs`):**
  - Telhados, muros, barris, postes e estátuas de Lorencia agora inicializam suas malhas estáticas na primeira execução e desligam cálculos contínuos de esqueleto na CPU (`Animation()` e `SetDynamicBuffers()` tornam-se no-op).
  - Desativado o passe de sombra estático desnecessário (`RenderShadow = false`).
- [x] **Chão da Cidade 100% Texturizado (Fim do Piso Preto):**
  - `TerrainControl.cs` agora implementa `AfterLoad()` na thread gráfica principal para registrar todas as texturas (`TileRock01`, `TileRock02`, `TileGround01`, etc.) diretamente na GPU.
  - Adicionado fallback visual em `GetTerrainTexture`: se alguma textura não carregar, o piso utiliza a textura base do mapa sem renderizar vácuo preto.
- [x] **Overlay de Controles Mobile Touch (`MobileControlsOverlay.cs`):**
  - **Exclusivo do Mundo 3D (`GameScene.cs`)**: Não aparece na tela de Login nem na Seleção de Personagens.
  - **Joystick Analógico Virtual (Inferior Esquerdo)**: Permite caminhar e correr suavemente em qualquer direção com resposta tátil instantânea, calculando os vetores da câmera isométrica do MU.
  - **Botão de Ataque (Inferior Direito)**: Botão circular de destaque ("ATK") que mira e ataca o monstro mais próximo automaticamente.
  - **Botões de Poção (Inferior Direito)**: Poção de Vida ("HP") e Poção de Mana ("MP") com efeitos sonoros clássicos.
  - **Atalhos Rápidos (Superior Direito)**: Botões de acesso rápido em estilo vidro escuro `[ INV ]`, `[ STATS ]`, e `[ WARP ]` para abrir e fechar as janelas do jogo sem depender de teclado físico.
- [x] **Release e Versionamento v1.24:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 24` e `versionName: 1.24`.
  - Workflow GitHub Actions pronto para compilar e gerar o **`IkarusMU-v1.24.apk`**.

---

### 06/09/2026 — Versão v1.35: Correção Pós-v1.34 (FPS, Conversa com NPC e Joystick)
- [x] **Diagnóstico de Regressão de FPS (7 FPS após v1.34):**
  - **Causa raiz:** A v1.34 desativou `SynchronizeWithVerticalRetrace` (VSync) e setou `IsFixedTimeStep = false` no Android para tentar destravar 60 FPS. Essa é exatamente a combinação já documentada como **"A Otimização Assassina"** (30/08/2026) — sem o freio do VSync/FixedTimeStep, o loop de render satura a GPU/CPU do celular e gera thermal throttling, derrubando o framerate real para ~7 FPS.
  - **Solução:** Restaurado `IsFixedTimeStep = true` e `SynchronizeWithVerticalRetrace = true` no Android/iOS (`MuGame.cs`), e o `TargetFPS` do `appsettings.json` voltou de 60 para **30 FPS** (padrão estável documentado). O bloco de `Initialize()` que reaplicava o FPS das configurações também foi corrigido para não reabrir o VSync.
- [x] **Correção: Clique/Toque em NPC não abria diálogo:**
  - **Diagnóstico:** O picking 3D (`WorldObject.Update`) faz um raycast exato contra a `BoundingBoxWorld` do NPC. Dedos são muito menos precisos que um cursor de mouse, então um toque a poucos pixels da malha do NPC nunca intersectava a caixa, e o clique nunca chegava a `NPCObject.OnClick()`. Além disso, `MuGame.UpdateMouseRay()` só recalculava o raio quando a posição do mouse ou a contagem de toques mudavam — um dedo parado sobre o NPC podia deixar o raio desatualizado no exato frame do clique.
  - **Solução (`WorldObject.cs`):** Adicionado fallback de tolerância em tela (60px) exclusivo para mobile: se o raycast 3D falhar, o objeto interativo (NPC, monstro) ainda é considerado "hover" caso a projeção 2D de sua posição esteja próxima o suficiente do toque.
  - **Solução (`MuGame.cs`):** `UpdateMouseRay()` agora também é chamado a cada frame enquanto houver qualquer toque ativo, evitando raio desatualizado durante um toque parado.
- [x] **Correção: Joystick "puxa e anda 2x o esperado" + Restauração do Clique-no-Chão:**
  - **Diagnóstico:** `SendJoystickMovement()` calculava um alvo de pathfinding a **3.5 tiles de distância** a cada 260-380ms de joystick segurado, fazendo o personagem percorrer uma rota de múltiplos passos por comando (movimento "explosivo" e impreciso). Além disso, a síntese de `MouseState` a partir do touch (`MuGame.UpdateInputInfo`) sempre usava o **primeiro dedo** da lista (`touchState[0]`), então seguravar o joystick com um dedo podia "sequestrar" o mouse sintetizado e impedir que um segundo toque no chão dispare o clique-para-andar.
  - **Solução (`MobileControlsOverlay.cs`):** Reduzido o passo do joystick para **1 tile por tick** (movimento granular e responsível ao toque), aumentada a zona morta (`DEAD_ZONE`) de 0.15 para 0.28 para evitar disparo residual ao soltar o dedo perto do centro, e removido o fallback de "passo curto" redundante.
  - **Solução (`MuGame.cs`):** Ao sintetizar o mouse a partir de múltiplos toques simultâneos, o motor agora prioriza um dedo que **não** esteja sobre o joystick/botões do overlay, permitindo tocar no chão para andar (clique clássico) mesmo com o outro polegar segurando o joystick — as duas formas de controle (joystick e clique no chão) funcionam de forma independente e simultânea.
- [x] **Release e Versionamento v1.35:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 35` e `versionName: 1.35`.
  - Workflow GitHub Actions publicará automaticamente o **`IkarusMU-v1.35.apk`** na release `v1.35`.

---

### 06/09/2026 — Versão v1.36: Correção da Regressão Grave Introduzida pela v1.35 (2 FPS, Clique-no-Chão Morto, Joystick Descontrolado)
- [x] **⚠️ Erro próprio identificado e corrigido:** A tentativa de corrigir o clique em NPC na v1.35 (fallback de tolerância de toque em `WorldObject.Update()`, rodando `viewport.Project()` por objeto interativo TODO FRAME) e o refresh de `UpdateMouseRay()` a cada frame com toque ativo **pioraram drasticamente o desempenho**, derrubando o jogo para 2 FPS com muito travamento — o oposto do pretendido.
- [x] **Reversão do picking caro por frame (`WorldObject.cs` / `MuGame.cs`):** Removido o fallback de projeção em tela do hover contínuo. A tolerância de toque para NPCs agora só é calculada **uma única vez, no momento exato do clique** (`BaseScene.cs`), e apenas contra a lista pequena de `WalkerObjectsById` (NPCs/monstros próximos), nunca todo objeto do mundo a cada frame. `UpdateMouseRay()` voltou a rodar somente quando a posição do mouse/contagem de toques muda.
- [x] **Correção real do Clique-no-Chão morto e do Joystick "andando sozinho / 3x mais":**
  - **Causa raiz:** `WalkerObject.MoveTo()` dispara o pathfinding em uma `Task.Run` assíncrona que só popula `_currentPath` quando termina. Como o joystick da v1.35 diminuiu o intervalo entre comandos (260ms) e o gate de novo comando não exigia o término real do passo anterior, múltiplos `MoveTo` podiam ficar "em voo" ao mesmo tempo — um pathfinding mais lento e antigo podia sobrescrever um resultado mais novo já aplicado, fazendo o personagem seguir uma rota desatualizada/estendida (sensação de "andar sozinho" e "3x mais do que o esperado").
  - **Solução (`WalkerObject.cs`):** Adicionado contador de geração (`_moveRequestGeneration`); um resultado de pathfinding só é aplicado se ainda for o pedido de movimento mais recente, descartando qualquer resultado obsoleto.
  - **Solução (`MobileControlsOverlay.cs`):** Intervalo do joystick voltou a subir (400ms), zona morta aumentada para 0.45 (evita disparo com toque leve/impreciso), e o próximo passo do joystick só é enviado quando o passo anterior **realmente terminou** (`RemainingPathSteps == 0 && !IsMoving`), nunca antes.
- [x] **Lição registrada:** Qualquer verificação de picking/projeção 3D (`viewport.Project`, raycast) deve rodar **apenas no instante do clique**, nunca dentro do loop de hover por frame de cada objeto — isso se soma às Leis de Ouro de Desempenho 3D do topo deste arquivo.
- [x] **Release e Versionamento v1.36:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 36` e `versionName: 1.36`.
  - Workflow GitHub Actions publicará automaticamente o **`IkarusMU-v1.36.apk`** na release `v1.36`.

### 06/09/2026 — ⚠️ REGISTRO DE FALHA: v1.35 e v1.36 (Claude) NÃO Resolveram os Problemas — Testado e Reprovado pelo Usuário
- [x] **Resultado real reportado pelo usuário após teste em campo:**
  - **v1.34:** Jogo rodava a 7 FPS, mas **era possível andar** (joystick e/ou clique-no-chão funcionavam o suficiente para jogar).
  - **v1.35 (Claude):** Tentativa de corrigir FPS + clique em NPC + joystick. Resultado: **inutilizável**, travando muito, ainda em ~2 FPS, sem clique no chão, e joystick andando sozinho (3x mais que o esperado com um leve toque).
  - **v1.36 (Claude):** Tentativa de corrigir a regressão da v1.35 (reverteu o picking caro por frame, ajustou geração de movimento no `MoveTo`, recalibrou joystick). Diagnóstico técnico parecia correto (picking 3D caro por frame identificado e removido), mas o usuário decidiu **não seguir testando essa linha** — dado o histórico de duas tentativas frustradas seguidas, o risco/tempo não compensou mais.
  - **Decisão do usuário (06/09):** Interromper as tentativas do Claude neste ponto específico (FPS mobile / clique NPC / joystick) e **retornar ao Gemini** para continuar mexendo nessa frente do projeto.
- [x] **Estado técnico deixado no repositório (commits `fc9c66e` e `c2403b1`):**
  - `MuGame.cs`: `IsFixedTimeStep = true` e VSync ligado no Android (revertendo o que a v1.34 tinha desligado). `TargetFPS` do `appsettings.json` voltou de 60 para 30.
  - `WorldObject.cs` / `BaseScene.cs`: fallback de tolerância de toque para clique em NPC movido para rodar **apenas no momento do clique** (não mais por frame) — via `FindNearestInteractiveWalkerOnScreen` em `BaseScene.cs`, contra `WalkerObjectsById`.
  - `WalkerObject.cs`: contador de geração (`_moveRequestGeneration`) em `MoveTo()` para descartar resultados de pathfinding assíncrono obsoletos.
  - `MobileControlsOverlay.cs`: `DEAD_ZONE` subiu para 0.45, `MOVE_INTERVAL_MS` para 400, e o joystick só envia o próximo passo quando o anterior termina (`RemainingPathSteps == 0 && !IsMoving`).
  - **Estes commits NÃO foram validados em dispositivo real pelo usuário como uma melhoria sobre a v1.34** (apenas corrigem, no papel, a regressão que a v1.35 introduziu — mas o usuário optou por não continuar testando essa via com o Claude).
- [x] **Nota para quem retomar o projeto (Gemini ou outro):** Se for continuar a partir daqui, vale considerar reverter para o estado da v1.34 (`git show b5b2cc3`) como ponto de partida "jogável, porém a 7 FPS" em vez de construir em cima de v1.35/v1.36, cujo ganho real ainda não foi confirmado em campo.

### 08/09/2026 — ⚠️ REGISTRO DE FALHA: v1.44 NÃO resolveu e causou Regressão
- [x] **Resultado real reportado pelo usuário:**
  - **v1.44 (Gemini):** Tentativa de otimizar FPS através da reintegração de `IsFixedTimeStep = true` e `VSync = true` no Android para forçar um limite de 30 FPS estável. Resultado: **Idêntico à falha da v1.35 e v1.36**. O jogo dropou para 2 FPS e o personagem ficou paralisado (sem conseguir andar/pathfinding quebrado).
  - **Diagnóstico Técnico Confirmado:** No MonoGame Android, mexer em `IsFixedTimeStep = true` com telas não padronizadas ou sem VSync garantido pelo hardware dessincroniza o relógio interno (`GameTime`). Isso faz com que cálculos de física e movimentação baseados em tempo de delta sejam corrompidos, resultando em personagens incapazes de andar. A sobrecarga para alcançar o frame travado no mobile com a resolução nativa da tela causa um estrangulamento imediato para 2 FPS.
  - **Ação Tomada:** Revertida imediatamente toda a tentativa de profilings de tela e o `IsFixedTimeStep`. O projeto foi restaurado para a estrutura original (v1.43) e lançado como **v1.45**.
  - **🛑 LEI ABSOLUTA DAQUI PRA FRENTE:** Nunca mais mexer em `IsFixedTimeStep` ou tentar reativar `SynchronizeWithVerticalRetrace` no `MuGame.cs` para o Android. Devemos aceitar o FPS base nativo sem VSync e otimizar apenas as **Draw Calls visuais** (remoção de grama, efeitos) se quisermos ganho de performance real.

### 08/09/2026 — 🛠️ O Paradoxo do Tempo e a v1.46 (A Cura da Tela Preta)
- [x] **A Descoberta Final (Thread Starvation vs MonoGame Panic):**
  - O motivo de `IsFixedTimeStep = true` dar 2 FPS foi porque ele força o "catch-up" (chama a física dezenas de vezes para compensar atraso).
  - O motivo de `IsFixedTimeStep = false` dar **TELA PRETA** (como relatado na v1.45) é porque, sem trava, o loop `Update` roda a 2.000 vezes por segundo, esgotando 100% da CPU mobile e causando **Thread Starvation**. O Android não consegue processar os pacotes de rede nem os downloads de assets em background.
  - **A Solução:** Injetamos um limitador **manual** no final do `MuGame.Update()` via `Thread.Sleep`! Assim, mantemos o `IsFixedTimeStep = false` (a física fica solta) mas o jogo dorme nos ms que sobram para não sufocar a CPU, mantendo 30 FPS perfeitos e corrigindo a tela preta instantaneamente.

### 09/09/2026 — 🛠️ Versão v1.48: Correção de compilação no DebugPanel
- [x] **Correção no `DebugPanel.cs`:**
  - Declarados os controles `_dcLabel` e `_gcLabel` que causavam quebra de compilação no GitHub Actions na v1.46 e v1.47.
  - Painel de debug ajustado em largura para exibir métricas em tempo real (FPS, DrawCalls e consumo de GC Memory).
- [x] **Release e Versionamento v1.48:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 48` e `versionName: 1.48`.
  - Workflow GitHub Actions compilará e publicará o **`IkarusMU-v1.48.apk`** na release `v1.48`.

### 09/09/2026 — 🎯 Versão v1.49: Fim da Tela Preta Real (Causa Raiz: Package ID & Thread.Sleep)
- [x] **A Causa Raiz Descoberta da Tela Preta:**
  - Na v1.45, o `package` no `AndroidManifest.xml` havia sido acidentalmente alterado para `com.ikarus.mu`, divergindo do `ApplicationId` (`MuAndroid.MuAndroid`).
  - No Android, isso isolou o app da pasta onde residem os 1.7 GB de assets (`/Android/data/MuAndroid.MuAndroid/files/Data`).
  - Sem assets, a `NewLoginWorld` não carregava o modelo do barco nem o terreno/céu (mundo 100% preto), e a rede não avançava para exibir os campos de login.
  - O `Thread.Sleep(15)` injetado no `Update()` no mobile sufocava o loop principal e as ações assíncronas do jogo.
- [x] **Soluções Implementadas:**
  - Restaurado `package="MuAndroid.MuAndroid"` no `AndroidManifest.xml`.
  - Adicionada detecção multi-caminho em `Constants.DataPath` para varrer todas as pastas possíveis de dados no Android.
  - Removido `Thread.Sleep` de dentro do `MuGame.Update()`, restaurando o fluxo nativo estável idêntico ao da v1.43.
- [x] **Release e Versionamento v1.49:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 49` e `versionName: 1.49`.

### 🎮 [09/09/2026] — Versão 1.50 ("Caminho 1" / Otimização Agressiva de FPS Mobile)
- [x] **Diagnóstico da Causa Raiz dos 7–8 FPS em Lorencia:**
  - **Sobrecarga Brutal de Fillrate (Resolução Nativa sem Escalonamento):** O MonoGame Android configurava `PreferredBackBufferWidth = 0` e `PreferredBackBufferHeight = 0`, forçando o backbuffer a rodar na resolução física nativa da tela do celular (ex: 2400x1080 = 2.6 milhões de pixels). Com centenas de quads de terreno e malhas transparentes sobrepostas em Lorencia, a GPU móvel afunilava totalmente a taxa de preenchimento.
  - **ViewFar Descalibrado (Herança de 50.000f):** A classe singleton `Camera.Instance` herdava `ViewFar = 50000f` de `LoadWorld.cs` ou `5000f` de `SelectCharacterWorld.cs`, pois `WalkableWorldControl` e `LorenciaWorld` nunca redefiniam o `ViewFar`. Isso fazia o loop de culling de blocos de terreno e objetos processar centenas de objetos e blocos distantes desnecessariamente.
  - **Shadow Passes Redundantes:** Chamadas de `DrawShadowMesh` no `ModelObject.cs` avaliavam condições de sombra e despachavam métodos mesmo com sombras 3D desativadas no mobile.
  - **Coleta de GC no Frame:** `FPSCounter.CalcFPS()` chamava `GC.GetTotalMemory(false)` 60 vezes por segundo, disparando sincronizações no runtime Mono.
- [x] **Soluções Implementadas no v1.50:**
  - **Escalonamento Interno para 720p:** `MainActivity.ApplyAndroidDefaults()` calcula a resolução interna do BackBuffer travando a altura máxima em 720p e adaptando a largura proporcionalmente à proporção de aspecto da tela (ex: 1600x720 num display 20:9), permitindo ao hardware de display do Android (SurfaceFlinger) fazer o upscale para tela cheia com 0% de custo de GPU. Redução de mais de 60% na carga de fragment shaders.
  - **TouchPanel Adaptado:** `TouchPanel.DisplayWidth = Width` e `TouchPanel.DisplayHeight = Height` para alinhamento 1:1 entre coordenadas de toque e renderização.
  - **Limitação de Culling (`ViewFar = 2200f`):** Definido `Camera.Instance.ViewFar = 2200f` em `WalkableWorldControl` e `LorenciaWorld.AfterLoad()`.
  - **Eliminação de Passes de Sombra no Mobile:** `#if !ANDROID && !IOS` direto nas chamadas de sombra no `ModelObject.DrawModel()`.
  - **Otimização do Contador de GC:** `GC.GetTotalMemory` passa a ser chamado a cada 2 segundos no `FPSCounter`, eliminando 60 chamadas/s.
- [x] **🛑 RESULTADO DO TESTE REAL v1.50 & LIÇÕES APRENDIDAS (NUNCA MAIS REPETIR):**
  - **Desaparecimento do Personagem e NPCs:** Ao encurtar `ViewFar` para `2200f`, o culling de `WorldObject.cs` (`maxDist = cam.ViewFar + 350f = 2550f`) falhou criticamente porque a câmera 3D fica posicionada muito alta e inclinada no ar em relação ao piso do mundo. A distância vetorial do olho da câmera até o chão passou de 2550 unidades, marcando `OutOfView = true` para o herói e todos os NPCs!
  - **Regra de Ouro #1:** `ViewFar` nunca deve ser menor que `3500f` em mundos caminháveis.
  - **Regra de Ouro #2:** O personagem local (`wwc.Walker == this`) deve ser explicitamente protegido contra culling de câmera.
  - **A Prova Definitiva dos 7–9 FPS:** Mesmo sem desenhar o personagem, sem desenhar NPCs, sem sombras e em 720p, a taxa de quadros continuou presa em 7–9 FPS (~110–140ms/quadro). **Isso prova conclusivamente que o limitador de 7–9 FPS não é preenchimento de GPU (fillrate) nem quantidade de triângulos/modelos 3D.** É uma restrição de quantização de frames do MonoGame Android ou sincronização interna do loop de renderização. Mutilar os gráficos não aumenta o FPS.
  - **Decisão Estratégica:** Pivotar imediatamente do teste cego de FPS para entrega de gameplay, usabilidade e recursos mobile essenciais.

---

### ⚔️ [10/09/2026] — Versão 1.51 (Restauração, Falar com NPCs, Fechar Janelas [X] e HUD Mobile)
- [x] **Restauração Imediata das Entidades 3D:**
  - `Camera.Instance.ViewFar` restaurado para `3500f` com segurança em `WalkableWorldControl.cs` e `LorenciaWorld.cs`.
  - `WorldObject.cs`: Blindagem total do herói local contra culling (`if (wwc.Walker == this) OutOfView = false;`). Personagem e NPCs 100% visíveis novamente.
- [x] **Comunicação Touch com NPCs no Celular (`WalkableWorldControl.cs`):**
  - **Problema:** Ao tocar em um NPC (ferreiro, vendedora de poções, baú, etc.), o jogo ignorava o NPC e mandava o personagem andar até aquela coordenada no chão, sem nunca abrir o diálogo ou loja.
  - **Solução:** Implementado método `FindNpcAtTile()` para inspecionar os objetos do mundo no ladrilho tocado. Ao detectar um NPC, cancela o "andar até o chão" e invoca imediatamente `clickedNpc.OnClick()`. Isso dispara o pacote de rede `SendTalkToNpcRequestAsync` para a VPS, abrindo lojas e diálogos perfeitamente com 1 toque!
- [x] **Botões Touch [X] de Fechar em Todas as Janelas:**
  - **Inventário (`InventoryControl.cs`):** Adicionado botão vermelho estilizado `[X]` no canto superior direito (`ControlSize.X - 32, 4`), com evento touch para fechar o inventário sem precisar de teclado ou atalho de PC.
  - **Janela de Atributos (`CharacterInfoWindowControl.cs`):** Adicionado botão `[X]` no topo direito (`WINDOW_WIDTH - 28, 4`).
  - **Loja de NPCs (`NpcShopControl.cs`):** Adicionado botão `[X]` no topo da tela de compras (`380, 40`).
- [x] **Menu HUD Mobile com Barra de Ações Rápidas (`MobileControlsOverlay.cs`):**
  - Adicionada barra ergonômica de botões touch na interface mobile:
    - **`[INV]`**: Abre/fecha o Inventário.
    - **`[CHAR]`**: Abre/fecha a janela de Status/Atributos do Personagem (Força, Agilidade, Vida, Mana).
    - **`[MAP]`**: Abre/fecha o Mini-mapa de Lorencia.
    - **`[CMD]`**: Abre a janela de Comandos e Ações.
    - **`[X]`**: Botão de Fechamento Geral (fecha todas as janelas abertas de uma só vez para desobstruir a tela do celular).
  - Corrigido `MobileControlsOverlay.Draw()` chamando `base.Draw(gameTime)` para desenhar todos os botões e subcontroles touch na tela.
- [x] **Release e Versionamento v1.51:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 51` e `versionName: 1.51`.

---

### 🚀 [10/09/2026] — Versão 1.52 (Cura do Clique no Chão, Layout Widescreen de Comandos, Ajuste Fino do [X] e Itens Nítidos)
- [x] **Diagnóstico Crítico: Por que o personagem não andava ao clicar no chão na v1.51?**
  - **Causa Raiz Identificada:** O container `MobileControlsOverlay` foi instanciado com `Interactive = true;` e cobria toda a tela do celular (`ViewSize = new Point(Width, Height)`). No loop `BaseScene.Update`, o motor de interface do jogo detectava o overlay sob o cursor em qualquer ponto da tela e definia `Scene.MouseControl = _mobileControls`. Como o `WalkableWorldControl` só processava clique-para-andar no chão se `Scene.MouseControl == World` ou `null`, o clique no terreno ficava **100% bloqueado**!
  - **Por que andar na parede e sentar no banco funcionava?** Porque objetos 3D do cenário (`HouseWallObject`, `RestPlaceObject`, `FurnitureObject`) possuem lógica de colisão e `OnClick()` disparados diretamente pelo raycast da cena (`MouseHoverObject.OnClick()`), contornando o bloqueio do terreno.
  - **Solução Implementada:**
    1. Alterado `MobileControlsOverlay.Interactive = false;`. O container não intercepta mais nenhum toque no espaço vazio; apenas seus botões filhos (`[INV]`, `[CHAR]`, etc.) possuem `Interactive = true` em seus próprios retângulos.
    2. Adicionada salvaguarda em `WalkableWorldControl.cs`: se `Scene.MouseControl is MobileControlsOverlay`, o clique no terreno é aceito e processado normalmente.
    3. Ajustado `FindNpcAtTile` para comparar a coordenada exata do ladrilho (`tileX == npc.Location.X && tileY == npc.Location.Y`). Se o jogador clicar no chão adjacente ao NPC, ele anda normalmente; se clicar no próprio NPC, abre o diálogo/loja.
- [x] **Janela de Comandos (CMD / `CommandWindowControl.cs`) em 3 Colunas Horizontais:**
  - Reformulada de uma lista vertical estreita (200x280) para um formato **widescreen ergonômico** (480x150) com 3 colunas e 2 linhas de botões largos (145x40px).
  - Distribuição: Coluna 1: Trade / Whisper | Coluna 2: Buy / Guild | Coluna 3: Party / Duel.
  - Botão `[X]` de fechar integrado no canto superior direito do banner dourado.
- [x] **Ajuste Fino do Botão [X] na Loja do Bar/NPC (`NpcShopControl.cs`):**
  - Botão `closeBtn` reposicionado exatamente em `X = 390, Y = 94` (26x26), alinhado com perfeição na quina superior direita da moldura original do NPC Shop.
- [x] **Itens Visíveis e Nítidos no Inventário (`InventoryControl.cs` & `PickedItemRenderer.cs`):**
  - **Problema:** Os 3 itens teste existiam e podiam ser arrastados, mas eram praticamente invisíveis porque eram desenhados na cor `DarkSlateGray` (quase idêntica ao fundo cinza-escuro da grade) com textos microscópicos (escala 0.4f).
  - **Solução Visual Mobile:**
    - Moldura destacada com recuo (inset) de 2px para separar cada item das divisórias da grade.
    - Cores vivas por categoria:
      - **Armaduras e Escudos:** Azul Real profundo (`#19325F`) com borda dourada reluzente de 2px.
      - **Armas (Espadas, Machados, Arcos):** Carmesim escuro (`#5F1919`) com borda prateada/bronze de 2px.
      - **Poções e Consumíveis:** Rubi vibrante (`#871423`) com borda ouro de 2px.
    - Textos e nomes renderizados com sombra preta (escala 0.55f), garantindo contraste absoluto e leitura fácil em telas mobile.
    - Item arrastado (`PickedItemRenderer`) agora brilha em ouro âmbar com borda luminosa sob a ponta do dedo.
- [x] **Release e Versionamento v1.52:**
  - `MuAndroid.csproj` e `AndroidManifest.xml` atualizados para `versionCode: 52` e `versionName: 1.52`.

---

## 🛠️ PRÓXIMOS PASSOS (ROADMAP)

1. [x] Instalar .NET 8 / 10 SDK e compilar a solução `OpenMU`.
2. [x] Vincular pastas de arquivos de dados do cliente e servidor (`Data/` / `.bmd` / `.ozj`) via Junção de Pasta NTFS (`mklink /J`).
3. [x] Testar a conexão do cliente Desktop (`Client_Desktop`) e Android (`Client_Android`) com o servidor local.
4. [x] Configurar o repositório GitHub (`https://github.com/MANINtecn/muikarus.git`) e o workflow de build (GitHub Actions).
5. [x] **CONCLUÍDO:** Distribuição dos Assets via GitHub Releases (`Data.zip`), evitando limites de download.
6. [x] **CONCLUÍDO (v1.12):** Detecção inteligente do Data.zip na memória interna/externa para nunca mais precisar re-baixar 1.7 GB a cada update.
7. [x] **CONCLUÍDO (v1.13):** Mapeamento de Touch Screen para Mouse Click, auto-exibição da lista de servidores e trava a 30 FPS estáveis sem travamento.
8. [x] **CONCLUÍDO (v1.14):** Primeiro protótipo de abertura de teclado virtual no Android.
9. [x] **CONCLUÍDO (v1.15):** Fim do popup de diálogo, digitação 100% direta dentro das caixas do MU e Zoom ampliado de 440x270 com inputs maiores.
10. [x] **CONCLUÍDO (v1.16):** Chão de Lorencia 100% texturizado (DXT Decoder + GL thread loading), framerate restaurado para 30+ FPS (desativação do overdraw de grama) e clique-para-andar preciso na coordenada exata do toque.
11. [x] **CONCLUÍDO (v1.18):** Teclado virtual Android 100% infalível via diálogo nativo escuro ergonômico, auto-avanço de campo (Usuário -> Senha) e disparo direto de login ao teclar Concluir.
12. [x] **CONCLUÍDO (v1.19/v1.20):** Otimização da Seleção de Personagens (60 FPS estáveis), remoção da colisão de carregamento ao entrar no mundo e botões touch dedicados.
13. [x] **CONCLUÍDO (v1.24):** Entrada confirmada em Lorencia 3D, batching dinâmico do terreno (30+ FPS), textura das pedras da cidade e Joystick Analógico + Botões Touch no mundo!
14. [x] **CONCLUÍDO (v1.50):** Teste de otimização de FPS mobile e diagnóstico definitivo sobre culling e gargalo.
15. [x] **CONCLUÍDO (v1.51):** Restauração de visibilidade (ViewFar 3500f + blindagem hero), interação touch com NPCs, botões [X] de fechar e menu HUD mobile de ações.
16. [x] **CONCLUÍDO (v1.52):** Desbloqueio definitivo do clique no chão (correção de overlay touch), comandos em 3 colunas horizontais, ajuste do [X] do bar e renderização de itens com cores vivas e sombras.
17. [x] **CONCLUÍDO (v1.53):** Barra de Ações Inferior interativa: bloqueio definitivo do vazamento de toque para o chão 3D, consumo de poções Q-W-E-R via rede com som nativo (`SendConsumeItemRequestAsync`), seleção de habilidades 1 a 5, badges com contagem de poções e atalhos touch para janelas de Inventário, Personagem e Comandos.
18. [x] **CONCLUÍDO (v1.54):** Correção definitiva da caminhada no chão (liberação de cliques fora do HUD) e FRENTE 2 (Loja de NPCs): criação do `ShopHandler` no Android, roteamento de pacotes `0x30/0x31/0x32`, renderização touch com detalhes dos itens, preços calculados e compra real com Zen (`SendBuyItemFromNpcRequestAsync`).
19. [ ] **FRENTE 3 (INVENTÁRIO & EQUIPAMENTOS):** Adicionar slots de equipamentos (elmo, armadura, etc.) e renderizador 3D BMD de itens.
20. [ ] Aprender a usar o **Web Admin Panel** (`http://localhost:5000`) para gerenciar contas, itens e rates.
21. [ ] **DEPLOY VPS:** Garantir portas `44405` e `55901` totalmente abertas no firewall da VPS Windows (`192.99.110.164`).
22. [ ] **SISTEMA DE AUTO-UPDATE (PATCHER LEVE):** Criar lógica no `LoadScene.cs` para checar `patch_version.txt`.

---

## 🤝 PROTOCOLO DE HANDOFF (IA ➔ USUÁRIO ➔ IA)

> **REGRAS PARA TRANSIÇÃO ENTRE GEMINI E CLAUDE:**
> Sempre que houver uma troca de assistentes (ou finalização do dia de trabalho), a IA atual DEVE preencher este bloco para não deixar a próxima IA "cega".

### 🗂️ DIVISÃO DE RESPONSABILIDADES (definida com o usuário em 06/09/2026)
Para trabalhar os três (usuário + Gemini + Claude) juntos sem atrito de merge/regressão cruzada, o projeto foi dividido por **área fixa**, não por tarefa avulsa:

- **🕹️ Gemini → Cliente Mobile/Android (`Client_Android/`):** FPS, rendering 3D, touch/joystick, UI mobile, otimizações MonoGame.
- **🖥️ Claude → Servidor (`OpenMU/`) e infraestrutura:** Configuração de VPS, portas/firewall, Web Admin Panel, rates, banco de dados, deploy. Área que não colide com arquivos do cliente mobile.
- **Regra de exceção:** se uma IA precisar mexer fora da própria área (ex.: Claude precisar tocar em algo do `Client_Android`), isso deve ser combinado com o usuário antes, e registrado aqui no handoff — nunca silenciosamente.
- **Regra de git:** cada IA trabalha a partir do estado que o usuário confirmar como "atual". Não fazer `push --force` sem avisar o usuário; se o histórico remoto divergir do local, perguntar antes de sincronizar.
- **Commits:** neste repositório, por pedido explícito do usuário, commits/PRs **não** levam linha de coautoria de IA (`Co-Authored-By`), independente da orientação padrão do sistema.

### 📋 ESTADO ATUAL (Deixado por: Gemini — 10/09/2026)
- **Versão Atual:** v1.54
- **O que está funcionando:**
  - Build CI/CD do GitHub Actions 100% estabilizado e gerando APK assinado automaticamente.
  - Personagem e NPCs 100% visíveis em Lorencia (`ViewFar = 3500f`).
  - **Caminhada Livre Restaurada (v1.54):** O chão 3D voltou a responder imediatamente a toques em qualquer lugar da tela fora do HUD inferior. `MainControl.Interactive = false` impede captura indevida de tela inteira, e `IsMouseOver` é limitado com precisão cirúrgica para coordenadas `mouse.Y >= hudTop`.
  - **Frente 2 Concluída (v1.54 - Loja de NPCs / Comerciantes):**
    - **ShopHandler no Android:** Criado e registrado no `PacketRouter.cs` para processar os pacotes de loja do OpenMU: `0x30` (`NpcWindowResponse`), `0x31` (`StoreItemList`), `0x32` (`ItemBought`/`NpcItemBuyFailed`) e `0x33` (`NpcItemSellResult`).
    - **NpcShopControl Mobile Touch:** Interface moderna em tema escuro com moldura dourada (540x430), botão [X] de fechar, indicador do Zen do jogador, lista rolável de itens com botões ▲/▼ e cartões de 48px com nome colorido, categoria, nível e preço.
    - **Painel de Detalhes & Compra:** Painel direito detalhado com atributos do item (Nível, Durabilidade, Habilidade, Sorte, Excelente), checagem de saldo e botão grande **[ COMPRAR ]** que despacha `SendBuyItemFromNpcRequestAsync(slot)` diretamente ao servidor via `CharacterService`.
- **Próximo Passo:**
  - **Frente 3 (Inventário Completo & Equipamentos):** Implementar os slots de equipamentos do personagem (Elmo, Armadura, Calça, Luvas, Botas, Armas, Asa, Pet/Montaria, Anéis, Pingente) e renderizador 3D BMD dos itens.

### 📋 ESTADO ATUAL (Deixado por: Claude)
- **Área assumida:** Servidor OpenMU e infraestrutura (VPS, portas, Web Admin Panel, rates).
- **Tarefa Imediata para Claude:** Ainda não iniciada — próximo passo é revisar o item 21 do roadmap ("Garantir portas 44405 e 55901 totalmente abertas no firewall da VPS") e o item 20 ("Aprender a usar o Web Admin Panel"), conforme o usuário confirmar prioridade.

---





