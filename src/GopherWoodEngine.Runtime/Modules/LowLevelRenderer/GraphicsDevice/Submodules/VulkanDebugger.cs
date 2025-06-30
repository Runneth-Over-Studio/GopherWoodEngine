using Microsoft.Extensions.Logging;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanDebugger : IDisposable
{
    // Represents a debug messenger that is specific to our Vulkan instance and its layers, that receives messages from the Vulkan API.
    internal DebugUtilsMessengerEXT? Messenger { get; }

    private readonly ExtDebugUtils? _utils;
    private readonly Instance _instance;
    private bool _disposed = false;

    public VulkanDebugger(Instance instance, Vk vk, ILogger<IGraphicsDevice> logger)
    {
        _instance = instance;

        _utils = GetExtDebugUtils(_instance, vk);
        Messenger = CreateDebugMessenger(_utils, _instance, logger);
    }

    // Returns the names of the enabled validation layers.
    // Validation layers are optional components that hook into Vulkan function calls to 
    // catch common mistakes and provide additional debugging information.
    // LunarG Vulkan SDK required to be installed.
    internal static string[] GetEnabledLayerNames()
    {
        return ["VK_LAYER_KHRONOS_validation"];
    }

    // Check if all of the requested layers are available.
    internal static void CheckValidationLayerSupport(Vk vk, string vulkanSDKVersion)
    {
        uint layerCount = 0;
        vk.EnumerateInstanceLayerProperties(ref layerCount, null);
        LayerProperties[] availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            vk.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        HashSet<string?> availableLayerNames = [.. availableLayers.Select(layer => Marshal.PtrToStringAnsi((nint)layer.LayerName))];
        bool validationLayerSupported = GetEnabledLayerNames().All(availableLayerNames.Contains);

        if (!validationLayerSupported)
        {
            throw new Exception($"Vulkan validation layers requested, but not available. Verify Vulkan SDK {vulkanSDKVersion}, or greater, is installed.");
        }
    }

    // Fill in <see cref="DebugUtilsMessengerCreateInfoEXT"/> structure with details about the messenger and its callback.
    internal static void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo, ILogger<IGraphicsDevice> logger)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;

        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;

        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;

        createInfo.PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback);

        // Store the logger in a GCHandle and pass it as pUserData.
        GCHandle loggerHandle = GCHandle.Alloc(logger);
        createInfo.PUserData = (void*)GCHandle.ToIntPtr(loggerHandle);
    }

    private static uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        LogLevel logLevel = messageSeverity switch
        {
            DebugUtilsMessageSeverityFlagsEXT.None => LogLevel.None,
            DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt => LogLevel.Trace,
            DebugUtilsMessageSeverityFlagsEXT.InfoBitExt => LogLevel.Information,
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => LogLevel.Warning,
            DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => LogLevel.Error,
            _ => LogLevel.Debug,
        };

        // Retrieve the logger from pUserData.
        GCHandle handle = GCHandle.FromIntPtr((nint)pUserData);
        ILogger<IGraphicsDevice>? logger = handle.Target as ILogger<IGraphicsDevice>;

        logger?.Log(logLevel, "Vulkan: {message}", Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False; // False means don't abort with the VK_ERROR_VALIDATION_FAILED_EXT error.
    }

    private static ExtDebugUtils? GetExtDebugUtils(Instance instance, Vk vk)
    {
        if (vk.TryGetInstanceExtension(instance, out ExtDebugUtils debugUtils))
        {
            return debugUtils;
        }

        return null;
    }

    private static DebugUtilsMessengerEXT? CreateDebugMessenger(ExtDebugUtils? utils, Instance instance, ILogger<IGraphicsDevice> logger)
    {
        if (utils != null)
        {
            DebugUtilsMessengerCreateInfoEXT createInfo = new();
            PopulateDebugMessengerCreateInfo(ref createInfo, logger);

            if (utils.CreateDebugUtilsMessenger(instance, in createInfo, null, out DebugUtilsMessengerEXT debugUtilsMessenger) != Result.Success)
            {
                throw new Exception("Vulkan debug messenger creation unsuccessful.");
            }

            return debugUtilsMessenger;
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing && _utils != null && Messenger != null)
            {
                _utils.DestroyDebugUtilsMessenger(_instance, Messenger.Value, null);
            }

            _disposed = true;
        }
    }
}
