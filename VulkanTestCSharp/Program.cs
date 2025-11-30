using Evergine.Bindings.Vulkan;
using Microsoft.FSharp.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using System.Linq;

namespace VulkanTestCSharp;

public struct QueueFamilyIndices
{
    public uint? GraphicsFamily { get; set; }
    public uint? PresentFamily { get; set; }

    public bool IsComplete()
    {
        return GraphicsFamily.HasValue && PresentFamily.HasValue;
    }
}

public unsafe struct SwapChainSupportDetails {
    public VkSurfaceCapabilitiesKHR Capabilities { get; set; }
    public VkSurfaceFormatKHR[] Formats { get; set; }
    public VkPresentModeKHR[] PresentModes { get; set; }
}

public unsafe class Vulkan
{
    // private VkInstance instance = VkInstance.Null;
    // private VkPhysicalDevice physicalDevice = VkPhysicalDevice.Null;
    // private VkDevice device = VkDevice.Null;
    // private VkQueue graphicsQueue = VkQueue.Null;

//     private WindowHandle* window;
//     private const int Width = 800;
//     private const int Height = 600;
//
//     private Glfw glfw = Glfw.GetApi();
//     private readonly AnsiString[] validationLayers = [ new("VK_LAYER_KHRONOS_validation") ];
// #if DEBUG
//     private const bool enableValidationLayers = true;
// #else
//     private const bool enableValidationLayers = false;
// #endif

    // public void Run()
    // {
    //     glfw.Init();
    //     glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
    //     glfw.WindowHint(WindowHintBool.Resizable, false);
    //     window = glfw.CreateWindow(Width, Height, "Vulkan C#", null, null);
    //
    //     CreateInstance();
    //     PickPhysicalDevice();
    //     CreateLogicalDevice();
    //
    //     MainLoop();
    // }
    //
    // private void MainLoop()
    // {
    //     while (!glfw.WindowShouldClose(window))
    //     {
    //         glfw.PollEvents();
    //     }
    // }
    //
    // public void Dispose()
    // {
    //     VulkanNative.vkDestroyDevice(device, null);
    //     VulkanNative.vkDestroyInstance(instance, null);
    //     glfw.DestroyWindow(window);
    //     glfw.Terminate();
    // }

    public static Result<VkInstance> CreateInstance(Glfw glfw, UnmanagedString appName, UnmanagedString engineName, FSharpValueOption<UnmanagedString[]> validationLayers)
    {
        if (validationLayers.IsSome)
        {
            var r = CheckValidationLayerSupport(validationLayers.Value);
            if (!r.IsOk) return r.Error;
            if (r.IsOk && !r.Value) return "validation layers requested, but not available!";
        }

        var appInfo = new VkApplicationInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
            pApplicationName = appName.Pointer,
            applicationVersion = Helpers.Version(1, 0, 0),
            pEngineName = engineName.Pointer,
            engineVersion = Helpers.Version(1, 0, 0),
            apiVersion = Helpers.Version(1, 4, 0)
        };

        var createInfo = new VkInstanceCreateInfo
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
            pApplicationInfo = &appInfo
        };

        // Load extensions
        var extensions = glfw.GetRequiredInstanceExtensions(out var extensionsCount);
        createInfo.enabledExtensionCount = extensionsCount;
        createInfo.ppEnabledExtensionNames = extensions;

        // Load layers
        if (validationLayers.IsSome)
        {
            var validationLayersArray = stackalloc byte*[validationLayers.Value.Length];
            for (var i = 0; i < validationLayers.Value.Length; i++)
                validationLayersArray[i] = validationLayers.Value[i].Pointer;

            createInfo.enabledLayerCount = (uint)validationLayers.Value.Length;
            createInfo.ppEnabledLayerNames = validationLayersArray;
        } else {
            createInfo.enabledLayerCount = 0;
        }

        // Create instance
        VkInstance instance;
        var res = VulkanNative.vkCreateInstance(&createInfo, null, &instance);
        return res != VkResult.VK_SUCCESS ? "Failed to create instance." : instance;
    }

    public static Result<VkSurfaceKHR> CreateSurface(Glfw glfw, VkInstance instance, WindowHandle* window)
    {
        var surface = new VkNonDispatchableHandle();
        var vkHandle = new VkHandle(instance.Handle);
        var res = glfw.CreateWindowSurface(vkHandle, window, null, &surface);
        if ((VkResult)res != VkResult.VK_SUCCESS
            || surface.Handle == VkSurfaceKHR.Null.Handle) return "Failed to create surface";

        return new VkSurfaceKHR(surface.Handle);
    }

    private static Result<bool> CheckValidationLayerSupport(UnmanagedString[] validationLayers)
    {
        uint layerCount = 0;
        var res = VulkanNative.vkEnumerateInstanceLayerProperties(&layerCount, null);
        if (res != VkResult.VK_SUCCESS) return "Failed to get load layer properties.";

        var availableLayers = stackalloc VkLayerProperties[(int)layerCount];
        var res2 = VulkanNative.vkEnumerateInstanceLayerProperties(&layerCount, availableLayers);
        if (res2 != VkResult.VK_SUCCESS) return "Failed to get load layer properties.";

        foreach (var layer in validationLayers)
        {
            var layerFound = false;

            for (var i = 0; i < layerCount; i++)
            {
                var layerName = Helpers.GetString(availableLayers[i].layerName);
                if (layer.Equals(layerName)) continue;
                layerFound = true;
                break;
            }

            if (!layerFound) return true;
        }

        return true;
    }

    public static Result<(VkPhysicalDevice, QueueFamilyIndices)> PickPhysicalDevice(
        VkInstance instance,
        VkSurfaceKHR surface,
        UnmanagedString[] deviceExtensions
    )
    {
        var deviceCount = 0u;
        var r1 = VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, null);
        if (r1 != VkResult.VK_SUCCESS) return "Failed to enumerate physical devices.";
        if (deviceCount == 0) return "No physical devices found!";

        var devices = stackalloc VkPhysicalDevice[(int)deviceCount];
        var r2 = VulkanNative.vkEnumeratePhysicalDevices(instance, &deviceCount, devices);
        if (r2 != VkResult.VK_SUCCESS) return "Failed to enumerate physical devices.";

        var device = VkPhysicalDevice.Null;
        var indices = default(QueueFamilyIndices);
        for (var i = 0; i < deviceCount; i++)
        {
            if (!IsDeviceSuitable(devices[i], surface, deviceExtensions, out indices)) continue;
            device = devices[i];
            break;
        }

        return device == VkPhysicalDevice.Null
            ? "Failed to find a suitable GPU."
            : (device, indices);
    }

    private static bool IsDeviceSuitable(
        VkPhysicalDevice device,
        VkSurfaceKHR surface,
        UnmanagedString[] extensions,
        out QueueFamilyIndices queueFamilyIndices
    )
    {
        // VkPhysicalDeviceProperties properties;
        // VulkanNative.vkGetPhysicalDeviceProperties(device, &properties);
        // Features
        VkPhysicalDeviceFeatures features;
        VulkanNative.vkGetPhysicalDeviceFeatures(device, &features);

        // Queues
        queueFamilyIndices = FindQueueFamilies(device, surface);

        // Swap chain support
        var swapChainDetails = QuerySwapChainSupport(device, surface);
        if (swapChainDetails.IsError) ;
        var swapChainAdequate = false;
        if (swapChainDetails.)

        // Console.WriteLine($"{device.Handle}: {properties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU}; {features.geometryShader}; {indices.IsComplete()}");
        // properties.deviceType == VkPhysicalDeviceType.VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU
        return features.geometryShader
               && queueFamilyIndices.IsComplete()
               && CheckDeviceExtensionSupport(device, extensions)
               && swapChainAdequate;
    }

    private static bool CheckDeviceExtensionSupport(VkPhysicalDevice device, UnmanagedString[] extensions)
    {
        // Retrieve available extensions
        var extensionCount = 0u;
        VulkanNative.vkEnumerateDeviceExtensionProperties(device, null, &extensionCount, null);
        var availableExtensionsData = stackalloc VkExtensionProperties[(int)extensionCount];
        VulkanNative.vkEnumerateDeviceExtensionProperties(device, null, &extensionCount, availableExtensionsData);

        var availableExtensions = new Span<VkExtensionProperties>(availableExtensionsData, (int)extensionCount);

        foreach (var extension in extensions)
        {
            var isExtensionAvailable = false;
            foreach (var availableExtension in availableExtensions)
                isExtensionAvailable = isExtensionAvailable || extension.Equals(availableExtension.extensionName);

            if (!isExtensionAvailable) return false;
        }

        return true;
    }

    private static Result<QueueFamilyIndices> FindQueueFamilies(VkPhysicalDevice device, VkSurfaceKHR surface)
    {
        var indices = new QueueFamilyIndices();

        // Retrieve queue families
        var queueFamilyCount = 0u;
        VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
        var queueFamilies = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
        VulkanNative.vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

        // Find indices
        for (var i = 0u; i < queueFamilyCount; i++)
        {
            if (queueFamilies[i].queueFlags.HasFlag(VkQueueFlags.VK_QUEUE_GRAPHICS_BIT))
                indices.GraphicsFamily = i;

            var presentSupport = VkBool32.False;
            var res = VulkanNative.vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, &presentSupport);
            if (res != VkResult.VK_SUCCESS) return "Failed to get physical device surface support";
            if (presentSupport) indices.PresentFamily = i;

            if (indices.IsComplete()) break;
        }

        return indices;
    }

    public static Result<(VkDevice, VkQueue, VkQueue)> CreateLogicalDevice(
        VkPhysicalDevice physicalDevice,
        QueueFamilyIndices indices,
        FSharpValueOption<UnmanagedString[]> validationLayers,
        UnmanagedString[] deviceExtensions
    )
    {
        // Queue create infos
        var queueFamilies = new HashSet<uint>(2) {indices.GraphicsFamily!.Value, indices.PresentFamily!.Value};
        var queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[queueFamilies.Count];
        var queuePriority = 1.0f;

        var idx = 0;
        foreach (var queueFamily in queueFamilies)
        {
            var queueCreateInfo = new VkDeviceQueueCreateInfo
            {
                sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO,
                queueFamilyIndex = queueFamily,
                queueCount = 1,
                pQueuePriorities = &queuePriority,
            };

            queueCreateInfos[idx] = queueCreateInfo;
            idx++;
        }

        Console.WriteLine($"{idx} queue families");
        Console.WriteLine($"{queueFamilies.Count} queue families");
        Console.WriteLine($"{queueFamilies} queue families");

        // Prepare extensions array
        var extensions = stackalloc byte*[deviceExtensions.Length];
        for (var i = 0; i < deviceExtensions.Length; i++)
            extensions[i] = deviceExtensions[i].Pointer;

        // Creation info
        var deviceFeatures = new VkPhysicalDeviceFeatures();
        var createInfo = new VkDeviceCreateInfo()
        {
            sType = VkStructureType.VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
            queueCreateInfoCount = (uint)queueFamilies.Count,
            pQueueCreateInfos = queueCreateInfos,
            pEnabledFeatures = &deviceFeatures,
            enabledExtensionCount = (uint)deviceExtensions.Length,
            ppEnabledExtensionNames = extensions
        };

        // Add validation layers
        if (validationLayers.IsSome) {
            var validationLayersArray = stackalloc byte*[validationLayers.Value.Length];
            for (var i = 0; i < validationLayers.Value.Length; i++)
                validationLayersArray[i] = validationLayers.Value[i].Pointer; // TODO: Find a way to release "ToPointer"

            createInfo.enabledLayerCount = (uint)validationLayers.Value.Length;
            createInfo.ppEnabledLayerNames = validationLayersArray;
        } else {
            createInfo.enabledLayerCount = 0;
        }

        // Create device
        var device = VkDevice.Null;
        var res1 = VulkanNative.vkCreateDevice(physicalDevice, &createInfo, null, &device);
        if (res1 != VkResult.VK_SUCCESS || device == VkDevice.Null) return "Failed to create vulkan logical device";

        // Get queues
        var graphicsQueue = VkQueue.Null;
        VulkanNative.vkGetDeviceQueue(device, indices.GraphicsFamily!.Value, 0, &graphicsQueue);
        if (graphicsQueue == VkQueue.Null) return "Failed to get graphics queue";

        var presentQueue = VkQueue.Null;
        VulkanNative.vkGetDeviceQueue(device, indices.PresentFamily!.Value, 0, &presentQueue);
        if (presentQueue == VkQueue.Null) return "Failed to get present queue";

        return (device, graphicsQueue, presentQueue);
    }

    public static Result<SwapChainSupportDetails> QuerySwapChainSupport(
        VkPhysicalDevice device,
        VkSurfaceKHR surface)
    {
        var details = new SwapChainSupportDetails();

        // Retrieve capabilities
        var capabilities = new VkSurfaceCapabilitiesKHR();
        var res = VulkanNative.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, surface, &capabilities);
        if (res != VkResult.VK_SUCCESS) return "Failed to retrieve the capabilities";
        details.Capabilities = capabilities;

        // Retrieve formats
        var formatCount = 0u;
        VulkanNative.vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, null);

        var formats = new VkSurfaceFormatKHR[formatCount];
        fixed (VkSurfaceFormatKHR* formatsPointer = &formats[0])
            VulkanNative.vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, formatsPointer);

        details.Formats = formats;

        // Retrieve present modes
        var presentModesCount = 0u;
        VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModesCount, null);

        var presentModes = new VkPresentModeKHR[presentModesCount];
        fixed (VkPresentModeKHR* presentModesPointer = &presentModes[0])
            VulkanNative.vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModesCount, presentModesPointer);

        details.PresentModes = presentModes;

        return details;
    }

    // private unsafe void GetAvailableExtensions()
    // {
    //     uint extensionsCount;
    //     VulkanNative
    //         .vkEnumerateInstanceExtensionProperties(null, &extensionsCount, null)
    //         .CheckErrors();
    //
    //     var extensions = stackalloc VkExtensionProperties[(int)extensionsCount];
    //     VulkanNative
    //         .vkEnumerateInstanceExtensionProperties(null, &extensionsCount, extensions)
    //         .CheckErrors();
    // }
}
