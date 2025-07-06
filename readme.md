<p align="left">
  <img src="icon.png" width="175" alt="GopherWood Logo">
</p>

# Gopher Wood Engine
Runneth Over Studio's in-house game engine. Written in C#.

In general, development is following [Game Engine Architecture](https://www.gameenginebook.com/) by Jason Gregory and the [Vulkan Game Engine Series](https://kohiengine.com/) by Travis Vroman. However, this is still a fairly bespoke solution. If you're interested in open source C# game engines/frameworks, I'd encourage you to check out the proven [Stride](https://github.com/stride3d/stride), [FlatRedBall](https://github.com/vchelaru/FlatRedBall), and [MonoGame](https://github.com/MonoGame/MonoGame).

> [!WARNING]
> This is an in-development, pre-alpha, engine and subject to significant change.

## Versioning
Gopher Wood Engine uses [Semantic Versioning](https://semver.org/). Given a version number MAJOR.MINOR.PATCH, increment the:

    MAJOR version when you make incompatible API changes
    MINOR version when you add functionality in a backward compatible manner
    PATCH version when you make backward compatible bug fixes
During intial development the version will at 0.1.0 and once a usable API is released we'll still be in version 0.y.z during rapid development. When the engine is ready to be used in production (building a real game), version 1.0.0 will be reeleased.

## Build Requirements
- All projects target the LTS version of the [.NET SDK](https://dotnet.microsoft.com/en-us/download). The SDK also provides the dotnet command line tool which the build makes use of.
- The engine renderer requires the [Vulkan SDK](https://www.lunarg.com/vulkan-sdk/). The SDK provides the Vulkan validation layers as well as the command line tools to compile the shaders. 
	- 'VULKAN_SDK' environment variable expected to be set during installation and is required by the build.

- Build project must be run prior to launching other projects so the neccesary shaders get compiled and embedded into the engine runtime.
	- The Build project uses the [Cake](https://cakebuild.net/) (C# Make) build orchestrator and can be launched from within your IDE or run from script.

		- On OSX/Linux run:

		```bash
		build.sh
		```
 
		- On Windows PowerShell run:

		```powershell
		./build.ps1
		```
