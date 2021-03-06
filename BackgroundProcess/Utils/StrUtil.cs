/**
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * Copyright(c) 2018 LastPass.
 */

﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using System.Drawing;
using System.Security.Cryptography;

using BackgroundProcess.Native;

namespace BackgroundProcess.Utils
{
    /// <summary>
    /// Character stream class.
    /// </summary>
    public sealed class CharStream
    {
        private string m_strString = string.Empty;
        private int m_nPos = 0;

        public CharStream(string str)
        {
            Debug.Assert(str != null);
            if (str == null) throw new ArgumentNullException("str");

            m_strString = str;
        }

        public void Seek(SeekOrigin org, int nSeek)
        {
            if (org == SeekOrigin.Begin)
                m_nPos = nSeek;
            else if (org == SeekOrigin.Current)
                m_nPos += nSeek;
            else if (org == SeekOrigin.End)
                m_nPos = m_strString.Length + nSeek;
        }

        public char ReadChar()
        {
            if (m_nPos < 0) return char.MinValue;
            if (m_nPos >= m_strString.Length) return char.MinValue;

            char chRet = m_strString[m_nPos];
            ++m_nPos;
            return chRet;
        }

        public char ReadChar(bool bSkipWhiteSpace)
        {
            if (bSkipWhiteSpace == false) return ReadChar();

            while (true)
            {
                char ch = ReadChar();

                if ((ch != ' ') && (ch != '\t') && (ch != '\r') && (ch != '\n'))
                    return ch;
            }
        }

        public char PeekChar()
        {
            if (m_nPos < 0) return char.MinValue;
            if (m_nPos >= m_strString.Length) return char.MinValue;

            return m_strString[m_nPos];
        }

        public char PeekChar(bool bSkipWhiteSpace)
        {
            if (bSkipWhiteSpace == false) return PeekChar();

            int iIndex = m_nPos;
            while (true)
            {
                if (iIndex < 0) return char.MinValue;
                if (iIndex >= m_strString.Length) return char.MinValue;

                char ch = m_strString[iIndex];

                if ((ch != ' ') && (ch != '\t') && (ch != '\r') && (ch != '\n'))
                    return ch;

                ++iIndex;
            }
        }
    }

    public enum StrEncodingType
    {
        Unknown = 0,
        Default,
        Ascii,
        Utf7,
        Utf8,
        Utf16LE,
        Utf16BE,
        Utf32LE,
        Utf32BE
    }

    public sealed class StrEncodingInfo
    {
        private readonly StrEncodingType m_type;
        public StrEncodingType Type
        {
            get { return m_type; }
        }

        private readonly string m_strName;
        public string Name
        {
            get { return m_strName; }
        }

        private readonly Encoding m_enc;
        public Encoding Encoding
        {
            get { return m_enc; }
        }

        private readonly uint m_cbCodePoint;
        /// <summary>
        /// Size of a character in bytes.
        /// </summary>
        public uint CodePointSize
        {
            get { return m_cbCodePoint; }
        }

        private readonly byte[] m_vSig;
        /// <summary>
        /// Start signature of the text (byte order mark).
        /// May be <c>null</c> or empty, if no signature is known.
        /// </summary>
        public byte[] StartSignature
        {
            get { return m_vSig; }
        }

        public StrEncodingInfo(StrEncodingType t, string strName, Encoding enc,
            uint cbCodePoint, byte[] vStartSig)
        {
            if (strName == null) throw new ArgumentNullException("strName");
            if (enc == null) throw new ArgumentNullException("enc");
            if (cbCodePoint <= 0) throw new ArgumentOutOfRangeException("cbCodePoint");

            m_type = t;
            m_strName = strName;
            m_enc = enc;
            m_cbCodePoint = cbCodePoint;
            m_vSig = vStartSig;
        }
    }

    /// <summary>
    /// A class containing various string helper methods.
    /// </summary>
    public static class StrUtil
    {
        public const StringComparison CaseIgnoreCmp = StringComparison.OrdinalIgnoreCase;

        public static StringComparer CaseIgnoreComparer
        {
            get { return StringComparer.OrdinalIgnoreCase; }
        }

        private static bool m_bRtl = false;
        public static bool RightToLeft
        {
            get { return m_bRtl; }
            set { m_bRtl = value; }
        }

        private static UTF8Encoding m_encUtf8 = null;
        public static UTF8Encoding Utf8
        {
            get
            {
                if (m_encUtf8 == null) m_encUtf8 = new UTF8Encoding(false, false);
                return m_encUtf8;
            }
        }

        private static List<StrEncodingInfo> m_lEncs = null;
        public static IEnumerable<StrEncodingInfo> Encodings
        {
            get
            {
                if (m_lEncs != null) return m_lEncs;

                List<StrEncodingInfo> l = new List<StrEncodingInfo>();

                l.Add(new StrEncodingInfo(StrEncodingType.Default,
                    Encoding.Default.EncodingName,
                    Encoding.Default,
                    (uint)Encoding.Default.GetBytes("a").Length, null));

                l.Add(new StrEncodingInfo(StrEncodingType.Ascii,
                    "ASCII", Encoding.ASCII, 1, null));
                l.Add(new StrEncodingInfo(StrEncodingType.Utf7,
                    "Unicode (UTF-7)", Encoding.UTF7, 1, null));
                l.Add(new StrEncodingInfo(StrEncodingType.Utf8,
                    "Unicode (UTF-8)", StrUtil.Utf8, 1, new byte[] { 0xEF, 0xBB, 0xBF }));
                l.Add(new StrEncodingInfo(StrEncodingType.Utf16LE,
                    "Unicode (UTF-16 LE)", new UnicodeEncoding(false, false),
                    2, new byte[] { 0xFF, 0xFE }));
                l.Add(new StrEncodingInfo(StrEncodingType.Utf16BE,
                    "Unicode (UTF-16 BE)", new UnicodeEncoding(true, false),
                    2, new byte[] { 0xFE, 0xFF }));

                l.Add(new StrEncodingInfo(StrEncodingType.Utf32LE,
                    "Unicode (UTF-32 LE)", new UTF32Encoding(false, false),
                    4, new byte[] { 0xFF, 0xFE, 0x0, 0x0 }));
                l.Add(new StrEncodingInfo(StrEncodingType.Utf32BE,
                    "Unicode (UTF-32 BE)", new UTF32Encoding(true, false),
                    4, new byte[] { 0x0, 0x0, 0xFE, 0xFF }));

                m_lEncs = l;
                return l;
            }
        }

        // public static string RtfPar
        // {
        //	// get { return (m_bRtl ? "\\rtlpar " : "\\par "); }
        //	get { return "\\par "; }
        // }

        // /// <summary>
        // /// Convert a string into a valid RTF string.
        // /// </summary>
        // /// <param name="str">Any string.</param>
        // /// <returns>RTF-encoded string.</returns>
        // public static string MakeRtfString(string str)
        // {
        //	Debug.Assert(str != null); if(str == null) throw new ArgumentNullException("str");
        //	str = str.Replace("\\", "\\\\");
        //	str = str.Replace("\r", string.Empty);
        //	str = str.Replace("{", "\\{");
        //	str = str.Replace("}", "\\}");
        //	str = str.Replace("\n", StrUtil.RtfPar);
        //	StringBuilder sbEncoded = new StringBuilder();
        //	for(int i = 0; i < str.Length; ++i)
        //	{
        //		char ch = str[i];
        //		if((int)ch >= 256)
        //			sbEncoded.Append(StrUtil.RtfEncodeChar(ch));
        //		else sbEncoded.Append(ch);
        //	}
        //	return sbEncoded.ToString();
        // }

        public static string RtfEncodeChar(char ch)
        {
            // Unicode character values must be encoded using
            // 16-bit numbers (decimal); Unicode values greater
            // than 32767 must be expressed as negative numbers
            short sh = (short)ch;
            return ("\\u" + sh.ToString(NumberFormatInfo.InvariantInfo) + "?");
        }

        public static string XmlToString(string str)
        {
            Debug.Assert(str != null); if (str == null) throw new ArgumentNullException("str");

            str = str.Replace(@"&amp;", @"&");
            str = str.Replace(@"&lt;", @"<");
            str = str.Replace(@"&gt;", @">");
            str = str.Replace(@"&quot;", "\"");
            str = str.Replace(@"&#39;", "\'");

            return str;
        }

        public static string ReplaceCaseInsensitive(string strString, string strFind,
            string strNew)
        {
            Debug.Assert(strString != null); if (strString == null) return strString;
            Debug.Assert(strFind != null); if (strFind == null) return strString;
            Debug.Assert(strNew != null); if (strNew == null) return strString;

            string str = strString;

            int nPos = 0;
            while (nPos < str.Length)
            {
                nPos = str.IndexOf(strFind, nPos, StringComparison.OrdinalIgnoreCase);
                if (nPos < 0) break;

                str = str.Remove(nPos, strFind.Length);
                str = str.Insert(nPos, strNew);

                nPos += strNew.Length;
            }

            return str;
        }

        /// <summary>
        /// Split up a command line into application and argument.
        /// </summary>
        /// <param name="strCmdLine">Command line to split.</param>
        /// <param name="strApp">Application path.</param>
        /// <param name="strArgs">Arguments.</param>
        public static void SplitCommandLine(string strCmdLine, out string strApp, out string strArgs)
        {
            Debug.Assert(strCmdLine != null); if (strCmdLine == null) throw new ArgumentNullException("strCmdLine");

            string str = strCmdLine.Trim();

            strApp = null; strArgs = null;

            if (str.StartsWith("\""))
            {
                int nSecond = str.IndexOf('\"', 1);
                if (nSecond >= 1)
                {
                    strApp = str.Substring(1, nSecond - 1).Trim();
                    strArgs = str.Remove(0, nSecond + 1).Trim();
                }
            }

            if (strApp == null)
            {
                int nSpace = str.IndexOf(' ');

                if (nSpace >= 0)
                {
                    strApp = str.Substring(0, nSpace);
                    strArgs = str.Remove(0, nSpace).Trim();
                }
                else strApp = strCmdLine;
            }

            if (strApp == null) strApp = string.Empty;
            if (strArgs == null) strArgs = string.Empty;
        }

        // /// <summary>
        // /// Initialize an RTF document based on given font face and size.
        // /// </summary>
        // /// <param name="sb"><c>StringBuilder</c> to put the generated RTF into.</param>
        // /// <param name="strFontFace">Face name of the font to use.</param>
        // /// <param name="fFontSize">Size of the font to use.</param>
        // public static void InitRtf(StringBuilder sb, string strFontFace, float fFontSize)
        // {
        //	Debug.Assert(sb != null); if(sb == null) throw new ArgumentNullException("sb");
        //	Debug.Assert(strFontFace != null); if(strFontFace == null) throw new ArgumentNullException("strFontFace");
        //	sb.Append("{\\rtf1");
        //	if(m_bRtl) sb.Append("\\fbidis");
        //	sb.Append("\\ansi\\ansicpg");
        //	sb.Append(Encoding.Default.CodePage);
        //	sb.Append("\\deff0{\\fonttbl{\\f0\\fswiss MS Sans Serif;}{\\f1\\froman\\fcharset2 Symbol;}{\\f2\\fswiss ");
        //	sb.Append(strFontFace);
        //	sb.Append(";}{\\f3\\fswiss Arial;}}");
        //	sb.Append("{\\colortbl\\red0\\green0\\blue0;}");
        //	if(m_bRtl) sb.Append("\\rtldoc");
        //	sb.Append("\\deflang1031\\pard\\plain\\f2\\cf0 ");
        //	sb.Append("\\fs");
        //	sb.Append((int)(fFontSize * 2));
        //	if(m_bRtl) sb.Append("\\rtlpar\\qr\\rtlch ");
        // }

        // /// <summary>
        // /// Convert a simple HTML string to an RTF string.
        // /// </summary>
        // /// <param name="strHtmlString">Input HTML string.</param>
        // /// <returns>RTF string representing the HTML input string.</returns>
        // public static string SimpleHtmlToRtf(string strHtmlString)
        // {
        //	StringBuilder sb = new StringBuilder();
        //	StrUtil.InitRtf(sb, "Microsoft Sans Serif", 8.25f);
        //	sb.Append(" ");
        //	string str = MakeRtfString(strHtmlString);
        //	str = str.Replace("<b>", "\\b ");
        //	str = str.Replace("</b>", "\\b0 ");
        //	str = str.Replace("<i>", "\\i ");
        //	str = str.Replace("</i>", "\\i0 ");
        //	str = str.Replace("<u>", "\\ul ");
        //	str = str.Replace("</u>", "\\ul0 ");
        //	str = str.Replace("<br />", StrUtil.RtfPar);
        //	sb.Append(str);
        //	return sb.ToString();
        // }

        /// <summary>
        /// Convert a <c>Color</c> to a HTML color identifier string.
        /// </summary>
        /// <param name="color">Color to convert.</param>
        /// <param name="bEmptyIfTransparent">If this is <c>true</c>, an empty string
        /// is returned if the color is transparent.</param>
        /// <returns>HTML color identifier string.</returns>
        public static string ColorToUnnamedHtml(Color color, bool bEmptyIfTransparent)
        {
            if (bEmptyIfTransparent && (color.A != 255))
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            byte bt;

            sb.Append('#');

            bt = (byte)(color.R >> 4);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
            bt = (byte)(color.R & 0x0F);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

            bt = (byte)(color.G >> 4);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
            bt = (byte)(color.G & 0x0F);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

            bt = (byte)(color.B >> 4);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));
            bt = (byte)(color.B & 0x0F);
            if (bt < 10) sb.Append((char)('0' + bt)); else sb.Append((char)('A' - 10 + bt));

            return sb.ToString();
        }

        public static bool TryParseUShort(string str, out ushort u)
        {
            return ushort.TryParse(str, out u);
        }

        public static bool TryParseInt(string str, out int n)
        {
            return int.TryParse(str, out n);
        }

        public static bool TryParseIntInvariant(string str, out int n)
        {
            return int.TryParse(str, NumberStyles.Integer,
                NumberFormatInfo.InvariantInfo, out n);
        }

        public static bool TryParseUInt(string str, out uint u)
        {
            return uint.TryParse(str, out u);
        }

        public static bool TryParseUIntInvariant(string str, out uint u)
        {
            return uint.TryParse(str, NumberStyles.Integer,
                NumberFormatInfo.InvariantInfo, out u);
        }

        public static bool TryParseLong(string str, out long n)
        {
            return long.TryParse(str, out n);
        }

        public static bool TryParseLongInvariant(string str, out long n)
        {
            return long.TryParse(str, NumberStyles.Integer,
                NumberFormatInfo.InvariantInfo, out n);
        }

        public static bool TryParseULong(string str, out ulong u)
        {
            return ulong.TryParse(str, out u);
        }

        public static bool TryParseULongInvariant(string str, out ulong u)
        {
            return ulong.TryParse(str, NumberStyles.Integer,
                NumberFormatInfo.InvariantInfo, out u);
        }

        public static bool TryParseDateTime(string str, out DateTime dt)
        {
            return DateTime.TryParse(str, out dt);
        }

        public static string CompactString3Dots(string strText, int nMaxChars)
        {
            Debug.Assert(strText != null);
            if (strText == null) throw new ArgumentNullException("strText");
            Debug.Assert(nMaxChars >= 0);
            if (nMaxChars < 0) throw new ArgumentOutOfRangeException("nMaxChars");

            if (nMaxChars == 0) return string.Empty;
            if (strText.Length <= nMaxChars) return strText;

            if (nMaxChars <= 3) return strText.Substring(0, nMaxChars);

            return strText.Substring(0, nMaxChars - 3) + "...";
        }

        public static string GetStringBetween(string strText, int nStartIndex,
            string strStart, string strEnd)
        {
            int nTemp;
            return GetStringBetween(strText, nStartIndex, strStart, strEnd, out nTemp);
        }

        public static string GetStringBetween(string strText, int nStartIndex,
            string strStart, string strEnd, out int nInnerStartIndex)
        {
            if (strText == null) throw new ArgumentNullException("strText");
            if (strStart == null) throw new ArgumentNullException("strStart");
            if (strEnd == null) throw new ArgumentNullException("strEnd");

            nInnerStartIndex = -1;

            int nIndex = strText.IndexOf(strStart, nStartIndex);
            if (nIndex < 0) return string.Empty;

            nIndex += strStart.Length;

            int nEndIndex = strText.IndexOf(strEnd, nIndex);
            if (nEndIndex < 0) return string.Empty;

            nInnerStartIndex = nIndex;
            return strText.Substring(nIndex, nEndIndex - nIndex);
        }

        /// <summary>
        /// Removes all characters that are not valid XML characters,
        /// according to https://www.w3.org/TR/xml/#charsets .
        /// </summary>
        /// <param name="strText">Source text.</param>
        /// <returns>Text containing only valid XML characters.</returns>
        public static string SafeXmlString(string strText)
        {
            Debug.Assert(strText != null); // No throw
            if (string.IsNullOrEmpty(strText)) return strText;

            int nLength = strText.Length;
            StringBuilder sb = new StringBuilder(nLength);

            for (int i = 0; i < nLength; ++i)
            {
                char ch = strText[i];

                if (((ch >= '\u0020') && (ch <= '\uD7FF')) ||
                    (ch == '\u0009') || (ch == '\u000A') || (ch == '\u000D') ||
                    ((ch >= '\uE000') && (ch <= '\uFFFD')))
                    sb.Append(ch);
                else if ((ch >= '\uD800') && (ch <= '\uDBFF')) // High surrogate
                {
                    if ((i + 1) < nLength)
                    {
                        char chLow = strText[i + 1];
                        if ((chLow >= '\uDC00') && (chLow <= '\uDFFF')) // Low sur.
                        {
                            sb.Append(ch);
                            sb.Append(chLow);
                            ++i;
                        }
                        else { Debug.Assert(false); } // Low sur. invalid
                    }
                    else { Debug.Assert(false); } // Low sur. missing
                }

                Debug.Assert((ch < '\uDC00') || (ch > '\uDFFF')); // Lonely low sur.
            }

            return sb.ToString();
        }

        public static string RemoveAccelerator(string strMenuText)
        {
            if (strMenuText == null) throw new ArgumentNullException("strMenuText");

            string str = strMenuText;

            for (char ch = 'A'; ch <= 'Z'; ++ch)
            {
                string strEnhAcc = @"(&" + ch.ToString() + @")";
                if (str.IndexOf(strEnhAcc) >= 0)
                {
                    str = str.Replace(@" " + strEnhAcc, string.Empty);
                    str = str.Replace(strEnhAcc, string.Empty);
                }
            }

            str = str.Replace(@"&", string.Empty);

            return str;
        }

        public static string AddAccelerator(string strMenuText,
            List<char> lAvailKeys)
        {
            if (strMenuText == null) { Debug.Assert(false); return null; }
            if (lAvailKeys == null) { Debug.Assert(false); return strMenuText; }

            int xa = -1, xs = 0;
            for (int i = 0; i < strMenuText.Length; ++i)
            {
                char ch = strMenuText[i];

                char chUpper = char.ToUpperInvariant(ch);
                xa = lAvailKeys.IndexOf(chUpper);
                if (xa >= 0) { xs = i; break; }

                char chLower = char.ToLowerInvariant(ch);
                xa = lAvailKeys.IndexOf(chLower);
                if (xa >= 0) { xs = i; break; }
            }

            if (xa < 0) return strMenuText;

            lAvailKeys.RemoveAt(xa);
            return strMenuText.Insert(xs, @"&");
        }

        public static string EncodeMenuText(string strText)
        {
            if (strText == null) throw new ArgumentNullException("strText");

            return strText.Replace(@"&", @"&&");
        }

        public static string EncodeToolTipText(string strText)
        {
            if (strText == null) throw new ArgumentNullException("strText");

            return strText.Replace(@"&", @"&&&");
        }

        public static bool IsHexString(string str, bool bStrict)
        {
            if (str == null) throw new ArgumentNullException("str");

            foreach (char ch in str)
            {
                if ((ch >= '0') && (ch <= '9')) continue;
                if ((ch >= 'a') && (ch <= 'f')) continue;
                if ((ch >= 'A') && (ch <= 'F')) continue;

                if (bStrict) return false;

                if ((ch == ' ') || (ch == '\t') || (ch == '\r') || (ch == '\n'))
                    continue;

                return false;
            }

            return true;
        }

        public static bool IsHexString(byte[] pbUtf8, bool bStrict)
        {
            if (pbUtf8 == null) throw new ArgumentNullException("pbUtf8");

            for (int i = 0; i < pbUtf8.Length; ++i)
            {
                byte bt = pbUtf8[i];
                if ((bt >= (byte)'0') && (bt <= (byte)'9')) continue;
                if ((bt >= (byte)'a') && (bt <= (byte)'f')) continue;
                if ((bt >= (byte)'A') && (bt <= (byte)'F')) continue;

                if (bStrict) return false;

                if ((bt == (byte)' ') || (bt == (byte)'\t') ||
                    (bt == (byte)'\r') || (bt == (byte)'\n'))
                    continue;

                return false;
            }

            return true;
        }

        private static readonly char[] m_vPatternPartsSep = new char[] { '*' };
        public static bool SimplePatternMatch(string strPattern, string strText,
            StringComparison sc)
        {
            if (strPattern == null) throw new ArgumentNullException("strPattern");
            if (strText == null) throw new ArgumentNullException("strText");

            if (strPattern.IndexOf('*') < 0) return strText.Equals(strPattern, sc);

            string[] vPatternParts = strPattern.Split(m_vPatternPartsSep,
                StringSplitOptions.RemoveEmptyEntries);
            if (vPatternParts == null) { Debug.Assert(false); return true; }
            if (vPatternParts.Length == 0) return true;

            if (strText.Length == 0) return false;

            if (!strPattern.StartsWith(@"*") && !strText.StartsWith(vPatternParts[0], sc))
            {
                return false;
            }

            if (!strPattern.EndsWith(@"*") && !strText.EndsWith(vPatternParts[
                vPatternParts.Length - 1], sc))
            {
                return false;
            }

            int iOffset = 0;
            for (int i = 0; i < vPatternParts.Length; ++i)
            {
                string strPart = vPatternParts[i];

                int iFound = strText.IndexOf(strPart, iOffset, sc);
                if (iFound < iOffset) return false;

                iOffset = iFound + strPart.Length;
                if (iOffset == strText.Length) return (i == (vPatternParts.Length - 1));
            }

            return true;
        }

        public static bool StringToBool(string str)
        {
            if (string.IsNullOrEmpty(str)) return false; // No assert

            string s = str.Trim().ToLower();
            if (s == "true") return true;
            if (s == "yes") return true;
            if (s == "1") return true;
            if (s == "enabled") return true;
            if (s == "checked") return true;

            return false;
        }

        public static bool? StringToBoolEx(string str)
        {
            if (string.IsNullOrEmpty(str)) return null;

            string s = str.Trim().ToLower();
            if (s == "true") return true;
            if (s == "false") return false;

            return null;
        }

        public static string BoolToString(bool bValue)
        {
            return (bValue ? "true" : "false");
        }

        public static string BoolToStringEx(bool? bValue)
        {
            if (bValue.HasValue) return BoolToString(bValue.Value);
            return "null";
        }

        /// <summary>
        /// Normalize new line characters in a string. Input strings may
        /// contain mixed new line character sequences from all commonly
        /// used operating systems (i.e. \r\n from Windows, \n from Unix
        /// and \r from Mac OS.
        /// </summary>
        /// <param name="str">String with mixed new line characters.</param>
        /// <param name="bWindows">If <c>true</c>, new line characters
        /// are normalized for Windows (\r\n); if <c>false</c>, new line
        /// characters are normalized for Unix (\n).</param>
        /// <returns>String with normalized new line characters.</returns>
        public static string NormalizeNewLines(string str, bool bWindows)
        {
            if (string.IsNullOrEmpty(str)) return str;

            str = str.Replace("\r\n", "\n");
            str = str.Replace("\r", "\n");

            if (bWindows) str = str.Replace("\n", "\r\n");

            return str;
        }

        public static string AlphaNumericOnly(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < str.Length; ++i)
            {
                char ch = str[i];
                if (((ch >= 'a') && (ch <= 'z')) || ((ch >= 'A') && (ch <= 'Z')) ||
                    ((ch >= '0') && (ch <= '9')))
                    sb.Append(ch);
            }

            return sb.ToString();
        }

        public static string FormatDataSize(ulong uBytes)
        {
            const ulong uKB = 1024;
            const ulong uMB = uKB * uKB;
            const ulong uGB = uMB * uKB;
            const ulong uTB = uGB * uKB;

            if (uBytes == 0) return "0 KB";
            if (uBytes <= uKB) return "1 KB";
            if (uBytes <= uMB) return (((uBytes - 1UL) / uKB) + 1UL).ToString() + " KB";
            if (uBytes <= uGB) return (((uBytes - 1UL) / uMB) + 1UL).ToString() + " MB";
            if (uBytes <= uTB) return (((uBytes - 1UL) / uGB) + 1UL).ToString() + " GB";

            return (((uBytes - 1UL) / uTB) + 1UL).ToString() + " TB";
        }

        public static string FormatDataSizeKB(ulong uBytes)
        {
            const ulong uKB = 1024;

            if (uBytes == 0) return "0 KB";
            if (uBytes <= uKB) return "1 KB";

            return (((uBytes - 1UL) / uKB) + 1UL).ToString() + " KB";
        }

        private static readonly char[] m_vVersionSep = new char[] { '.', ',' };
        public static ulong ParseVersion(string strVersion)
        {
            if (strVersion == null) { Debug.Assert(false); return 0; }

            string[] vVer = strVersion.Split(m_vVersionSep);
            if ((vVer == null) || (vVer.Length == 0)) { Debug.Assert(false); return 0; }

            ushort uPart;
            StrUtil.TryParseUShort(vVer[0].Trim(), out uPart);
            ulong uVer = ((ulong)uPart << 48);

            if (vVer.Length >= 2)
            {
                StrUtil.TryParseUShort(vVer[1].Trim(), out uPart);
                uVer |= ((ulong)uPart << 32);
            }

            if (vVer.Length >= 3)
            {
                StrUtil.TryParseUShort(vVer[2].Trim(), out uPart);
                uVer |= ((ulong)uPart << 16);
            }

            if (vVer.Length >= 4)
            {
                StrUtil.TryParseUShort(vVer[3].Trim(), out uPart);
                uVer |= (ulong)uPart;
            }

            return uVer;
        }

        public static string VersionToString(ulong uVersion)
        {
            return VersionToString(uVersion, 1U);
        }

        [Obsolete]
        public static string VersionToString(ulong uVersion,
            bool bEnsureAtLeastTwoComp)
        {
            return VersionToString(uVersion, (bEnsureAtLeastTwoComp ? 2U : 1U));
        }

        public static string VersionToString(ulong uVersion, uint uMinComp)
        {
            StringBuilder sb = new StringBuilder();
            uint uComp = 0;

            for (int i = 0; i < 4; ++i)
            {
                if (uVersion == 0UL) break;

                ushort us = (ushort)(uVersion >> 48);

                if (sb.Length > 0) sb.Append('.');

                sb.Append(us.ToString(NumberFormatInfo.InvariantInfo));
                ++uComp;

                uVersion <<= 16;
            }

            while (uComp < uMinComp)
            {
                if (sb.Length > 0) sb.Append('.');

                sb.Append('0');
                ++uComp;
            }

            return sb.ToString();
        }

        private static readonly byte[] m_pbOptEnt = { 0xA5, 0x74, 0x2E, 0xEC };

        public static string EncryptString(string strPlainText)
        {
            if (string.IsNullOrEmpty(strPlainText)) return string.Empty;

            try
            {
                byte[] pbPlain = StrUtil.Utf8.GetBytes(strPlainText);
                byte[] pbEnc = ProtectedData.Protect(pbPlain, m_pbOptEnt,
                    DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(pbEnc, Base64FormattingOptions.None);
            }
            catch (Exception) { Debug.Assert(false); }

            return strPlainText;
        }

        public static string DecryptString(string strCipherText)
        {
            if (string.IsNullOrEmpty(strCipherText)) return string.Empty;

            try
            {
                byte[] pbEnc = Convert.FromBase64String(strCipherText);
                byte[] pbPlain = ProtectedData.Unprotect(pbEnc, m_pbOptEnt,
                    DataProtectionScope.CurrentUser);

                return StrUtil.Utf8.GetString(pbPlain, 0, pbPlain.Length);
            }
            catch (Exception) { Debug.Assert(false); }

            return strCipherText;
        }

        public static string SerializeIntArray(int[] vNumbers)
        {
            if (vNumbers == null) throw new ArgumentNullException("vNumbers");

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < vNumbers.Length; ++i)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(vNumbers[i].ToString(NumberFormatInfo.InvariantInfo));
            }

            return sb.ToString();
        }

        public static int[] DeserializeIntArray(string strSerialized)
        {
            if (strSerialized == null) throw new ArgumentNullException("strSerialized");
            if (strSerialized.Length == 0) return new int[0];

            string[] vParts = strSerialized.Split(' ');
            int[] v = new int[vParts.Length];

            for (int i = 0; i < vParts.Length; ++i)
            {
                int n;
                if (!TryParseIntInvariant(vParts[i], out n)) { Debug.Assert(false); }
                v[i] = n;
            }

            return v;
        }

        private static readonly char[] m_vTagSep = new char[] { ',', ';', ':' };
        public static string TagsToString(List<string> vTags, bool bForDisplay)
        {
            if (vTags == null) throw new ArgumentNullException("vTags");

            StringBuilder sb = new StringBuilder();
            bool bFirst = true;

            foreach (string strTag in vTags)
            {
                if (string.IsNullOrEmpty(strTag)) { Debug.Assert(false); continue; }
                Debug.Assert(strTag.IndexOfAny(m_vTagSep) < 0);

                if (!bFirst)
                {
                    if (bForDisplay) sb.Append(", ");
                    else sb.Append(';');
                }
                sb.Append(strTag);

                bFirst = false;
            }

            return sb.ToString();
        }

        public static List<string> StringToTags(string strTags)
        {
            if (strTags == null) throw new ArgumentNullException("strTags");

            List<string> lTags = new List<string>();
            if (strTags.Length == 0) return lTags;

            string[] vTags = strTags.Split(m_vTagSep);
            foreach (string strTag in vTags)
            {
                string strFlt = strTag.Trim();
                if (strFlt.Length > 0) lTags.Add(strFlt);
            }

            return lTags;
        }

        public static string Obfuscate(string strPlain)
        {
            if (strPlain == null) { Debug.Assert(false); return string.Empty; }
            if (strPlain.Length == 0) return string.Empty;

            byte[] pb = StrUtil.Utf8.GetBytes(strPlain);

            Array.Reverse(pb);
            for (int i = 0; i < pb.Length; ++i) pb[i] = (byte)(pb[i] ^ 0x65);

            return Convert.ToBase64String(pb, Base64FormattingOptions.None);
        }

        public static string Deobfuscate(string strObf)
        {
            if (strObf == null) { Debug.Assert(false); return string.Empty; }
            if (strObf.Length == 0) return string.Empty;

            try
            {
                byte[] pb = Convert.FromBase64String(strObf);

                for (int i = 0; i < pb.Length; ++i) pb[i] = (byte)(pb[i] ^ 0x65);
                Array.Reverse(pb);

                return StrUtil.Utf8.GetString(pb, 0, pb.Length);
            }
            catch (Exception) { Debug.Assert(false); }

            return string.Empty;
        }

        /// <summary>
        /// Split a string and include the separators in the splitted array.
        /// </summary>
        /// <param name="str">String to split.</param>
        /// <param name="vSeps">Separators.</param>
        /// <param name="bCaseSensitive">Specifies whether separators are
        /// matched case-sensitively or not.</param>
        /// <returns>Splitted string including separators.</returns>
        public static List<string> SplitWithSep(string str, string[] vSeps,
            bool bCaseSensitive)
        {
            if (str == null) throw new ArgumentNullException("str");
            if (vSeps == null) throw new ArgumentNullException("vSeps");

            List<string> v = new List<string>();
            while (true)
            {
                int minIndex = int.MaxValue, minSep = -1;
                for (int i = 0; i < vSeps.Length; ++i)
                {
                    string strSep = vSeps[i];
                    if (string.IsNullOrEmpty(strSep)) { Debug.Assert(false); continue; }

                    int iIndex = (bCaseSensitive ? str.IndexOf(strSep) :
                        str.IndexOf(strSep, StrUtil.CaseIgnoreCmp));
                    if ((iIndex >= 0) && (iIndex < minIndex))
                    {
                        minIndex = iIndex;
                        minSep = i;
                    }
                }

                if (minIndex == int.MaxValue) break;

                v.Add(str.Substring(0, minIndex));
                v.Add(vSeps[minSep]);

                str = str.Substring(minIndex + vSeps[minSep].Length);
            }

            v.Add(str);
            return v;
        }

        public static string MultiToSingleLine(string strMulti)
        {
            if (strMulti == null) { Debug.Assert(false); return string.Empty; }
            if (strMulti.Length == 0) return string.Empty;

            string str = strMulti;
            str = str.Replace("\r\n", " ");
            str = str.Replace("\r", " ");
            str = str.Replace("\n", " ");

            return str;
        }

        public static List<string> SplitSearchTerms(string strSearch)
        {
            List<string> l = new List<string>();
            if (strSearch == null) { Debug.Assert(false); return l; }

            StringBuilder sbTerm = new StringBuilder();
            bool bQuoted = false;

            for (int i = 0; i < strSearch.Length; ++i)
            {
                char ch = strSearch[i];

                if (((ch == ' ') || (ch == '\t') || (ch == '\r') ||
                    (ch == '\n')) && !bQuoted)
                {
                    if (sbTerm.Length > 0) l.Add(sbTerm.ToString());

                    sbTerm.Remove(0, sbTerm.Length);
                }
                else if (ch == '\"') bQuoted = !bQuoted;
                else sbTerm.Append(ch);
            }
            if (sbTerm.Length > 0) l.Add(sbTerm.ToString());

            return l;
        }

        public static int CompareLengthGt(string x, string y)
        {
            if (x.Length == y.Length) return 0;
            return ((x.Length > y.Length) ? -1 : 1);
        }

        public static bool IsDataUri(string strUri)
        {
            return IsDataUri(strUri, null);
        }

        public static bool IsDataUri(string strUri, string strReqMimeType)
        {
            if (strUri == null) { Debug.Assert(false); return false; }
            // strReqMimeType may be null

            const string strPrefix = "data:";
            if (!strUri.StartsWith(strPrefix, StrUtil.CaseIgnoreCmp))
                return false;

            int iC = strUri.IndexOf(',');
            if (iC < 0) return false;

            if (!string.IsNullOrEmpty(strReqMimeType))
            {
                int iS = strUri.IndexOf(';', 0, iC);
                int iTerm = ((iS >= 0) ? iS : iC);

                string strMime = strUri.Substring(strPrefix.Length,
                    iTerm - strPrefix.Length);
                if (!strMime.Equals(strReqMimeType, StrUtil.CaseIgnoreCmp))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Create a data URI (according to RFC 2397).
        /// </summary>
        /// <param name="pbData">Data to encode.</param>
        /// <param name="strMimeType">Optional MIME type. If <c>null</c>,
        /// an appropriate type is used.</param>
        /// <returns>Data URI.</returns>
        public static string DataToDataUri(byte[] pbData, string strMimeType)
        {
            if (pbData == null) throw new ArgumentNullException("pbData");

            if (strMimeType == null) strMimeType = "application/octet-stream";

            return ("data:" + strMimeType + ";base64," + Convert.ToBase64String(
                pbData, Base64FormattingOptions.None));
        }

        /// <summary>
        /// Convert a data URI (according to RFC 2397) to binary data.
        /// </summary>
        /// <param name="strDataUri">Data URI to decode.</param>
        /// <returns>Decoded binary data.</returns>
        public static byte[] DataUriToData(string strDataUri)
        {
            if (strDataUri == null) throw new ArgumentNullException("strDataUri");
            if (!strDataUri.StartsWith("data:", StrUtil.CaseIgnoreCmp)) return null;

            int iSep = strDataUri.IndexOf(',');
            if (iSep < 0) return null;

            string strDesc = strDataUri.Substring(5, iSep - 5);
            bool bBase64 = strDesc.EndsWith(";base64", StrUtil.CaseIgnoreCmp);

            string strData = strDataUri.Substring(iSep + 1);

            if (bBase64) return Convert.FromBase64String(strData);

            MemoryStream ms = new MemoryStream();
            Encoding enc = Encoding.ASCII;

            string[] v = strData.Split('%');
            byte[] pb = enc.GetBytes(v[0]);
            ms.Write(pb, 0, pb.Length);
            for (int i = 1; i < v.Length; ++i)
            {
                ms.WriteByte(Convert.ToByte(v[i].Substring(0, 2), 16));
                pb = enc.GetBytes(v[i].Substring(2));
                ms.Write(pb, 0, pb.Length);
            }

            pb = ms.ToArray();
            ms.Close();
            return pb;
        }

        /// <summary>
        /// Remove placeholders from a string (wrapped in '{' and '}').
        /// This doesn't remove environment variables (wrapped in '%').
        /// </summary>
        public static string RemovePlaceholders(string str)
        {
            if (str == null) { Debug.Assert(false); return string.Empty; }

            while (true)
            {
                int iPlhStart = str.IndexOf('{');
                if (iPlhStart < 0) break;

                int iPlhEnd = str.IndexOf('}', iPlhStart); // '{' might be at end
                if (iPlhEnd < 0) break;

                str = (str.Substring(0, iPlhStart) + str.Substring(iPlhEnd + 1));
            }

            return str;
        }

        public static StrEncodingInfo GetEncoding(StrEncodingType t)
        {
            foreach (StrEncodingInfo sei in StrUtil.Encodings)
            {
                if (sei.Type == t) return sei;
            }

            return null;
        }

        public static StrEncodingInfo GetEncoding(string strName)
        {
            foreach (StrEncodingInfo sei in StrUtil.Encodings)
            {
                if (sei.Name == strName) return sei;
            }

            return null;
        }

        public static char ByteToSafeChar(byte bt)
        {
            const char chDefault = '.';

            // 00-1F are C0 control chars
            if (bt < 0x20) return chDefault;

            // 20-7F are basic Latin; 7F is DEL
            if (bt < 0x7F) return (char)bt;

            // 80-9F are C1 control chars
            if (bt < 0xA0) return chDefault;

            // A0-FF are Latin-1 supplement; AD is soft hyphen
            if (bt == 0xAD) return '-';
            return (char)bt;
        }

        public static int Count(string str, string strNeedle)
        {
            if (str == null) { Debug.Assert(false); return 0; }
            if (string.IsNullOrEmpty(strNeedle)) { Debug.Assert(false); return 0; }

            int iOffset = 0, iCount = 0;
            while (iOffset < str.Length)
            {
                int p = str.IndexOf(strNeedle, iOffset);
                if (p < 0) break;

                ++iCount;
                iOffset = p + 1;
            }

            return iCount;
        }

        internal static string ReplaceNulls(string str)
        {
            if (str == null) { Debug.Assert(false); return null; }

            if (str.IndexOf('\0') < 0) return str;

            // Replacing null characters by spaces is the
            // behavior of Notepad (on Windows 10)
            return str.Replace('\0', ' ');
        }
    }
}
