/**
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * Copyright(c) 2018 LastPass.
 */

﻿using CommunicationInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace BackgroundProcess
{
    public class UwpConnection
    {
        private AppServiceConnection connection = null;

        public UwpConnection()
        {
        }

        public event TypedEventHandler<AppServiceConnection, AppServiceClosedEventArgs> ServiceClosed
        {
            add => connection.ServiceClosed += value;
            remove => connection.ServiceClosed -= value;
        }

        public event TypedEventHandler<AppServiceConnection, AppServiceRequestReceivedEventArgs> RequestReceived
        {
            add => connection.RequestReceived += value;
            remove => connection.RequestReceived -= value;
        }

        public async Task<AppServiceConnection> GetConnection()
        {
            if (connection == null)
            {
                await OpenConnection();
            }
            return connection;
        }

        public async Task<bool> OpenConnection()
        {
            connection = new AppServiceConnection();
            connection.PackageFamilyName = Package.Current.Id.FamilyName;
            connection.AppServiceName = "LpUwpCommunicationService";
            connection.ServiceClosed += Connection_ServiceClosed;
            AppServiceConnectionStatus connectionStatus = await connection.OpenAsync();

            if (connectionStatus != AppServiceConnectionStatus.Success)
            {
                MessageBox.Show("Status: " + connectionStatus.ToString());
                return false;
            }

            return true;
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            if (connection != null)
            {
                connection.ServiceClosed -= Connection_ServiceClosed;
            }

            connection = null;
        }

        public async Task SendReadyMessage()
        {
            if (connection != null)
            {
                await connection.SendMessageAsync(new ValueSet() { { MsgField.Type, MsgType.Ready } });
            }
        }

        public async Task SendExitMessage()
        {
            if (connection != null)
            {
                await connection.SendMessageAsync(new ValueSet() { { MsgField.Type, MsgType.Exit } });
            }
        }

        public async Task<CommunicationInterface.FillData?> RequestFillScript(string accountId)
        {
            AppServiceResponse res = await (await GetConnection()).SendMessageAsync(new ValueSet()
                {
                    { MsgField.Type, MsgType.RequestScript },
                    { MsgField.AccountId, accountId }
                });

            if (res.Message.ContainsKey(MsgField.Type) &&
                    res.Message[MsgField.Type] is string &&
                    res.Message[MsgField.Type] as string == MsgType.ScriptFound &&
                    res.Message[MsgField.Script] is string &&
                    res.Message[MsgField.UserName] is string &&
                    res.Message[MsgField.Password] is string)
            {
                return new FillData()
                {
                    script = await Encryption.decryptData(res.Message[MsgField.Script] as string),
                    userName = await Encryption.decryptData(res.Message[MsgField.UserName] as string),
                    password = await Encryption.decryptData(res.Message[MsgField.Password] as string)
                };
            }

            return null;
        }

        public async Task<ValueSet> RequestAccountsByWindowTitle(string windowTitle)
        {
            AppServiceResponse res = await (await GetConnection()).SendMessageAsync(new ValueSet()
            {
                { MsgField.Type, MsgType.RequestAccounts },
                { MsgField.WindowTitle, windowTitle }
            });

            if (res.Message.ContainsKey(MsgField.Type) &&
                res.Message[MsgField.Type] is string &&
                res.Message[MsgField.Type] as string == MsgType.AccountsFound &&
                res.Message[MsgField.AccountList] is ValueSet)
            {
                return res.Message[MsgField.AccountList] as ValueSet;
            }

            return null;
        }
    }
}
