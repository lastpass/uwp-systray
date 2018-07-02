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
using System.Windows.Forms;

namespace BackgroundProcess.SendInputExt
{
    public interface ISiEngine
    {
        void Init();
        void Release();

        void SendKey(int iVKey, bool? bExtKey, bool? bDown);
        void SetKeyModifier(Keys kMod, bool bDown);

        void SendChar(char ch, bool? bDown);

        void Delay(uint uMs);
    }
}
