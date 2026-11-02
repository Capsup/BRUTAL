/*
 * DemoImGuiVulkan
 *
 * Dear ImGui on GLFW + Vulkan, over a spinning triangle you control from an ImGui window. The file reads top to
 * bottom: the frame loop, with every ImGui call it needs; Scene, which holds all the rendering and UI (the triangle,
 * drawn with shaders compiled at startup by ShaderC, and its window); and VulkanWindow, the least Vulkan needed to show
 * them: one render pass, one frame in flight and a fixed-size window. Clear out Scene to start your own.
 *
 * Press Escape to close it.
 */

#:property AllowUnsafeBlocks=true
#:package Ahwoo.Brutal.Glfw@2026.10.5
#:package Ahwoo.Brutal.ImGui@2026.10.3
#:package Ahwoo.Brutal.ImGui.Backends@2026.10.3
#:package Ahwoo.Brutal.Vulkan@2026.10.3
#:package Ahwoo.Brutal.Vulkan.Abstractions@2026.10.3
#:package Ahwoo.Brutal.ShaderC@2026.10.3

using System.Runtime.InteropServices;
using Brutal;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Backends;
using Brutal.Numerics;
using Brutal.ShaderCApi;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;

// The window and the Vulkan behind it: see VulkanWindow at the bottom.
using var window = new VulkanWindow("Demo ImGui Vulkan", new int2(1280, 720));

var imguiContext = ImGui.CreateContext();
try
{
    ImGui.GetIO().IniFilename = null; // Don't write imgui.ini.
    ImGui.StyleColorsDark();

    // The GLFW backend feeds ImGui input; the Vulkan backend draws it. The Vulkan backend needs a DeviceEx (from
    // Vulkan.Abstractions) and a classic render pass to draw in, since it doesn't support dynamic rendering.
    using var glfwBackend = new GlfwBackend(window.GlfwWindow, GlfwBackend.ClientApi.Vulkan);
    using var vulkanBackend = new VulkanBackend(
        new VulkanBackend.CreateInfo
        {
            Device = window.Device,
            GraphicsQueue = window.Queue,
            RenderPass = window.RenderPass,
            MinImageCount = 2,
            ImageCount = window.ImageCount,
            SampleCount = VkSampleCountFlags._1Bit,
            DescriptorPoolSize = 8, // One per texture ImGui draws; here just the font atlas.
        }
    );

    using var scene = new Scene(window.Device, window.Queue, window.RenderPass); // Your rendering and UI: see below.

    while (window.NextFrame(out var deltaTime))
    {
        // 1. Advance the scene. It doesn't need ImGui, so it runs before the UI frame starts.
        scene.Update(deltaTime);

        // 2. Build the UI: the scene describes its windows, and ImGui.Render() collects them for drawing in step 3.
        // Recycle the scratch memory that interpolated ImGui strings ($"...") use, once per frame. It's a fixed
        // arena that nothing else clears, so without this it fills up. UTF-8 literals ("Label"u8) don't use it.
        ImString.SharedStorage.Reset();
        glfwBackend.NewFrame();
        vulkanBackend.NewFrame();
        ImGui.NewFrame();
        scene.DrawUi();
        ImGui.Render();

        // 3. Render into the next swapchain image: the scene, then the UI on top.
        var commandBuffer = window.BeginFrame(clearColor: new float4(0.10f, 0.12f, 0.16f, 1f));
        scene.Draw(commandBuffer, window.Extent);
        vulkanBackend.RenderDrawData(ImGui.GetDrawData(), commandBuffer);
        window.EndFrame();
    }

    window.Device.WaitIdle(); // Let the GPU finish before the scene and the ImGui backends are disposed.
}
finally
{
    ImGui.DestroyContext(imguiContext);
}

/// <summary>
/// Your own rendering and UI go here. The constructor runs once, then every frame <see cref="Update"/>,
/// <see cref="DrawUi"/> and <see cref="Draw"/>, then <see cref="Dispose"/> when the window closes.
/// This one draws a spinning triangle, and an ImGui window with its speed and size, the frame rate and a toggle for
/// ImGui's demo window. The triangle's corners come from the vertex shader, so there's no vertex buffer, and the
/// angle, size and aspect ratio reach the shader as push constants. Clear out the members' contents to start your own.
/// </summary>
sealed class Scene : IDisposable
{
    private readonly DeviceEx _device;
    private readonly VkPipelineLayout _layout;
    private readonly VkPipeline _pipeline;

    private float _angle;
    private float _speed = 1f;
    private float _size = 0.6f;
    private bool _spinning = true;
    private bool _showDemoWindow = true;

    // Matches the shader's push_constant block: three floats, 12 bytes.
    private struct PushConstants
    {
        public float Angle;
        public float Size;
        public float Aspect;
    }

    /// <summary>
    /// Create pipelines, buffers and textures here (<paramref name="queue"/> is for uploading them). Create pipelines
    /// against <paramref name="renderPass"/> (subpass 0): one colour attachment in the swapchain's format and no depth
    /// buffer. If your scene needs depth, add a depth attachment to that render pass and its framebuffers in
    /// <see cref="VulkanWindow"/>.
    /// </summary>
    public unsafe Scene(DeviceEx device, Queue queue, VkRenderPass renderPass)
    {
        _device = device;

        // Compile GLSL to SPIR-V at startup with ShaderC. ShaderStages owns the modules and destroys them on Dispose,
        // which is fine as soon as the pipeline exists.
        using var compiler = ShaderC.CreateCompiler();
        using var options = ShaderC.CreateCompilerOptions();
        VkShaderModule Compile(ReadOnlySpan<byte> glsl, ShaderKind kind)
        {
            using var spirv = compiler.Compile(glsl, kind, "scene"u8, "main"u8, options);
            return ShaderModuleExtensions.FromSpirv(
                device,
                new ReadOnlySpan<byte>(spirv.GetBytes(), (int)spirv.GetLength()),
                null
            );
        }

        using var stages = new ShaderStages(
            device,
            [
                Presets.ShaderStage.Vertex(
                    Compile(
                        """
                        #version 450

                        layout(push_constant) uniform PushConstants { float angle; float size; float aspect; } push;
                        layout(location = 0) out vec3 color;

                        const vec2 corners[3] = vec2[](vec2(0.0, -1.0), vec2(0.866, 0.5), vec2(-0.866, 0.5));
                        const vec3 colors[3] = vec3[](vec3(1.0, 0.35, 0.2), vec3(0.2, 0.9, 0.4), vec3(0.25, 0.45, 1.0));

                        void main()
                        {
                            float c = cos(push.angle), s = sin(push.angle);
                            vec2 p = mat2(c, s, -s, c) * corners[gl_VertexIndex] * push.size;
                            gl_Position = vec4(p.x / push.aspect, p.y, 0.0, 1.0); // Keeps it from stretching.
                            color = colors[gl_VertexIndex];
                        }
                        """u8,
                        ShaderKind.VertexShader
                    )
                ),
                Presets.ShaderStage.Fragment(
                    Compile(
                        """
                        #version 450

                        layout(location = 0) in vec3 color;
                        layout(location = 0) out vec4 outColor;

                        void main()
                        {
                            outColor = vec4(color, 1.0);
                        }
                        """u8,
                        ShaderKind.FragmentShader
                    )
                ),
            ],
            null
        );

        _layout = device.CreatePipelineLayout(
            [],
            [
                new VkPushConstantRange
                {
                    StageFlags = VkShaderStageFlags.VertexBit,
                    Size = new ByteSize32((uint)sizeof(PushConstants)),
                },
            ],
            null
        );

        // Viewport and scissor are dynamic: Draw sets them, so the pipeline doesn't depend on the window size.
        var dynamicStates = stackalloc VkDynamicState[] { VkDynamicState.Viewport, VkDynamicState.Scissor };
        var dynamicState = new VkPipelineDynamicStateCreateInfo
        {
            DynamicStateCount = 2,
            DynamicStates = dynamicStates,
        };
        var viewportState = new VkPipelineViewportStateCreateInfo { ViewportCount = 1, ScissorCount = 1 };
        var noVertexBuffers = new VkPipelineVertexInputStateCreateInfo(); // The corners come from the shader.

        _pipeline = device.CreateGraphicsPipeline(
            VkPipelineCache.NullHandle,
            new VkGraphicsPipelineCreateInfo
            {
                StageCount = stages.Count,
                Stages = stages,
                VertexInputState = &noVertexBuffers,
                InputAssemblyState = Presets.InputAssembly.TriangleList,
                ViewportState = &viewportState,
                RasterizationState = Presets.Rasterization.Fill.CullNone,
                MultisampleState = Presets.Multisample.None,
                ColorBlendState = Presets.BlendState.BlendNone,
                DynamicState = &dynamicState,
                Layout = _layout,
                RenderPass = renderPass, // The render pass the triangle is drawn in, subpass 0.
                Subpass = 0,
            },
            null
        );
    }

    /// <summary>
    /// Advances the scene by <paramref name="deltaTime"/> seconds. Called every frame before the UI is built, so no
    /// ImGui calls here.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (_spinning)
            _angle += _speed * deltaTime;
    }

    /// <summary>
    /// Describes this frame's ImGui windows; this is the whole UI. Called every frame between <c>ImGui.NewFrame</c>
    /// and <c>ImGui.Render</c>. Nothing is drawn here: the main loop draws the UI over the scene once this returns.
    /// </summary>
    public void DrawUi()
    {
        if (_showDemoWindow)
            ImGui.ShowDemoWindow(ref _showDemoWindow); // Every ImGui widget, with the code that makes it.

        ImGui.SetNextWindowPos(new float2(16f, 16f), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Demo ImGui Vulkan"u8))
        {
            var io = ImGui.GetIO();
            ImGui.Text($"{io.Framerate:F0} FPS");
            ImGui.Checkbox("ImGui demo window"u8, ref _showDemoWindow);

            ImGui.SeparatorText("Triangle"u8);
            ImGui.Checkbox("Spinning"u8, ref _spinning);
            ImGui.SliderFloat("Speed"u8, ref _speed, -5f, 5f, "%.1f rad/s"u8);
            ImGui.SliderFloat("Size"u8, ref _size, 0.1f, 1.5f);

            // Check these before your own camera or key bindings react, so input meant for a window stays there.
            ImGui.Separator();
            ImGui.TextDisabled($"ImGui wants mouse: {io.WantCaptureMouse}, keyboard: {io.WantCaptureKeyboard}");
        }
        ImGui.End();
    }

    /// <summary>
    /// Records the scene's draws into <paramref name="commandBuffer"/>, inside the render pass, which has cleared the
    /// image; the UI is drawn on top afterwards. Set your own viewport and scissor (<paramref name="extent"/> is the
    /// image size): ImGui sets its own, after you.
    /// </summary>
    public void Draw(CommandBuffer commandBuffer, VkExtent2D extent)
    {
        commandBuffer.SetViewport(
            0,
            [
                new VkViewport
                {
                    Width = extent.Width,
                    Height = extent.Height,
                    MaxDepth = 1f,
                },
            ]
        );
        commandBuffer.SetScissor(0, [new VkRect2D(extent)]);
        commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, _pipeline);
        commandBuffer.PushConstants(
            _layout,
            VkShaderStageFlags.VertexBit,
            new ByteSize(0), // Offset.
            new PushConstants { Angle = _angle, Size = _size, Aspect = extent.Width / (float)extent.Height }
        );
        commandBuffer.Draw(3, 1, 0, 0);
    }

    /// <summary>Destroy what the constructor created. The GPU is idle by the time this is called.</summary>
    public void Dispose()
    {
        _device.DestroyPipeline(_pipeline, null);
        _device.DestroyPipelineLayout(_layout, null);
    }
}

/// <summary>
/// A fixed-size GLFW window with the least Vulkan needed to draw into it: an instance, a device, a swapchain, one
/// render pass and one frame in flight. Each frame, <see cref="BeginFrame"/> returns a command buffer that is recording
/// inside the render pass, and <see cref="EndFrame"/> submits it and presents the image. You don't need to read this to
/// use the sample; it's here for when you want to change how frames are set up, for example to add a depth buffer.
/// </summary>
sealed unsafe class VulkanWindow : IDisposable
{
    public GlfwWindow GlfwWindow { get; }
    public DeviceEx Device { get; }

    /// <summary>The one queue, which both draws and presents.</summary>
    public Queue Queue { get; }

    /// <summary>
    /// The render pass that everything is drawn in. It clears the image to the colour passed to
    /// <see cref="BeginFrame"/> and leaves it ready to present.
    /// </summary>
    public VkRenderPass RenderPass { get; }

    /// <summary>The size of the swapchain images, in pixels.</summary>
    public VkExtent2D Extent { get; }

    public int ImageCount => _framebuffers.Length;

    private readonly Instance _instance;
    private readonly VkSurfaceKHR _surface;
    private readonly VkSwapchainKHR _swapchain;

    // Per swapchain image.
    private readonly VkImageView[] _imageViews;
    private readonly VkFramebuffer[] _framebuffers;
    private readonly VkSemaphore[] _renderFinished;

    // One frame in flight: one command buffer, one "image acquired" semaphore, one fence.
    private readonly VkCommandPool _commandPool;
    private readonly CommandBuffer _commandBuffer;
    private readonly VkSemaphore _imageAcquired;
    private readonly VkFence _frameFence;

    private int _imageIndex;
    private double _lastTime;

    public VulkanWindow(string title, int2 size)
    {
        // Hand GLFW the Vulkan loader Brutal has loaded, so GLFW doesn't search for its own copy (and miss it where
        // Brutal found it, such as Homebrew on macOS). This must happen before Glfw.Init().
        Glfw.PInvoke.InitVulkanLoader((nint)Vulkan.GetInstanceProcAddr.Invoke);
        if (!Glfw.Init())
        {
            var errorCode = Glfw.GetError(out var errorMessage);
            throw new InvalidOperationException($"Failed to initialize GLFW: {errorCode}, {errorMessage}");
        }

        Glfw.WindowHint(GlfwWindowHint.ClientApi, Glfw.Constants.GLFW_NO_API); // Vulkan presents, not OpenGL.
        Glfw.WindowHint(GlfwWindowHint.Resizable, Glfw.Constants.GLFW_FALSE); // A fixed size means no swapchain rebuilds.
        GlfwWindow = Glfw.CreateWindow(new GlfwWindow.CreateInfo { Title = title, Size = size });
        GlfwWindow.OnKey += (handle, key, _, _, _) =>
        {
            if (key == GlfwKey.Escape)
                handle.ShouldClose = true;
        };

        // Instance, with the extensions GLFW needs for a window surface. VulkanHelpers adds whatever this OS needs
        // (portability enumeration for MoltenVK on macOS).
        var instanceExtensions = new HashSet<string>();
        VulkanHelpers.AddPlatformRequiredExtensions(instanceExtensions);
        var glfwExtensions = Glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        for (var i = 0; i < glfwExtensionCount; i++)
            instanceExtensions.Add(Marshal.PtrToStringUTF8((nint)glfwExtensions[i])!);

        _instance = Vulkan.CreateInstance(
            new Instance.CreateInfo
            {
                AppInfo = new VkApplicationInfo { ApiVersion = (int)VulkanHelpers.Api.VERSION_1_3 },
                Flags = VulkanHelpers.GetPlatformRequiredInstanceCreateFlags(),
                EnabledExtensions = instanceExtensions,
                EnabledLayers = [],
            }
        );

        if (Glfw.CreateWindowSurface(_instance.VkHandle, GlfwWindow, out var surfaceHandle) != (int)VkResult.Success)
            throw new InvalidOperationException("Failed to create a Vulkan surface for the window");
        _surface = new VkSurfaceKHR((nint)surfaceHandle);

        // A device with a queue family that can both draw and present to the window: a discrete GPU if there is one
        // (laptops often have an integrated GPU too), otherwise the first that works.
        PhysicalDevice? physicalDevice = null;
        var queueFamily = -1;
        foreach (var candidate in _instance.PhysicalDevices)
        {
            var families = new VkQueueFamilyProperties[candidate.GetQueueFamilyProperties_Count()];
            candidate.GetQueueFamilyProperties_Write(families);
            var family = Enumerable
                .Range(0, families.Length)
                .FirstOrDefault(
                    i =>
                        families[i].QueueFlags.HasFlag(VkQueueFlags.GraphicsBit)
                        && candidate.GetSurfaceSupportKHR(i, _surface),
                    -1
                );
            if (family < 0)
                continue;

            var isDiscrete = candidate.GetProperties().DeviceType == VkPhysicalDeviceType.DiscreteGpu;
            if (physicalDevice is null || isDiscrete)
                (physicalDevice, queueFamily) = (candidate, family);
            if (isDiscrete)
                break;
        }

        if (physicalDevice is null)
            throw new InvalidOperationException("No Vulkan device can draw and present to the window");
        var deviceProperties = physicalDevice.GetProperties();
        Console.WriteLine($"Rendering on {Marshal.PtrToStringUTF8((nint)deviceProperties.DeviceName)}.");

        var deviceExtensions = new HashSet<string>();
        VulkanHelpers.AddSwapchainExtensions(deviceExtensions);
        VulkanHelpers.AddMandatoryIfSupportedDeviceExtensions(deviceExtensions, physicalDevice); // MoltenVK's portability.

        var priority = 1f;
        Device = physicalDevice.CreateDevice(
            new DeviceEx.CreateInfo
            {
                Extensions = deviceExtensions,
                QueueCreateInfos =
                [
                    new VkDeviceQueueCreateInfo
                    {
                        QueueFamilyIndex = queueFamily,
                        QueueCount = 1,
                        QueuePriorities = &priority,
                    },
                ],
            }
        );
        Queue = Device.GetQueue(queueFamily, 0);

        // Swapchain. A UNORM format, because ImGui's colours are sRGB values meant to reach the screen unchanged: an
        // sRGB swapchain would encode them again and wash the UI out. A 3D scene usually wants sRGB; then convert the
        // vertex colours in ImGui.GetDrawData() from sRGB to linear after ImGui.Render().
        var formats = new VkSurfaceFormatKHR[physicalDevice.GetSurfaceFormatsKHR_Count(_surface)];
        physicalDevice.GetSurfaceFormatsKHR_Write(_surface, formats);
        var surfaceFormat = formats.FirstOrDefault(f => f.Format == VkFormat.B8G8R8A8UNorm, formats[0]);

        var capabilities = physicalDevice.GetSurfaceCapabilitiesKHR(_surface);
        var extent = capabilities.CurrentExtent;
        if (extent.Width == -1) // 0xFFFFFFFF: the surface lets the swapchain pick its size.
        {
            var framebufferSize = GlfwWindow.FramebufferSize;
            extent = new VkExtent2D { Width = framebufferSize.X, Height = framebufferSize.Y };
        }
        Extent = extent;

        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount != 0)
            imageCount = Math.Min(imageCount, capabilities.MaxImageCount);

        _swapchain = Device.CreateSwapchainKHR(
            new VkSwapchainCreateInfoKHR
            {
                Surface = _surface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = VkImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = VkSharingMode.Exclusive,
                PreTransform = capabilities.CurrentTransform,
                CompositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKHR,
                PresentMode = VkPresentModeKHR.FifoKHR, // Vsync; always available.
                Clipped = true,
            },
            null
        );
        var images = new VkImage[Device.GetSwapchainImagesKHR_Count(_swapchain)];
        Device.GetSwapchainImagesKHR_Write(_swapchain, images);

        // The render pass clears the image, everything draws over it, and the pass leaves it ready to present, so no
        // barriers are needed.
        var attachment = new VkAttachmentDescription
        {
            Format = surfaceFormat.Format,
            Samples = VkSampleCountFlags._1Bit,
            LoadOp = VkAttachmentLoadOp.Clear,
            StoreOp = VkAttachmentStoreOp.Store,
            StencilLoadOp = VkAttachmentLoadOp.DontCare,
            StencilStoreOp = VkAttachmentStoreOp.DontCare,
            InitialLayout = VkImageLayout.Undefined,
            FinalLayout = VkImageLayout.PresentSrcKHR,
        };
        var colorReference = new VkAttachmentReference { Attachment = 0, Layout = VkImageLayout.ColorAttachmentOptimal };
        var subpass = new VkSubpassDescription
        {
            PipelineBindPoint = VkPipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            ColorAttachments = &colorReference,
        };
        // Orders the pass's layout transition after the acquire semaphore, which the submit waits on at this stage.
        var dependency = new VkSubpassDependency
        {
            SrcSubpass = VK.SUBPASS_EXTERNAL,
            DstSubpass = 0,
            SrcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            DstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
            DstAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
        };
        RenderPass = Device.CreateRenderPass(
            new VkRenderPassCreateInfo
            {
                AttachmentCount = 1,
                Attachments = &attachment,
                SubpassCount = 1,
                Subpasses = &subpass,
                DependencyCount = 1,
                Dependencies = &dependency,
            },
            null
        );

        // Per swapchain image: a view, a framebuffer, and a "render finished" semaphore (presentation may still be
        // waiting on an image's semaphore, so it's only safe to reuse once that image comes back).
        _imageViews = new VkImageView[images.Length];
        _framebuffers = new VkFramebuffer[images.Length];
        _renderFinished = new VkSemaphore[images.Length];
        for (var i = 0; i < images.Length; i++)
        {
            var imageView = Device.CreateImageView(
                new VkImageViewCreateInfo
                {
                    Image = images[i],
                    ViewType = VkImageViewType._2D,
                    Format = surfaceFormat.Format,
                    SubresourceRange = new VkImageSubresourceRange
                    {
                        AspectMask = VkImageAspectFlags.ColorBit,
                        LevelCount = 1,
                        LayerCount = 1,
                    },
                },
                null
            );
            _imageViews[i] = imageView;
            _framebuffers[i] = Device.CreateFramebuffer(
                new VkFramebufferCreateInfo
                {
                    RenderPass = RenderPass,
                    AttachmentCount = 1,
                    Attachments = &imageView,
                    Width = extent.Width,
                    Height = extent.Height,
                    Layers = 1,
                },
                null
            );
            _renderFinished[i] = Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        }

        _commandPool = Device.CreateCommandPool(
            new VkCommandPoolCreateInfo
            {
                QueueFamilyIndex = queueFamily,
                Flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
            },
            null
        );
        _commandBuffer = Device.AllocateCommandBuffer(
            new VkCommandBufferAllocateInfo { CommandPool = _commandPool, Level = VkCommandBufferLevel.Primary }
        );
        _imageAcquired = Device.CreateSemaphore(new VkSemaphoreCreateInfo(), null);
        _frameFence = Device.CreateFence(VkFenceCreateFlags.SignaledBit, null);

        _lastTime = Glfw.Time;
    }

    /// <summary>
    /// Handles window events, and sleeps while the window is minimised. Returns false once the window should close;
    /// otherwise <paramref name="deltaTime"/> is the time since the last frame, in seconds.
    /// </summary>
    public bool NextFrame(out float deltaTime)
    {
        Glfw.PollEvents();
        while (!GlfwWindow.ShouldClose && (GlfwWindow.FramebufferSize.X == 0 || GlfwWindow.FramebufferSize.Y == 0))
            Glfw.WaitEvents(); // Minimised: nothing to present to, so sleep until something happens.

        var now = Glfw.Time;
        deltaTime = (float)(now - _lastTime);
        _lastTime = now;
        return !GlfwWindow.ShouldClose;
    }

    /// <summary>
    /// Waits for the previous frame, acquires the next swapchain image and begins the render pass on it, cleared to
    /// <paramref name="clearColor"/>. Record your draws into the returned command buffer, then call
    /// <see cref="EndFrame"/>.
    /// </summary>
    public CommandBuffer BeginFrame(float4 clearColor)
    {
        // The window can't be resized, so the swapchain is never rebuilt: ErrorOutOfDateKHR throws and SuboptimalKHR
        // is ignored. A resizable window would recreate the swapchain, its views and framebuffers in both cases.
        Device.WaitForFence(_frameFence, -1);
        Device.ResetFences([_frameFence]);
        Device.AcquireNextImageKHR(_swapchain, -1, _imageAcquired, VkFence.NullHandle, out _imageIndex);

        var clearValue = new VkClearValue { Color = new VkClearColorValue { Float32 = clearColor } };
        _commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
        _commandBuffer.BeginRenderPass(
            new VkRenderPassBeginInfo
            {
                RenderPass = RenderPass,
                Framebuffer = _framebuffers[_imageIndex],
                RenderArea = new VkRect2D(Extent),
                ClearValueCount = 1,
                ClearValues = &clearValue,
            },
            VkSubpassContents.Inline
        );
        return _commandBuffer;
    }

    /// <summary>Ends the render pass, submits the frame and presents it.</summary>
    public void EndFrame()
    {
        _commandBuffer.EndRenderPass();
        _commandBuffer.End();

        var signal = _renderFinished[_imageIndex];
        Queue.Submit(
            [_imageAcquired],
            [VkPipelineStageFlags.ColorAttachmentOutputBit],
            [_commandBuffer],
            [signal],
            _frameFence
        );

        var swapchain = _swapchain;
        var imageIndex = _imageIndex;
        Queue.PresentKHR(
            new VkPresentInfoKHR
            {
                WaitSemaphoreCount = 1,
                WaitSemaphores = &signal,
                SwapchainCount = 1,
                Swapchains = &swapchain,
                ImageIndices = &imageIndex,
            }
        );
    }

    public void Dispose()
    {
        Device.WaitIdle();
        Device.DestroyFence(_frameFence, null);
        Device.DestroySemaphore(_imageAcquired, null);
        Device.DestroyCommandPool(_commandPool, null);
        for (var i = 0; i < _framebuffers.Length; i++)
        {
            Device.DestroySemaphore(_renderFinished[i], null);
            Device.DestroyFramebuffer(_framebuffers[i], null);
            Device.DestroyImageView(_imageViews[i], null);
        }
        Device.DestroyRenderPass(RenderPass, null);
        Device.DestroySwapchainKHR(_swapchain, null);
        Device.Dispose();
        _instance.DestroySurfaceKHR(_surface, null);
        _instance.Dispose();
        GlfwWindow.Dispose();
        Glfw.Terminate();
    }
}
