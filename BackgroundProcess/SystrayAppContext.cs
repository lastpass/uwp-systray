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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using BackgroundProcess.Native;
using System.Text.RegularExpressions;
using System.Threading;
using CommunicationInterface;
using System.Drawing;

namespace BackgroundProcess
{
    class SystrayAppContext : ApplicationContext
    {
        private NotifyIcon notifyIcon = null;
        private UwpConnection connection = new UwpConnection();
        private GlobalKeyboardHook keyboardHook = null;
        private MenuItem openMenuItem = null;
        private EventWaitHandle eventWaitHandle = null;
        private Form dummyForm = new Form();
        private Mutex fillMutex = new Mutex();
        private bool ongoingFill = false;

        public SystrayAppContext()
        {
            createNotifyIcon();

            keyboardHook = new GlobalKeyboardHook(Keys.Control | Keys.Alt | Keys.F);
            keyboardHook.KeyPressed += OnKeyPressed;

            eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Program.EventName);
            ThreadPool.RegisterWaitForSingleObject(eventWaitHandle, OnReconnectPossible, null, -1, false);

            OpenConnection();
        }

        private void createNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = BackgroundProcess.Properties.Resources.LastpassIconGrey;
            notifyIcon.Text = "LastPass";
            notifyIcon.ContextMenu = createContextMenu();
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += OpenApp;
        }

        private ContextMenu createContextMenu()
        {
            openMenuItem = new MenuItem("Login to LastPass", new EventHandler(OpenApp));
            MenuItem exitMenuItem = new MenuItem("Exit", new EventHandler(Exit));
            openMenuItem.DefaultItem = true;

            return new ContextMenu(new MenuItem[] { openMenuItem, exitMenuItem });
        }

        private async void OnReconnectPossible(object state, bool timedOut)
        {
            await OpenConnection();
        }

        private void SetToLoggedInState()
        {
            openMenuItem.Visible = false;
            notifyIcon.Icon = BackgroundProcess.Properties.Resources.LastpassIcon;
        }

        private void SetToLoggedOffState()
        {
            openMenuItem.Visible = true;
            notifyIcon.Icon = BackgroundProcess.Properties.Resources.LastpassIconGrey;
            notifyIcon.ShowBalloonTip(0,
                    "Logged out from LastPass",
                    "You have either closed LastPass application or logged out. " +
                    "LastPass will continue to run  in the background, but application " +
                    "fill won't be available until the next login.",
                    ToolTipIcon.Warning);
        }

        private async void Exit(object sender, EventArgs e)
        {
            await connection.SendExitMessage();

            notifyIcon.Visible = false;
            Application.Exit();
        }

        private async void OpenApp(object sender, EventArgs e)
        {
            IEnumerable<AppListEntry> appListEntries = await Package.Current.GetAppListEntriesAsync();
            await appListEntries.First().LaunchAsync();
        }

        private async void OnKeyPressed(object sender, EventArgs args)
        {
            if (fillMutex.WaitOne(0 /*millisec*/) && !ongoingFill)
            {
                ongoingFill = true;
                try
                {
                    NativeMethods.GetForegroundWindowInfo(out IntPtr hWnd, out string windowTitle, true);

                    FillData? fillData = await RequestFillScript(windowTitle, hWnd);

                    await FillOnWindow(hWnd, fillData);
                }
                catch (FormatException e)
                {
                    notifyIcon.ShowBalloonTip(0,
                                "Error during application fill",
                                e.Message,
                                ToolTipIcon.Error);
                }
                catch (Exception)
                {
                }
                finally
                {
                    ongoingFill = false;
                    fillMutex.ReleaseMutex();
                }
            }
        }

        private async Task FillOnWindow(IntPtr hWnd, FillData? fillData)
        {
            if (fillData.HasValue)
            {
                int i = 0;
                while (false == await Task.Run(() => NativeMethods.SetForegroundWindowEx(hWnd)) && i < 5)
                {
                    await Task.Delay(100);
                    i++;
                }

                if (NativeMethods.GetForegroundWindowHandle() == hWnd)
                {
                        string scriptWithAllData = ReplacePlaceholders(fillData.Value.script,
                                fillData.Value.userName,
                                fillData.Value.password);

                        await Task.Run(() => SendInputEx.SendKeysWait(scriptWithAllData, false));
                }
                else
                {
                    notifyIcon.ShowBalloonTip(0,
                        "LastPass",
                        "Sorry, We couldn't bring the application to fill into the foreground, " +
                        "please wait a few seconds and open the application again, and press ctrl+alt+f",
                        ToolTipIcon.Error);
                }
            }
        }

        private async Task<FillData?> RequestFillScript(string windowTitle, IntPtr hWnd)
        {
            string accountId = await SelectSingleAccountIdByWindowTitle(windowTitle, hWnd);

            if (accountId != null)
            {
                return await connection.RequestFillScript(accountId);
            }

            return null;
        }

        private async Task<string> SelectSingleAccountIdByWindowTitle(string windowTitle, IntPtr hWnd)
        {
            ValueSet accounts = await connection.RequestAccountsByWindowTitle(windowTitle);

            if (accounts != null)
            {
                if (accounts.Count == 1)
                {
                    return accounts.First().Key;
                }

                return await ShowFillScriptSelector(accounts, hWnd);
            }

            return null;
        }

        private Task<string> ShowFillScriptSelector(ValueSet accountList, IntPtr hWnd)
        {
            var selectionResultPromise = new TaskCompletionSource<string>();
            var ctxStrip = new ContextMenuStrip();
            ctxStrip.ShowImageMargin = false;
            ctxStrip.Items.Insert(0, new ToolStripLabel("Select an account to fill", Properties.Resources.LastpassIcon.ToBitmap()) { Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) });
            ctxStrip.Items.Add(new ToolStripSeparator());

            foreach(KeyValuePair<string, object> account in accountList)
            {
                ctxStrip.Items.Add(new ToolStripMenuItem(account.Value as string, null, null, account.Key));
            }

            ctxStrip.ItemClicked += (object sender, ToolStripItemClickedEventArgs e) =>
            {
                ctxStrip.AutoClose = true;
                (sender as ContextMenuStrip)?.Close(ToolStripDropDownCloseReason.ItemClicked);
                selectionResultPromise.TrySetResult(e.ClickedItem.Name);
            };

            Native.NativeMethods.RECT targetWindowRect = new Native.NativeMethods.RECT();
            Native.NativeMethods.GetWindowRect(hWnd, ref targetWindowRect);

            var centerPointInWindowCoordinates = new Point(
                (targetWindowRect.Right - targetWindowRect.Left) / 2,
                (targetWindowRect.Bottom - targetWindowRect.Top) / 2);

            var centerPointinScreenCoordinates = new Point(
                centerPointInWindowCoordinates.X + targetWindowRect.Left,
                centerPointInWindowCoordinates.Y + targetWindowRect.Top);

            ctxStrip.AutoClose = false;
            ctxStrip.Show(centerPointinScreenCoordinates);

            return selectionResultPromise.Task;
        }

        private async Task OpenConnection()
        {
            if (await connection.OpenConnection())
            {
                connection.ServiceClosed += Connection_ServiceClosed;
                connection.RequestReceived += Connection_RequestReceived;
                await connection.SendReadyMessage();
            }
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            SetToLoggedOffState();
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var type = args.Request.Message[MsgField.Type] as string;
            if (type == MsgType.PrepareForReprompt)
            {
                OpenApp(null, null);
                notifyIcon.ShowBalloonTip(0,
                    "LastPass Reprompt Required",
                    "Open the LastPass UWP application and re-enter your master password." +
                    " Do NOT minimize the application, which you would like to fill.",
                    ToolTipIcon.Warning);
            }
            else if (type == MsgType.LoggingOff)
            {
                SetToLoggedOffState();
            }
            else if (type == MsgType.LoggingIn)
            {
                SetToLoggedInState();
            }

            else if (type == MsgType.LaunchApp)
            {
                string path = args.Request.Message[MsgField.Path] as string;
                string cmdArgs = await Encryption.decryptData(args.Request.Message[MsgField.Args] as string);
                string userName = await Encryption.decryptData(args.Request.Message[MsgField.UserName] as string);
                string password = await Encryption.decryptData(args.Request.Message[MsgField.Password] as string);

                string argsWithAllData = ReplacePlaceholders(cmdArgs, userName, password);

                System.Diagnostics.Process process = null;
                try
                {
                    process = System.Diagnostics.Process.Start(path, argsWithAllData);
                }
                catch (Exception)
                { }
                finally
                {
                    if (process == null)
                    {
                        notifyIcon.ShowBalloonTip(0,
                        "LastPass failed to launch app",
                        "Lastpass has failed to launch selected application. Check if the" +
                        " path and arguments are set correctly.",
                        ToolTipIcon.Error);
                    }
                }
            }
        }

        private static string ReplacePlaceholders(string str, string userName, string password)
        {
            var argsWithUserName = Regex.Replace(
                        str,
                        Regex.Escape("{USERNAME}"),
                        userName,
                        RegexOptions.IgnoreCase);

            var argsWithAllData = Regex.Replace(
                        argsWithUserName,
                        Regex.Escape("{PASSWORD}"),
                        password,
                        RegexOptions.IgnoreCase);
            return argsWithAllData;
        }
    }
}
