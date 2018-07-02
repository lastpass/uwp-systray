/**
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * Copyright(c) 2018 LastPass.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;



namespace BackgroundProcess.Utils
{
    public static partial class ClipboardUtil
    {
        private static byte[] m_pbDataHash32 = null;
        private static string m_strFormat = null;
        private static bool m_bEncoded = false;

        private static CriticalSectionEx g_csClearing = new CriticalSectionEx();

        private const string ClipboardIgnoreFormatName = "Clipboard Viewer Ignore";

    }
}
