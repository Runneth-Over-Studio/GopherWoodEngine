using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;

internal unsafe class VulkanPipeline
{
    private readonly Vk _vk;
    private readonly Device _logicalDevice;

    public VulkanPipeline(Vk vk, Device logicalDevice)
    {
        _vk = vk;
        _logicalDevice = logicalDevice;
    }

    internal void CreateGraphicsPipeline()
    {
        byte[] vertShaderCode = GetEmbeddedShaderBytes("09_shader_base.vert.spv");
        byte[] fragShaderCode = GetEmbeddedShaderBytes("09_shader_base.frag.spv");

        ShaderModule vertShaderModule = CreateShaderModule(vertShaderCode);
        ShaderModule fragShaderModule = CreateShaderModule(fragShaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo* shaderStages = stackalloc[]
        {
            vertShaderStageInfo,
            fragShaderStageInfo
        };

        _vk.DestroyShaderModule(_logicalDevice, fragShaderModule, null);
        _vk.DestroyShaderModule(_logicalDevice, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };

        ShaderModule shaderModule;
        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            if (_vk.CreateShaderModule(_logicalDevice, in createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception("Creation of shader module was unsuccessful.");
            }
        }

        return shaderModule;
    }

    /// <summary>
    /// Read shader modules (SPV files) that were built and stored locally relative to the application.
    /// </summary>
    private static byte[] GetLocalShaderBytes(string filePath)
    {
        return File.ReadAllBytes(filePath);
    }

    /// <summary>
    /// Read shader modules (SPV files) that were built and embedded into the application.
    /// </summary>
    private static byte[] GetEmbeddedShaderBytes(string filename)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string? resourceName = assembly.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(filename))
            ?? throw new ApplicationException($"No shader file found with name {filename}. Did you forget to set glsl file to Embedded Resource/Do Not Copy?");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ApplicationException($"No shader file found at {resourceName}. Did you forget to set glsl file to Embedded Resource/Do Not Copy?");

        using MemoryStream ms = new();
        stream.CopyTo(ms);

        return ms.ToArray();
    }
}
