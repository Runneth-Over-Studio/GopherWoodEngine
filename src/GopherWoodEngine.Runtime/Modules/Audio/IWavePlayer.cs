using System;
using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Represents an interface for playing WAVE audio files.
/// </summary>
public interface IWavePlayer : IDisposable
{
    /// <summary>
    /// Plays a WAVE audio file with 2D playback (non-positional audio).
    /// </summary>
    void PlayWave2D(string waveFilePath, float volume = 1.0f, bool looping = false);

    /// <summary>
    /// Plays a WAVE audio file with 3D spatial audio (positional audio with attenuation).
    /// </summary>
    public unsafe void PlayWave3D(string waveFilePath, Vector3 position, Vector3 velocity = default, float volume = 1.0f, bool looping = false, float referenceDistance = 1.0f, float maxDistance = 100.0f, float rolloffFactor = 1.0f);

    /// <summary>
    /// Sets the 3D listener position and orientation in world space.
    /// </summary>
    public unsafe void SetListenerPosition(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity = default);
}
