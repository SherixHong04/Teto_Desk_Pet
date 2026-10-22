# Teto Desk Pet

[中文说明](README.md) · [Download](https://github.com/SherixHong04/Teto_Desk_Pet/releases/latest)

A Windows x64 Kasane Teto desktop companion with local ChatGPT/Codex and DeepSeek Harness chat, attachments, native task tools and animated interactions. No API-key setup is introduced.

Install and sign in to at least one supported client first: native Codex CLI with ChatGPT subscription login, or the official Electron DeepSeek Harness Desktop with DeepSeek Account login. A ChatGPT browser/desktop UI alone is not a usable CLI transport. Other community Harness shells are not guaranteed compatible.

Unzip the complete Windows release and run `TetoDesk.exe`. Windows 10/11 x64 and .NET Framework 4.8 are required. The app starts as a pet; open chat using the compose bubble, tray, or Ctrl+Alt+T.

The service menu lists only detected clients. With only Codex installed, ChatGPT is the only choice and account queries show 5-hour/weekly subscription limits. With only Harness, DeepSeek is the only choice and queries show wallet balance and the current pricing period. With both, the query follows the selected service. Custom portable paths can be selected manually; re-detect after changing an installation.

Data defaults to `%LOCALAPPDATA%\TetoDesk\Data`, tasks to `%LOCALAPPDATA%\TetoDesk\Tasks`. Add an empty `portable.flag` beside the EXE to use writable application-local storage instead. Distributions never contain the author's sessions, credentials, attachments or machine paths.

Build on Windows with `src/Build.ps1`; run isolated deployment checks using `src/Test.ps1 -Mode deployment`; create binary/source ZIPs and checksums with `tools/Package.ps1`. Python is optional for development image utilities/PDF extraction, not a runtime requirement. Speech uses Harness's optional local transcription plugin, not direct audio model input.

Code: [MIT](LICENSE). Artwork and third-party rights are separate: [ARTWORK.md](ARTWORK.md), [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). This is an unofficial fan project.
