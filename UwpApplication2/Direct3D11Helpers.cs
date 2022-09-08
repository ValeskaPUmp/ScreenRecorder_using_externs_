using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.System;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Device = SharpDX.Direct3D11.Device;
using Device3 = SharpDX.Direct3D11.Device3;
using DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags;

namespace UwpApplication1
{
    /// <summary>
    /// Це є моя API ;););)
    /// </summary>
    public unsafe static class Direct3D11Helpers
    {
        private const int t=20;
        static Guid IInspectable = new Guid("AF86E2E0-B12D-4c6a-9C5A-D7AA65101E90");
        static Guid ID3D11Resource = new Guid("dc8e63f3-d12b-4952-b47b-5e45026a862d");
        static Guid IDXGIAdapter3 = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
        static Guid ID3D11Device = new Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140");
        static Guid ID3D11Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface([In] ref Guid iid);
        };
        [DllImport(
            "d3d11.dll",
            EntryPoint = "Createdirect3ddevice",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall
        )]
        internal static extern UInt32 Createdirect3ddevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [DllImport("d3d11.dll", EntryPoint ="Createdirectsurface",SetLastError = true,CharSet = CharSet.Unicode,ExactSpelling =true,CallingConvention = CallingConvention.StdCall)]
        internal static extern UInt32 Createdirectsurface(IntPtr dxgiDevice, out IntPtr surface);

        public static IDirect3DDevice CreateDevice()
        {
            return CreateDevice(false);

        }

        public static IDirect3DDevice CreateDevice(bool usewarp)
        {
            Device d3device = new SharpDX.Direct3D11.Device(usewarp ? DriverType.Software : DriverType.Hardware,DeviceCreationFlags.BgraSupport);
            return Createdirect3ddevicesharp(d3device);

        }

        public static IDirect3DDevice Createdirect3ddevicesharp(Device d3device)
        {
            IDirect3DDevice device = null;
            using (Device3 dxgi=d3device.QueryInterface<Device3>())
            {
                uint hr = Createdirect3ddevice(dxgi.NativePointer, out IntPtr pUnk);
                if (hr == 0)
                {
                    device=Marshal.GetObjectForIUnknown(pUnk) as IDirect3DDevice;
                    Marshal.Release(pUnk);
                }

            }
            return device;
        }

        public static IDirect3DSurface CreatedDirect3DSurface(Texture2D texture2D)
        {
            IDirect3DSurface surface = null;
            using (Surface s=texture2D.QueryInterface<Surface>())
            {
                uint hr = Createdirectsurface(s.NativePointer, out IntPtr pUnk);
                if (hr == 0)
                {
                    surface = Marshal.GetObjectForIUnknown(pUnk) as IDirect3DSurface;
                    Marshal.Release(pUnk);
                }

            }

            return surface;
        }

        public static Device CreateSharpDxDevice(IDirect3DDevice device)
        {
            IDirect3DDxgiInterfaceAccess access = (IDirect3DDxgiInterfaceAccess) device;
            IntPtr r = access.GetInterface(ref ID3D11Device);
            return new Device(r);
        }
        public static SharpDX.Direct3D11.Texture2D InitializeComposeTexture(
            SharpDX.Direct3D11.Device sharpDxD3dDevice,
            SizeInt32 size)
        {
            var description = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = size.Width,
                Height = size.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription()
                {
                    Count = 1,
                    Quality = 0
                },
                Usage = SharpDX.Direct3D11.ResourceUsage.Default,
                BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
            };
            var composeTexture = new SharpDX.Direct3D11.Texture2D(sharpDxD3dDevice, description);
       

            using (var renderTargetView = new SharpDX.Direct3D11.RenderTargetView(sharpDxD3dDevice, composeTexture))
            {
                sharpDxD3dDevice.ImmediateContext.ClearRenderTargetView(renderTargetView, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));
            }

            return composeTexture;
        }


        public static Texture2D CreateSharpDXTexture2D(IDirect3DSurface surface)
        {
            IDirect3DDxgiInterfaceAccess access = (IDirect3DDxgiInterfaceAccess) surface;
            IntPtr r = access.GetInterface(ref ID3D11Texture2D);
            return new Texture2D(r);
        }
        
    }
}