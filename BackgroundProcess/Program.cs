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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BackgroundProcess
{
    static class Program
    {
        public static SystrayAppContext Systray { set; get; } = null;

        public static class Config
        {
            public const int MaxPath = 260;
        }

        public static string EventName { get => "{7CB69075-62DF-4149-A3F8-4EC3BB8CAF84}"; }

        public static string GetApplicationName()
        {
            return Assembly.GetExecutingAssembly()
                     .GetCustomAttributes(typeof(AssemblyTitleAttribute), false)
                     .OfType<AssemblyTitleAttribute>()
                     .FirstOrDefault()?
                     .Title ?? "LastPass Background Process";
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            const string mutexName = "{8137EF99-442C-4EE0-8C8D-ADDB9C17068E}";
            Mutex mutex = null;

            if (!Mutex.TryOpenExisting(mutexName, out mutex))
            {
                mutex = new Mutex(false, mutexName);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Systray = new SystrayAppContext();
                Application.Run(Systray);

                mutex.Close();
            }
            else
            {
                /**
                 * TODO: Maybe this could go into the main UWP application.
                 * */
                var eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Program.EventName);
                eventWaitHandle.Set();
            }
        }
    }
}
