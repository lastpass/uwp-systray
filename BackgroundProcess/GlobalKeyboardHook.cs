/**
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * Copyright(c) 2018 LastPass.
 */

﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace BackgroundProcess
{
    class GlobalKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc m_callback;

        private Keys m_watchedKey;

        public event EventHandler KeyPressed;

        public GlobalKeyboardHook(Keys watchedKey)
        {
            m_watchedKey = watchedKey;
            m_callback = HookCallback;
            _hookID = SetHook(m_callback);
        }

        ~GlobalKeyboardHook()
        {
            UnhookWindowsHookEx(_hookID);
        }

        protected virtual void OnKeyPressed(EventArgs e)
        {
            KeyPressed?.Invoke(this, e);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                key = AddModifiers(key);

                Console.WriteLine($"{key} ({vkCode})");

                if (key == m_watchedKey)
                {
                    OnKeyPressed(EventArgs.Empty);
                    return (IntPtr)(-1);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private Keys AddModifiers(Keys key)
        {
            if ((GetKeyState(VK_CAPITAL) & 0x0001) != 0) key = key | Keys.CapsLock;
            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0) key = key | Keys.Shift;
            if ((GetKeyState(VK_CONTROL) & 0x8000) != 0) key = key | Keys.Control;
            if ((GetKeyState(VK_MENU) & 0x8000) != 0) key = key | Keys.Alt;

            return key;
        }

        private bool IsKeyCombination(Keys key)
        {
            return (key == (Keys.Control | Keys.Alt | Keys.F));
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]

        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        //Modifier key vkCode constants
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private const int VK_CAPITAL = 0x14;
    }
}