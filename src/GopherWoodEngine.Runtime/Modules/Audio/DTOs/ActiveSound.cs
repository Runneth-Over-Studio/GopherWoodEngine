using System;

namespace GopherWoodEngine.Runtime.Modules.Audio.DTOs;

internal struct ActiveSound
{
    public uint Source;
    public uint Buffer;
    public bool IsLooping;
}
