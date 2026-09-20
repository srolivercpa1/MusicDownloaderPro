# MusicDownloader Pro

Aplicativo Windows para baixar arquivos de áudio disponibilizados oficialmente por URLs HTTPS diretas, organizar uma biblioteca local, reproduzir músicas e converter arquivos próprios com FFmpeg.

> Use somente conteúdos cujo download e processamento tenham sido autorizados pela fonte ou pelo titular. O aplicativo não contorna DRM, paywalls, autenticação ou limitações técnicas.

## Recursos

- Downloads diretos de MP3, WAV, FLAC, M4A e OGG.
- Validação de URL, tipo de arquivo, DNS público, nome e pasta de destino.
- Biblioteca local SQLite, pesquisa, favoritos e player.
- Conversão local com uma instalação de FFmpeg escolhida pelo usuário.
- Interface escura em português, logs locais e configurações.
- Publicação autocontida para Windows x64 e instalador Inno Setup.

## Gerar o executável

```powershell
dotnet restore MusicDownloaderPro.sln
dotnet test MusicDownloaderPro.sln -c Release
dotnet publish src/MusicDownloaderPro/MusicDownloaderPro.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MusicDownloaderPro.iss
```

O GitHub Actions executa esses passos automaticamente. Baixe o artefato `MusicDownloaderPro-Windows-x64` na execução mais recente.

© 2026 Mateus Oliveira.
