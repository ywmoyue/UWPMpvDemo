﻿using System;
using System.Runtime.InteropServices;

namespace UWPMpvDemo;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEvent
{
    public int event_id;
    public int error;
    public ulong reply_userdata;
    public IntPtr data;
}