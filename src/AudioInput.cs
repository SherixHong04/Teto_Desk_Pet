using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace TetoDesk {
    public static class AudioInput {
        public static async Task<string> TranscribeAsync(string source, string workspace, string dataDirectory, CancellationToken token) {
            Directory.CreateDirectory(dataDirectory);
            string normalized = Path.Combine(dataDirectory, "audio-" + Guid.NewGuid().ToString("N") + ".wav");
            try {
                await Task.Run(delegate {
                    token.ThrowIfCancellationRequested();
                    try {
                        using (var reader = new MediaFoundationReader(source)) {
                            if (reader.TotalTime.TotalSeconds > 600) throw new IOException("语音附件最长支持10分钟，请先分段。");
                            using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1))) {
                                resampler.ResamplerQuality = 60;
                                // SenseVoice requires a canonical 44-byte PCM WAV header.
                                // NAudio's general writer emits an extended fmt chunk instead.
                                using (var output = new BinaryWriter(File.Create(normalized))) {
                                    output.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); output.Write(0);
                                    output.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16);
                                    output.Write((short)1); output.Write((short)1); output.Write(16000); output.Write(32000); output.Write((short)2); output.Write((short)16);
                                    output.Write(System.Text.Encoding.ASCII.GetBytes("data")); output.Write(0);
                                    byte[] buffer = new byte[8192]; int read, total = 0;
                                    while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0) { token.ThrowIfCancellationRequested(); total += read; if (total > 20 * 1024 * 1024) throw new IOException("转换后的音频超过20MB，请先分段。"); output.Write(buffer, 0, read); }
                                    output.Seek(4, SeekOrigin.Begin); output.Write(total + 36); output.Seek(40, SeekOrigin.Begin); output.Write(total);
                                }
                            }
                        }
                    } catch (IOException) { throw; }
                    catch { throw new IOException("无法解码音频，请使用 WAV、MP3 或 M4A；其他格式取决于系统音频解码器。"); }
                    if (new FileInfo(normalized).Length > 20 * 1024 * 1024) throw new IOException("转换后的音频超过20MB，请先分段。");
                }, token);
                token.ThrowIfCancellationRequested();
                object result = await HarnessRuntime.AuxiliaryAsync("audio", workspace, dataDirectory, normalized, token);
                string text = J.S(result, "text");
                if (String.IsNullOrWhiteSpace(text)) throw new IOException("音频中没有识别到可转写的语音；未将其当作已识别内容发送。");
                return text;
            } finally { try { File.Delete(normalized); } catch { } }
        }
    }
}
