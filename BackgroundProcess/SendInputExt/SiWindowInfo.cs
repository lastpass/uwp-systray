/**
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * Copyright(c) 2018 LastPass.
 */

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BackgroundProcess.SendInputExt
{
    internal enum SiSendMethod
    {
        Default = 0,
        KeyEvent,
        UnicodePacket // VK_PACKET via SendInput
    }

    internal sealed class SiWindowInfo
    {
        private readonly IntPtr m_hWnd;
        public IntPtr HWnd
        {
            get { return m_hWnd; }
        }

        private IntPtr m_hkl = IntPtr.Zero;
        public IntPtr KeyboardLayout
        {
            get { return m_hkl; }
            set { m_hkl = value; }
        }

        private SiSendMethod m_sm = SiSendMethod.Default;
        public SiSendMethod SendMethod
        {
            get { return m_sm; }
            set { m_sm = value; }
        }

        public SiWindowInfo(IntPtr hWnd)
        {
            m_hWnd = hWnd;
        }
    }
}
