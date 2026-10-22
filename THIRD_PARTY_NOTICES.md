# Third-party notices

- **NAudio.Core 2.2.1 / NAudio.Wasapi 2.2.1**, Mark Heath and contributors, MIT. The required .NET Standard binaries are vendored for reproducible offline builds. The full license is retained in `src/Bridge/licenses/NAudio-LICENSE.txt` and in the binary distribution's `Bridge/licenses/` directory. Upstream: https://github.com/naudio/NAudio .
- **Windows / .NET Framework / Media Foundation** are supplied by the user's Windows installation; they are not redistributed here.
- **OpenAI Codex CLI** and **DeepSeek Harness** are external user-installed clients. No executable, account, API key or authentication file for either client is bundled. Each remains subject to its own terms and license. Official documentation: https://learn.chatgpt.com/docs/cli and https://github.com/deepseek-ai/deepseek-harness .
- DeepSeek's optional local **SenseVoice** speech plugin and model files are managed/downloaded by Harness when requested; they are not bundled. No claim is made that audio is passed directly to a multimodal model.
- **Pillow** is an optional development dependency for icon conversion and existing art export utilities. It is not required by the Windows application or included in the binary distribution.
- Character artwork has a separate scope described in [ARTWORK.md](ARTWORK.md).
