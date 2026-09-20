# MusicDownloader Pro — Busca online autorizada

## Objetivo

Adicionar ao MusicDownloader Pro uma busca online por músicas disponibilizadas oficialmente para download, acessível por uma lupa na tela inicial e por uma nova opção “Busca Online” no menu lateral. A pesquisa aceitará título, artista ou álbum, unificará resultados de providers sem chave de API e enviará o arquivo escolhido ao gerenciador de downloads existente.

O módulo não acessará fontes privadas, não contornará autenticação, DRM, paywalls ou limitações técnicas e não apresentará como baixável um arquivo cuja origem não ofereça download autorizado.

## Experiência do usuário

- A tela inicial receberá uma caixa “Buscar músicas na internet” com botão de lupa.
- O menu lateral receberá “Busca Online”.
- Pressionar Enter ou clicar na lupa abrirá a página e iniciará a busca.
- A tela mostrará campo de pesquisa, estado de carregamento, filtros e resultados em cards/lista.
- Cada resultado exibirá capa quando fornecida, título, artista, álbum, duração, formato, qualidade, fonte e licença.
- Cada resultado terá os comandos “Ouvir prévia”, “Baixar” e “Abrir na fonte” quando cada ação for fornecida oficialmente.
- O download reutilizará a fila, progresso, histórico, pasta configurada e importação automática na biblioteca.
- Resultados sem arquivo autorizado poderão ser abertos na fonte, mas não terão botão “Baixar”.
- Uma pesquisa nova cancelará a anterior e resultados antigos não substituirão os novos.
- Falhas de uma fonte não apagarão resultados válidos das demais; a interface apresentará uma mensagem parcial.

## Arquitetura

### Contratos

`IOnlineMusicProvider` terá:

- `Name`: nome público da fonte.
- `SearchAsync(string query, int limit, CancellationToken token)`: pesquisa paginada ou limitada.
- `GetDownloadOptionsAsync(string itemId, CancellationToken token)`: resolve arquivos oficialmente baixáveis.

`OnlineMusicResult` conterá identificador estável, provider, título, artista, álbum, duração, URL da capa, URL da página oficial, licença e opções de download. `AuthorizedDownloadOption` conterá URL HTTPS direta, formato, MIME, tamanho, qualidade/originalidade e nome sugerido.

`OnlineMusicSearchService` executará providers em paralelo, aplicará timeout individual, reunirá sucessos, removerá duplicatas conservadoramente e ordenará por relevância. Um provider com falha retornará diagnóstico separado, não uma exceção global, desde que ao menos outra fonte responda.

### Providers iniciais

1. `InternetArchiveMusicProvider`: usará APIs públicas de metadados e pesquisa do Internet Archive. Aceitará apenas itens com arquivos de áudio explicitamente listados para download. A página do item e a licença serão preservadas.
2. `DirectCatalogProvider`: aceitará catálogos Creative Commons configurados no aplicativo somente quando possuírem endpoint HTTPS público, licença identificável e URL direta de áudio. Nenhum catálogo arbitrário será rastreado ou interpretado como página HTML.

A primeira entrega funcionará sem chave de API. Providers que venham a exigir credenciais serão adicionados separadamente e permanecerão desativados sem configuração explícita.

## Regras de autorização e segurança

- Somente URLs HTTPS públicas aprovadas pelo validador existente.
- Revalidação após redirecionamentos e antes do download.
- Bloqueio de loopback, redes privadas, link-local, esquemas locais e credenciais na URL.
- Download disponível apenas quando o provider retornar arquivo de áudio oficial e licença/estado de acesso compatível.
- Allowlist de MP3, WAV, FLAC, M4A e OGG; HTML, playlists, executáveis e tipos desconhecidos serão rejeitados.
- Capas serão carregadas apenas por HTTPS, com limite de tamanho e fallback visual.
- Texto retornado por fontes será tratado como dados, nunca executado ou interpretado como marcação ativa.
- Nenhum arquivo será executado depois do download.
- Consultas terão tamanho máximo de 200 caracteres e resultados serão limitados inicialmente a 50 por busca.
- Timeout padrão de 15 segundos por provider e cancelamento imediato ao iniciar nova pesquisa.

## Busca, filtros e duplicatas

O usuário poderá filtrar por provider, formato e disponibilidade de download. A ordenação padrão será relevância, seguida por título. Resultados duplicados serão combinados apenas quando título normalizado, artista normalizado e duração aproximada coincidirem; opções de fontes diferentes continuarão visíveis no item combinado.

Campos ausentes aparecerão como “Não informado”. O aplicativo não inventará artista, álbum, licença, duração ou qualidade.

## Prévia e download

A prévia só aparecerá quando o provider fornecer uma URL oficial de streaming ou arquivo autorizado. Ela usará o player existente sem salvar automaticamente o conteúdo. O usuário poderá interromper a prévia e retornar à faixa local anterior.

Ao clicar em “Baixar”, o usuário escolherá entre as opções realmente oferecidas pela fonte. O arquivo será encaminhado ao serviço de download já existente, passará por nova validação e será salvo inicialmente com extensão `.part`. Após conclusão, será registrado no histórico e importado para a biblioteca.

## Persistência

Não será criado cache permanente de resultados nesta primeira versão. O histórico registrará provider, página oficial, URL direta sanitizada, destino, tamanho e status. Consultas não serão registradas, preservando privacidade. Preferências de filtros poderão ser salvas nas configurações locais.

## Estados e mensagens

- Estado inicial: orientação para buscar por música, artista ou álbum.
- Buscando: indicador de progresso e botão cancelar.
- Sem resultados: “Nenhuma música com download autorizado foi encontrada.”
- Falha total: mensagem de conexão sem remover a consulta digitada.
- Falha parcial: resultados válidos mais indicação das fontes indisponíveis.
- Resultado incompatível: “Esta fonte não disponibiliza um método de download autorizado.”

## Testes

- Pesquisa vazia e consulta acima do limite.
- Serialização e interpretação de respostas válidas do Internet Archive.
- Itens sem licença, sem áudio ou com tipos proibidos não oferecem download.
- URLs privadas, HTTP, redirecionamentos inseguros e HTML são recusados.
- Cancelamento de pesquisa antiga impede atualização tardia da tela.
- Timeout ou erro de um provider mantém resultados de outro.
- Deduplicação não combina artistas diferentes ou durações incompatíveis.
- Filtros por fonte, formato e disponibilidade.
- Resultado baixável entra na fila existente e chega à biblioteca após conclusão.
- Comandos de busca, Enter, lupa, prévia, download e abertura da fonte nos ViewModels.
- Build, testes e inicialização real do executável em runner Windows.

## Critérios de aceite

1. A lupa aparece na tela inicial e abre a Busca Online com a consulta informada.
2. A nova opção “Busca Online” aparece no menu lateral.
3. Uma pesquisa retorna músicas de pelo menos um provider sem chave quando a fonte estiver disponível.
4. Cada download oferecido corresponde a um arquivo oficial de áudio autorizado pela origem.
5. Resultados podem ser filtrados e baixados pela fila existente.
6. Downloads concluídos aparecem na biblioteca local.
7. Pesquisas não bloqueiam a interface e podem ser canceladas.
8. O teste de inicialização Windows passa sem exceções registradas.
9. O workflow produz um novo `MusicDownloaderPro.exe` e `MusicDownloaderPro-Setup.exe`.

## Fora do escopo

- Busca irrestrita por rastreamento de toda a web.
- Extração de áudio de páginas ou plataformas de streaming.
- Acesso a conteúdo privado, autenticado, pago ou protegido.
- Bypass de DRM, login, paywall, limites geográficos ou proteções técnicas.
- Download de fontes sem autorização do titular ou do serviço.
