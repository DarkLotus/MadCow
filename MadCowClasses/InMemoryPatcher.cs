// Copyright (C) 2011 MadCow Project
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadCow
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
        static Thread _workerThread;
        static bool _patched = false;

        internal static void Patch(Process Diablo3)
        {
            // Start a new thread, needed so we can wait for D3 to load battle.net.dll.
            if (_workerThread == null) 
            { 
                _workerThread = new Thread(new ParameterizedThreadStart(_patchThread)); 
                _workerThread.Start(Diablo3); 
                return; 
            }
            if (_workerThread.ThreadState != System.Threading.ThreadState.Running) 
            { 
                _patched = false;  
                _workerThread = new Thread(new ParameterizedThreadStart(_patchThread));
                _workerThread.Start(Diablo3); 
            }
        }

        internal static void _patchThread(object obj)
        {
            if (obj == null)
                return;
            Process Diablo3 = (Process)obj;
            
            while (!Diablo3.HasExited && !_patched)
            {
                foreach (var p in Process.GetProcesses())
                {
                    if (p.ProcessName == "Diablo III")
                    {
                        Diablo3 = p;
                        foreach (ProcessModule module in Diablo3.Modules)
                        {
                            if (module.ModuleName == "battle.net.dll")
                            {
                                _patch(Diablo3);
                                _patched = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not find Diablo III Process");
                    }
                }              
                Thread.Sleep(10);
            }
            return;
        }
               
        internal static void _patch(Process Diablo3)
        {
            // Thanks to Egris for the patching code. and Shadow^Dancer for the locations. - DarkLotus
            try
            {
                if (Diablo3.ProcessName == "Diablo III")
                {
                    var hWnd = OpenProcess(0x001F0FFF, false, Diablo3.Id);
                    if (hWnd == IntPtr.Zero)
                    {
                        Console.WriteLine("Failed to open process.");
                        return;
                    }

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
                        Console.WriteLine("Failed to locate battle.net.dll");

                    var offset = 0x000B4475;
                    var JMPAddr = baseAddr.ToInt32() + offset;
                    var BytesWritten = IntPtr.Zero;
                    byte[] JMP = new byte[] { 0xEB };
                    Console.WriteLine("Attempting Client memory patch...");
                    Console.WriteLine("battle.net.dll address: 0x{0:X8}", baseAddr.ToInt32());
                    Console.WriteLine("Before write: 0x{0:X2}", ReadByte(hWnd, JMPAddr));
                    WriteProcessMemory(hWnd, new IntPtr(JMPAddr), JMP, 1, out BytesWritten);
                    Console.WriteLine("After write: 0x{0:X2}", ReadByte(hWnd, JMPAddr));

                    CloseHandle(hWnd);

                    if (BytesWritten.ToInt32() < 1)
                        Console.WriteLine("Failed to patch client");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine("Client successfully patched");
        }

        static byte ReadByte(IntPtr _handle, int offset)
        {
            byte result = 0;
            ReadProcessMemory(_handle, offset, out result, 1, IntPtr.Zero);
            return result;
        }       
    }
}
