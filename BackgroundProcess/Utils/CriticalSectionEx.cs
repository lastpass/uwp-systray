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
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace BackgroundProcess.Utils
{
    /// <summary>
    /// Mechanism to synchronize access to an object.
    /// In addition to a usual critical section (which locks an object
    /// to a single thread), <c>CriticalSectionEx</c> also prevents
    /// subsequent accesses from the same thread.
    /// </summary>
    public sealed class CriticalSectionEx
    {
        private int m_iLock = 0;

#if DEBUG
        private int m_iThreadId = -1;
#endif

        public CriticalSectionEx() { }

#if DEBUG
        ~CriticalSectionEx()
        {
            // The object should be unlocked when the lock is disposed
            Debug.Assert(Interlocked.CompareExchange(ref m_iLock, 0, 2) == 0);
        }
#endif

        public bool TryEnter()
        {
            bool b = (Interlocked.Exchange(ref m_iLock, 1) == 0);

#if DEBUG
            if (b) m_iThreadId = Thread.CurrentThread.ManagedThreadId;
#endif

            return b;
        }

        public void Exit()
        {
            if (Interlocked.Exchange(ref m_iLock, 0) != 1)
            {
                Debug.Assert(false);
            }
#if DEBUG
            else
            {
                // Lock should be released by the original thread
                Debug.Assert(Thread.CurrentThread.ManagedThreadId == m_iThreadId);
                m_iThreadId = -1;
            }
#endif
        }
    }
}
