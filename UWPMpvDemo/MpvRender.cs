using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Controls;

namespace UWPMpvDemo
{
    /// <summary>
    /// 用于 MPV_RENDER_PARAM_OPENGL_FBO 的结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MpvOpenGlFbo
    {
        /// <summary>
        /// 帧缓冲对象名称
        /// </summary>
        /// <remarks>
        /// 必须是由 glGenFramebuffers() 生成的有效 FBO（已完成且可颜色渲染），或 0。
        /// 如果值为 0，则表示 OpenGL 默认帧缓冲区。
        /// </remarks>
        public int fbo;

        /// <summary>
        /// 帧缓冲区的宽度
        /// </summary>
        public int w;

        /// <summary>
        /// 帧缓冲区的高度
        /// </summary>
        public int h;

        /// <summary>
        /// 底层纹理的内部格式（如 GL_RGBA8），如果未知则为 0
        /// </summary>
        /// <remarks>
        /// 如果是默认帧缓冲区，可以是等效格式。
        /// </remarks>
        public int internal_format;
    }

    public enum MpvRenderParamType
    {
        Invalid = 0,
        ApiType = 1,
        InitParams = 2,
        Fbo = 3,
        FlipY = 4,
        Depth = 5,
        IccProfile = 6,
        AmbientLight = 7,
        X11Display = 8,
        WlDisplay = 9,
        AdvancedControl = 10,
        NextFrameInfo = 11,
        BlockForTargetTime = 12,
        SkipRendering = 13,
        DrmDisplay = 14,
        DrmDrawSurfaceSize = 15,
        DrmDisplayV2 = 15,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvRenderParam
    {
        public MpvRenderParamType type;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvOpenGlInitParams
    {
        public MpvRender.OpenGlRenderContextCallback get_proc_address;
        public IntPtr get_proc_address_ctx;
        public IntPtr extra_exts;
    }

    /// <summary>
    /// MPV渲染器封装类，提供对mpv渲染API的基本封装
    /// </summary>
    public class MpvRender : IDisposable
    {
        private SwapChainPanel _panel;
        private IntPtr _mpvRenderContext;
        private const int MPV_RENDER_PARAM_OPENGL_FBO = 3;
        private const int MPV_RENDER_PARAM_FLIP_Y = 4;
        private readonly MpvClient _mpvClient;
        private readonly EglContext _eglContext = new EglContext();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvRenderContextCreate(ref IntPtr context, IntPtr mpvHandler, IntPtr parameters);
        private MpvRenderContextCreate _mpvRenderContextCreate;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr OpenGlRenderContextCallback(IntPtr ctx, IntPtr name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int MpvRenderContextRender(IntPtr ctx, IntPtr[] parameters);
        private MpvRenderContextRender _mpvRenderContextRender;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MpvRenderContextSetUpdateCallback(IntPtr ctx, MpvRenderUpdateFn callback, IntPtr callback_ctx);
        private MpvRenderContextSetUpdateCallback _mpvRenderContextSetUpdateCallback;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void MpvRenderUpdateFn(IntPtr callback_ctx);

        public MpvRender(MpvClient mpvClient)
        {
            _mpvClient = mpvClient;
        }

        private object GetDllType(Type type, string name)
        {
            IntPtr address = GetProcAddress(_mpvClient.LibMpvDll, name);
            if (address != IntPtr.Zero)
                return Marshal.GetDelegateForFunctionPointer(address, type);
            return null;
        }

        private void LoadMpvDynamic()
        {
            _mpvRenderContextCreate = (MpvRenderContextCreate)GetDllType(typeof(MpvRenderContextCreate), "mpv_render_context_create");
            _mpvRenderContextRender = (MpvRenderContextRender)GetDllType(typeof(MpvRenderContextRender), "mpv_render_context_render");
            _mpvRenderContextSetUpdateCallback = (MpvRenderContextSetUpdateCallback)GetDllType(typeof(MpvRenderContextSetUpdateCallback), "mpv_render_context_set_update_callback");
        }

        private IntPtr GetGLProcAddress(IntPtr ctx, IntPtr name)
        {
            string functionName = Marshal.PtrToStringAnsi(name);

            var address = EglContext.eglGetProcAddress(functionName);
            return address;
        }

        public unsafe void Initialize(SwapChainPanel panel, string videoPath)
        {
            if (_mpvClient.MpvHandle == IntPtr.Zero)
                throw new Exception("MpvClient has not initialized");
            _panel = panel;

            if (!_eglContext.InitializeEGL(panel))
                throw new Exception("Failed to initialize EGL");

            LoadMpvDynamic();

            var oglInitParams = new MpvOpenGlInitParams();
            oglInitParams.get_proc_address = (ctx, name) =>
            {
                return GetGLProcAddress(ctx, name);
            };
            oglInitParams.get_proc_address_ctx = IntPtr.Zero;
            oglInitParams.extra_exts = IntPtr.Zero;

            var size = Marshal.SizeOf<MpvOpenGlInitParams>();
            var oglInitParamsBuf = new byte[size];

            fixed (byte* arrPtr = oglInitParamsBuf)
            {
                IntPtr oglInitParamsPtr = new IntPtr(arrPtr);
                Marshal.StructureToPtr(oglInitParams, oglInitParamsPtr, true);

                MpvRenderParam* parameters = stackalloc MpvRenderParam[3];

                parameters[0].type = MpvRenderParamType.ApiType;
                parameters[0].data = Marshal.StringToHGlobalAnsi("opengl");

                parameters[1].type = MpvRenderParamType.InitParams;
                parameters[1].data = oglInitParamsPtr;

                parameters[2].type = MpvRenderParamType.Invalid;
                parameters[2].data = IntPtr.Zero;

                var renderParamSize = Marshal.SizeOf<MpvRenderParam>();

                var paramBuf = new byte[renderParamSize * 3];
                fixed (byte* paramBufPtr = paramBuf)
                {
                    IntPtr param1Ptr = new IntPtr(paramBufPtr);
                    Marshal.StructureToPtr(parameters[0], param1Ptr, true);

                    IntPtr param2Ptr = new IntPtr(paramBufPtr + renderParamSize);
                    Marshal.StructureToPtr(parameters[1], param2Ptr, true);

                    IntPtr param3Ptr = new IntPtr(paramBufPtr + renderParamSize + renderParamSize);
                    Marshal.StructureToPtr(parameters[2], param3Ptr, true);


                    IntPtr context = new IntPtr(0);
                    _mpvRenderContextCreate(ref context, _mpvClient.MpvHandle, param1Ptr);
                    _mpvRenderContext = context;
                }
            }
            _mpvRenderContextSetUpdateCallback(_mpvRenderContext, OnMpvRenderUpdate, IntPtr.Zero);

            // 设置常用选项
            _mpvClient.MpvSetOptionString(_mpvClient.MpvHandle, MpvUtilsExtensions.GetUtf8Bytes("keep-open"), MpvUtilsExtensions.GetUtf8Bytes("always"));
            _mpvClient.MpvSetOptionString(_mpvClient.MpvHandle, MpvUtilsExtensions.GetUtf8Bytes("hwdec"), MpvUtilsExtensions.GetUtf8Bytes("auto-safe"));
            _mpvClient.MpvSetOptionString(_mpvClient.MpvHandle, MpvUtilsExtensions.GetUtf8Bytes("vo"), MpvUtilsExtensions.GetUtf8Bytes("libmpv"));

            // 加载视频文件
            _mpvClient.DoMpvCommand("loadfile", videoPath);
        }


        private void OnMpvRenderUpdate(IntPtr callback_ctx)
        {
            // This gets called when a new frame should be rendered
            RenderFrame();
        }

        public void RenderFrame()
        {
            if (_mpvRenderContext == IntPtr.Zero || _panel == null)
                return;

            // 确保在UI线程执行
            _panel.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    // 获取当前OpenGL上下文和FBO
                    int fbo = _eglContext.GetCurrentFbo(); // 需要实现这个方法来获取当前FBO
                    int width = (int)_panel.ActualWidth;
                    int height = (int)_panel.ActualHeight;

                    if (width <= 0 || height <= 0)
                        return;

                    // 准备FBO参数
                    var fboParams = new MpvOpenGlFbo
                    {
                        fbo = fbo,
                        w = width,
                        h = height,
                        internal_format = 0 // 通常为0，表示默认格式
                    };

                    // 创建渲染参数数组
                    IntPtr[] renderParams = new IntPtr[6];

                    // FBO参数
                    IntPtr fboPtr = Marshal.AllocHGlobal(Marshal.SizeOf(fboParams));
                    Marshal.StructureToPtr(fboParams, fboPtr, false);
                    renderParams[0] = new IntPtr(MPV_RENDER_PARAM_OPENGL_FBO);
                    renderParams[1] = fboPtr;

                    // 翻转Y坐标 (UWP通常需要)
                    int flip_y = 1;
                    IntPtr flipYPtr = Marshal.AllocHGlobal(sizeof(int));
                    Marshal.WriteInt32(flipYPtr, flip_y);
                    renderParams[2] = new IntPtr(MPV_RENDER_PARAM_FLIP_Y);
                    renderParams[3] = flipYPtr;

                    // 结束标记
                    renderParams[4] = IntPtr.Zero;
                    renderParams[5] = IntPtr.Zero;

                    // 执行渲染
                    int result = _mpvRenderContextRender(_mpvRenderContext, renderParams);
                    if (result < 0)
                    {
                        // 处理渲染错误
                        Debug.WriteLine($"mpv渲染失败: {result}");
                    }

                    // 交换缓冲区 (需要在您的OpenGL上下文中实现)
                    _eglContext.SwapBuffers();

                    // 清理资源
                    Marshal.FreeHGlobal(fboPtr);
                    Marshal.FreeHGlobal(flipYPtr);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"渲染帧时出错: {ex.Message}");
                }
            });

        }

        public void Dispose()
        {
            _eglContext.CleanupEGL();
        }
    }

}