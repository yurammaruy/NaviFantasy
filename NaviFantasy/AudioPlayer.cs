using System;
using System.IO;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using NAudio.Wave;
using NLayer.NAudioSupport;

namespace NaviFantasy;

/// <summary>Plays the "Hey! Listen!" clip. Uses a managed MP3 decoder so it also works under Wine/Proton.</summary>
public sealed class AudioPlayer : IDisposable
{
    private const float Volume = 0.6f;

    private readonly string path;
    private readonly IPluginLog log;
    private readonly object gate = new();
    private WaveOutEvent? output;
    private Mp3FileReader? reader;
    private bool disposed;

    public AudioPlayer(string path, IPluginLog log)
    {
        this.path = path;
        this.log = log;
    }

    public void Play() => Task.Run(PlayInternal);

    private void PlayInternal()
    {
        try
        {
            if (!File.Exists(path))
            {
                log.Warning("Audio file not found: {Path}", path);
                return;
            }

            lock (gate)
            {
                if (disposed) return;
                StopInternal();

                reader = new Mp3FileReader(path, wf => new Mp3FrameDecompressor(wf));
                output = new WaveOutEvent { Volume = Volume };
                output.PlaybackStopped += OnPlaybackStopped;
                output.Init(reader);
                output.Play();
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to play audio.");
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (gate)
        {
            if (ReferenceEquals(sender, output)) StopInternal();
        }
    }

    private void StopInternal()
    {
        if (output != null)
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            output.Dispose();
            output = null;
        }

        reader?.Dispose();
        reader = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            StopInternal();
        }
    }
}
