using Microsoft.Extensions.Logging;
using Silk.NET.OpenAL;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Provides functionality for playing WAVE audio files using OpenAL.
/// </summary>
/// <remarks>
/// This class parses RIFF-WAVE formatted files and plays them using the OpenAL audio library.
/// Supports PCM encoded audio with mono or stereo channels at 8 or 16 bits per sample.
/// </remarks>
public sealed class OpenALWavePlayer : IWavePlayer
{
    private readonly ILogger<IWavePlayer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenALWavePlayer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public OpenALWavePlayer(ILogger<IWavePlayer> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// This method reads and parses a RIFF-WAVE format audio file, validates its format,
    /// and plays it using OpenAL. The method supports:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Mono and stereo audio (1-2 channels)</description></item>
    /// <item><description>8-bit and 16-bit sample depths</description></item>
    /// <item><description>PCM audio format (format code 1)</description></item>
    /// </list>
    /// <para>
    /// The method handles various WAVE file chunks including 'fmt ', 'data', 'JUNK', and 'iXML'.
    /// </para>
    /// <para>
    /// The audio plays to completion before returning. OpenAL resources (source, buffer,
    /// context, and device) are properly cleaned up after playback.
    /// </para>
    /// </remarks>
    public unsafe void PlayWave(string waveFilePath)
    {
        if (string.IsNullOrWhiteSpace(waveFilePath))
        {
            _logger.LogError("Wave file path is null or empty.");
            return;
        }

        if (!File.Exists(waveFilePath))
        {
            _logger.LogError("Wave file doesn't exist at path: {FilePath}", waveFilePath);
            return;
        }

        ReadOnlySpan<byte> file = File.ReadAllBytes(waveFilePath);
        int index = 0;

        // Validate RIFF header
        if (file.Length < 12 ||
            file[index++] != 'R' || file[index++] != 'I' || file[index++] != 'F' || file[index++] != 'F')
        {
            _logger.LogError("Given file is not in RIFF format: {FilePath}", waveFilePath);
            return;
        }

        int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
        index += 4;

        // Validate WAVE format
        if (file[index++] != 'W' || file[index++] != 'A' || file[index++] != 'V' || file[index++] != 'E')
        {
            _logger.LogError("Given file is not in WAVE format: {FilePath}", waveFilePath);
            return;
        }

        short numChannels = -1;
        int sampleRate = -1;
        int byteRate = -1;
        short blockAlign = -1;
        short bitsPerSample = -1;
        BufferFormat format = 0;
        bool formatParsed = false;

        ALContext alc = ALContext.GetApi();
        AL al = AL.GetApi();
        Device* device = alc.OpenDevice("");
        if (device == null)
        {
            _logger.LogError("Could not create OpenAL device");
            return;
        }

        Context* context = alc.CreateContext(device, null);
        if (context == null)
        {
            _logger.LogError("Could not create OpenAL context");
            alc.CloseDevice(device);
            alc.Dispose();
            return;
        }

        alc.MakeContextCurrent(context);

        // Clear any existing errors
        al.GetError();

        uint source = al.GenSource();
        uint buffer = al.GenBuffer();

        // Check for errors after resource creation
        var error = al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to create OpenAL source/buffer: {Error}", error);
            alc.MakeContextCurrent(null);
            alc.DestroyContext(context);
            alc.CloseDevice(device);
            al.Dispose();
            alc.Dispose();
            return;
        }

        // Note: Looping is currently hardcoded - consider making this configurable
        al.SetSourceProperty(source, SourceBoolean.Looping, false);

        // Parse WAVE chunks
        while (index + 8 <= file.Length)
        {
            string identifier = "" + (char)file[index++] + (char)file[index++] + (char)file[index++] + (char)file[index++];
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
                    index += size - 2; // Skip rest of chunk
                    continue;
                }

                numChannels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                byteRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(index, 4));
                index += 4;
                blockAlign = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(index, 2));
                index += 2;

                // Skip any extra format bytes (for non-PCM formats, size > 16)
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
                        break;
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
                        break;
                    }
                }
                else
                {
                    _logger.LogError("Unsupported channel count: {NumChannels}. Only mono (1) or stereo (2) supported.", numChannels);
                    break;
                }

                formatParsed = true;
            }
            else if (identifier == "data")
            {
                if (!formatParsed)
                {
                    _logger.LogError("Encountered 'data' chunk before 'fmt ' chunk. Invalid WAVE file structure.");
                    break;
                }

                var data = file.Slice(index, size);
                index += size;

                fixed (byte* pData = data)
                {
                    al.BufferData(buffer, format, pData, size, sampleRate);
                }

                error = al.GetError();
                if (error != AudioError.NoError)
                {
                    _logger.LogError("Failed to buffer audio data: {Error}", error);
                }
                else
                {
                    _logger.LogInformation("Buffered {Size} bytes of audio data", size);
                }
            }
            else if (identifier == "JUNK")
            {
                // JUNK chunks exist for alignment purposes - skip them
                index += size;
            }
            else if (identifier == "iXML")
            {
                var xmlData = file.Slice(index, size);
                var str = Encoding.ASCII.GetString(xmlData);
                _logger.LogDebug("iXML Chunk: {XmlContent}", str);
                index += size;
            }
            else
            {
                _logger.LogDebug("Skipping unknown chunk: {Identifier} ({Size} bytes)", identifier, size);
                index += size;
            }
        }

        if (!formatParsed)
        {
            _logger.LogError("Failed to parse WAVE file format information");
            al.DeleteSource(source);
            al.DeleteBuffer(buffer);
            alc.MakeContextCurrent(null);
            alc.DestroyContext(context);
            alc.CloseDevice(device);
            al.Dispose();
            alc.Dispose();
            return;
        }

        _logger.LogInformation(
            "Successfully loaded WAVE file: {Channels} channel(s), {SampleRate} Hz, {BitsPerSample}-bit, {ByteRate} bytes/sec",
            numChannels,
            sampleRate,
            bitsPerSample,
            byteRate);

        al.SetSourceProperty(source, SourceInteger.Buffer, buffer);

        al.SourcePlay(source);
        error = al.GetError();
        if (error != AudioError.NoError)
        {
            _logger.LogError("Failed to play audio source: {Error}", error);
        }

        // Wait for playback to complete (only if not looping)
        // NOTE: This blocks the thread - consider async implementation or returning control immediately
        int state;
        do
        {
            al.GetSourceProperty(source, GetSourceInteger.SourceState, out state);
            System.Threading.Thread.Sleep(10); // Avoid busy waiting
        } while ((SourceState)state == SourceState.Playing);

        // Cleanup
        al.SourceStop(source);
        al.DeleteSource(source);
        al.DeleteBuffer(buffer);
        alc.MakeContextCurrent(null);
        alc.DestroyContext(context);
        alc.CloseDevice(device);
        al.Dispose();
        alc.Dispose();

        _logger.LogInformation("Audio playback completed and resources cleaned up");
    }
}
