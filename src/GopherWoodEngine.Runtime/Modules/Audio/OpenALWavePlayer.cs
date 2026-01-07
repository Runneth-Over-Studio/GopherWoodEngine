using GopherWoodEngine.Runtime.Modules.Audio.DTOs;
using Microsoft.Extensions.Logging;
using Silk.NET.OpenAL;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Provides functionality for playing WAVE audio files using OpenAL.
/// </summary>
/// <remarks>
/// This class parses RIFF-WAVE formatted files and plays them using the OpenAL audio library.
/// Supports PCM encoded audio with mono or stereo channels at 8 or 16 bits per sample.
/// Designed to be used as a sub-module within a higher-level audio manager.
/// </remarks>
public sealed class OpenALWavePlayer : IWavePlayer
{
    private readonly ILogger<IWavePlayer> _logger;
    private readonly AL _al;
    private readonly ALContext _alc;
    private unsafe Device* _device;
    private unsafe Context* _context;
    private readonly List<ActiveSound> _activeSounds = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenALWavePlayer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public unsafe OpenALWavePlayer(ILogger<IWavePlayer> logger)
    {
        _logger = logger;
        _alc = ALContext.GetApi();
        _al = AL.GetApi();

        _device = _alc.OpenDevice(string.Empty);
        if (_device == null)
        {
            throw new InvalidOperationException("Could not create OpenAL device");
        }

        _context = _alc.CreateContext(_device, null);
        if (_context == null)
        {
            _alc.CloseDevice(_device);
            throw new InvalidOperationException("Could not create OpenAL context");
        }

        _alc.MakeContextCurrent(_context);
        _al.GetError(); // Clear any existing errors
    }

    /// <inheritdoc/>
    /// <param name="waveFilePath">The path to the WAVE file to play.</param>
    /// <param name="volume">The playback volume (0.0 to 1.0). Default is 1.0.</param>
    /// <param name="looping">Whether the audio should loop continuously. Default is false.</param>
    /// <remarks>
    /// This method is suitable for UI sounds, background music, and 2D games.
    /// Audio is played without spatial/positional effects.
    /// This method returns immediately without blocking (fire-and-forget).
    /// </remarks>
    public unsafe void PlayWave2D(string waveFilePath, float volume = 1.0f, bool looping = false)
    {
        OpenALAudioData? audioData = LoadWaveFile(waveFilePath);
        if (audioData == null)
        {
            return;
        }

        uint source = _al.GenSource();
        uint buffer = _al.GenBuffer();

        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to create OpenAL source/buffer: {Error}", error);
            return;
        }

        // Buffer the audio data
        fixed (byte* pData = audioData.Value.Data)
        {
            _al.BufferData(buffer, audioData.Value.Format, pData, audioData.Value.Data.Length, audioData.Value.SampleRate);
        }

        error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to buffer audio data: {Error}", error);
            _al.DeleteSource(source);
            _al.DeleteBuffer(buffer);
            return;
        }

        // Configure source for 2D playback
        _al.SetSourceProperty(source, SourceInteger.Buffer, buffer);
        _al.SetSourceProperty(source, SourceBoolean.Looping, looping);
        _al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(volume, 0.0f, 1.0f));
        _al.SetSourceProperty(source, SourceBoolean.SourceRelative, true); // Make it non-positional

        // Play the audio
        _al.SourcePlay(source);
        error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to play audio source: {Error}", error);
            _al.DeleteSource(source);
            _al.DeleteBuffer(buffer);
            return;
        }

        _logger.LogInformation("Playing 2D audio: {FilePath} (Volume: {Volume}, Looping: {Looping})", waveFilePath, volume, looping);

        // Track the active sound for cleanup
        lock (_lock)
        {
            _activeSounds.Add(new ActiveSound { Source = source, Buffer = buffer, IsLooping = looping });
        }
    }

    /// <inheritdoc/>
    /// <param name="waveFilePath">The path to the WAVE file to play.</param>
    /// <param name="position">The 3D position of the sound source in world space.</param>
    /// <param name="velocity">The velocity vector of the sound source for Doppler effect. Default is zero.</param>
    /// <param name="volume">The playback volume (0.0 to 1.0). Default is 1.0.</param>
    /// <param name="looping">Whether the audio should loop continuously. Default is false.</param>
    /// <param name="referenceDistance">The distance at which the volume is at maximum. Default is 1.0.</param>
    /// <param name="maxDistance">The maximum distance beyond which the sound is no longer attenuated. Default is 100.0.</param>
    /// <param name="rollOffFactor">The rate at which sound attenuates with distance. Default is 1.0.</param>
    /// <remarks>
    /// <para>
    /// This method provides spatial 3D audio suitable for games with positional sound effects.
    /// The audio will be attenuated based on distance from the listener and panned based on direction.
    /// </para>
    /// <para>
    /// Note: For stereo audio files, spatial effects are limited. Use mono audio for best 3D positioning.
    /// </para>
    /// <para>
    /// This method returns immediately without blocking (fire-and-forget).
    /// </para>
    /// </remarks>
    public unsafe void PlayWave3D(string waveFilePath, Vector3 position, Vector3 velocity = default, float volume = 1.0f, bool looping = false, float referenceDistance = 1.0f, float maxDistance = 100.0f, float rollOffFactor = 1.0f)
    {
        OpenALAudioData? audioData = LoadWaveFile(waveFilePath);
        if (audioData == null)
        {
            return;
        }

        // Warn if using stereo for 3D audio
        if (audioData.Value.Format == BufferFormat.Stereo8 || audioData.Value.Format == BufferFormat.Stereo16)
        {
            _logger.LogWarning("Using stereo audio for 3D spatial sound. Mono audio is recommended for better positioning.");
        }

        uint source = _al.GenSource();
        uint buffer = _al.GenBuffer();

        AudioError error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to create OpenAL source/buffer: {Error}", error);
            return;
        }

        // Buffer the audio data
        fixed (byte* pData = audioData.Value.Data)
        {
            _al.BufferData(buffer, audioData.Value.Format, pData, audioData.Value.Data.Length, audioData.Value.SampleRate);
        }

        error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to buffer audio data: {Error}", error);
            _al.DeleteSource(source);
            _al.DeleteBuffer(buffer);
            return;
        }

        // Configure source for 3D spatial playback
        _al.SetSourceProperty(source, SourceInteger.Buffer, buffer);
        _al.SetSourceProperty(source, SourceBoolean.Looping, looping);
        _al.SetSourceProperty(source, SourceFloat.Gain, Math.Clamp(volume, 0.0f, 1.0f));
        _al.SetSourceProperty(source, SourceBoolean.SourceRelative, false); // Enable spatial positioning

        // Set 3D position and velocity
        _al.SetSourceProperty(source, SourceVector3.Position, position.X, position.Y, position.Z);
        _al.SetSourceProperty(source, SourceVector3.Velocity, velocity.X, velocity.Y, velocity.Z);

        // Set distance attenuation parameters
        _al.SetSourceProperty(source, SourceFloat.ReferenceDistance, referenceDistance);
        _al.SetSourceProperty(source, SourceFloat.MaxDistance, maxDistance);
        _al.SetSourceProperty(source, SourceFloat.RolloffFactor, rollOffFactor);

        // Play the audio
        _al.SourcePlay(source);
        error = _al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to play audio source: {Error}", error);
            _al.DeleteSource(source);
            _al.DeleteBuffer(buffer);
            return;
        }

        _logger.LogInformation("Playing 3D audio: {FilePath} at position {Position} (Volume: {Volume}, Looping: {Looping})", waveFilePath, position, volume, looping);

        // Track the active sound for cleanup
        lock (_lock)
        {
            _activeSounds.Add(new ActiveSound { Source = source, Buffer = buffer, IsLooping = looping });
        }
    }

    /// <inheritdoc/>
    /// <param name="position">The position of the listener (typically the camera/player position).</param>
    /// <param name="forward">The forward direction vector of the listener.</param>
    /// <param name="up">The up direction vector of the listener.</param>
    /// <param name="velocity">The velocity of the listener for Doppler effect. Default is zero.</param>
    /// <remarks>
    /// This should be called each frame to update the listener's position based on the camera or player movement.
    /// </remarks>
    public unsafe void SetListenerPosition(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity = default)
    {
        _al.SetListenerProperty(ListenerVector3.Position, position.X, position.Y, position.Z);
        _al.SetListenerProperty(ListenerVector3.Velocity, velocity.X, velocity.Y, velocity.Z);

        // OpenAL requires orientation as a 6-float array: [forward.x, forward.y, forward.z, up.x, up.y, up.z]
        float[] orientation = [forward.X, forward.Y, forward.Z, up.X, up.Y, up.Z];
        fixed (float* pOrientation = orientation)
        {
            _al.SetListenerProperty(ListenerFloatArray.Orientation, pOrientation);
        }

        _logger.LogTrace("Listener position updated: Position={Position}, Forward={Forward}, Up={Up}", position, forward, up);
    }

    /// <inheritdoc/>
    public unsafe void Update()
    {
        lock (_lock)
        {
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                ActiveSound sound = _activeSounds[i];

                _al.GetSourceProperty(sound.Source, GetSourceInteger.SourceState, out int state);

                // Clean up non-looping sounds that have finished playing
                if (!sound.IsLooping && (SourceState)state != SourceState.Playing)
                {
                    _al.DeleteSource(sound.Source);
                    _al.DeleteBuffer(sound.Buffer);
                    _activeSounds.RemoveAt(i);
                    _logger.LogDebug("Cleaned up finished audio source");
                }
            }
        }
    }

    /// <inheritdoc/>
    public unsafe void StopAll()
    {
        lock (_lock)
        {
            foreach (var sound in _activeSounds)
            {
                _al.SourceStop(sound.Source);
                _al.DeleteSource(sound.Source);
                _al.DeleteBuffer(sound.Buffer);
            }
            _activeSounds.Clear();
            _logger.LogInformation("Stopped all audio playback");
        }
    }

    private OpenALAudioData? LoadWaveFile(string waveFilePath)
    {
        if (string.IsNullOrWhiteSpace(waveFilePath))
        {
            _logger.LogError("Wave file path is null or empty.");
            return null;
        }

        if (!File.Exists(waveFilePath))
        {
            _logger.LogError("Wave file doesn't exist at path: {FilePath}", waveFilePath);
            return null;
        }

        ReadOnlySpan<byte> file = File.ReadAllBytes(waveFilePath);
        int index = 0;

        // Validate RIFF header
        if (file.Length < 12 || file[index++] != 'R' || file[index++] != 'I' || file[index++] != 'F' || file[index++] != 'F')
        {
            _logger.LogError("Given file is not in RIFF format: {FilePath}", waveFilePath);
            return null;
        }

        int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
        index += 4;

        // Validate WAVE format
        if (file[index++] != 'W' || file[index++] != 'A' || file[index++] != 'V' || file[index++] != 'E')
        {
            _logger.LogError("Given file is not in WAVE format: {FilePath}", waveFilePath);
            return null;
        }

        short numChannels = -1;
        int sampleRate = -1;
        short bitsPerSample = -1;
        BufferFormat format = 0;
        bool formatParsed = false;
        byte[]? audioData = null;
        Span<char> chars = stackalloc char[4];

        // Parse WAVE chunks
        while (index + 8 <= file.Length)
        {
            chars[0] = (char)file[index++];
            chars[1] = (char)file[index++];
            chars[2] = (char)file[index++];
            chars[3] = (char)file[index++];
            string identifier = new(chars);

            int size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
            index += 4;

            if (index + size > file.Length)
            {
                _logger.LogWarning("Chunk '{Identifier}' size ({Size}) exceeds file bounds. Stopping parse.", identifier, size);
                break;
            }

            if (identifier == "fmt ")
            {
                if (size < 16)
                {
                    _logger.LogError("Invalid fmt chunk size: {Size}", size);
                    break;
                }

                short audioFormat = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;

                if (audioFormat != 1)
                {
                    _logger.LogError("Unsupported audio format: {AudioFormat}. Only PCM (1) is supported.", audioFormat);
                    index += size - 2;
                    continue;
                }

                numChannels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                int byteRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                short blockAlign = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;

                // Skip any extra format bytes
                if (size > 16)
                {
                    index += size - 16;
                }

                // Determine OpenAL buffer format
                if (numChannels == 1)
                {
                    if (bitsPerSample == 8)
                        format = BufferFormat.Mono8;
                    else if (bitsPerSample == 16)
                        format = BufferFormat.Mono16;
                    else
                    {
                        _logger.LogError("Unsupported mono bit depth: {BitsPerSample}. Only 8 or 16 bits supported.", bitsPerSample);
                        return null;
                    }
                }
                else if (numChannels == 2)
                {
                    if (bitsPerSample == 8)
                        format = BufferFormat.Stereo8;
                    else if (bitsPerSample == 16)
                        format = BufferFormat.Stereo16;
                    else
                    {
                        _logger.LogError("Unsupported stereo bit depth: {BitsPerSample}. Only 8 or 16 bits supported.", bitsPerSample);
                        return null;
                    }
                }
                else
                {
                    _logger.LogError("Unsupported channel count: {NumChannels}. Only mono (1) or stereo (2) supported.", numChannels);
                    return null;
                }

                formatParsed = true;
            }
            else if (identifier == "data")
            {
                if (!formatParsed)
                {
                    _logger.LogError("Encountered 'data' chunk before 'fmt ' chunk. Invalid WAVE file structure.");
                    return null;
                }

                audioData = file.Slice(index, size).ToArray();
                index += size;

                _logger.LogDebug("Loaded {Size} bytes of audio data", size);
            }
            else if (identifier == "JUNK")
            {
                // JUNK chunks exist for alignment purposes - skip them
                index += size;
            }
            else if (identifier == "iXML")
            {
                ReadOnlySpan<byte> xmlData = file.Slice(index, size);
                string str = Encoding.ASCII.GetString(xmlData);
                _logger.LogTrace("iXML Chunk: {XmlContent}", str);
                index += size;
            }
            else
            {
                _logger.LogTrace("Skipping unknown chunk: {Identifier} ({Size} bytes)", identifier, size);
                index += size;
            }
        }

        if (!formatParsed || audioData == null)
        {
            _logger.LogError("Failed to parse WAVE file: {FilePath}", waveFilePath);
            return null;
        }

        _logger.LogDebug("Successfully loaded WAVE file: {Channels} channel(s), {SampleRate} Hz, {BitsPerSample}-bit", numChannels, sampleRate, bitsPerSample);

        return new OpenALAudioData
        {
            Data = audioData,
            Format = format,
            SampleRate = sampleRate,
            NumChannels = numChannels,
            BitsPerSample = bitsPerSample
        };
    }

    /// <inheritdoc/>
    public unsafe void Dispose()
    {
        StopAll();

        if (_context != null)
        {
            _alc.MakeContextCurrent(null);
            _alc.DestroyContext(_context);
            _context = null;
        }

        if (_device != null)
        {
            _alc.CloseDevice(_device);
            _device = null;
        }

        _al?.Dispose();
        _alc?.Dispose();

        _logger.LogInformation("OpenAL resources disposed");
    }
}

/* TODO: Audio Management
•	Audio source pooling: Reuse sources instead of creating/destroying them
•	Audio handles: Return handles to control playing sounds (pause, stop, adjust volume mid-playback)
•	DSP/Effects chain: Apply reverb, echo, filters, etc.
•	Audio groups/buses: Categorize sounds (SFX, Music, Voice) with independent volume controls
•	Streaming: For large music files to avoid memory overhead
 */