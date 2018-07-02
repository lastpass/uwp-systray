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
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace CommunicationInterface
{
    public static class MsgField
    {
        public static readonly string Type = "type";
        public static readonly string WindowTitle = "windowTitle";
        public static readonly string Script = "script";
        public static readonly string UserName = "userName";
        public static readonly string Password = "password";
        public static readonly string Path = "path";
        public static readonly string Args = "args";
        public static readonly string AccountList = "accountList";
        public static readonly string AccountId = "accountId";
    }

    public static class MsgType
    {
        public static readonly string Exit = "exit";
        public static readonly string RequestAccounts = "requestAccounts";
        public static readonly string RequestScript = "requestScript";
        public static readonly string ScriptFound = "scriptFound";
        public static readonly string Ready = "ready";
        public static readonly string PrepareForReprompt = "prepareForReprompt";
        public static readonly string LoggingOff = "loggingOff";
        public static readonly string LoggingIn = "loggingIn";
        public static readonly string ScriptNotFound = "scriptNotFound";
        public static readonly string LaunchApp = "launchApp";
        public static readonly string AccountsFound = "accountsFound";
    }

    public struct FillData
    {
        public string script;
        public string userName;
        public string password;
    }

    public class Encryption
    {
        public static async Task<string> encryptData(string str)
        {
            IBuffer buffMsg = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);

            DataProtectionProvider Provider = new DataProtectionProvider("LOCAL=user");
            return CryptographicBuffer.EncodeToBase64String(await Provider.ProtectAsync(buffMsg));
        }

        public static async Task<string> decryptData(string str)
        {
            DataProtectionProvider Provider = new DataProtectionProvider("LOCAL=user");
            IBuffer decryptedBuffer = await Provider.UnprotectAsync(CryptographicBuffer.DecodeFromBase64String(str));

            return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, decryptedBuffer);
        }
    }
}
