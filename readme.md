<p align="left">
  <img src="content/icon/gopherwood-icon.png" width="175" alt="GopherWood Logo">
</p>

# Gopher Wood Engine
Runneth Over Studio's in-house game engine. Written in C#.

In general, development is following [Game Engine Architecture](https://www.gameenginebook.com/) by Jason Gregory and the [Vulkan Game Engine Series](https://kohiengine.com/) by Travis Vroman. However, this is still a fairly bespoke solution. If you're interested in open source C# game engines/frameworks, I'd encourage you to check out the proven [Stride](https://github.com/stride3d/stride), [FlatRedBall](https://github.com/vchelaru/FlatRedBall), and [MonoGame](https://github.com/MonoGame/MonoGame).

> [!WARNING]
> This is an in-development, pre-alpha, engine and subject to significant change.

## Build Requirements
- The runtime requires the [Vulkan SDK](https://www.lunarg.com/vulkan-sdk/) to build/run. The SDK provides the Vulkan validation layers as well as the command line tools to compile the shaders. 
	- 'VULKAN_SDK' environment variable expected to be set during installation.

- Build project must be run prior to launching other projects so the neccesary shaders get compiled and embedded into the engine runtime.
	- The Build project uses the [Cake](https://cakebuild.net/) (C# Make) build orchestrator and can be launched from Visual Studio or run from script.

		- On OSX/Linux run:

		```bash
		build.sh
		```
 
		- On Windows PowerShell run:

		```powershell
		./build.ps1
		```
