using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SharpDX.Direct3D11;
using UnityEngine;
using UwpApplication1;
using Application = Windows.UI.Xaml.Application;
using Debug = System.Diagnostics.Debug;
using Texture2D = SharpDX.Direct3D11.Texture2D;

namespace UwpApplication2
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private CompositionDrawingSurface surface;
        private IDirect3DDevice device;
        private CompositionGraphicsDevice compositionGraphics;
        private Compositor compositor;
        private Device directpool;
        private GraphicsCaptureItem captureitem;
        private VideoStreamDescriptor videoStreamDescriptor;
        private MediaEncodingProfile profile;
        private VideoStreamDescriptor vsd;
        private MediaStreamSource mss;
        private MediaTranscoder mtra;
        private bool isRecording = false;
        private Direct3D11CaptureFrame currentframe;
        private ManualResetEvent frameevent;
        private ManualResetEvent closedevent;
        private ManualResetEvent[] _events;
        private Multithread _multithread;
        private RenderTargetView composetargview;
        private Texture2D texture;
        private Direct3D11CaptureFramePool framepool;
        private GraphicsCaptureSession gcs;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

        }

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

        private async Task start()
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                return;
            }

            if (device == null)
            {
                device = Direct3D11Helpers.CreateDevice();
            }

            if (directpool == null)
            {
                directpool = Direct3D11Helpers.CreateSharpDxDevice(device);
            }

            try
            {
                GraphicsCapturePicker picker = new GraphicsCapturePicker();
                captureitem = await picker.PickSingleItemAsync();
                if (picker == null)
                {
                    return;
                }

                texture = Direct3D11Helpers.InitializeComposeTexture(directpool, captureitem.Size);
                composetargview = new SharpDX.Direct3D11.RenderTargetView(directpool, texture);
                uint width = (uint) captureitem.Size.Width;
                uint height = (uint) captureitem.Size.Height;
                width = (width % 2 == 0) ? width : width + 1;
                height = (height % 2 == 0) ? height : height + 1;
                MediaEncodingProfile temp = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
                uint bitrate = temp.Video.Bitrate;
                uint framerate = 30;
                VideoEncodingProperties vep =
                    VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, width, height);
                videoStreamDescriptor = new VideoStreamDescriptor(vep);
                profile = new MediaEncodingProfile();
                profile.Container.Subtype = "MPEG4";
                profile.Video.Subtype = "H264";
                profile.Video.Width = width;
                profile.Video.Height = height;
                profile.Video.Bitrate = bitrate;
                profile.Video.FrameRate.Numerator = framerate;
                profile.Video.FrameRate.Denominator = 1;
                profile.Video.PixelAspectRatio.Numerator = 1;
                profile.Video.PixelAspectRatio.Denominator = 1;
                VideoEncodingProperties properties =
                    VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, width, height);
                vsd = new VideoStreamDescriptor(properties);
                mss = new MediaStreamSource(vsd);
                mss.BufferTime = TimeSpan.FromSeconds(0);
                mss.Starting += OnMediaStreamSourceStarting;
                mss.SampleRequested += OnMediaStreamSourceSampleRequested;
                mtra = new MediaTranscoder();
                mtra.HardwareAccelerationEnabled = true;
                StorageFolder video = KnownFolders.VideosLibrary;
                string name = DateTime.Now.ToString("yyyyMMdd-HHmm-ss");
                StorageFile file = await video.CreateFileAsync($"{name}.mp4");
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await EncodeAsync(stream);
                }
            }
            catch (Exception e)
            {
                return;
            }

        }

        private async Task EncodeAsync(IRandomAccessStream stream)
        {
            if (!isRecording)
            {
                isRecording = true;
                startcapture();
                var transcode = await mtra.PrepareMediaStreamSourceTranscodeAsync(mss, stream, profile);
                await transcode.TranscodeAsync();
            }
        }

        private void startcapture()
        {
            _multithread = directpool.QueryInterface<Multithread>();
            _multithread.SetMultithreadProtected(true);
            frameevent = new ManualResetEvent(false);
            closedevent = new ManualResetEvent(false);
            _events = new[] {frameevent, closedevent};
            captureitem.Closed += OnClosed;
            framepool = Direct3D11CaptureFramePool.CreateFreeThreaded(device,DirectXPixelFormat.B8G8R8A8UIntNormalized,1,captureitem.Size);
            framepool.FrameArrived += OFR;
            gcs = framepool.CreateCaptureSession(captureitem);
            gcs.StartCapture();

        }

        private void OnClosed(GraphicsCaptureItem sender, object args)
        {
            closedevent.Set();
        }

        private void OFR(Direct3D11CaptureFramePool sender, object args)
        {
            currentframe = sender.TryGetNextFrame();
            frameevent.Set();
        }

        private void OnMediaStreamSourceSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (isRecording)
            {
                try
                {
                    using (var frame = Waitfornewframe())
                    {
                        if (frame == null)
                        {
                            args.Request.Sample = null;
                            Stop();
                            Cleanup();
                            return;
                        }

                        TimeSpan timeSpan = frame.SRT;
                        var sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, timeSpan);
                        args.Request.Sample = sample;

                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                    Debug.WriteLine(e);
                    args.Request.Sample = null;
                    Stop();
                    Cleanup();
                }
            }
            else
            {
                args.Request.Sample = null;
                Stop();
                Cleanup();
            }
        }

        private void Cleanup()
        {
            device = null;
            directpool = null;
            framepool?.Dispose();
            captureitem = null;
            texture = null;
            composetargview?.Dispose();
        }

        private void Stop()
        {
            closedevent.Set();
        }
        public class SurfaceWithInfo:IDisposable
        {
            public IDirect3DSurface Surface { get; internal set; }
            public TimeSpan SRT { get; internal set; }


            public void Dispose()
            {
                Surface?.Dispose();
                Surface = null;
            }
        }
        class MultithreadLock : IDisposable
        {
            private SharpDX.Direct3D11.Multithread _multithread;
            public MultithreadLock(SharpDX.Direct3D11.Multithread multithread)
            {
                _multithread = multithread;
                _multithread?.Enter();
            }

            public void Dispose()
            {
                _multithread?.Leave();
                _multithread = null;
            }
            
        }

        private SurfaceWithInfo Waitfornewframe()
        {

            currentframe?.Dispose();
            frameevent.Reset();

            var signaledEvent = _events[WaitHandle.WaitAny(_events)];
            if (signaledEvent == closedevent)
            {
                Cleanup();
                return null;
            }

            var result = new SurfaceWithInfo();
            result.SRT = currentframe.SystemRelativeTime;
            using (var multithreadLock = new MultithreadLock(_multithread))
            using (var sourceTexture = Direct3D11Helpers.CreateSharpDXTexture2D(currentframe.Surface))
            {

                directpool.ImmediateContext.ClearRenderTargetView(composetargview, new SharpDX.Mathematics.Interop.RawColor4(0, 0, 0, 1));

                var width = Math.Clamp(currentframe.ContentSize.Width, 0, currentframe.Surface.Description.Width);
                var height = Math.Clamp(currentframe.ContentSize.Height, 0, currentframe.Surface.Description.Height);
                var region = new SharpDX.Direct3D11.ResourceRegion(0, 0, 0, width, height, 1);
                directpool.ImmediateContext.CopySubresourceRegion(sourceTexture, 0, region, texture, 0);

                var description = sourceTexture.Description;
                description.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
                description.BindFlags = SharpDX.Direct3D11.BindFlags.ShaderResource | SharpDX.Direct3D11.BindFlags.RenderTarget;
                description.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None;
                description.OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None;

                using (var copyTexture = new SharpDX.Direct3D11.Texture2D(directpool, description))
                {
                    directpool.ImmediateContext.CopyResource(texture, copyTexture);
                    result.Surface = Direct3D11Helpers.CreatedDirect3DSurface(copyTexture);
                }
            }

            return result;
        }

        private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            using (SurfaceWithInfo frame=Waitfornewframe())
            {
                args.Request.SetActualStartPosition(frame.SRT);
                
            }
        }

        ///
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: Load state from previously suspended application
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            Console.WriteLine("dhfjkh");
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("dsfghjkf");
        }
    }
}