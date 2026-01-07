using Silk.NET.OpenAL;

namespace GopherWoodEngine.Runtime.Modules.Audio.DTOs;

internal struct OpenALAudioData
{
    public byte[] Data;
    public BufferFormat Format;
    public int SampleRate;
    public short NumChannels;
    public short BitsPerSample;
}
