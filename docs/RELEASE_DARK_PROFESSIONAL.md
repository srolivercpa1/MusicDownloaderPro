# MusicDownloader Pro 1.1.0 — Dark Professional

- Busca online com resultados em cards escuros, textos legíveis e ações por música.
- Capa quando disponível, com símbolo musical se a imagem não carregar.
- Seleção e foco visíveis, cabeçalhos escuros, campos e menus consistentes.
- Navegação lateral com seleção e rolagem; indicadores iniciais reorganizam conforme a largura.
- Cancelamento da busca separado de downloads e conversões.
- Mensagens para busca inicial, carregamento, resultados, ausência de resultados, falha e cancelamento.
- Inicialização da janela aguarda a preparação do banco SQLite.

## Instalação

Execute `installer/MusicDownloaderPro-Setup.exe` no pacote de distribuição. O instalador mantém o mesmo identificador do aplicativo e pode atualizar a instalação existente. Feche o programa antes de atualizar. Biblioteca, arquivos baixados e configurações ficam fora da pasta de instalação.

## Compilação

Em Windows, com .NET SDK 8 e Inno Setup 6:

```powershell
dotnet test MusicDownloaderPro.sln -c Release
dotnet publish src/MusicDownloaderPro/MusicDownloaderPro.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' installer/MusicDownloaderPro.iss
```

A distribuição contém o runtime .NET. O funcionamento da busca depende da disponibilidade da fonte. A atualização visual não amplia as fontes nem concede autorização para baixar conteúdo protegido.
