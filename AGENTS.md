# Teto Desk public copy

- Work only within this repository copy. Do not edit or install over the author's other Teto Desk checkout or running app.
- Mouse interaction acceptance belongs to the human user. Do not synthesize input, open visible test windows, or kill/restart a running desktop app. Headless WPF checks and explicit native account-metadata checks are allowed; model-turn tests require an explicit reason and must use isolated fixtures.
- Support installations with ChatGPT/Codex only, DeepSeek Harness only, or both. Detect installed clients without reading credentials or silently choosing an unavailable service. ChatGPT uses native Codex CLI subscription login; do not introduce an API-key workflow.
- Never commit or package Data, Tasks, attachment snapshots, account state, executable paths from this computer, .local, test fixtures, or logs. Release packaging uses an allowlist and scans the resulting archives.
- Code uses MIT. Character artwork and third-party dependencies have separate notices; never claim MIT ownership of the character or supplied reference art. Do not change approved animation pixels, timings, or unfinished native edits.
- Use src/Build.ps1 to compile. src/Test.ps1 -Mode deployment runs isolated client-detection/configuration checks without account requests or visible windows. Existing modes with real model turns must not be run by the CI workflow.
- Version 0.1.21 adds portable deployment and local-client discovery. Asset-only repairs should keep the version unchanged.
