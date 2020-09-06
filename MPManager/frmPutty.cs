using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace SessionManagement
{
	// Token: 0x0200000A RID: 10
	public partial class frmPutty : DockContent
	{
		// Token: 0x060000A8 RID: 168
		[DllImport("user32.dll")]
		private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		// Token: 0x060000A9 RID: 169
		[DllImport("User32.dll")]
		private static extern int SetForegroundWindow(IntPtr point);

		// Token: 0x060000AA RID: 170
		[DllImport("USER32.DLL")]
		public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int cx, int cy, bool repaint);

		// Token: 0x060000AB RID: 171
		[DllImport("user32.dll")]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height, uint uFlags);

		// Token: 0x060000AC RID: 172
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		// Token: 0x060000AD RID: 173
		[DllImport("user32.dll")]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

		// Token: 0x060000AE RID: 174 RVA: 0x0000A2EE File Offset: 0x000084EE
		public frmPutty()
		{
			this.InitializeComponent();
			this.info = null;
			this.proc = null;
			this.autoThread = null;
			this.session = new Session();
		}

		// Token: 0x060000AF RID: 175 RVA: 0x0000A327 File Offset: 0x00008527
		public frmPutty(Session sess)
		{
			this.InitializeComponent();
			this.info = null;
			this.proc = null;
			this.session = sess;
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x0000A358 File Offset: 0x00008558
		public void openConnection()
		{
			try
			{
				string str = "";
				str = str + " -load \"" + this.session.sessionConfig + "\"";
				str = str + " -" + this.session.sessionProtocol.ToLower();
				if (this.session.sessionPort != -1)
				{
					str = str + " -P " + this.session.sessionPort.ToString();
				}

				if (session.sessionUserName.Length > 0)
				{
					str = str + " -l " + session.sessionUserName;
				}

				if (session.sessionPassword.Length > 0)
				{
					str = str + " -pw " + session.sessionPassword;
				}

				this.info = new ProcessStartInfo(Global.strPuttyLocation);
				this.info.Arguments = str + " " + this.session.sessionHost;
				this.info.UseShellExecute = false;
				this.info.CreateNoWindow = false;
				this.info.WindowStyle = ProcessWindowStyle.Hidden;
				this.info.RedirectStandardInput = true;
				this.info.RedirectStandardOutput = true;
				this.info.RedirectStandardError = true;
				this.proc = Process.Start(this.info);
				this.proc.WaitForInputIdle();
				frmPutty.SetParent(this.proc.MainWindowHandle, this.pnMain.Handle);
				this.moveWindow(this.proc.MainWindowHandle, this.pnMain.Size);
				this.Text = this.session.sessionName;
				if (Global.arrOpenSessions == null)
				{
					Global.arrOpenSessions = new ArrayList();
				}
				Global.arrOpenSessions.Add(this.session);
			}
			catch (Exception ex)
			{
			}
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x0000A51C File Offset: 0x0000871C
		public void autoLoginAndRunCommands()
		{
			if (Control.ModifierKeys != Keys.Control)
			{
				this.autoThread = new Thread(delegate()
				{
					this.autoCommands();
				});
				this.autoThread.Start();
			}
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x0000A564 File Offset: 0x00008764
		private void autoCommands()
		{
			try
			{
				if (this.session.sessionUserName != "" && this.session.sessionPassword != "")
				{
					Thread.Sleep(this.session.connectionTimeout);
					if (this.session.postLogin)
					{
						for (int i = 0; i < this.session.postLoginCommands.Count; i++)
						{
							this.runCommand(this.session.postLoginCommands[i].ToString());
							Thread.Sleep(this.session.commandTimeout);
						}
					}
				}
				this.autoThread.Abort();
			}
			catch (Exception ex)
			{
				this.autoThread.Abort();
			}
		}

		// Token: 0x060000B3 RID: 179 RVA: 0x0000A6A8 File Offset: 0x000088A8
		public System.Threading.ThreadState getMultiCommandsThreadState()
		{
			System.Threading.ThreadState result;
			if (this.multiCommandsThread != null)
			{
				result = this.multiCommandsThread.ThreadState;
			}
			else
			{
				result = System.Threading.ThreadState.Stopped;
			}
			return result;
		}

		// Token: 0x060000B4 RID: 180 RVA: 0x0000A6D8 File Offset: 0x000088D8
		private void runCommand(string command)
		{
			try
			{
				if (this.proc != null && this.proc.Responding)
				{
					Global.sendString(this.proc.MainWindowHandle, command);
				}
			}
			catch (Exception ex)
			{
			}
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x0000A734 File Offset: 0x00008934
		private void multiCommands(ArrayList arrCommands)
		{
			try
			{
				string[] array = new string[0];
				for (int i = 0; i < arrCommands.Count; i++)
				{
					if (!this.multiCommandsThread.IsAlive)
					{
						break;
					}
					array = arrCommands[i].ToString().Split(new char[]
					{
						';'
					});
					this.runCommand(array[0]);
					Thread.Sleep(int.Parse(array[1]));
				}
				this.multiCommandsThread.Abort();
			}
			catch (Exception ex)
			{
				this.multiCommandsThread.Abort();
			}
		}

		// Token: 0x060000B6 RID: 182 RVA: 0x0000A800 File Offset: 0x00008A00
		public void runMultiCommands(ArrayList arrCommands)
		{
			if (this.proc != null)
			{
				try
				{
					if (!this.proc.HasExited)
					{
						if (this.multiCommandsThread == null || !this.multiCommandsThread.IsAlive)
						{
							this.multiCommandsThread = null;
							this.multiCommandsThread = new Thread(delegate()
							{
								this.multiCommands(arrCommands);
							});
							this.multiCommandsThread.Start();
						}
					}
				}
				catch (Exception ex)
				{
				}
			}
		}

		// Token: 0x060000B7 RID: 183 RVA: 0x0000A8B4 File Offset: 0x00008AB4
		public void stopRunningMultiCommands()
		{
			if (this.multiCommandsThread != null)
			{
				try
				{
					if (this.multiCommandsThread.IsAlive)
					{
						this.multiCommandsThread.Abort();
					}
				}
				catch (Exception ex)
				{
					this.multiCommandsThread.Abort();
				}
			}
		}

		// Token: 0x060000B8 RID: 184 RVA: 0x0000A918 File Offset: 0x00008B18
		public void moveWindow(IntPtr handle, Size size)
		{
			try
			{
				frmPutty.MoveWindow(handle, -9, -30, size.Width + 18, size.Height + 39, true);
			}
			catch (Exception ex)
			{
			}
		}

		// Token: 0x060000B9 RID: 185 RVA: 0x0000A960 File Offset: 0x00008B60
		private void frmPutty_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (this.autoThread != null)
			{
				this.autoThread.Abort();
			}
			if (this.multiCommandsThread != null)
			{
				this.multiCommandsThread.Abort();
			}
			if (this.proc != null)
			{
				this.proc.Close();
			}
		}

		// Token: 0x060000BA RID: 186 RVA: 0x0000A9BC File Offset: 0x00008BBC
		private void frmPutty_FormClosed(object sender, FormClosedEventArgs e)
		{
			this.closeSession(this.session, base.Handle.ToString());
		}

		// Token: 0x060000BB RID: 187 RVA: 0x0000A9F0 File Offset: 0x00008BF0
		private void tmCheckProcess_Tick(object sender, EventArgs e)
		{
			if (this.proc.HasExited)
			{
				this.tmCheckProcess.Stop();
				base.Close();
			}
		}

		// Token: 0x060000BC RID: 188 RVA: 0x0000AA25 File Offset: 0x00008C25
		private void frmPutty_Load(object sender, EventArgs e)
		{
			this.tmCheckProcess.Start();
		}

		// Token: 0x060000BD RID: 189 RVA: 0x0000AA34 File Offset: 0x00008C34
		private void frmPutty_Resize(object sender, EventArgs e)
		{
			if (this.proc != null)
			{
				try
				{
					if (!this.proc.HasExited)
					{
						if (base.Height > 0)
						{
							this.moveWindow(this.proc.MainWindowHandle, this.pnMain.Size);
						}
					}
				}
				catch (Exception ex)
				{
				}
			}
		}

		// Token: 0x060000BE RID: 190 RVA: 0x0000AAAC File Offset: 0x00008CAC
		public string getPuttyWindowTitle()
		{
			string result;
			try
			{
				StringBuilder stringBuilder = new StringBuilder(256);
				if (frmPutty.GetWindowText(this.proc.MainWindowHandle, stringBuilder, 256) > 0)
				{
					result = stringBuilder.ToString();
				}
				else
				{
					result = null;
				}
			}
			catch (Exception ex)
			{
				result = null;
			}
			return result;
		}

		// Token: 0x060000BF RID: 191 RVA: 0x0000AB0C File Offset: 0x00008D0C
		public string getCurrentDirectory()
		{
			string result;
			try
			{
				string puttyWindowTitle = this.getPuttyWindowTitle();
				if (puttyWindowTitle == null)
				{
					result = null;
				}
				else
				{
					string[] array = puttyWindowTitle.Split(new char[]
					{
						':'
					});
					result = array[1];
				}
			}
			catch (Exception ex)
			{
				result = null;
			}
			return result;
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x0000AB68 File Offset: 0x00008D68
		public int getPuttyProcessStatus()
		{
			return 0;
		}

		// Token: 0x040000A2 RID: 162
		private const uint SHOWWINDOW = 64u;

		// Token: 0x040000A3 RID: 163
		public ProcessStartInfo info;

		// Token: 0x040000A4 RID: 164
		public Process proc;

		// Token: 0x040000A5 RID: 165
		public Session session;

		// Token: 0x040000A6 RID: 166
		private Thread autoThread;

		// Token: 0x040000A7 RID: 167
		private Thread multiCommandsThread;

		// Token: 0x040000A8 RID: 168
		public frmPutty.CloseSession closeSession;

		// Token: 0x0200000B RID: 11
		// (Invoke) Token: 0x060000C5 RID: 197
		public delegate void CloseSession(Session sess, string puttyName);
	}
}
