open System
open System.Runtime.InteropServices
open Evergine.Bindings.Vulkan
open Microsoft.FSharp.NativeInterop
open Silk.NET.GLFW
open FsToolkit.ErrorHandling
open VulkanTestCSharp

// module Result =
//     let inline ofVk error (vkResult: VkResult) =
//         match vkResult with
//         | VkResult.VK_SUCCESS -> Ok ()
//         | _ -> Error error

    // let inline requireVk (vkResult: VkResult) err r =
    //     match r with
    //     | Error err -> Error err
    //     | Ok value ->
    //         match vkResult with
    //         | VkResult.VK_SUCCESS -> Ok value
    //         | _ -> Error err

// [<Struct>]
// type AnsiString =
//     { Pointer: byte nativeptr }
//
//     #nowarn 9
//     static member ofString str =
//         { Pointer =
//             str
//             |> Marshal.StringToHGlobalAnsi
//             |> NativePtr.ofNativeInt }
//
//     static member free ptr =
//         ptr
//         |> NativePtr.toNativeInt
//         |> Marshal.FreeHGlobal
//
//     interface IDisposable with
//         member this.Dispose() = AnsiString.free this.Pointer
//     #warnon 9
//
// [<RequireQualifiedAccess>]
// module VulkanHelpers =
//     let version major minor patch : uint = (major <<< 22) ||| (minor <<< 12) ||| patch

// #nowarn 9
// let createInstance (glfw: Glfw) (validationLayers: string array voption) =
//     match validationLayers with
//     | ValueNone -> ()
//     | ValueSome layers -> () // CheckValidationLayerSupport()
//         // validation layers requested, but not available!
//
//     use appName = "Hello Triangle" |> AnsiString.ofString
//     use engineName = "No engine" |> AnsiString.ofString
//     let appInfo = VkApplicationInfo(
//         sType = VkStructureType.VK_STRUCTURE_TYPE_APPLICATION_INFO,
//         pApplicationName = appName.Pointer,
//         applicationVersion = VulkanHelpers.version 1u 0u 0u,
//         pEngineName = engineName.Pointer,
//         engineVersion = VulkanHelpers.version 1u 0u 0u,
//         apiVersion = VulkanHelpers.version 1u 4u 0u
//     )
//
//     use appInfoPtr = fixed &appInfo
//     let mutable createInfo = VkInstanceCreateInfo(
//         sType = VkStructureType.VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
//         pApplicationInfo = appInfoPtr
//     )
//
//     // Load extensions
//     let mutable extensionsCount = 0u
//     let extensions = glfw.GetRequiredInstanceExtensions(&extensionsCount)
//     createInfo.enabledExtensionCount <- extensionsCount
//     createInfo.ppEnabledExtensionNames <- extensions
//
//     // Load layers
//     let layersToFree =
//         match validationLayers with
//         | ValueNone ->
//             createInfo.enabledLayerCount <- 0u
//             ValueNone
//         | ValueSome layers ->
//             let layersArray = NativePtr.stackalloc layers.Length
//             for i = 0 to layers.Length-1 do
//                 layers[i]
//                 |> AnsiString.ofString
//                 |> _.Pointer
//                 |> NativePtr.set layersArray i
//
//             createInfo.enabledLayerCount <- uint layers.Length
//             createInfo.ppEnabledLayerNames <- layersArray
//             ValueSome struct (layersArray, layers.Length)
//
//     let mutable instance = VkInstance.Null
//     use instancePtr = fixed &instance
//     use createInfoPtr = fixed &createInfo
//     let result = VulkanNative.vkCreateInstance(createInfoPtr, NativePtr.nullPtr, instancePtr)
//
//     // Free memory
//     match layersToFree with
//     | ValueNone -> ()
//     | ValueSome struct (layers, count) ->
//         for i = 0 to count-1 do
//             NativePtr.get layers i |> AnsiString.free
//
//     match result with
//     | VkResult.VK_SUCCESS -> Ok instance
//     | _ -> Error "Failed to create instance"
//
// let isDeviceSuitable (device: VkPhysicalDevice) =
//     true
//
// let pickPhysicalDevice instance =
//     let mutable deviceCount = 0u
//     use deviceCountPtr = fixed &deviceCount
//     let result = VulkanNative.vkEnumeratePhysicalDevices(instance, deviceCountPtr, NativePtr.nullPtr)
//
//     match result with
//     | VkResult.VK_SUCCESS when deviceCount <> 0u ->
//         let devices = NativePtr.stackalloc<VkPhysicalDevice> (int deviceCount)
//         let result = VulkanNative.vkEnumeratePhysicalDevices(instance, deviceCountPtr, devices)
//
//         match result with
//         | VkResult.VK_SUCCESS ->
//             let mutable device = VkPhysicalDevice.Null
//             let mutable i = 0
//             for i = 0 to int deviceCount-1 do
//                 NativePtr.get devices i
//                 |> isDeviceSuitable
//                 |>
//                 device = devices[i];
//                 break;
//             }
//
//             if (device == VkPhysicalDevice.Null) throw new Exception("Failed to find a suitable GPU!");
//             device
//         | _ ->
//
//     | _ -> Error "No physical devices found"
//     // if (deviceCount == 0) throw new Exception("No physical devices found!");
//

let [<Literal>] Width = 800
let [<Literal>] Height = 600

[<RequireQualifiedAccess>]
module Result =
    let ofVk (result: _ Result) =
        match result.IsOk with
        | true -> Ok result.Value
        | false -> Error result.Error

[<EntryPoint>]
let main args =
    let glfw = Glfw.GetApi()

    glfw.Init() |> ignore
    glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi)
    glfw.WindowHint(WindowHintBool.Resizable, false)
    #nowarn 9
    let window = glfw.CreateWindow(Width, Height, "Vulkan F#", NativePtr.nullPtr, NativePtr.nullPtr)
    #warnon 9

    let validationLayers =
        #if DEBUG
        [| new UnmanagedString("VK_LAYER_KHRONOS_validation") |] |> ValueSome
        #else
        ValueNone
        #endif

    let deviceExtensions = [| new UnmanagedString("VK_KHR_swapchain") |]

    result {
        use appName = new UnmanagedString("Vulkan with F#")
        use engineName = new UnmanagedString("No Engine")

        let! instance =
            Vulkan.CreateInstance(
                glfw,
                appName,
                engineName,
                validationLayers
            ) |> Result.ofVk

        let! surface = Vulkan.CreateSurface(glfw, instance, window) |> Result.ofVk

        let! struct (physicalDevice, indices) =
            Vulkan.PickPhysicalDevice(instance, surface, deviceExtensions) |> Result.ofVk

        let! struct (device, graphicsQueue, presentQueue) =
            Vulkan.CreateLogicalDevice(
                physicalDevice,
                indices,
                validationLayers,
                deviceExtensions
            ) |> Result.ofVk

        validationLayers |> ValueOption.iter (Array.iter _.Dispose()) // Dispose validation layer strings
        printfn "Vulkan initialised!:"
        printfn $"    Instance = {instance.Handle}"
        printfn $"    Surface  = {surface.Handle}"
        printfn $"    Device   = {device.Handle}"
        printfn $"    Queues"
        printfn $"        Graphics  = {graphicsQueue.Handle}"
        printfn $"        Present   = {presentQueue.Handle}"

        while not <| glfw.WindowShouldClose(window) do
            glfw.PollEvents()
    }
    |> function
        | Ok _ -> printfn "Exit"; 0
        | Error err -> printfn $"{err}"; 0


    //     VulkanNative.vkDestroyDevice(device, null);
    // vkDestroySurfaceKHR
    //     VulkanNative.vkDestroyInstance(instance, null);
    //     glfw.DestroyWindow(window);
    //     glfw.Terminate();
