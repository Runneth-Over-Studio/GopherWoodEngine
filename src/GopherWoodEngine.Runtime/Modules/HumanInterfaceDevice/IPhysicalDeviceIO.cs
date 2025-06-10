using Silk.NET.Input;

namespace GopherWoodEngine.Runtime.Modules;

public interface IPhysicalDeviceIO
{
    IInputContext InputContext { get; }
}
