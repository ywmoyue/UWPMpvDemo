using System;
using System.Runtime.InteropServices;

namespace UWPMpvDemo;

[StructLayout(LayoutKind.Sequential)]
public struct MpvRenderParam
{
    public MpvRenderParamType type;
    public IntPtr data;
}