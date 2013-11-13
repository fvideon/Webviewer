using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using SHDocVw;
using System.Runtime.InteropServices;
using System.Threading;

namespace UW.CSE.CXP
{
	/// <summary>
	/// WebViewer's content selection browser form
	/// </summary>
	public class frmBrowse : System.Windows.Forms.Form, DWebBrowserEvents
	{
        System.Runtime.InteropServices.ComTypes.IConnectionPoint icp;
		private int cookie = -1; 

		private System.Windows.Forms.Panel panel1;
		private AxSHDocVw.AxWebBrowser axWebBrowser1;
		private System.Windows.Forms.Panel panel3;
		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button buttonBack;
		private System.Windows.Forms.Button buttonFwd;
		private System.Windows.Forms.Button buttonHome;
		public System.Windows.Forms.ComboBox cbUrlList;

		private System.ComponentModel.Container components = null;

		public frmBrowse(ArrayList urlList)
		{
			InitializeComponent();
			
			this.cbUrlList.Items.AddRange(urlList.ToArray());
			if (urlList.Count > 0)
			{
				this.cbUrlList.SelectedIndex = 0;
			}

			System.Runtime.InteropServices.ComTypes.IConnectionPointContainer icpc = 
                (System.Runtime.InteropServices.ComTypes.IConnectionPointContainer)axWebBrowser1.GetOcx(); // ADDed
 
			Guid g = typeof(DWebBrowserEvents).GUID;
			icpc.FindConnectionPoint(ref g, out icp);
			icp.Advise(this, out cookie);
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				// Release event sink
				if (-1 != cookie) icp.Unadvise(cookie);
				cookie = -1;
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(frmBrowse));
			this.panel1 = new System.Windows.Forms.Panel();
			this.panel2 = new System.Windows.Forms.Panel();
			this.cbUrlList = new System.Windows.Forms.ComboBox();
			this.panel3 = new System.Windows.Forms.Panel();
			this.buttonHome = new System.Windows.Forms.Button();
			this.buttonFwd = new System.Windows.Forms.Button();
			this.buttonBack = new System.Windows.Forms.Button();
			this.button1 = new System.Windows.Forms.Button();
			this.axWebBrowser1 = new AxSHDocVw.AxWebBrowser();
			this.panel1.SuspendLayout();
			this.panel2.SuspendLayout();
			this.panel3.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.axWebBrowser1)).BeginInit();
			this.SuspendLayout();
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.panel2);
			this.panel1.Controls.Add(this.panel3);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
			this.panel1.Location = new System.Drawing.Point(0, 0);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(792, 40);
			this.panel1.TabIndex = 3;
			// 
			// panel2
			// 
			this.panel2.Controls.Add(this.cbUrlList);
			this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panel2.DockPadding.All = 10;
			this.panel2.Location = new System.Drawing.Point(0, 0);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(608, 40);
			this.panel2.TabIndex = 2;
			// 
			// cbUrlList
			// 
			this.cbUrlList.Dock = System.Windows.Forms.DockStyle.Fill;
			this.cbUrlList.Location = new System.Drawing.Point(10, 10);
			this.cbUrlList.Name = "cbUrlList";
			this.cbUrlList.Size = new System.Drawing.Size(588, 21);
			this.cbUrlList.TabIndex = 0;
			this.cbUrlList.Text = "cbUrlList";
			this.cbUrlList.SelectedIndexChanged += new System.EventHandler(this.cbUrlList_SelectedIndexChanged);
			// 
			// panel3
			// 
			this.panel3.Controls.Add(this.buttonHome);
			this.panel3.Controls.Add(this.buttonFwd);
			this.panel3.Controls.Add(this.buttonBack);
			this.panel3.Controls.Add(this.button1);
			this.panel3.Dock = System.Windows.Forms.DockStyle.Right;
			this.panel3.DockPadding.All = 8;
			this.panel3.Location = new System.Drawing.Point(608, 0);
			this.panel3.Name = "panel3";
			this.panel3.Size = new System.Drawing.Size(184, 40);
			this.panel3.TabIndex = 1;
			// 
			// buttonHome
			// 
			this.buttonHome.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.buttonHome.Location = new System.Drawing.Point(8, 8);
			this.buttonHome.Name = "buttonHome";
			this.buttonHome.Size = new System.Drawing.Size(48, 24);
			this.buttonHome.TabIndex = 3;
			this.buttonHome.Text = "Home";
			this.buttonHome.Click += new System.EventHandler(this.buttonHome_Click);
			// 
			// buttonFwd
			// 
			this.buttonFwd.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.buttonFwd.Location = new System.Drawing.Point(96, 8);
			this.buttonFwd.Name = "buttonFwd";
			this.buttonFwd.Size = new System.Drawing.Size(32, 24);
			this.buttonFwd.TabIndex = 2;
			this.buttonFwd.Text = ">>";
			this.buttonFwd.Click += new System.EventHandler(this.buttonFwd_Click);
			// 
			// buttonBack
			// 
			this.buttonBack.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.buttonBack.Location = new System.Drawing.Point(64, 8);
			this.buttonBack.Name = "buttonBack";
			this.buttonBack.Size = new System.Drawing.Size(32, 24);
			this.buttonBack.TabIndex = 1;
			this.buttonBack.Text = "<<";
			this.buttonBack.Click += new System.EventHandler(this.buttonBack_Click);
			// 
			// button1
			// 
			this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.button1.Location = new System.Drawing.Point(136, 8);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(40, 24);
			this.button1.TabIndex = 0;
			this.button1.Text = "Go";
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// axWebBrowser1
			// 
			this.axWebBrowser1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.axWebBrowser1.Enabled = true;
			this.axWebBrowser1.Location = new System.Drawing.Point(0, 40);
			this.axWebBrowser1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("axWebBrowser1.OcxState")));
			this.axWebBrowser1.Size = new System.Drawing.Size(792, 590);
			this.axWebBrowser1.TabIndex = 4;
			// 
			// frmBrowse
			// 
			this.AcceptButton = this.button1;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(792, 630);
			this.Controls.Add(this.axWebBrowser1);
			this.Controls.Add(this.panel1);
			this.Name = "frmBrowse";
			this.Text = "Browse for Windows Media Content";
			this.Load += new System.EventHandler(this.frmBrowse_Load);
			this.panel1.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.panel3.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.axWebBrowser1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		private void frmBrowse_Load(object sender, System.EventArgs e)
		{
			object obj = null;
			axWebBrowser1.Navigate (this.cbUrlList.Text, ref obj, ref obj, ref obj, ref obj);
		}

		public void BeforeNavigate(string URL, int Flags, string TargetFrameName, 
			ref object PostData, string Headers, ref bool Cancel)
		{
			//Console.WriteLine("beforenavigate url=" + URL);

			if (isWmURL(URL.Trim()))
			{
				if (OnWmLinkClicked != null)
					OnWmLinkClicked(URL,new System.EventArgs());
				Cancel = true; //Cancel tells the browser not to actually navigate there.
				//this.Close();  Closing here can sometimes cause an exception deep in Windows.Forms!
				//This seemed to happen when the asx path was typed directly into the address bar.
				//A dumb workaround is to queue the close to happen after a short delay.
				//This is certainly not the "correct" solution.  Possibly use "Hide" and "Show" instead of
				//close.
				ThreadPool.QueueUserWorkItem(new WaitCallback(closeLater));
			} 
			else
			{
				this.cbUrlList.Text = URL;
			}
			
		}

		private void closeLater(object o)
		{
			Thread.Sleep(500);
			this.Close();
		}

		private bool isWmURL(string url)
		{
			string extent = (url.Substring(url.LastIndexOf("."))).ToLower();	
			if ((extent == ".wmv") ||
				(extent == ".asx") ||
				(extent == ".asf") ||
				(extent == ".wma") ||
				(extent == ".wbv"))
				return true;

			return false;
		}

		//Stuff required by the interface:
		public void PropertyChange(string Property){}

		public void NavigateComplete(string URL) //This only happens if the cancel flag on beforenavigate is false.
		{
		}

		public void WindowActivate(){}

		public void FrameBeforeNavigate(string URL, int Flags, string TargetFrameName, ref object PostData, string Headers, ref bool Cancel){}

		public void NewWindow(string URL, int Flags, string TargetFrameName, ref object PostData, string Headers, ref bool Processed){}

		public void FrameNewWindow(string URL, int Flags, string TargetFrameName, ref object PostData, string Headers, ref bool Processed){}

		public void TitleChange(string Text){}

		public void DownloadBegin(){}

		public void DownloadComplete(){}

		public void WindowMove(){}

		public void WindowResize(){}

		public void Quit(ref bool Cancel){}

		public void ProgressChange(int Progress, int ProgressMax){}

		public void StatusTextChange(string Text){}

		public void CommandStateChange(int Command, bool Enable)
		{
			switch (Command)
			{
				case 1:
					buttonFwd.Enabled = Enable;
					break;
				case 2:
					buttonBack.Enabled = Enable;
					break;
			}
		}

		public void FrameNavigateComplete(string URL)
		{
		}

		private void button1_Click(object sender, System.EventArgs e)
		{
			object obj = null;
			axWebBrowser1.Navigate(this.cbUrlList.Text,ref obj, ref obj, ref obj, ref obj);
		}

		private void buttonBack_Click(object sender, System.EventArgs e)
		{
			
			axWebBrowser1.GoBack();
		}

		private void buttonFwd_Click(object sender, System.EventArgs e)
		{
			axWebBrowser1.GoForward();
		}

		private void buttonHome_Click(object sender, System.EventArgs e)
		{
			axWebBrowser1.GoHome();
		}

		private void cbUrlList_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			Debug.WriteLine("cbUrlList selected index changed.");
			object obj = null;
			axWebBrowser1.Navigate(this.cbUrlList.Text,ref obj, ref obj, ref obj, ref obj);
		}

		public event WmLinkClickedHandler OnWmLinkClicked;

		public delegate void WmLinkClickedHandler(object link, EventArgs eventArgs);

	}
}
