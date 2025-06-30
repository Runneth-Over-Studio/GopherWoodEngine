using Silk.NET.Input;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Represents an interface for interacting with the input context of physical devices.
/// </summary>
public interface IPhysicalDeviceIO
{
    /// <summary>
    /// Encapsulates the state and behavior of input operations for physical devices.
    /// </summary>
    IInputContext InputContext { get; }
}
