using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Represents an interface for playing WAVE audio files.
/// </summary>
public interface IWavePlayer
{
    /// <summary>
    /// Plays a WAV audio file from the specified file path.
    /// </summary>
    /// <param name="waveFilePath">The full path to the WAV file to play.</param>
    void PlayWave(string waveFilePath);
}
