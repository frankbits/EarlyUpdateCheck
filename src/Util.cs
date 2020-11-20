﻿using PluginTools;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EarlyUpdateCheck
{
	internal enum UpdateCheckType
	{
		NotRequired = 0,
		Required = 1,
		OnlyTranslations = 2,
	}

	internal enum UpdateCheckStatus
	{
		NotChecked,
		Checking,
		Checked,
		Error
	};

	internal static class PluginConfig
	{
		private static KeePass.App.Configuration.AceCustomConfig Config = KeePass.Program.Config.CustomConfig;
		internal static bool Active = true;
		internal static bool CheckSync = true;
		internal static bool OneClickUpdate = true;
		internal static bool DownloadActiveLanguage = true;

		internal static int RestoreMutexThreshold
		{
			get
			{
				int t = (int)Config.GetLong("EarlyUpdateCheck.RestoreMutexThreshold", 2000);
				Config.SetLong("EarlyUpdateCheck.RestoreMutexThreshold", t);
				return t;
			}
		}

		internal static void Read()
		{
			PluginConfig.Active = Config.GetBool("EarlyUpdateCheck.Active", PluginConfig.Active);
			PluginConfig.CheckSync = Config.GetBool("EarlyUpdateCheck.CheckSync", PluginConfig.CheckSync);
			PluginConfig.OneClickUpdate = Config.GetBool("EarlyUpdateCheck.OneClickUpdate", PluginConfig.OneClickUpdate);
			PluginConfig.DownloadActiveLanguage = Config.GetBool("EarlyUpdateCheck.DownloadActiveLanguage", PluginConfig.DownloadActiveLanguage);
		}

		internal static void Write()
		{
			Config.SetBool("EarlyUpdateCheck.Active", PluginConfig.Active);
			Config.SetBool("EarlyUpdateCheck.CheckSync", PluginConfig.CheckSync);
			Config.SetBool("EarlyUpdateCheck.OneClickUpdate", PluginConfig.OneClickUpdate);
			Config.SetBool("EarlyUpdateCheck.DownloadActiveLanguage", PluginConfig.DownloadActiveLanguage);
		}
	}

	internal static class FileCopier
	{
		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct SHFILEOPSTRUCT
		{
			internal IntPtr hwnd;
			internal FILE_OP_TYPE wFunc;
			[MarshalAs(UnmanagedType.LPWStr)]
			internal string pFrom;
			[MarshalAs(UnmanagedType.LPWStr)]
			internal string pTo;
			internal FILE_OP_FLAGS fFlags;
			[MarshalAs(UnmanagedType.Bool)]
			internal bool fAnyOperationsAborted;
			internal IntPtr hNameMappings;
			[MarshalAs(UnmanagedType.LPWStr)]
			internal string lpszProgressTitle;
		}

		private enum FILE_OP_TYPE : uint
		{
			FO_MOVE = 0x0001,
			FO_COPY = 0x0002,
			FO_DELETE = 0x0003,
			FO_RENAME = 0x0004,
		}

		[Flags]
		private enum FILE_OP_FLAGS : ushort
		{
			FOF_MULTIDESTFILES = 0x0001,
			FOF_CONFIRMMOUSE = 0x0002,
			FOF_SILENT = 0x0004,
			FOF_RENAMEONCOLLISION = 0x0008,
			FOF_NOCONFIRMATION = 0x0010,
			FOF_WANTMAPPINGHANDLE = 0x0020,
			FOF_ALLOWUNDO = 0x0040,
			FOF_FILESONLY = 0x0080,
			FOF_SIMPLEPROGRESS = 0x0100,
			FOF_NOCONFIRMMKDIR = 0x0200,
			FOF_NOERRORUI = 0x0400,
			FOF_NOCOPYSECURITYATTRIBS = 0x0800,
			FOF_NORECURSION = 0x1000,
			FOF_NO_CONNECTED_ELEMENTS = 0x2000,
			FOF_WANTNUKEWARNING = 0x4000,
			FOF_NORECURSEREPARSE = 0x8000,
		}

		internal static bool DeleteFiles(params string[] files)
		{
			bool success = false;

			string from = string.Empty;
			foreach (string file in files)
				from += file + "\0";
			from += "\0";

			SHFILEOPSTRUCT lpFileOp = new SHFILEOPSTRUCT();
			lpFileOp.hwnd = IntPtr.Zero;
			lpFileOp.wFunc = FILE_OP_TYPE.FO_DELETE;
			lpFileOp.pFrom = from;
			lpFileOp.pTo = "\0\0";
			lpFileOp.fFlags = FILE_OP_FLAGS.FOF_NOCONFIRMATION | FILE_OP_FLAGS.FOF_ALLOWUNDO;
			lpFileOp.fAnyOperationsAborted = false;
			lpFileOp.hNameMappings = IntPtr.Zero;
			lpFileOp.lpszProgressTitle = string.Empty;

			int result = SHFileOperation(ref lpFileOp);
			if (result == 0)
				success = !lpFileOp.fAnyOperationsAborted;
			PluginDebug.AddInfo("Delete in UAC mode: " + success.ToString());
			return success;
		}

		internal static bool CopyFiles(string from, string to)
		{
			bool success = false;

			from += "*\0\0";
			to += "\0\0";

			SHFILEOPSTRUCT lpFileOp = new SHFILEOPSTRUCT();
			lpFileOp.hwnd = IntPtr.Zero;
			lpFileOp.wFunc = FILE_OP_TYPE.FO_COPY;
			lpFileOp.pFrom = from;
			lpFileOp.pTo = to;
			lpFileOp.fFlags = FILE_OP_FLAGS.FOF_NOCONFIRMATION;
			lpFileOp.fAnyOperationsAborted = false;
			lpFileOp.hNameMappings = IntPtr.Zero;
			lpFileOp.lpszProgressTitle = string.Empty;

			int result = SHFileOperation(ref lpFileOp);
			if (result == 0)
				success = !lpFileOp.fAnyOperationsAborted;
			PluginDebug.AddInfo("Copy in UAC mode: " + success.ToString());
			return success;
		}
	}

	internal static class NativeMethods
	{
		[DllImport("kernel32")]
		internal static extern uint GetCurrentThreadId();

		internal delegate bool EnumWindowsProcedure(IntPtr windowHandle, IntPtr param);
		[DllImport("user32")]
		internal static extern bool EnumChildWindows(IntPtr windowHandle, EnumWindowsProcedure enumProc, IntPtr param);

		[DllImport("user32")]
		internal static extern bool EnumThreadWindows(uint threadId, EnumWindowsProcedure enumProc, IntPtr param);

		[DllImport("user32", EntryPoint = "GetClassNameW", SetLastError = true, CharSet = CharSet.Unicode)]
		internal static extern int GetClassName(IntPtr windowHandle, System.Text.StringBuilder buffer, int bufferSize);

		[DllImport("user32", EntryPoint = "GetDlgCtrlID")]
		internal static extern int GetDialogControlId(IntPtr windowHandle);

		[DllImport("user32")]
		internal static extern uint SendMessage(IntPtr param, uint message, uint wParam, uint lParam);

		//Credits go to Falahati: https://github.com/falahati/UACHelper - file WinForm.cs
		internal static DialogResult ShieldifyNativeDialog(DialogResult button, KeePassLib.Delegates.GFunc<DialogResult> dialogShowCode)
		{
			var callingThreadId = NativeMethods.GetCurrentThreadId();
			var thread = new Thread(() =>
			{
				try
				{
					var found = false;
					while (!found)
					{
						Thread.Sleep(100);
						NativeMethods.EnumThreadWindows(callingThreadId, (wnd, param) =>
						{
							var buffer = new System.Text.StringBuilder(256);
							NativeMethods.GetClassName(wnd, buffer, buffer.Capacity);
							if (buffer.ToString() == @"#32770")
							{
								ShieldifyNativeDialog(button, wnd);
								found = true;
								return false;
							}
							return true;
						}, IntPtr.Zero);
					}
				}
				catch { }
			});
			thread.IsBackground = true;
			thread.Start();
			var result = dialogShowCode();
			thread.Abort();
			return result;
		}

		internal static bool ShieldifyNativeDialog(DialogResult button, IntPtr windowHandle)
		{
			int numberOfItems = 0;
			bool notFound = NativeMethods.EnumChildWindows(windowHandle, (wnd, param) =>
			{
				var buffer = new System.Text.StringBuilder(256);
				NativeMethods.GetClassName(wnd, buffer, buffer.Capacity);
				numberOfItems++;
				if (buffer.ToString().ToLower().Contains(@"button"))
				{
					if (NativeMethods.GetDialogControlId(wnd) == (int)button)
					{
						NativeMethods.SendMessage(wnd, 0x160C, 0, 0xFFFFFFFF);
						return false;
					}
				}
				else
				{
					if (ShieldifyNativeDialog(button, wnd))
					{
						return false;
					}
				}
				return true;
			}, IntPtr.Zero);

			return !notFound && numberOfItems > 0;
		}
	}
}
