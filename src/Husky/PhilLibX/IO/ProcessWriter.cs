﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhilLibX.IO
{
    public class ProcessWriter
    {
        /// <summary>
        /// Internal Process Property
        /// </summary>
        private Process _Process { get; set; }

        /// <summary>
        /// Internal Handle Property
        /// </summary>
        private IntPtr _Handle { get; set; }

        /// <summary>
        /// Active Process
        /// </summary>
        public Process ActiveProcess
        {
            get { return _Process; }
            set
            {
                _Process = value;
                _Handle = NativeMethods.OpenProcess(MemoryUtil.ProcessVMOperation | MemoryUtil.ProcessVMWrite, false, _Process.Id);
            }
        }

        /// <summary>
        /// Active Process Handle
        /// </summary>
        public IntPtr Handle { get { return _Handle; } }

        /// <summary>
        /// Initalizes a Process Reader with a Process
        /// </summary>
        public ProcessWriter(Process process)
        {
            ActiveProcess = process;
        }

        /// <summary>
        /// Writes bytes to the processes' memory
        /// </summary>
        /// <param name="address"></param>
        /// <param name="buffer"></param>
        public void WriteBytes(long address, byte[] buffer)
        {
            int bytesRead = 0;
            if (!NativeMethods.WriteProcessMemory((int)Handle, address, buffer, buffer.Length, out bytesRead))
            {
                throw new ArgumentException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            }
        }
    }
}
