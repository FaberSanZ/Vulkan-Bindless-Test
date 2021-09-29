using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.GLFW;
using Vortice.ShaderCompiler;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace App
{

    public struct QueueFamilyIndices
    {
        public readonly int GraphicsFamily;
        public readonly int PresentFamily;

        public bool IsComplete => GraphicsFamily >= 0 && PresentFamily >= 0;

        public unsafe QueueFamilyIndices(VkPhysicalDevice device, VkSurfaceKHR surface)
        {
            int graphicsIndex = -1;
            int presentIndex = -1;

            uint queueFamilyCount = 0;
            vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
            VkQueueFamilyProperties* queueFamilies = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
            vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

            for (int i = 0; i < queueFamilyCount; i++)
            {
                VkQueueFamilyProperties q = queueFamilies[i];

                if (q.queueCount > 0 && (q.queueFlags & VkQueueFlags.Graphics) != 0)
                {
                    graphicsIndex = i;
                }

                vkGetPhysicalDeviceSurfaceSupportKHR(device, (uint)i, surface, out VkBool32 presentSupported);
                if (presentIndex < 0 && q.queueCount > 0 && presentSupported)
                {
                    presentIndex = i;
                }


            }

            GraphicsFamily = graphicsIndex;
            PresentFamily = presentIndex;
        }
    }
    public ref struct SwapChainSupportDetails
    {
        public VkSurfaceCapabilitiesKHR capabilities;
        public VkSurfaceFormatKHR[] formats;
        public VkPresentModeKHR[] presentModes;

        public bool IsComplete => formats.Length > 0 && presentModes.Length > 0;

        public unsafe SwapChainSupportDetails(VkPhysicalDevice device, VkSurfaceKHR surface)
        {
            formats = default;
            presentModes = default;
            capabilities = default;

            vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, surface, out capabilities);

            uint formatCount;
            vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, null); // Count
            formats = new VkSurfaceFormatKHR[formatCount];
            fixed (VkSurfaceFormatKHR* formatsPtr = formats)
            {
                vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, formatsPtr);
            }


            uint presentModeCount;
            vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModeCount, null); //Count 
            presentModes = new VkPresentModeKHR[presentModeCount];
            fixed (VkPresentModeKHR* presentsPtr = presentModes)
            {
                vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModeCount, presentsPtr);
            }

        }
    }

    public unsafe class Render
    {
        private Glfw glfw;
        private WindowHandle* windowImpl;
        private VkInstance instance;

        internal VkDebugUtilsMessengerEXT _debugMessenger = VkDebugUtilsMessengerEXT.Null;
        internal uint VK_SUBPASS_EXTERNAL = ~0U;
        internal int height;
        internal int width;
        internal IntPtr handle;
        internal VkPhysicalDevice physicalDevice;
        internal VkDevice device;
        internal VkQueue graphicsQueue;
        internal VkQueue presentQueue;
        internal VkSurfaceKHR surface;
        internal VkSwapchainKHR swapChain;
        internal VkImage[] swapChainImages;
        internal VkFormat swapChainImageFormat;
        internal VkExtent2D swapChainExtent;
        internal VkImageView[] swapChainImageViews;
        internal VkRenderPass renderPass;
        internal VkPipelineLayout pipelineLayout;
        internal VkPipeline graphicsPipeline;
        internal VkFramebuffer[] swapChainFramebuffers;
        internal VkCommandPool commandPool;
        internal VkCommandBuffer[] commandBuffers;
        internal VkSemaphore imageAvailableSemaphore;
        internal VkSemaphore renderFinishedSemaphore;


        private const int Width = 800;
        private const int Height = 600;
        private string Title = "Vulkan Ray";

        public void Run()
        {
            InitWindow();
            InitVulkan();
            MainLoop();
            CleanUp();
        }

        private void InitWindow()
        {
            glfw = Glfw.GetApi();

            glfw.Init();
            glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
            glfw.WindowHint(WindowHintBool.Resizable, false);

            windowImpl = glfw.CreateWindow(Width, Height, Title, null, null);

        }

        private void InitVulkan()
        {
            CreateInstance();
            CreateSurface();
            CreatePhysicalDevice();
            CreateLogicalDevice();
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateGraphicsPipeline();
            CreateFrameBuffers();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateSemaphores();
        }

        private void CreateInstance()
        {
            vkInitialize();

            VkApplicationInfo appInfo = new();
            appInfo.sType = VkStructureType.ApplicationInfo;
            appInfo.applicationVersion = new VkVersion(1, 0, 0);
            appInfo.engineVersion = new VkVersion(1, 0, 0);
            appInfo.apiVersion = VkVersion.Version_1_0;

            VkInstanceCreateInfo createInfo = new();
            createInfo.sType = VkStructureType.InstanceCreateInfo;
            createInfo.pApplicationInfo = &appInfo;

            string[] arr = new[]
            {
                "VK_KHR_win32_surface",
                "VK_KHR_surface",
                "VK_KHR_get_physical_device_properties2",
            };

            VkStringArray ext = new VkStringArray(arr);

            createInfo.enabledExtensionCount = ext.Length;
            createInfo.ppEnabledExtensionNames = (byte**)ext;

            //createInfo.enabledLayerCount = 0;

            vkCreateInstance(&createInfo, null, out instance).CheckResult();
            vkLoadInstance(instance);
        }



        private void CreateSurface()
        {

            GlfwNativeWindow glfw_native = new(glfw, windowImpl);

            VkWin32SurfaceCreateInfoKHR windowsSurfaceInfo = new VkWin32SurfaceCreateInfoKHR
            {
                sType = VkStructureType.Win32SurfaceCreateInfoKHR,
                hwnd = glfw_native.Win32.Value.Hwnd,
                hinstance = glfw_native.Win32.Value.HInstance
            };

            vkCreateWin32SurfaceKHR(instance, &windowsSurfaceInfo, null, out surface).CheckResult();
        }

        internal unsafe ReadOnlySpan<VkExtensionProperties> Instance_Extensions()
        {
            uint count = 0;
            vkEnumerateInstanceExtensionProperties(null, &count, null).CheckResult();

            ReadOnlySpan<VkExtensionProperties> properties = new VkExtensionProperties[count];
            fixed (VkExtensionProperties* ptr = properties)
            {
                vkEnumerateInstanceExtensionProperties(null, &count, ptr).CheckResult();
            }

            return properties;
        }


        private void CreatePhysicalDevice()
        {


            uint device_count = 0;
            vkEnumeratePhysicalDevices(instance, &device_count, null);
            VkPhysicalDevice* physicalDevicesPtr = stackalloc VkPhysicalDevice[(int)device_count];
            vkEnumeratePhysicalDevices(instance, &device_count, physicalDevicesPtr);

            for (int i = 0; i < device_count; i++)
            {
                vkGetPhysicalDeviceProperties(physicalDevicesPtr[i], out var properties);
                string deviceName = properties.GetDeviceName();

                if (properties.deviceType != VkPhysicalDeviceType.IntegratedGpu)
                {
                    VkPhysicalDeviceAccelerationStructureFeaturesKHR rtAccelerationFeatures = new()
                    {
                        sType = VkStructureType.PhysicalDeviceAccelerationStructureFeaturesKHR
                    };

                    VkPhysicalDeviceFeatures2 deviceFeatures2 = new();
                    deviceFeatures2.sType = VkStructureType.PhysicalDeviceFeatures2;
                    deviceFeatures2.pNext = &rtAccelerationFeatures;

                    vkGetPhysicalDeviceFeatures2KHR(physicalDevicesPtr[i], out deviceFeatures2);
                    Console.WriteLine("Use " + deviceName);

                    if (rtAccelerationFeatures.accelerationStructure)
                    {
                        physicalDevice = physicalDevicesPtr[i];
                        break;
                    }
                }

 

            }
        }



        private void CreateLogicalDevice()
        {
            QueueFamilyIndices indices = new QueueFamilyIndices(physicalDevice, surface);

            float priority = 1f;
            VkDeviceQueueCreateInfo* queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[]
            {
                new VkDeviceQueueCreateInfo()
                {
                    sType = VkStructureType.DeviceQueueCreateInfo,
                    queueFamilyIndex = (uint)indices.GraphicsFamily,
                    queueCount = 1,
                    pQueuePriorities = &priority,
                }
            };


            string[] layers = new string[]
            {
                "VK_KHR_swapchain",
                "VK_KHR_acceleration_structure",
                "VK_KHR_ray_tracing_pipeline",
                "VK_EXT_descriptor_indexing",
                "VK_KHR_buffer_device_address",
                "VK_KHR_deferred_host_operations",
                "VK_KHR_spirv_1_4",
                "VK_KHR_shader_float_controls",
            };

            byte** extensionsToEnableArray = new VkStringArray(layers);

            VkPhysicalDeviceFeatures deviceFeatures = new VkPhysicalDeviceFeatures();



            // chain multiple features required for RT into deviceInfo.pNext

            // require buffer device address feature
            VkPhysicalDeviceBufferDeviceAddressFeatures deviceBufferDeviceAddressFeatures = new VkPhysicalDeviceBufferDeviceAddressFeatures()
            {
                sType = VkStructureType.PhysicalDeviceBufferDeviceAddressFeatures,
                bufferDeviceAddress = true,
                pNext = null,
            };

            // require ray tracing pipeline feature
            VkPhysicalDeviceRayTracingPipelineFeaturesKHR deviceRayTracingPipelineFeatures = new VkPhysicalDeviceRayTracingPipelineFeaturesKHR()
            {
                sType = VkStructureType.PhysicalDeviceRayTracingPipelineFeaturesKHR,
                pNext = &deviceBufferDeviceAddressFeatures,
                rayTracingPipeline = true,
            };

            // require acceleration structure feature
            VkPhysicalDeviceAccelerationStructureFeaturesKHR deviceAccelerationStructureFeatures = new VkPhysicalDeviceAccelerationStructureFeaturesKHR()
            {
                sType = VkStructureType.PhysicalDeviceAccelerationStructureFeaturesKHR,
                accelerationStructure = true,
                pNext = &deviceRayTracingPipelineFeatures,
            };

            VkPhysicalDeviceVulkan12Features deviceVulkan12Features = new VkPhysicalDeviceVulkan12Features()
            {
                sType = VkStructureType.PhysicalDeviceVulkan12Features,
                pNext = &deviceAccelerationStructureFeatures,
                bufferDeviceAddress = true,
            };



            VkDeviceCreateInfo createInfo = new VkDeviceCreateInfo()
            {
                sType = VkStructureType.DeviceCreateInfo,
                ppEnabledExtensionNames = extensionsToEnableArray,
                enabledExtensionCount = 8, // TODO: swapchain
                pQueueCreateInfos = queueCreateInfos,
                queueCreateInfoCount = 1,
                pNext = &deviceVulkan12Features,
                pEnabledFeatures = &deviceFeatures,
            };

            vkCreateDevice(physicalDevice, &createInfo, null, out device);

            vkGetDeviceQueue(device, (uint)indices.GraphicsFamily, 0, out graphicsQueue);
            vkGetDeviceQueue(device, (uint)indices.PresentFamily, 0, out presentQueue);


        }



        private void CreateSwapChain()
        {
            SwapChainSupportDetails swapChainSupport = new SwapChainSupportDetails(physicalDevice, surface);

            VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = ChooseSwapExtent(swapChainSupport.capabilities);

            uint imageCount = swapChainSupport.capabilities.minImageCount + 1;
            if (swapChainSupport.capabilities.maxImageCount > 0 && imageCount > swapChainSupport.capabilities.maxImageCount)
            {
                imageCount = Math.Min(imageCount, swapChainSupport.capabilities.maxImageCount);
            }

            VkSwapchainCreateInfoKHR createInfo = new VkSwapchainCreateInfoKHR()
            {
                sType = VkStructureType.SwapchainCreateInfoKHR,
                surface = surface,
                minImageCount = imageCount,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                preTransform = swapChainSupport.capabilities.currentTransform,
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
            };

            QueueFamilyIndices indices = new QueueFamilyIndices(physicalDevice, surface);

            uint* QueueFamilyIndicesPtr = stackalloc uint[]
            {
                (uint)indices.GraphicsFamily,
                (uint)indices.PresentFamily,
            };

            if (indices.GraphicsFamily != indices.PresentFamily)
            {
                createInfo.imageSharingMode = VkSharingMode.Concurrent;
                createInfo.pQueueFamilyIndices = QueueFamilyIndicesPtr;
            }
            else
            {
                createInfo.imageSharingMode = VkSharingMode.Exclusive;
            }

            vkCreateSwapchainKHR(device, &createInfo, null, out swapChain);


            vkGetSwapchainImagesKHR(device, swapChain, &imageCount, null);
            swapChainImages = new VkImage[imageCount];

            fixed (VkImage* img = swapChainImages)
                vkGetSwapchainImagesKHR(device, swapChain, &imageCount, img);

            swapChainImageFormat = surfaceFormat.format;
            swapChainExtent = extent;
        }

        // SRGB TODO: Correct?
        private VkSurfaceFormatKHR ChooseSwapSurfaceFormat(VkSurfaceFormatKHR[] formats)
        {
            if (formats.Length == 1 && formats[0].format == VkFormat.Undefined)
            {
                return new VkSurfaceFormatKHR()
                {
                    format = VkFormat.B8G8R8A8UNorm,// 32 BITS BGRA
                    colorSpace = VkColorSpaceKHR.SrgbNonLinear
                };
            }

            foreach (VkSurfaceFormatKHR availableFormat in formats)
            {
                if (availableFormat.format == VkFormat.B8G8R8A8UNorm && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
                {
                    return availableFormat;
                }
            }

            return formats[0];
        }

        private VkPresentModeKHR ChooseSwapPresentMode(VkPresentModeKHR[] presentModes)
        {
            //VkPresentModeKHR bestMode = VkPresentModeKHR.FifoKHR;

            foreach (VkPresentModeKHR availablePresentMode in presentModes)
            {
                if (availablePresentMode == VkPresentModeKHR.Mailbox)
                {
                    return availablePresentMode; // MailboxKHR
                }
                else if (availablePresentMode == VkPresentModeKHR.Immediate)
                {
                    return availablePresentMode; // ImmediateKHR;
                }
            }

            return VkPresentModeKHR.Immediate;
        }

        private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.currentExtent.width != int.MaxValue)
            {
                return capabilities.currentExtent;
            }

            return new VkExtent2D()
            {
                width = (uint)Math.Max(capabilities.minImageExtent.width, Math.Min(capabilities.maxImageExtent.width, (uint)width)),
                height = (uint)Math.Max(capabilities.minImageExtent.height, Math.Min(capabilities.maxImageExtent.height, (uint)height)),
            };
        }




        private void CreateImageViews()
        {
            swapChainImageViews = new VkImageView[swapChainImages.Length];

            for (int i = 0; i < swapChainImages.Length; i++)
            {
                VkImageViewCreateInfo createInfo = new VkImageViewCreateInfo()
                {
                    sType = VkStructureType.ImageViewCreateInfo,
                    image = swapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = swapChainImageFormat,
                    components = new VkComponentMapping()
                    {
                        r = VkComponentSwizzle.Identity,
                        g = VkComponentSwizzle.Identity,
                        b = VkComponentSwizzle.Identity,
                        a = VkComponentSwizzle.Identity,
                    },
                    subresourceRange = new VkImageSubresourceRange()
                    {
                        aspectMask = VkImageAspectFlags.Color,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                vkCreateImageView(device, &createInfo, null, out swapChainImageViews[i]).CheckResult();
            }
        }
        private void CreateRenderPass()
        {
            VkAttachmentDescription colorAttachment = new VkAttachmentDescription()
            {
                format = swapChainImageFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR,
            };

            VkAttachmentReference colorAttachmentRef = new VkAttachmentReference()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal,
            };

            VkSubpassDescription subpass = new VkSubpassDescription()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colorAttachmentRef,
            };

            VkSubpassDependency dependency = new VkSubpassDependency()
            {
                srcSubpass = VK_SUBPASS_EXTERNAL,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = 0,

                dstSubpass = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
            };

            VkRenderPassCreateInfo renderPassInfo = new VkRenderPassCreateInfo()
            {
                sType = VkStructureType.RenderPassCreateInfo,
                attachmentCount = 1,
                pAttachments = &colorAttachment,
                subpassCount = 1,
                pSubpasses = &subpass,
                dependencyCount = 1,
                pDependencies = &dependency,
            };

            vkCreateRenderPass(device, &renderPassInfo, null, out VkRenderPass newRenderPass);
            renderPass = newRenderPass;
        }


        private byte[] LoadFromFile(string path, ShaderKind kind)
        {
            using Compiler compiler = new();
            using var result = compiler.Compile(File.ReadAllText(path), string.Empty, kind);

            return result.GetBytecode().ToArray();
        }

        private void CreateGraphicsPipeline()
        {
            // Shader stages
            byte[] vertShaderCode = LoadFromFile("Shaders/shader.vert", ShaderKind.VertexShader);
            byte[] fragShaderCode = LoadFromFile("Shaders/shader.frag", ShaderKind.FragmentShader);

            VkShaderModule vertShaderModule = CreateShaderModule(vertShaderCode);
            VkShaderModule fragShaderModule = CreateShaderModule(fragShaderCode);

            string name = "main";
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(name);
            byte* utf8Ptr = stackalloc byte[byteCount];

            fixed (char* namePtr = name)
            {
                System.Text.Encoding.UTF8.GetBytes(namePtr, name.Length, utf8Ptr, byteCount);
            }

            VkPipelineShaderStageCreateInfo vertShaderStageInfo = new VkPipelineShaderStageCreateInfo()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Vertex,
                module = vertShaderModule,
                pName = utf8Ptr,
            };

            VkPipelineShaderStageCreateInfo fragShaderStageInfo = new VkPipelineShaderStageCreateInfo()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Fragment,
                module = fragShaderModule,
                pName = utf8Ptr,
            };

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] { vertShaderStageInfo, fragShaderStageInfo };

            // VertexInput
            VkPipelineVertexInputStateCreateInfo vertexInputInfo = new VkPipelineVertexInputStateCreateInfo()
            {
                sType = VkStructureType.PipelineVertexInputStateCreateInfo,
                vertexBindingDescriptionCount = 0,
                pVertexBindingDescriptions = null,
                vertexAttributeDescriptionCount = 0,
                pVertexAttributeDescriptions = null,
            };

            VkPipelineInputAssemblyStateCreateInfo inputAssembly = new VkPipelineInputAssemblyStateCreateInfo()
            {
                sType = VkStructureType.PipelineInputAssemblyStateCreateInfo,
                topology = VkPrimitiveTopology.TriangleList,
                primitiveRestartEnable = false,
            };

            VkViewport viewport = new()
            {
                x = 0f,
                y = 0f,
                width = swapChainExtent.width,
                height = swapChainExtent.height,
                minDepth = 0f,
                maxDepth = 1f,
            };

            VkRect2D scissor = new()
            {
                offset = new() 
                { 
                    x = 0, 
                    y = 0 
                },
                extent = swapChainExtent,
            };

            VkPipelineViewportStateCreateInfo viewportState = new VkPipelineViewportStateCreateInfo()
            {
                sType = VkStructureType.PipelineViewportStateCreateInfo,
                viewportCount = 1,
                pViewports = &viewport,
                scissorCount = 1,
                pScissors = &scissor,
            };

            VkPipelineRasterizationStateCreateInfo rasterizer = new VkPipelineRasterizationStateCreateInfo()
            {
                sType = VkStructureType.PipelineRasterizationStateCreateInfo,
                depthClampEnable = false,
                rasterizerDiscardEnable = false,
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1f,
                cullMode = VkCullModeFlags.Back,
                frontFace = VkFrontFace.Clockwise,
                depthBiasEnable = false,
                depthBiasConstantFactor = 0f,
                depthBiasClamp = 0f,
                depthBiasSlopeFactor = 0f,
            };

            VkPipelineMultisampleStateCreateInfo multisampling = new VkPipelineMultisampleStateCreateInfo()
            {
                sType = VkStructureType.PipelineMultisampleStateCreateInfo,
                sampleShadingEnable = false,
                rasterizationSamples = VkSampleCountFlags.Count1,
                minSampleShading = 1f,
                pSampleMask = null,
                alphaToCoverageEnable = false,
                alphaToOneEnable = false,
            };

            VkPipelineColorBlendAttachmentState colorBlendAttachment = new VkPipelineColorBlendAttachmentState()
            {
                colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A,


                blendEnable = false,
            };

            float* blendConstants = stackalloc float[4]
            {
                0,
                0,
                0,
                0
            };
            VkPipelineColorBlendStateCreateInfo colorBlending = new VkPipelineColorBlendStateCreateInfo()
            {
                sType = VkStructureType.PipelineColorBlendStateCreateInfo,
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy,
                pAttachments = &colorBlendAttachment,
                attachmentCount = 1,
                //blendConstants = blendConstants

            };

            VkPipelineLayoutCreateInfo pipelineLayoutInfo = new VkPipelineLayoutCreateInfo()
            {
                sType = VkStructureType.PipelineLayoutCreateInfo,
                setLayoutCount = 0,
                pushConstantRangeCount = 0,
            };

            vkCreatePipelineLayout(device, &pipelineLayoutInfo, null, out VkPipelineLayout newPipelineLayout);
            pipelineLayout = newPipelineLayout;

            VkGraphicsPipelineCreateInfo pipelineInfo = new VkGraphicsPipelineCreateInfo()
            {
                sType = VkStructureType.GraphicsPipelineCreateInfo,
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputInfo,
                pInputAssemblyState = &inputAssembly,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizer,
                pMultisampleState = &multisampling,
                pDepthStencilState = null,
                pColorBlendState = &colorBlending,
                pDynamicState = null,
                layout = pipelineLayout,
                renderPass = renderPass,
                subpass = 0,
                basePipelineHandle = 0,
                basePipelineIndex = -1,
            };

            VkPipeline newPipeline;
            vkCreateGraphicsPipelines(device, 0, 1, &pipelineInfo, null, &newPipeline);
            graphicsPipeline = newPipeline;

            vkDestroyShaderModule(device, vertShaderModule, null);
            vkDestroyShaderModule(device, fragShaderModule, null);
        }

        private VkShaderModule CreateShaderModule(byte[] shaderCode)
        {
            VkShaderModuleCreateInfo createInfo = new VkShaderModuleCreateInfo()
            {
                sType = VkStructureType.ShaderModuleCreateInfo,
                codeSize = (UIntPtr)shaderCode.Length,
            };

            fixed (byte* sourcePointer = shaderCode)
            {
                createInfo.pCode = (uint*)sourcePointer;
            }

            vkCreateShaderModule(device, &createInfo, null, out VkShaderModule newShaderModule);

            return newShaderModule;
        }

        private void CreateFrameBuffers()
        {
            swapChainFramebuffers = new VkFramebuffer[swapChainImageViews.Length];

            for (int i = 0; i < swapChainImageViews.Length; i++)
            {
                VkImageView* attachments = stackalloc VkImageView[] { swapChainImageViews[i] };

                VkFramebufferCreateInfo frameBufferInfo = new VkFramebufferCreateInfo()
                {
                    sType = VkStructureType.FramebufferCreateInfo,
                    renderPass = renderPass,
                    attachmentCount = 1,
                    pAttachments = attachments,
                    width = (uint)swapChainExtent.width,
                    height = (uint)swapChainExtent.height,
                    layers = 1,
                };

                vkCreateFramebuffer(device, &frameBufferInfo, null, out swapChainFramebuffers[i]).CheckResult();
            }
        }

        private void CreateCommandPool()
        {
            QueueFamilyIndices indices = new QueueFamilyIndices(physicalDevice, surface);

            VkCommandPoolCreateInfo poolInfo = new VkCommandPoolCreateInfo()
            {
                sType = VkStructureType.CommandPoolCreateInfo,
                queueFamilyIndex = (uint)indices.GraphicsFamily,
                flags = 0,
            };

            vkCreateCommandPool(device, &poolInfo, null, out VkCommandPool newCommandPool);
            commandPool = newCommandPool;
        }

        private void CreateCommandBuffers()
        {
            VkCommandBufferAllocateInfo allocInfo = new VkCommandBufferAllocateInfo()
            {
                sType = VkStructureType.CommandBufferAllocateInfo,
                commandPool = commandPool,
                level = VkCommandBufferLevel.Primary,
                commandBufferCount = (uint)swapChainFramebuffers.Length,
            };

            commandBuffers = new VkCommandBuffer[swapChainFramebuffers.Length];

            fixed (VkCommandBuffer* newCommandBuffer = commandBuffers)
            {
                vkAllocateCommandBuffers(device, &allocInfo, newCommandBuffer);
            }

            for (int i = 0; i < commandBuffers.Length; i++)
            {
                VkCommandBuffer commandBuffer = commandBuffers[i];

                VkCommandBufferBeginInfo beginInfo = new VkCommandBufferBeginInfo()
                {
                    sType = VkStructureType.CommandBufferBeginInfo,
                    pNext = null,
                    flags = VkCommandBufferUsageFlags.SimultaneousUse,
                    pInheritanceInfo = null,
                };

                vkBeginCommandBuffer(commandBuffer, &beginInfo);

                VkClearValue clearColor = new VkClearValue()
                {
                    color = new VkClearColorValue(0, .2f, 0.4f, 1f),
                    //depthStencil = new VkClearDepthStencilValue(1, 0)
                };

                VkRenderPassBeginInfo renderPassInfo = new VkRenderPassBeginInfo()
                {
                    sType = VkStructureType.RenderPassBeginInfo,
                    renderPass = renderPass,
                    framebuffer = swapChainFramebuffers[i],
                    renderArea = new VkRect2D
                    {
                        extent = swapChainExtent 
                    },
                    pClearValues = &clearColor,
                    clearValueCount = 1,
                };

                vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
                vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, graphicsPipeline);
                vkCmdDraw(commandBuffer, 3, 1, 0, 0);
                vkCmdEndRenderPass(commandBuffer);
                vkEndCommandBuffer(commandBuffer);
            }
        }

        private void CreateSemaphores()
        {
            VkSemaphoreCreateInfo semaphoreInfo = new VkSemaphoreCreateInfo()
            {
                sType = VkStructureType.SemaphoreCreateInfo,
            };

            vkCreateSemaphore(device, &semaphoreInfo, null, out VkSemaphore vkSemaphore);
            imageAvailableSemaphore = vkSemaphore;

            vkCreateSemaphore(device, &semaphoreInfo, null, out vkSemaphore);
            renderFinishedSemaphore = vkSemaphore;
        }



        public void DrawFrame()
        {
            vkQueueWaitIdle(presentQueue);

            vkAcquireNextImageKHR(device, swapChain, ulong.MaxValue, imageAvailableSemaphore, 0, out uint imageIndex);




            VkSemaphore* waitSemaphores = stackalloc VkSemaphore[]
            {
                imageAvailableSemaphore
            };

            VkPipelineStageFlags* waitStages = stackalloc VkPipelineStageFlags[]
            {
                VkPipelineStageFlags.ColorAttachmentOutput
            };

            VkSemaphore* signalSemaphores = stackalloc VkSemaphore[]
            {
                renderFinishedSemaphore
            };

            VkCommandBuffer* commandBuffers = stackalloc VkCommandBuffer[]
            {
                this.commandBuffers[imageIndex] // TODO
            };
            VkSubmitInfo submitInfo = new VkSubmitInfo()
            {
                sType = VkStructureType.SubmitInfo,
                pWaitSemaphores = waitSemaphores,
                waitSemaphoreCount = 1,
                pWaitDstStageMask = waitStages,
                commandBufferCount = 1,
                pCommandBuffers = commandBuffers,
                signalSemaphoreCount = 1,
                pSignalSemaphores = signalSemaphores,
            };

            vkQueueSubmit(graphicsQueue, 1, &submitInfo, 0);



            VkSwapchainKHR* swapChains = stackalloc VkSwapchainKHR[]
            {
                swapChain
            };
            VkPresentInfoKHR presentInfo = new VkPresentInfoKHR()
            {
                sType = VkStructureType.PresentInfoKHR,
                waitSemaphoreCount = 1,
                pWaitSemaphores = signalSemaphores,
                swapchainCount = 1,
                pSwapchains = swapChains,
                pImageIndices = &imageIndex,
            };

            vkQueuePresentKHR(presentQueue, &presentInfo);
        }


        private static VkBool32 DebugMessengerCallback(VkDebugUtilsMessageSeverityFlagsEXT messageSeverity, VkDebugUtilsMessageTypeFlagsEXT messageTypes, VkDebugUtilsMessengerCallbackDataEXT* pCallbackData, IntPtr userData)
        {
            

            return VkBool32.False;
        }

        private static readonly string[] s_RequestedValidationLayers = new[] { "VK_LAYER_KHRONOS_validation" };
        private static void FindValidationLayers(List<string> appendTo)
        {
            ReadOnlySpan<VkLayerProperties> availableLayers = vkEnumerateInstanceLayerProperties();

            for (int i = 0; i < s_RequestedValidationLayers.Length; i++)
            {
                bool hasLayer = false;
                for (int j = 0; j < availableLayers.Length; j++)
                {
                    if (s_RequestedValidationLayers[i] == availableLayers[j].GetLayerName())
                    {
                        hasLayer = true;
                        break;
                    }
                }

                if (hasLayer)
                {
                    appendTo.Add(s_RequestedValidationLayers[i]);
                }
                else
                {
                    // TODO: Warn
                }
            }
        }




        public void DeviceWaitIdle()
        {
            vkDeviceWaitIdle(device);
        }

        public void Dispose()
        {
            vkDestroySemaphore(device, renderFinishedSemaphore, null);
            vkDestroySemaphore(device, imageAvailableSemaphore, null);
            vkDestroyCommandPool(device, commandPool, null);
            foreach (VkFramebuffer framebuffer in swapChainFramebuffers)
            {
                vkDestroyFramebuffer(device, framebuffer, null);
            }
            vkDestroyPipeline(device, graphicsPipeline, null);
            vkDestroyPipelineLayout(device, pipelineLayout, null);
            vkDestroyRenderPass(device, renderPass, null);
            foreach (VkImageView imageView in swapChainImageViews)
            {
                vkDestroyImageView(device, imageView, null);
            }
            vkDestroySwapchainKHR(device, swapChain, null);
            vkDestroyDevice(device, null);
            vkDestroySurfaceKHR(instance, surface, null);
            vkDestroyDebugUtilsMessengerEXT(instance, _debugMessenger, null);
            vkDestroyInstance(instance, null);
        }
        private void MainLoop()
        {
            while (!glfw.WindowShouldClose(windowImpl))
            {
                DrawFrame();
                glfw.PollEvents();
            }
        }

        private void CleanUp()
        {
            glfw.DestroyWindow(windowImpl);
            glfw.Terminate();
        }

    }
}
