using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadCow.MadCowClasses
{
    internal static class InMemoryPatcher
    {
        [DllImport("kernel32.dll", ExactSpelling = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, int lpBaseAddress, out byte lpBuffer, int dwSize, IntPtr lpNumberOfBytesRead);
        static Thread t;
        internal static void Patch(Process Diablo3)
        {
            // Start a new thread, needed so we can wait for D3 to load battle.net.dll.
            if (t == null) { t = new Thread(new ParameterizedThreadStart(_patchThread)); t.Start(Diablo3); }
        }

        internal static void _patchThread(object obj)
        {
            if (obj == null)
                return;
            Process Diablo3 = (Process)obj;
            
            while (!Diablo3.HasExited)
            {
                foreach (ProcessModule module in Diablo3.Modules)
                {
                    if (module.ModuleName == "battle.net.dll")
                    {
                        _patch(Diablo3);
                        break;
                    }
                }
            }
            return;
        }
               
        internal static void _patch(Process Diablo3)
        {
            try
            {
                if (Diablo3.ProcessName == "Diablo III")
                {
                    var hWnd = OpenProcess(0x001F0FFF, false, Diablo3.Id);
                    if (hWnd == IntPtr.Zero)
                        throw new Exception("Failed to open process.");

                    var modules = Diablo3.Modules;
                    IntPtr baseAddr = IntPtr.Zero;

                    foreach (ProcessModule module in modules)
                    {
                        if (module.ModuleName == "battle.net.dll")
                        {
                            baseAddr = module.BaseAddress;
                            break;
                        }
                    }

                    if (baseAddr == IntPtr.Zero)
                        throw new Exception("Failed to located battle.net.dll");

                    var offset = 0x000B4475;
                    var JMPAddr = baseAddr.ToInt32() + offset;
                    var BytesWritten = IntPtr.Zero;
                    byte[] JMP = new byte[] { 0xEB };
                    Console.WriteLine("battle.net.dll address: 0x{0:X8}", baseAddr.ToInt32());
                    Console.WriteLine("Before write: 0x{0:X2}", ReadByte(hWnd, JMPAddr));
                    WriteProcessMemory(hWnd, new IntPtr(JMPAddr), JMP, 1, out BytesWritten);
                    Console.WriteLine("After write: 0x{0:X2}", ReadByte(hWnd, JMPAddr));

                    CloseHandle(hWnd);

                    if (BytesWritten.ToInt32() < 1)
                        throw new Exception("Failed to write to process.");
                }
            }
            catch { }

        }

        static byte ReadByte(IntPtr _handle, int offset)
        {
            byte result = 0;
            ReadProcessMemory(_handle, offset, out result, 1, IntPtr.Zero);
            return result;
        }

        
    }
}
