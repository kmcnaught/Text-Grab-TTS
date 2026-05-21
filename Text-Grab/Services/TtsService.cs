using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Text_Grab.Interfaces;
using Text_Grab.Properties;

namespace Text_Grab.Services;

public class TtsService
{
    private ITtsEngine _engine = new WindowsSpeechEngine();
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private readonly CancellationTokenSource _cts = new();

    public ITtsEngine Engine
    {
        set => _engine = value;
    }

    public TtsService()
    {
        _ = Task.Run(DrainLoopAsync);
    }

    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        int wordLimit = Settings.Default.TtsSpeakWordLimit;
        if (wordLimit > 0)
        {
            string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > wordLimit)
                text = string.Join(' ', words[..wordLimit]);
        }

        _queue.Writer.TryWrite(text);
    }

    private async Task DrainLoopAsync()
    {
        CancellationToken ct = _cts.Token;
        try
        {
            await foreach (string text in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await _engine.SpeakAsync(text, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // swallow per-item errors so the queue keeps draining
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
