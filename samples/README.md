# BRUTAL Samples

Small, self-contained samples for the BRUTAL packages. Each sample is a single C# file that you can run directly with the [.NET 10 SDK](https://dotnet.microsoft.com/download), with no project file. From a fresh clone of the repository:

```sh
cd samples/Hello
dotnet run HelloGlfw.cs
```

Or from the repository root, `dotnet run samples/Hello/HelloGlfw.cs`.

The samples come in two kinds:

- **[Hello/](Hello)** has one sample per package. Each is the smallest useful starting point for that package.
- **[Demo/](Demo)** has fuller programs that combine several packages.

These samples are deliberately minimal.

### Error handling

Error handling is kept light on purpose. The samples check the failures you're likely to hit, such as a window that can't be created, a missing GPU feature or a file that doesn't exist, and let everything else throw.

## Requirements

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- Windows x64, Linux x64 or macOS on Apple silicon. The packages bundle native libraries for these three.
- **Vulkan samples:** the packages ship Brutal's own Vulkan helper library, which loads the system's Vulkan loader, and the loader then finds your GPU driver. On Windows both come with your GPU driver. On Linux, install the Vulkan loader and a driver, for example `libvulkan1` and `mesa-vulkan-drivers` on Debian or Ubuntu. On macOS, install the [Vulkan SDK](https://vulkan.lunarg.com/sdk/home), which provides the loader and MoltenVK.
- **Windowed samples:** a desktop session. On Linux that means X11 or Wayland.
- **Audio samples:** an audio output device.
- **HelloNativeFileDialog on Linux:** GTK 3.
- **HelloFmod:** your own licensed FMOD libraries; see the file's header.
- **HelloNotCurses:** a real terminal.

## Start here

- **Foundations:** [HelloCore](Hello/HelloCore.cs), then [HelloMemory](Hello/HelloMemory.cs), [HelloCollections](Hello/HelloCollections.cs) and [HelloStrings](Hello/HelloStrings.cs).
- **Windowing:** [HelloGlfw](Hello/HelloGlfw.cs) for an empty window, then [HelloImGui](Hello/HelloImGui.cs) for debug UI.
- **Graphics:** [HelloOpenGl](Hello/HelloOpenGl.cs) for the shortest path to a triangle. For Vulkan, [HelloVulkan](Hello/HelloVulkan.cs) then [DemoVulkan](Demo/DemoVulkan.cs) for the triangle.
- **Audio:** [HelloOpenAl](Hello/HelloOpenAl.cs).
- **Physics:** [HelloBox2D](Hello/HelloBox2D.cs) or [HelloJolt](Hello/HelloJolt.cs), then their demos.

## Foundations

| Sample | What it shows |
| --- | --- |
| [HelloCore.cs](Hello/HelloCore.cs) | Platform detection, `ByteSize`/`ElementCount` arithmetic and `Ptr<T>` |
| [HelloMemory.cs](Hello/HelloMemory.cs) | Allocating and freeing unmanaged memory with `Mem`, and owning a pointer with `PtrOwner<T>` |
| [HelloCollections.cs](Hello/HelloCollections.cs) | `NativeList<T>`, a growable list backed by unmanaged memory |
| [HelloStrings.cs](Hello/HelloStrings.cs) | UTF-8 text with `String8` and `StringBuilder<byte>`, which are designed to avoid allocations |
| [HelloNumerics.cs](Hello/HelloNumerics.cs) | Vector and matrix basics with `float3` and `float4x4` |
| [HelloMaths.cs](Hello/HelloMaths.cs) | The `Random` extensions: ranges, points in shapes and random directions |
| [HelloConcurrency.cs](Hello/HelloConcurrency.cs) | Running jobs across worker threads with `JobScheduler` |
| [HelloLogging.cs](Hello/HelloLogging.cs) | Logging to the console with the default and named categories |
| [HelloLocalization.cs](Hello/HelloLocalization.cs) | Localised strings with named variables, and swapping in a translation |

## Windowing and UI

| Sample | What it shows |
| --- | --- |
| [HelloGlfw.cs](Hello/HelloGlfw.cs) | An empty GLFW window |
| [HelloSdl.cs](Hello/HelloSdl.cs) | An SDL window with a renderer drawing a filled rectangle |
| [HelloSdlImage.cs](Hello/HelloSdlImage.cs) | Saving a generated image as PNG with Sdl.Image and loading it back |
| [HelloSdlTtf.cs](Hello/HelloSdlTtf.cs) | Rendering text to a surface with Sdl.Ttf, using a system font if you don't pass one |
| [HelloImGui.cs](Hello/HelloImGui.cs) | Dear ImGui on its GLFW and OpenGL backends with some text, a button and a slider |
| [HelloImGuiFreeType.cs](Hello/HelloImGuiFreeType.cs) | Building the ImGui font atlas with FreeType, without a window |
| [HelloImPlot.cs](Hello/HelloImPlot.cs) | Plotting a sine wave with ImPlot in one headless frame |
| [HelloImGuiAbstractions.cs](Hello/HelloImGuiAbstractions.cs) | The in-game console: registering `[TerminalAction]` commands and running command lines |
| [HelloImGuiExtensions.cs](Hello/HelloImGuiExtensions.cs) | `ImGuiEx` widgets bound straight to C# enums |
| [HelloNativeFileDialog.cs](Hello/HelloNativeFileDialog.cs) | The operating system's native file picker |
| [HelloNotCurses.cs](Hello/HelloNotCurses.cs) | Coloured text in the terminal with notcurses (run it in a real terminal) |

## Graphics

| Sample | What it shows |
| --- | --- |
| [HelloOpenGl.cs](Hello/HelloOpenGl.cs) | A coloured triangle with OpenGL |
| [HelloVulkan.cs](Hello/HelloVulkan.cs) | Creating a Vulkan instance, listing GPUs and their queue families, and creating a device |
| [HelloVulkanAbstractions.cs](Hello/HelloVulkanAbstractions.cs) | The Vulkan.Abstractions setup helpers, and a vertex input layout built from C# structs |
| [HelloVulkanVma.cs](Hello/HelloVulkanVma.cs) | Creating a VMA allocator and a mapped buffer, writing to it and reading it back |
| [HelloShaderC.cs](Hello/HelloShaderC.cs) | Compiling GLSL to SPIR-V with shaderc |
| [HelloSlang.cs](Hello/HelloSlang.cs) | Compiling Slang to SPIR-V |
| [HelloRenderMesh.cs](Hello/HelloRenderMesh.cs) | Generating a procedural sphere mesh |

## Textures and assets

| Sample | What it shows |
| --- | --- |
| [HelloStb.cs](Hello/HelloStb.cs) | Writing and reading a PNG with stb_image |
| [HelloTexture.cs](Hello/HelloTexture.cs) | Texture format descriptors, filling a `GenericTexture`, and saving and loading it as PNG and KTX2 with `TextureWriter` and `TextureLoader` |
| [HelloKtx.cs](Hello/HelloKtx.cs) | Creating, writing and loading a KTX2 texture |
| [HelloGli.cs](Hello/HelloGli.cs) | A texture with a full mip chain, saved and loaded as DDS with GLI |
| [HelloGltf.cs](Hello/HelloGltf.cs) | Parsing a tiny inline glTF, reading its vertices and saving it back out |
| [HelloMeshOptimizer.cs](Hello/HelloMeshOptimizer.cs) | Reordering triangles for the vertex cache with meshoptimizer |

## Audio

| Sample | What it shows |
| --- | --- |
| [HelloOpenAl.cs](Hello/HelloOpenAl.cs) | Playing a generated tone with OpenAL |
| [HelloSdlMixer.cs](Hello/HelloSdlMixer.cs) | Playing a generated tone with Sdl.Mixer |
| [HelloFmod.cs](Hello/HelloFmod.cs) | Playing a tone with FMOD. FMOD's native libraries aren't bundled, so you need your own licensed copy |

## Physics

| Sample | What it shows |
| --- | --- |
| [HelloBox2D.cs](Hello/HelloBox2D.cs) | Dropping a box onto the ground with Box2D and printing where it lands |
| [HelloJolt.cs](Hello/HelloJolt.cs) | Dropping a box onto the ground with Jolt Physics and printing where it lands |

## Networking

| Sample | What it shows |
| --- | --- |
| [HelloRakNet.cs](Hello/HelloRakNet.cs) | A RakNet server and client connecting over localhost and sending a message |

## Diagnostics

| Sample | What it shows |
| --- | --- |
| [HelloMonitorHost.cs](Hello/HelloMonitorHost.cs) | Starting the Monitor watchdog and writing logs to a file |
| [HelloRenderDoc.cs](Hello/HelloRenderDoc.cs) | Checking whether RenderDoc's in-application API is loaded (Windows and Linux) |
| [HelloRenderDocImGui.cs](Hello/HelloRenderDocImGui.cs) | Drawing the RenderDoc control panel with Dear ImGui, headless |

## Demos

| Sample | What it shows |
| --- | --- |
| [DemoVulkan.cs](Demo/DemoVulkan.cs) | A coloured triangle with Vulkan, the Vulkan.Abstractions helpers, a VMA-allocated vertex buffer and shaders compiled with ShaderC |
| [DemoGltfViewer.cs](Demo/DemoGltfViewer.cs) | Loading a glTF scene with its node transforms, optimising it with meshoptimizer and drawing it lit with OpenGL. Pass your own `.gltf` or `.glb`, or it builds a torus knot |
| [DemoTexturedQuad.cs](Demo/DemoTexturedQuad.cs) | Writing a mipmapped texture as KTX2, loading it back and drawing it on a floor with OpenGL, with each mip level tinted |
| [DemoImGui.cs](Demo/DemoImGui.cs) | Dear ImGui with FreeType fonts and an ImPlot plot, on GLFW and OpenGL |
| [DemoImGuiVulkan.cs](Demo/DemoImGuiVulkan.cs) | Dear ImGui on GLFW and Vulkan over a spinning triangle, with the least Vulkan needed and a `Scene` class to replace with your own rendering and UI |
| [DemoSdl.cs](Demo/DemoSdl.cs) | An SDL window using Sdl.Image (PNG save and load), Sdl.Mixer (a beep on Space) and Sdl.Ttf (text, if you pass a font) |
| [DemoRenderDoc.cs](Demo/DemoRenderDoc.cs) | An OpenGL triangle that captures a frame with RenderDoc when you press F12 (Windows and Linux) |
| [DemoBox2D.cs](Demo/DemoBox2D.cs) | Boxes falling onto the ground with Box2D, drawn with OpenGL |
| [DemoJolt.cs](Demo/DemoJolt.cs) | Boxes falling onto the ground with Jolt Physics, drawn in 3D with OpenGL |
