using Cake.Common.IO;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using static Build.BuildContext;

namespace Build.Tasks;

[TaskName("Process Images")]
[IsDependentOn(typeof(RestoreTask))]
[TaskDescription("Processes source logo image to be used in the readme, NuGet package, and documentation.")]
public sealed class ProcessImagesTask : AsyncFrostingTask<BuildContext>
{
    private const string LOGO_SVG_FILENAME = "gopherwood-logo.svg";

    public override bool ShouldRun(BuildContext context)
    {
        return context.Config == BuildConfigurations.Release;
    }

    public override async Task RunAsync(BuildContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Create content folder in output location to place resources.
        DirectoryPath releaseContentDirectory = context.RuntimeOutputDirectory + context.Directory("Content");
        context.EnsureDirectoryExists(releaseContentDirectory);

        // Convert source icon SVG to PNG and save to new content folder.
        context.Log.Information($"Creating project logo image (PNG) from source SVG file...");
        DirectoryPath sourceContentDirectory = context.RootDirectory + context.Directory("content");
        DirectoryPath sourceLogoDirectory = sourceContentDirectory + context.Directory("logo");
        string sourceSVGPath = System.IO.Path.Combine(sourceLogoDirectory.FullPath, LOGO_SVG_FILENAME);
        string pngPath = System.IO.Path.Combine(releaseContentDirectory.FullPath, "logo.png");
        await ConvertSvgToPngAsync(sourceSVGPath, pngPath);

        // Convert PNG to a favicon image and save to new content foler.
        context.Log.Information($"Creating project favicon image (ICO) from project logo image...");
        string icoPath = System.IO.Path.Combine(releaseContentDirectory.FullPath, "favicon.ico");
        await ConvertPngToIcoAsync(pngPath, icoPath);

        // Create NuGet package icon. Microsoft recommends an image resolution of 128x128 and must be either JPEG or PNG.
        context.Log.Information($"Creating NuGet package icon...");
        string packageIconPath = System.IO.Path.Combine(sourceContentDirectory.FullPath, "package-icon.png");
        using (Image image = await Image.LoadAsync(pngPath))
        using (Image resized = image.Clone(ctx => ctx.Resize(128, 128)))
        {
            await resized.SaveAsync(packageIconPath, new PngEncoder());
        }

        // Copy the logo image to the repo root folder. Used in the readme markdown document.
        context.CopyFile(pngPath, System.IO.Path.Combine(context.RootDirectory.Path.FullPath, "logo.png"));

        stopwatch.Stop();
        double completionTime = Math.Round(stopwatch.Elapsed.TotalSeconds, 1);
        context.Log.Information($"Processing of project images complete ({completionTime}s)");
    }

    private static async Task ConvertSvgToPngAsync(string sourceSvgPath, string targetPngPath)
    {
        // Load the SVG file.
        SKSvg svg = new();
        svg.Load(sourceSvgPath);

        // Determine the canvas size from the SVG's picture bounds.
        if (svg.Picture == null)
        {
            throw new InvalidOperationException("Failed to load SVG picture.");
        }

        var bounds = svg.Picture.CullRect;
        int width = (int)Math.Ceiling(bounds.Width);
        int height = (int)Math.Ceiling(bounds.Height);

        // Convert SVG to bitmap.
        using SKBitmap bitmap = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawPicture(svg.Picture);
        }

        // Convert bitmap to an ImageSharp image.
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(bitmap.Bytes, bitmap.Width, bitmap.Height);

        // Save as PNG.
        await image.SaveAsync(targetPngPath, new PngEncoder());
    }

    private static async Task ConvertPngToIcoAsync(string sourcePngPath, string targetIcoPath, int iconSize = 32)
    {
        // ref: https://www.meziantou.net/creating-ico-files-from-multiple-images-in-dotnet.htm

        const short NUM_IMAGES = 1;

        // Load and resize the image.
        using Image image = await Image.LoadAsync(sourcePngPath);
        using Image resizedImage = image.Clone(ctx => ctx.Resize(iconSize, iconSize));

        // Save resized image as PNG to memory.
        using MemoryStream pngStream = new();
        await resizedImage.SaveAsPngAsync(pngStream);
        byte[] pngData = pngStream.ToArray();

        // Create the ICO file.
        await using FileStream output = File.OpenWrite(targetIcoPath);
        await using BinaryWriter iconWriter = new(output);

        // Write ICO header.
        iconWriter.Write((byte)0); // reserved
        iconWriter.Write((byte)0);
        iconWriter.Write((short)1); // image type: icon
        iconWriter.Write(NUM_IMAGES); // number of images

        long offset = 6 + (16 * NUM_IMAGES); // ico header (6 bytes) + image directory (16 bytes per image)

        // Write image directory.
        iconWriter.Write((byte)(iconSize >= 256 ? 0 : iconSize));
        iconWriter.Write((byte)(iconSize >= 256 ? 0 : iconSize));
        iconWriter.Write((byte)0); // number of colors
        iconWriter.Write((byte)0); // reserved
        iconWriter.Write((short)0); // color planes
        iconWriter.Write((short)32); // bits per pixel
        iconWriter.Write((uint)pngData.Length); // size of image data
        iconWriter.Write((uint)offset); // offset of image data

        // Write image data.
        iconWriter.Write(pngData);
    }
}
