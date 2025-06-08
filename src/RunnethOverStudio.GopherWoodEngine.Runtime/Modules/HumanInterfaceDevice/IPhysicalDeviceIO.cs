using Silk.NET.Input;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Modules;

public interface IPhysicalDeviceIO
{
    IInputContext InputContext { get; }
}
