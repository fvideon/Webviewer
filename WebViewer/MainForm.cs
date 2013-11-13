using System;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Xml;
using System.Threading;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Reflection;
using Microsoft.Win32;
using WorkSpace;


namespace UW.CSE.CXP
{
	/// <summary> 
	/// Main CXP WebViewer form.
	/// </summary>
	/// Draw a LockedSlideView control, a Windows Media Player control, a Table of Contents, and 
	/// optionally a diagnostic log on the main form.
	/// Support loading media from URL, File, or via a hosted browser window.
	/// Process media metadata which may be available in the ASX file.  A reference to 
	/// presenter data found there may preloaded and made available to the slideviewer.
	/// Media without preloadable presenter data will observe embedded scripts.
	/// TOC is populated from preloaded presenter data if available, or from
	/// Marker data found in the media header.
	public class WebViewerForm : System.Windows.Forms.Form
	{
		#region Declarations

		private static string	launchFile;				//file that launched the app, if any
		private int				rsCount = 0;			//count non-url scripts received.
		private ArrayList		lastUrlList = null;		//The MRU list of URLs
		private int				lastUrlListSize = 10;	//How many URLs to remember.
		private int				TOCIndex = -1;			//current table of contents item
		private int				lastPlayState = -1;		//last known WMP state
		public	Logger			logger;					//diagnostic writer
		private SlideViewerGlue	slideViewerGlue = null;	//Pass messages to SlideViewer code
		private frmBrowse		browserForm = null;		//tie to IE window for content selection
		private bool			browserClosed = true;	//open/close state of IE window
		private ArrayList		lastBrowseUrlList;		//last url in IE window
		private int				lastBrowseUrlListSize = 10; //How many urls to remember
		private string			WebViewerHomePage;		//default for browser
		private byte[]			mergedStroke = null;	//reassembly of a fragmented stroke
		private bool			useLogFile = false;		//app.config flag
		private bool			showLog = false;		//another app.config flag
		private PresenterDataStore	pDataStore = null;	//pre-loaded annotation/slide data.
		private bool			ignoreInlineScripts = false;	//If metadata specified script data, ignore inline
		private double			ffwRwStart = 0;			//beginning media time for a ffwd/rewind operation.
		private Mutex			replayMutex = null;		//to synchronize replay threads.
		private bool			stopReplay = false;		//signal replay threads to stop.
		private Int32			replayThreadCount = 0;	//count of active and queued replay threads.
		private AutoResetEvent	replaysDone = null;		//signaled by the last replay thread to terminate.
		private string			currentUrl = null;		//url of the currently playing media;
		private ReplayQueue		replayQueue = null;
		private double			XDPIScaleFactor;		//With other than 96DPI we need to scale ink
		private double			YDPIScaleFactor;
		private bool			AutoTOC;				//If true use TOC built automatically from Presenter 2 data.
		private bool			upgradeWarningGiven;	//True if we've warned the user about unhandled Types in a stream.
		private bool			navButtonsEnabled;		//True if app.config tells us to enable the supplimentary nav buttons
		private double			jumpIncrement;			//How many seconds forward or backward the supplimentary nav buttons go.

		private System.Net.WebClient webClient1;
		private System.Windows.Forms.MainMenu mainMenu1;
		private System.Windows.Forms.MenuItem menuItem1;
		private System.Windows.Forms.MenuItem menuItem2;
		private System.Windows.Forms.MenuItem menuItem3;
		private System.Windows.Forms.MenuItem menuItem4;
		private System.Windows.Forms.OpenFileDialog openFileDialog1;
		private System.Windows.Forms.TextBox textDiagnosticLog;
		private WorkSpace.LockedSlideView lockedSlideView1;
		private System.Windows.Forms.MenuItem menuItem5;
		private System.Windows.Forms.MenuItem menuItem6;
		private System.Windows.Forms.ListBox TOClistBox;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.MenuItem menuItem7;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.StatusBar statusBar1;
		private AxWMPLib.AxWindowsMediaPlayer axWindowsMediaPlayer1;
		private System.Windows.Forms.Splitter splitter1;
		private System.Windows.Forms.Panel panel3;
		private System.Windows.Forms.Splitter splitter2;
		private System.Windows.Forms.Panel panel4;
		private System.Windows.Forms.Button buttonJumpForward;
        private System.Windows.Forms.Button buttonJumpBack;
        private IContainer components;


		#endregion

		#region Constructor/Main

		public WebViewerForm()
		{
			//The scale factor allows us to draw the UI properly for different DPI settings.
			Graphics g = this.CreateGraphics();
			XDPIScaleFactor = 96.0/g.DpiX;
			YDPIScaleFactor = 96.0/g.DpiY;
			g.Dispose();

			InitializeComponent();

			/// Panel1 contains the controls on the left side.  It has 10 px of padding along the
			/// left edge of the form so its initial width is 320+10.  Panel3 is within panel1, and
			/// only contains the WMP control.
			//Note: These size adjustments can't be done in InitializeComponent because the 
			//IDE form designer will object
			this.textDiagnosticLog.Size = new System.Drawing.Size((int)(320), 120);
			this.TOClistBox.Size = new System.Drawing.Size((int)(320), 199);
			this.panel1.Width = 330; //Panel has 10 pixels of padding on left side.
			this.panel3.Height = 240+ 64; //240 + 64 for WMP UI

			axWindowsMediaPlayer1.settings.autoStart = true;
			// with 6.4 control, used: 240 + 46 for controls + 26 for status bar == 312.
			
			//ArrayLists to support MRU lists.
			lastUrlList = new ArrayList();
			lastUrlListSize = 10;
			lastBrowseUrlList = new ArrayList();
			lastBrowseUrlListSize=10;

			replayMutex = new Mutex(false);

			//register for replay queue dequeue event.
			replayQueue = new ReplayQueue();
			replayQueue.OnPlayItem += new ReplayQueue.playItem(PlayDataItem);
			
			// Use app.config to let the user configure diagnostic logging and display.

			if (ConfigurationManager.AppSettings["CXPWebViewer.logfile"] != null)
			{
				try
				{
					useLogFile = Convert.ToBoolean(ConfigurationManager.AppSettings["CXPWebViewer.logfile"]);
				}
				catch
				{ useLogFile = false; }
			}
			if (ConfigurationManager.AppSettings["CXPWebViewer.logdisplay"] != null)
			{
				try
				{
					showLog = Convert.ToBoolean(ConfigurationManager.AppSettings["CXPWebViewer.logdisplay"]);
				}
				catch
				{ showLog = false; }
			}
			if (ConfigurationManager.AppSettings["CXPWebViewer.verboselogging"] != null)
			{
				try
				{
					Constants.VerboseLogging = Convert.ToBoolean(ConfigurationManager.AppSettings["CXPWebViewer.verboselogging"]);
				}
				catch
				{ Constants.VerboseLogging = false; }
			}

			navButtonsEnabled = false;
			jumpIncrement = 0.0;
			if (ConfigurationManager.AppSettings["CXPWebViewer.navbuttonsenabled"] != null)
			{
				try
				{
					navButtonsEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings["CXPWebViewer.navbuttonsenabled"]);
				}
				catch
				{ navButtonsEnabled = false; }
			}

			if (navButtonsEnabled)
			{
				if (ConfigurationManager.AppSettings["CXPWebViewer.jumpincrement"] != null)
				{
					try
					{
						jumpIncrement = Convert.ToDouble(ConfigurationManager.AppSettings["CXPWebViewer.jumpincrement"]);
					}
					catch
					{ jumpIncrement = 10.0; }
				}
			}

			initNavButtons();

			//prepare diagnostic writer/logger
			textDiagnosticLog.ReadOnly = true;
			textDiagnosticLog.Visible = showLog;
			String logPath = null;

			//Use an absolute path to the log file, since WBV launches us with an arbitrary cur dir.
			if (useLogFile) 
			{
				Assembly a = Assembly.GetExecutingAssembly();
				logPath = Path.GetDirectoryName(a.Location);
				logPath += @"\diagnostic_log.txt";
			}
			logger = new Logger(textDiagnosticLog,logPath,showLog,useLogFile);
			logger.Write("Application starting: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			if (useLogFile)
			{
				logger.Write("Logging to: " + logPath);
			}

			WebViewerHomePage = "http://www.cs.washington.edu/education/dl/confxp/webviewer.html"; 
			// This set of config can persist in the registry.. set defaults here
			lastUrlList.Add("http://localhost:8888");
			lastBrowseUrlList.Add(WebViewerHomePage);
			// now get the previously configured values here, if any:			
			RestoreConfig();

			//Allow Background color for the slideview to be set from config file
			if (ConfigurationManager.AppSettings["CXPWebViewer.BackgroundColor"] != null)
			{
				try
				{
					lockedSlideView1.LayerPanel.BackColor = System.Drawing.Color.FromName((ConfigurationManager.AppSettings["CXPWebViewer.BackgroundColor"]));
				}
				catch
				{
					lockedSlideView1.LayerPanel.BackColor = System.Drawing.Color.FromName("Wheat");
				}
			}
			else
			{
				lockedSlideView1.LayerPanel.BackColor = System.Drawing.Color.FromName("Wheat");
			}

			replaysDone = new AutoResetEvent(true);
			upgradeWarningGiven = false;


			//LaunchFile will contain something if:
			// -We were launched as a result of a web link click.  In this case the content will always (?) be
			//  a local path such as in the Temporary Internet Files directory.  This may vary by browser.
			//  This should always be a wbv file and should be guaranteed (?) to exist. Note some browsers
			//  require that the server indicate a custom mime type to cause the download and open to happen.
			// -We were launched as a result of a double-click on a wbv file.  In this case the content will be
			//  a local or unc path to a wbv file which should be guaranteed to exist.
			// -We were launched from a command line.  In this case the content could be anything.  Arguments
			//  after the first are ignored.
			//Since I don't know of a way to differentiate between the three cases we will assume nothing
			//special about the contents of launchFile.  The types we will handle are {wbv,wmv,wma,asx,asf}
			//LaunchFile may be a fully-qualified uri or a local or unc path.  A local path may be relative.
			if (launchFile != "")
			{
				String validPath = validateSourcePath(launchFile);
				if (validPath != null)
				{
					preloadAndOpen(validPath);
				}
				else
				{
					MessageBox.Show("WebViewer Failed to open " + launchFile + ".", "Failed to Open");
				}
			}
		}


		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if (pDataStore != null)
			{
				pDataStore.Stop();
			}

			if (replayQueue != null)
			{
				replayQueue.Stop();
			}

			if (browserClosed == false)
			{
				String newUrl = browserForm.cbUrlList.Text.Trim();
				if (! isWmURL(newUrl))
				{
					//save last URL in lastBrowseUrlList
					int i = lastBrowseUrlList.IndexOf(newUrl);
					if (i>=0)
					{
						lastBrowseUrlList.RemoveAt(i);
					}
					lastBrowseUrlList.Insert(0,newUrl);

					if (lastBrowseUrlList.Count > lastBrowseUrlListSize)
					{
						lastBrowseUrlList.RemoveAt(lastBrowseUrlList.Count-1);
					}
				}
			}

			//persist config to registry
			SaveConfig();

			logger.Write("Application closing.");
			logger.Close();

			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args) 
		{
            Application.EnableVisualStyles(); //<--Use the rounded buttons, etc. when running on XP
			launchFile = "";
			if (args.Length > 0)
			{
				launchFile = args[0];
			}
			Application.Run(new WebViewerForm());
		}
		#endregion

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WebViewerForm));
            this.webClient1 = new System.Net.WebClient();
            this.mainMenu1 = new System.Windows.Forms.MainMenu(this.components);
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.menuItem4 = new System.Windows.Forms.MenuItem();
            this.menuItem3 = new System.Windows.Forms.MenuItem();
            this.menuItem7 = new System.Windows.Forms.MenuItem();
            this.menuItem5 = new System.Windows.Forms.MenuItem();
            this.menuItem6 = new System.Windows.Forms.MenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.textDiagnosticLog = new System.Windows.Forms.TextBox();
            this.lockedSlideView1 = new WorkSpace.LockedSlideView();
            this.TOClistBox = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel4 = new System.Windows.Forms.Panel();
            this.buttonJumpBack = new System.Windows.Forms.Button();
            this.buttonJumpForward = new System.Windows.Forms.Button();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.panel3 = new System.Windows.Forms.Panel();
            this.axWindowsMediaPlayer1 = new AxWMPLib.AxWindowsMediaPlayer();
            this.panel2 = new System.Windows.Forms.Panel();
            this.statusBar1 = new System.Windows.Forms.StatusBar();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.panel1.SuspendLayout();
            this.panel4.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.axWindowsMediaPlayer1)).BeginInit();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // webClient1
            // 
            this.webClient1.BaseAddress = "";
            this.webClient1.CachePolicy = null;
            this.webClient1.Credentials = null;
            this.webClient1.Encoding = ((System.Text.Encoding)(resources.GetObject("webClient1.Encoding")));
            this.webClient1.Headers = ((System.Net.WebHeaderCollection)(resources.GetObject("webClient1.Headers")));
            this.webClient1.QueryString = ((System.Collections.Specialized.NameValueCollection)(resources.GetObject("webClient1.QueryString")));
            this.webClient1.UseDefaultCredentials = false;
            // 
            // mainMenu1
            // 
            this.mainMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem1,
            this.menuItem5});
            // 
            // menuItem1
            // 
            this.menuItem1.Index = 0;
            this.menuItem1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem2,
            this.menuItem4,
            this.menuItem3,
            this.menuItem7});
            this.menuItem1.Text = "File";
            // 
            // menuItem2
            // 
            this.menuItem2.Index = 0;
            this.menuItem2.Text = "Open URL..";
            this.menuItem2.Click += new System.EventHandler(this.menuItem2_Click);
            // 
            // menuItem4
            // 
            this.menuItem4.Index = 1;
            this.menuItem4.Text = "Open File..";
            this.menuItem4.Click += new System.EventHandler(this.menuItem4_Click);
            // 
            // menuItem3
            // 
            this.menuItem3.Index = 2;
            this.menuItem3.Text = "Open with Browser..";
            this.menuItem3.Click += new System.EventHandler(this.menuItem3_Click);
            // 
            // menuItem7
            // 
            this.menuItem7.Index = 3;
            this.menuItem7.Text = "Exit";
            this.menuItem7.Click += new System.EventHandler(this.menuItem7_Click);
            // 
            // menuItem5
            // 
            this.menuItem5.Index = 1;
            this.menuItem5.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem6});
            this.menuItem5.Text = "Help";
            // 
            // menuItem6
            // 
            this.menuItem6.Index = 0;
            this.menuItem6.Text = "About..";
            this.menuItem6.Click += new System.EventHandler(this.menuItem6_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.Filter = "Windows Media files (*.wmv,*.wma,*.asf,*.asx,*.wbv|*.wmv;*.asx;*.asf;*.wma;*.wbv|" +
                "All files|*.*";
            this.openFileDialog1.Title = "Choose a Windows Media File";
            // 
            // textDiagnosticLog
            // 
            this.textDiagnosticLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.textDiagnosticLog.Location = new System.Drawing.Point(0, 143);
            this.textDiagnosticLog.Multiline = true;
            this.textDiagnosticLog.Name = "textDiagnosticLog";
            this.textDiagnosticLog.ReadOnly = true;
            this.textDiagnosticLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textDiagnosticLog.Size = new System.Drawing.Size(320, 120);
            this.textDiagnosticLog.TabIndex = 3;
            this.textDiagnosticLog.Text = "textBox1";
            this.textDiagnosticLog.WordWrap = false;
            // 
            // lockedSlideView1
            // 
            this.lockedSlideView1.AspectRatio = 1.333333F;
            this.lockedSlideView1.Data = null;
            this.lockedSlideView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lockedSlideView1.FitToSlide = false;
            this.lockedSlideView1.Location = new System.Drawing.Point(0, 10);
            this.lockedSlideView1.Name = "lockedSlideView1";
            this.lockedSlideView1.Scrollable = false;
            this.lockedSlideView1.Size = new System.Drawing.Size(572, 583);
            this.lockedSlideView1.TabIndex = 5;
            // 
            // TOClistBox
            // 
            this.TOClistBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TOClistBox.Location = new System.Drawing.Point(0, 32);
            this.TOClistBox.Name = "TOClistBox";
            this.TOClistBox.Size = new System.Drawing.Size(320, 108);
            this.TOClistBox.TabIndex = 6;
            this.TOClistBox.DoubleClick += new System.EventHandler(this.TOClistBox_DoubleClick);
            this.TOClistBox.SelectedIndexChanged += new System.EventHandler(this.TOClistBox_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(320, 32);
            this.label1.TabIndex = 7;
            this.label1.Text = "Table of Contents for Current Media";
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.panel4);
            this.panel1.Controls.Add(this.splitter2);
            this.panel1.Controls.Add(this.panel3);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(10, 10, 0, 0);
            this.panel1.Size = new System.Drawing.Size(330, 593);
            this.panel1.TabIndex = 8;
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.buttonJumpBack);
            this.panel4.Controls.Add(this.buttonJumpForward);
            this.panel4.Controls.Add(this.TOClistBox);
            this.panel4.Controls.Add(this.textDiagnosticLog);
            this.panel4.Controls.Add(this.label1);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(10, 330);
            this.panel4.Name = "panel4";
            this.panel4.Size = new System.Drawing.Size(320, 263);
            this.panel4.TabIndex = 11;
            // 
            // buttonJumpBack
            // 
            this.buttonJumpBack.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonJumpBack.Location = new System.Drawing.Point(254, 0);
            this.buttonJumpBack.Name = "buttonJumpBack";
            this.buttonJumpBack.Size = new System.Drawing.Size(32, 16);
            this.buttonJumpBack.TabIndex = 9;
            this.buttonJumpBack.Text = "<<";
            this.buttonJumpBack.Visible = false;
            this.buttonJumpBack.Click += new System.EventHandler(this.buttonJumpBack_Click);
            // 
            // buttonJumpForward
            // 
            this.buttonJumpForward.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonJumpForward.Location = new System.Drawing.Point(286, 0);
            this.buttonJumpForward.Name = "buttonJumpForward";
            this.buttonJumpForward.Size = new System.Drawing.Size(32, 16);
            this.buttonJumpForward.TabIndex = 8;
            this.buttonJumpForward.Text = ">>";
            this.buttonJumpForward.Visible = false;
            this.buttonJumpForward.Click += new System.EventHandler(this.buttonJumpForward_Click);
            // 
            // splitter2
            // 
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitter2.Location = new System.Drawing.Point(10, 322);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(320, 8);
            this.splitter2.TabIndex = 10;
            this.splitter2.TabStop = false;
            this.splitter2.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitter2_SplitterMoved);
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.axWindowsMediaPlayer1);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel3.Location = new System.Drawing.Point(10, 10);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(320, 312);
            this.panel3.TabIndex = 9;
            // 
            // axWindowsMediaPlayer1
            // 
            this.axWindowsMediaPlayer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.axWindowsMediaPlayer1.Enabled = true;
            this.axWindowsMediaPlayer1.Location = new System.Drawing.Point(0, 0);
            this.axWindowsMediaPlayer1.Name = "axWindowsMediaPlayer1";
            this.axWindowsMediaPlayer1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("axWindowsMediaPlayer1.OcxState")));
            this.axWindowsMediaPlayer1.Size = new System.Drawing.Size(320, 312);
            this.axWindowsMediaPlayer1.TabIndex = 8;
            this.axWindowsMediaPlayer1.PlayStateChange += new AxWMPLib._WMPOCXEvents_PlayStateChangeEventHandler(this.axWindowsMediaPlayer1_PlayStateChange);
            this.axWindowsMediaPlayer1.MarkerHit += new AxWMPLib._WMPOCXEvents_MarkerHitEventHandler(this.axWindowsMediaPlayer1_MarkerHit);
            this.axWindowsMediaPlayer1.ScriptCommand += new AxWMPLib._WMPOCXEvents_ScriptCommandEventHandler(this.axWindowsMediaPlayer1_ScriptCommand);
            this.axWindowsMediaPlayer1.CurrentItemChange += new AxWMPLib._WMPOCXEvents_CurrentItemChangeEventHandler(this.axWindowsMediaPlayer1_CurrentItemChange);
            this.axWindowsMediaPlayer1.OpenStateChange += new AxWMPLib._WMPOCXEvents_OpenStateChangeEventHandler(this.axWindowsMediaPlayer1_OpenStateChange);
            this.axWindowsMediaPlayer1.PositionChange += new AxWMPLib._WMPOCXEvents_PositionChangeEventHandler(this.axWindowsMediaPlayer1_PositionChange);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.lockedSlideView1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(330, 0);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(0, 10, 10, 0);
            this.panel2.Size = new System.Drawing.Size(582, 593);
            this.panel2.TabIndex = 9;
            // 
            // statusBar1
            // 
            this.statusBar1.Location = new System.Drawing.Point(0, 593);
            this.statusBar1.Name = "statusBar1";
            this.statusBar1.Size = new System.Drawing.Size(912, 16);
            this.statusBar1.TabIndex = 6;
            // 
            // splitter1
            // 
            this.splitter1.Location = new System.Drawing.Point(330, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(8, 593);
            this.splitter1.TabIndex = 10;
            this.splitter1.TabStop = false;
            this.splitter1.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitter1_SplitterMoved);
            // 
            // WebViewerForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(912, 609);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.statusBar1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Menu = this.mainMenu1;
            this.Name = "WebViewerForm";
            this.Text = "CXP Web Viewer";
            this.panel1.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            this.panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.axWindowsMediaPlayer1)).EndInit();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

		}
		#endregion

		#region MediaPlayer Events
		private void axWindowsMediaPlayer1_OpenStateChange(object sender, AxWMPLib._WMPOCXEvents_OpenStateChangeEvent e)
		{
			Debug.WriteLine("OpenStateChange newState=" + e.newState.ToString());
			if (e.newState == 13) //MediaOpen
			{
				/// This includes the cases where the user clicked the previous item in playlist button
				/// to reopen the current media, where the media goes from 'Ready' state (for example after
				/// playing to the end) to play, and where the media is opened initially.
				/// Note that if the user clicked on the TOC while the media was in Ready state, we will want
				/// to begin somewhere other than time zero.
				Debug.WriteLine("WMP_OpenStateChange calling jumpAndRefresh pos=" + axWindowsMediaPlayer1.Ctlcontrols.currentPosition.ToString());
				jumpAndRefresh(0,axWindowsMediaPlayer1.Ctlcontrols.currentPosition);
				fillTOC();
				
				if ((AutoTOC) && (pDataStore != null))
				{
					//Setting TOCIndex first will disable the selectedIndexChanged event handler for the TOClistBox.
					TOCIndex = pDataStore.GetTocIndex(axWindowsMediaPlayer1.Ctlcontrols.currentPosition);
					Debug.WriteLine("WMP_OpenStateChange setting tocindex to " + TOCIndex.ToString());
					this.TOClistBox.SelectedIndex = TOCIndex;
				}
			}
		}

		/// <summary>
		/// Accept inline script commands
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void axWindowsMediaPlayer1_ScriptCommand(object sender, AxWMPLib._WMPOCXEvents_ScriptCommandEvent e)
		{
			if (ignoreInlineScripts)
				return;

			char[] trimAr = {'0','1','2','3','4','5','6','7','8','9'};
			byte[] outb;
			string outs;
			if (e.scType == "URL") //This type is only for the benefit of non-webviewer users.
			{ 
				//LoggerWriteInvoke("Script command type: URL Param: " + e.param.ToString());
			}
			else if ((e.scType == "CXP0") || (e.scType == "CXP3"))
			{   // CXP0 indicates this is either a whole packet or the final piece of a fragmented packet.
				outb = unpackData(e.param);
				if (outb == null)
					return;

				rsCount++;

				if (mergedStroke != null) 
				{
					byte[] tmpb = new byte[mergedStroke.Length + outb.Length];
					mergedStroke.CopyTo(tmpb,0);
					outb.CopyTo(tmpb,mergedStroke.Length);
					outb = tmpb;
					mergedStroke = null;
				}

				if (e.scType == "CXP0")
				{
					LoggerWriteInvoke("Type: " + e.scType + " count: " 
						+ rsCount.ToString() + " size: " + outb.Length + " opcode:" 
						+ outb[0].ToString() );
				}
				else if (e.scType == "CXP3")
				{
					LoggerWriteInvoke("Type: " + e.scType + " count: " 
						+ rsCount.ToString() + " size: " + outb.Length );
				}

				if (slideViewerGlue != null)
				{
					if (e.scType == "CXP0")
						slideViewerGlue.Receive(outb);
					else if (e.scType == "CXP3")
						slideViewerGlue.ReceiveRTNav(outb);
				}
			} 
			else if ((e.scType == "CXP1") || (e.scType == "CXP4"))  //this is part of a fragmented packet, but not the final part.
			{
				LoggerWriteInvoke("Script command type: " + e.scType );
				outb = unpackData(e.param);
				if (outb == null)
					return;
				
				if (mergedStroke == null)
				{
					mergedStroke = outb;
				}
				else
				{
					byte[] tmpb = new byte[mergedStroke.Length + outb.Length];
					mergedStroke.CopyTo(tmpb,0);
					outb.CopyTo(tmpb,mergedStroke.Length);
					mergedStroke = tmpb;
				}
			}
			else if (e.scType == "CXP2")  //The same data as URL, just not URL type, and packed. Presenter 1.1.03 only.
			{
				outs = unpackString(e.param);
				if (slideViewerGlue != null)
				{
					slideViewerGlue.Extent = outs.Substring(outs.LastIndexOf(".")+1);
					slideViewerGlue.BaseURL = (outs.Substring(0,outs.LastIndexOf("."))).TrimEnd(trimAr);
				}
				LoggerWriteInvoke("Type: CXP2 Param: " + outs );

			}
			else 
			{
				if ((e.scType.StartsWith("CXP")) && (!upgradeWarningGiven))
				{
					upgradeWarningGiven = true;
					MessageBox.Show("Your WebViewer version may require an upgrade to properly display presentation data in this stream. \r\n" +
						"For more information visit the URL in Help menu --> About.", "WebViewer Upgrade Recommended");
				}
				LoggerWriteInvoke("Unhandled script command type: " + e.scType );
			}

		}

		///  Update a marker-based TOC when we hit a marker.
		///  Otherwise we rely on a PresenterDataStore event to tell us when to update the SelectedIndex.
		private void axWindowsMediaPlayer1_MarkerHit(object sender, AxWMPLib._WMPOCXEvents_MarkerHitEvent e)
		{
			if (!AutoTOC)
			{
				TOCIndex = e.markerNum - 1;
				TOClistBox.SelectedIndex = TOCIndex;
			}
		}

		private void axWindowsMediaPlayer1_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
		{
			LoggerWriteInvoke("Playstate change.  Newstate:" + e.newState.ToString());
			Debug.WriteLine("Playstate change.  Newstate=" + e.newState.ToString() + " oldstate=" + lastPlayState.ToString());
			if ((e.newState == 1) || (e.newState == 10)) //transition to stop or "ready"
			{
				if (AutoTOC)
					TOCIndex = 0;
				else
					TOCIndex = getMarkerNum(0)-1;

				Debug.WriteLine("WMP_PlayStateChange calling jumpAndRefresh");
				TOClistBox.SelectedIndex = TOCIndex;
				jumpAndRefresh(0,0);
			}

			// transition from fastforward/rewind, but not to stop (which we have already handled):
			if ((e.newState != 1) && ((lastPlayState == 4) || (lastPlayState == 5)))
			{
				if (AutoTOC)
				{
					TOCIndex = pDataStore.GetTocIndex(axWindowsMediaPlayer1.Ctlcontrols.currentPosition);
				}
				else
					TOCIndex = getMarkerNum(axWindowsMediaPlayer1.Ctlcontrols.currentPosition) - 1;

				TOClistBox.SelectedIndex = TOCIndex;
				jumpAndRefresh(ffwRwStart, axWindowsMediaPlayer1.Ctlcontrols.currentPosition);

			}

			// transition to ffwd/rewind
			if ((e.newState == 4) || (e.newState == 5))
			{
				ffwRwStart = axWindowsMediaPlayer1.Ctlcontrols.currentPosition;
			}

			lastPlayState = e.newState;
		}

		// update TOC to reflect new postion.
		private void axWindowsMediaPlayer1_PositionChange(object sender, AxWMPLib._WMPOCXEvents_PositionChangeEvent e)
		{
			if (AutoTOC)
			{
				TOCIndex = pDataStore.GetTocIndex(e.newPosition);				
			}
			else
				TOCIndex = getMarkerNum(e.newPosition) -1;

			Debug.WriteLine("WMP_PositionChange: new TOC index: " + TOCIndex.ToString());
			Debug.WriteLine("WMP_PositionChange: old=" + e.oldPosition.ToString() + " new=" + e.newPosition.ToString());

			TOClistBox.SelectedIndex = TOCIndex;
			
			jumpAndRefresh(e.oldPosition,e.newPosition);

		}

		/// <summary>
		/// Change in playlist item
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void axWindowsMediaPlayer1_CurrentItemChange(object sender, AxWMPLib._WMPOCXEvents_CurrentItemChangeEvent e)
		{
		}

		#endregion

		#region Utility

		/// <summary>
		/// Initialize the optional navigation buttons.
		/// </summary>
		private void initNavButtons()
		{
			if (!navButtonsEnabled)
			{
				this.buttonJumpBack.Visible = false;
				this.buttonJumpForward.Visible = false;
				return;
			}

			this.buttonJumpBack.Visible = true;
			this.buttonJumpForward.Visible = true;

			this.buttonJumpBack.Enabled = false;
			this.buttonJumpForward.Enabled = false;

			ToolTip tt = new ToolTip();
			tt.SetToolTip(this.buttonJumpBack,"Navigate back by " + this.jumpIncrement.ToString() + " seconds.");
			tt.SetToolTip(this.buttonJumpForward,"Navigate forward by " + this.jumpIncrement.ToString() + " seconds.");
		}


		/// Input data is base64 encoded and packed up using both bytes of each unicode character.
		/// Output is unpacked and decoded to a byte array.
		private byte[] unpackData(string param)
		{
			char[] ca = param.ToCharArray();
			char[] outa;
			ushort tmp;
			tmp = (ushort)ca[0];
			outa = new char[param.Length*2];
			for(int i = 0; i<ca.Length;i++)
			{
				tmp = (ushort)((ushort)(ca[i] << 8) >> 8);
				outa[i*2] = (char)tmp;
				outa[(i*2)+1]= (char)(ca[i] >> 8);
			}
				
			byte[] outb;
			try 
			{
				outb = Convert.FromBase64CharArray(outa,0,outa.Length);
			}
			catch
			{
				LoggerWriteInvoke("Exception while decoding script.");
				return null;
			}
			return outb;

		}

		/// <summary>
		/// Convert a string of ascii packed into a unicode string to a normal managed string.
		/// </summary>
		/// <param name="param"></param>
		/// <returns></returns>
		private string unpackString(string param)
		{
			char[] ca = param.ToCharArray();
			char[] outa;
			string outs;
			ushort tmp;
			tmp = (ushort)ca[0];
			outa = new char[param.Length*2];
			for(int i = 0; i<ca.Length;i++)
			{
				tmp = (ushort)((ushort)(ca[i] << 8) >> 8);
				outa[i*2] = (char)tmp;
				outa[(i*2)+1]= (char)(ca[i] >> 8);
			}

			//be sure there isn't an extra null at the end when the string is created:
			if (outa[param.Length*2-1] == '\0')
				outs = new string(outa,0,(param.Length*2)-1);
			else
				outs = new string(outa);

			return outs;
		}


		/// <summary>
		/// Given a media position in seconds, find the previous marker number (1-based)
		/// </summary>
		/// <param name="pos"></param>
		/// <returns></returns>
		private int getMarkerNum(double pos)
		{
			int mcount = axWindowsMediaPlayer1.currentMedia.markerCount;
			int i = 0;
			if (mcount > 0) 
			{
				for (i=1; i<=mcount; i++)
				{
					if (axWindowsMediaPlayer1.currentMedia.getMarkerTime(i) > pos)
					{
						break;
					}
				}
				i--;
			}
			return i;
		}

		/// <summary>
		/// Try to guess the current slide number from text in the marker table
		/// </summary>
		/// <param name="pos">Current position in the media in seconds</param>
		/// <returns>slide number, or zero indicating unknown slide</returns>
		/// If the marker strings are formed with the substring "Slide N" where 
		/// N is an integer, return N.
		private int guessSlideNum(double pos)
		{
			int markerNum = getMarkerNum(pos);
			string markerText;
			Regex re;
			int sn=0;
			if (markerNum > 0)
			{
				markerText = axWindowsMediaPlayer1.currentMedia.getMarkerName(markerNum);
				re = new Regex("Slide (?<snum>[0-9]*)");
				Match match = re.Match(markerText);
				if (match.Success)
				{
					sn = Convert.ToInt32(match.Result("${snum}"));
				}
				
			}
			return sn;
		}


		/// <summary>
		/// Reset/rebuild slideViewer in response to a change in media position
		/// </summary>
		/// When the user does ffwd/rewind, drags the media slider, or jumps to a marker we need to 
		/// refresh the state of the slideviewer control.  What we are able to do depends upon the existence of 
		/// the PresenterDataStore.  If there is no preloaded presenter data we will simply find the 
		/// current slide, and display it.  If there is data, we will synchronize all relevant presenter state.
		/// <param name="jumpFrom">old media position in whole and fractional seconds</param>
		/// <param name="jumpTo">new media position in whole and fractional seconds</param>
		private void jumpAndRefresh(double jumpFrom, double jumpTo)
		{
			PresenterDataItem[] rData;
			object slideref;
			ScreenConfiguration sc;

			Debug.WriteLine("jumpAndRefresh from=" + jumpFrom.ToString() + " to=" + jumpTo.ToString());
			
			if (slideViewerGlue == null)
				return;

			if (pDataStore == null) //no preloaded data
			{
				// get current slide from marker table
				int slideNum = guessSlideNum(jumpTo);
				if ( slideNum != 0) 
				{
					slideViewerGlue.ShowSlide(slideNum);
				}
			} 
			else 
			{
				if ((jumpFrom > jumpTo) || (jumpTo==0)) //backward jump or jump to start
				{
					//get data from time zero up through jumpTo time
					rData = pDataStore.Jump(0,jumpTo);
					LoggerWriteInvoke("backward jump. Data item count: " + rData.Length.ToString());
					

					//In the case of a backward jump, we should signal any existing replay threads to terminate,
					// then wait until they do so.
					if (replayThreadCount > 0)
					{
						stopReplay = true;
						replaysDone.WaitOne();  
						stopReplay = false;
						//empty the queue of data items that may have arrived while the threads were working.
						replayQueue.Clear();
					}

					//clear existing annotation data in the slideViewer control
					slideViewerGlue.ClearAnnotations();

				}
				else  //forward jump or no change.
				{
					//get the data between jumpFrom and jumpTo, inclusive.
					rData = pDataStore.Jump(jumpFrom, jumpTo);
					LoggerWriteInvoke("forward jump. Data item count: " + rData.Length.ToString());
				}


				//Discover and set current slide
				slideref = pDataStore.GetSlideNum(jumpTo);//This will return "-1" if none is found in the next 3 seconds. Even with Presenter 2 data.
				if (slideref is ArchiveRTNav.RTUpdate)
					slideViewerGlue.ShowSlide((ArchiveRTNav.RTUpdate)slideref); //also sets sc and background color.
				else if (slideref is String)
				{
					int cslide = Convert.ToInt32(slideref);
					if (cslide >= 0)
						slideViewerGlue.ShowSlide(cslide);

					//Discover and set current screen configuration
					sc = pDataStore.GetScreenConfiguration(jumpTo);
					slideViewerGlue.SetScreenConfiguration(sc);
				}

				//store new stream-synchronized presentation data in a queue until replays are complete.
				replayQueue.Hold();

				//Replay data to restore presentation state
				Interlocked.Increment(ref replayThreadCount);
				replaysDone.Reset();
				ThreadPool.QueueUserWorkItem(new WaitCallback(replayPresenterDataThread),rData);
			}
		}


		/// <summary>
		/// Restore the slideview state by replaying an array of presenter data
		/// </summary>
		/// <param name="o">PresenterDataItem array to replay</param>
		/// We should never have more than one of these threads sending data to the
		/// SlideViewer at one time.  We use a Mutex to enforce this.
		/// In the case of a backward jump we will signal any running replays to 
		/// terminate before we begin the new one.  For the forward jump we will 
		/// use the Mutex to chain replay executions together.  Note that the 
		/// ordering of replay operations is not guaranteed to be the same order
		/// in which threads were invoked, however this should not be significant,
		/// provided that all replay operations can be completed in a reasonable
		/// time.
		/// 
		/// Note: this approach has a potential problem in that the order of
		/// replays can be important, for instance if the delete stroke should happen
		/// after a draw, but instead comes before.
		/// 
		/// NOTE: do not use LoggerWriteInvoke in this thread because the form thread may
		/// already be waiting for us.  Invoking on the form thread will cause a deadlock.
		/// Use event log instead.
		private void replayPresenterDataThread(object o)
		{
			if (slideViewerGlue == null)
				return;

			replayMutex.WaitOne();

			PresenterDataItem[] rData = (PresenterDataItem[])o;
			int pCount = 0;

			for (int i=0; i< rData.Length; i++)
			{
				if (stopReplay)
					break;

				if ((rData[i].Opcode == PacketType.Scribble) ||
					(rData[i].Opcode == PacketType.ScribbleDelete) ||
					(rData[i].Opcode == PacketType.ClearScribble) ||
					(rData[i].Opcode == PacketType.ClearAnnotations) ||
					(rData[i].Opcode == PacketType.Scroll) ||
					(rData[i].Opcode == PacketType.ResetSlides) ||
					(rData[i].Opcode == PacketType.RTText) ||
                    (rData[i].Opcode == PacketType.RTDeleteText) ||
                    (rData[i].Opcode == PacketType.RTQuickPoll) ||
                    (rData[i].Opcode == PacketType.RTImageAnnotation)
                    )
				{
					if ((rData[i].Type == "CXP0") || (rData[i].Type == "CXP3"))
					{
						if (slideViewerGlue != null)
						{
							if (rData[i].Type =="CXP0")
								slideViewerGlue.SpReceive(rData[i].Slide,rData[i].Data);
							if (rData[i].Type =="CXP3")
								slideViewerGlue.ReceiveRTNav(rData[i].RTNav);

							pCount++;
						}
					}
				}

			}

			if ((!stopReplay) && (replayThreadCount==1)) //if we weren't explicitly asked to exit this block
					replayQueue.Release();	//replay any new data items that arrived while we were working.

			if (Interlocked.Decrement(ref replayThreadCount) == 0)
			{
				replaysDone.Set(); 
			}
			replayMutex.ReleaseMutex();
		}


		/// <summary>
		/// Event handler for ReplayQueue.playItem.
		/// </summary>
		/// <param name="o"></param>
		private void PlayDataItem(Object o)
		{
			PresenterDataItem pdi = (PresenterDataItem)o;
			if (pdi.Type == "CXP0")
			{   
				rsCount++;
				if (slideViewerGlue != null)
				{
					slideViewerGlue.Receive(pdi.Data);
				}
			} 
			else if (pdi.Type == "CXP3")
			{
				rsCount++;
				if (slideViewerGlue != null)
				{
					slideViewerGlue.ReceiveRTNav(pdi.RTNav);
				}
			}
			else 
			{
				Console.WriteLine("Unhandled script command type: " +  pdi.Type);
			}
		}

		private delegate void TocListUpdateDelegate(string msg);
		private void TocListUpdate(string msg)
		{
			TOClistBox.Items.Clear();
			TOClistBox.Items.Add(msg);
		}
		/// <summary>
		/// Put a message in the TOC list -- this is how we show load progress for now..
		/// </summary>
		/// <param name="msg"></param>
		private void TocListUpdateInvoke(string msg)
		{
			try
			{
				this.BeginInvoke(new TocListUpdateDelegate(TocListUpdate), new object[] {msg});
			}
			catch
			{}
		}

		private delegate void LoggerWriteDelegate(string msg);
		
		private void LoggerWrite(string msg)
		{
			logger.Write(msg);
		}

		/// <summary>
		/// If InvokeRequired is true, invoke log messages on the main form thread, otherwise
		/// just write without invoke.
		/// </summary>
		/// <param name="msg"></param>
		public void LoggerWriteInvoke(string msg)
		{
			if (this.InvokeRequired)
			{
				try
				{
					//It's important to use BeginInvoke not Invoke because otherwise we can get into
					//a deadlock situation if the main form thread is waiting for a thread which
					//uses the log.
					this.BeginInvoke(new LoggerWriteDelegate(LoggerWrite), new object[] {msg});
				}
				catch 
				{
					//can throw an exception when the window handle doesn't exist
					// (during startup and shutdown.)
				}
			}
			else
			{
				LoggerWrite(msg);
			}
		}

		#endregion

		#region Menus

		//Open browser.
		private void menuItem3_Click(object sender, System.EventArgs e)
		{
			if (browserClosed)
			{
				browserForm = new frmBrowse(lastBrowseUrlList);
				browserForm.Closed += new System.EventHandler(onBrowseClosed);
				browserForm.Closing += new System.ComponentModel.CancelEventHandler(onBrowseClosing);
				browserForm.OnWmLinkClicked += new frmBrowse.WmLinkClickedHandler(onWmLinkClicked);
				browserForm.Show();
			} 
			else
			{
				browserForm.WindowState = FormWindowState.Normal;
				browserForm.Activate();
			}

			browserClosed = false;
		}

		private void onWmLinkClicked(object url, EventArgs e)
		{
			LoggerWriteInvoke("Windows Media URL selected: " + url.ToString());
			String validPath = validateSourcePath((url.ToString()).Trim());
			if (validPath != null)
			{
				preloadAndOpen(validPath);
			}
		}

		private void onBrowseClosed(object src, EventArgs e)
		{
			browserClosed = true;
		}

		private void onBrowseClosing(object src, System.ComponentModel.CancelEventArgs e)
		{
			String newUrl = browserForm.cbUrlList.Text.Trim();
			if (! isWmURL(newUrl))
			{
				//save last URL in lastBrowseUrlList
				int i = lastBrowseUrlList.IndexOf(newUrl);
				if (i>=0)
				{
					lastBrowseUrlList.RemoveAt(i);
				}
				lastBrowseUrlList.Insert(0,newUrl);

				if (lastBrowseUrlList.Count > lastBrowseUrlListSize)
				{
					lastBrowseUrlList.RemoveAt(lastBrowseUrlList.Count-1);
				}
			}
		}

		//open URL
		private void menuItem2_Click(object sender, System.EventArgs e)
		{
			OpenDialog openDialog = new OpenDialog();
				
			openDialog.cbUrl.Items.AddRange(lastUrlList.ToArray());
			if (lastUrlList.Count > 0)
				openDialog.cbUrl.SelectedIndex=0;
			
			if (openDialog.ShowDialog(this) == DialogResult.OK)
			{
				if (openDialog.cbUrl.Text.Trim() != "")
				{
					LoggerWriteInvoke ("Open URL: " + openDialog.cbUrl.Text);
					
					String newUrl = openDialog.cbUrl.Text.Trim();
					int i = lastUrlList.IndexOf(newUrl);
					if (i>=0)
					{
						lastUrlList.RemoveAt(i);
					}
					lastUrlList.Insert(0,newUrl);

					if (lastUrlList.Count > lastUrlListSize)
					{
						lastUrlList.RemoveAt(lastUrlList.Count-1);
					}

					String validPath = validateSourcePath(newUrl);
					if (validPath != null)
					{
						preloadAndOpen(validPath);
					}

				}
			}

		}

		//open file
		private void menuItem4_Click(object sender, System.EventArgs e)
		{
			if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
			{
				LoggerWriteInvoke("Open File: " + openFileDialog1.FileName);
				String validPath = validateSourcePath(openFileDialog1.FileName);
				if (validPath != null)
				{
					preloadAndOpen(validPath);
				}

			}
		}

		//About
		private void menuItem6_Click(object sender, System.EventArgs e) //About
		{
			frmAbout frmAbout = new frmAbout();
			frmAbout.ShowDialog(this);
		}

		//Exit
		private void menuItem7_Click(object sender, System.EventArgs e)
		{
			Application.Exit();
		}

		#endregion

		#region Config Persistence
		private void SaveConfig()
		{
			try
			{
				RegistryKey BaseKey = Registry.CurrentUser.OpenSubKey("Software\\UWCSE\\WebViewer", true);
				if ( BaseKey == null) 
				{
					BaseKey = Registry.CurrentUser.CreateSubKey("Software\\UWCSE\\WebViewer");
				}
				//store MRU for URLs
				for (int i=0;i<lastUrlList.Count;i++)
				{
					BaseKey.SetValue("Url"+i.ToString(),lastUrlList[i]);
				}

				//store MRU for Browser URLs
				for (int i=0;i<lastBrowseUrlList.Count;i++)
				{
					//No items in the list should be WM Urls. This should be assured by other code
					// so no need to check it here.
					BaseKey.SetValue("BrowseUrl" + i.ToString(),lastBrowseUrlList[i]);
				}
			}
			catch
			{
				LoggerWriteInvoke("Exception while saving configuration.");
			}
		}


		// Restore the user's last configuration from the registry.
		private void RestoreConfig()
		{
			try
			{
				RegistryKey BaseKey = Registry.CurrentUser.OpenSubKey("Software\\UWCSE\\WebViewer", true);
				if ( BaseKey == null) 
				{ //no configuration yet.. first run.
					logger.Write("No registry configuration found.");
					return;
				}
				//retrieve MRU for URLs
				object o;
				for(int i=0;i<lastUrlListSize;i++)
				{
					o = BaseKey.GetValue("Url"+i.ToString());
					if (o != null)
					{
						if (i==0)
							lastUrlList.Clear(); //Remove the default value
						lastUrlList.Add(Convert.ToString(o));
					}
					else
					{
						break;
					}
				}

				//Restore lastBrowseUrlList.  Check each item with isWmURL.
				String s;
				for(int i=0;i<lastBrowseUrlListSize;i++)
				{
					o = BaseKey.GetValue("BrowseUrl"+i.ToString());
					if (o != null)
					{
						s = Convert.ToString(o);
						if (isWmURL(s))
							s = WebViewerHomePage;
						if (i==0)
							lastBrowseUrlList.Clear(); //Remove the default value
						lastBrowseUrlList.Add(s);
					}
					else
					{
						break;
					}
				}

			}
			catch (Exception e)
			{
				Debug.WriteLine(e.ToString());
				logger.Write("Exception while restoring configuration.");
			}
		}

		#endregion

		#region TOC
		/// <summary>
		/// Populate the Table of Contents list box
		/// </summary>
		private void fillTOC()
		{
			AutoTOC = false;
			TOClistBox.Items.Clear();

			if (pDataStore != null)
			{
				if (pDataStore.TOCArray != null)
				{
					/// Prefer to use the TOC automatically built from presentation data (Presenter 2).
					AutoTOC = true;
					foreach (TOCEntry t in pDataStore.TOCArray)
					{
						TOClistBox.Items.Add(t);
					}
					TOCIndex = 0;
					TOClistBox.SelectedIndex = TOCIndex;
				}
			}

			if (!AutoTOC)
			{
				// Otherwise, use markers to build a TOC.
				int mcount = axWindowsMediaPlayer1.currentMedia.markerCount;
				string timestr;
				if (mcount > 0) 
				{
					for (int i=1; i<=mcount; i++)
					{
						timestr = (TimeSpan.FromSeconds(axWindowsMediaPlayer1.currentMedia.getMarkerTime(i))).ToString();
						TOClistBox.Items.Add( timestr + " - " + axWindowsMediaPlayer1.currentMedia.getMarkerName(i));
					}
					TOCIndex = getMarkerNum(0) - 1;
					TOClistBox.SelectedIndex = TOCIndex;
				}
			}
		}

		/// 
		/// If the user changed the selection clicking on the list box, change
		/// the media selection.  We'll know it was the user's action that caused
		/// the change if the new index does not match TOCIndex which we use to 
		/// track programmatic changes to the list box.
		/// 
		private void TOClistBox_SelectedIndexChanged(object sender, System.EventArgs e)
		{
			if (TOClistBox.SelectedIndex != TOCIndex)
			{
				TOCIndex = TOClistBox.SelectedIndex;
				if (AutoTOC)
				{
					//Note: Setting currentPosition triggers the position change event.
					axWindowsMediaPlayer1.Ctlcontrols.currentPosition = pDataStore.GetTocEntry(TOCIndex).Time.TotalSeconds;
				}
				else
					jumpToMarker(TOCIndex+1);
			}
		}

		/// <summary>
		/// On double click, move media postion to the marker without conditions
		/// </summary>
		private void TOClistBox_DoubleClick(object sender, System.EventArgs e)
		{
			if (AutoTOC)
			{
				axWindowsMediaPlayer1.Ctlcontrols.currentPosition = pDataStore.GetTocEntry(TOCIndex).Time.TotalSeconds;
				//Note: Setting currentPosition triggers the position change event.			
			}
			else
				jumpToMarker(TOClistBox.SelectedIndex + 1); 
		}

		private void jumpToMarker(int markernum)
		{
			double oldpos;
			oldpos = axWindowsMediaPlayer1.Ctlcontrols.currentPosition;
			axWindowsMediaPlayer1.Ctlcontrols.currentMarker = markernum;
			//Note that setting currentMarker does change the media position, but does not 
			//result in a WMP positionChange event. Setting currentPosition does trigger
			//the event.
			jumpAndRefresh(oldpos, axWindowsMediaPlayer1.Ctlcontrols.currentPosition);

		}

		#endregion

		#region File Open and Script Preload

		/// <summary>
		/// Given an arbitrary string: See if it is a file type we handle {wbv,wmv,wma,asf,asx}, 
		/// If wbv, parse and dereference. Fail if the WBV was not in the recognized format.
		/// If local or unc asx, set current directory to that of the asx.
		/// The output may be pretty much anything -- it is not guaranteed to exist,
		/// be readable, or contain valid data.   It may be one of the valid file types, but it may also
		/// be a mms or http Windows media distribution or publishing point url, or it may be
		/// unrecognizable garbage.  We don't care because WMP does the right things with inputs it
		/// can't handle.
		/// </summary>
		/// <param name="path"></param>
		/// <returns>WM reference, or null for any error</returns>
		private String validateSourcePath(string path)
		{
			String validPath = null;
			String origPath = path;
			if (this.isWmURL(path))
			{
				if (this.isWbv(path))
				{
					path = parseWbv(path);
					//if existing local or UNC wbv reference, set current dir to wbv path so that a 
					// relative reference to asx,wmv, etc. in the wbv is resolved correctly.
					if (File.Exists(origPath)) //Note: File.Exists is false for all http URI's, for example.
						setCurrentDirectory(origPath);
				}
				if (path != null)
				{
					if (isAsx(path))
					{
						if (File.Exists(path))  
						{
							//convert a relative local reference to fully qualified
							path = makeFullyQualified(path);
							//Set current directory to the asx directory so that any relative references contained 
							//within the asx will resolve correctly.
							setCurrentDirectory(path);
						}
					}
					validPath = path;
				}
				else
				{
					LoggerWriteInvoke("Failed to open or parse WBV input: " + origPath);
				}
			}
			else
			{
				validPath = path;
			}
			return validPath;
		}

		/// <summary>
		/// Attempt to set current directory to that of path.  Path may be relative or absolute.
		/// Note that a URI reference input excepts on SetCurrentDirectory but does nothing.  
		/// </summary>
		/// <param name="path"></param>
		private void setCurrentDirectory(String path)
		{
			//PRI2: is there a reliable way to filter out the URI references, other than just 
			// catching the exception?
			String dir = null;
			
			try 
			{
				dir = Path.GetDirectoryName(path);
			}
			catch
			{
				return;
			}

			try
			{
				Directory.SetCurrentDirectory(dir);
			}
			catch (Exception)
			{
				return;
			}
		}

		private String makeFullyQualified(String path)
		{
			if (path==null)
				return null;
			string retPath = null;
			try
			{
				retPath = Path.GetFullPath(path);
			}
			catch
			{
				retPath = path;
			}
			return retPath;
		}

		private delegate void DisplayInitialSlideDelegate(object slideref);
		public void invokeDisplayInitialSlide(object slideref)
		{
			if (this.InvokeRequired)
			{
				try
				{
					this.BeginInvoke(new DisplayInitialSlideDelegate(displayInitialSlide),new object[] {slideref});
				}
				catch {}
			}
			else
			{
				displayInitialSlide(slideref);
			}
		}

		/// <summary>
		/// Invoked by PresenterDataStore callback to load initial slide when media is opened 
		/// </summary>
		/// <param name="url"></param>
		public void displayInitialSlide(object slideref)
		{
			bool verboselogging = Constants.VerboseLogging;

			if (verboselogging)
				LoggerWriteInvoke("Entering displayInitialSlide callback.");

			if (slideViewerGlue == null) 
				return;

			slideViewerGlue.BaseURL = pDataStore.BaseUrl;
			slideViewerGlue.Extent = pDataStore.Extent;

			if (slideref is ArchiveRTNav.RTUpdate)
			{	
				slideViewerGlue.ShowSlide((ArchiveRTNav.RTUpdate)slideref);
			}
			else if (slideref is String)
			{
				int sn = Convert.ToInt32(slideref);
				slideViewerGlue.ShowSlide(sn);
			}
			if (verboselogging)
				LoggerWriteInvoke("Leaving displayInitialSlide callback.");

		}

		/// <summary>
		/// Event handler to update presenter data load progress indicator.
		/// </summary>
		/// <param name="percentLoaded"></param>
		public void updateLoadProgressHandler(object percentLoaded)
		{
			int pl = Convert.ToInt32(percentLoaded);
			TocListUpdateInvoke("Presentation data loading in progress .. " + pl.ToString() + "%");			
		}

		/// <summary>
		/// Invoked when PresenterDataStore determines that it is time to display something.
		/// </summary>
		/// <param name="data"></param>
		public void displayDataHandler(object data)
		{
			LoggerWriteInvoke("displayDataHandler: " + ((PresenterDataItem)data).ToString());	
			replayQueue.Enqueue(data);
			
		}

		private bool isWmURL(string url)
		{
			int dot = url.LastIndexOf(".");
			if (dot < 0)
				return false;

			string extent = (url.Substring(dot)).ToLower();	
			if ((extent == ".wmv") ||
				(extent == ".asx") ||
				(extent == ".asf") ||
				(extent == ".wma") ||
				(extent == ".wbv"))
				return true;

			return false;
		}

		private bool isAsx(string url)
		{
			int dot = url.LastIndexOf(".");
			if (dot < 0)
				return false;

			string extent = (url.Substring(dot)).ToLower();	
			if (extent == ".asx")
				return true;
			return false;
		}

		private bool isWbv(string url)
		{
			int dot = url.LastIndexOf(".");
			if (dot < 0)
				return false;

			string extent = (url.Substring(dot)).ToLower();	
			if (extent == ".wbv")
				return true;
			return false;
		}


		/// <summary>
		/// Open media, first clearing any previous state, and possibly preloading presentation data.
		/// </summary>
		/// <param name="url">Path to asf, wma, wmv or asx.</param>
		/// Note: If the input is asx, and the file is local or unc, we assume the current directory 
		/// has been set so that relative paths to script and media will work if they are relative to the asx.
		/// The input is not guaranteed to exist.
		private void preloadAndOpen(string url)
		{
			bool verboselogging = Constants.VerboseLogging;

            if (verboselogging)
				this.LoggerWriteInvoke("Starting preloadAndOpen");

			//The simple way to get rid of previous state: kill off slideViewerGlue and make a new one.
			slideViewerGlue = null;

			upgradeWarningGiven = false;

			try
			{
				slideViewerGlue = new SlideViewerGlue(lockedSlideView1, this);
			}
			catch (Exception exp)
			{
				MessageBox.Show(exp.ToString());
			}

			if (pDataStore != null)
			{
				pDataStore.Stop();
				pDataStore = null;
			}
			ignoreInlineScripts = false;

			TOClistBox.Items.Clear();

			// if the media is ASX (windows media metadata), and the the ASX contains a pointer to
			// script data, download and parse the data before attempting to start the player.  This is
			// to prevent bandwidth related issues on slow networks.
			if (isAsx(url))
			{
				if (verboselogging)
					this.LoggerWriteInvoke("preloadAndOpen: Opening asx: " + url);
				String scriptUrl = getScriptUrl(url);
				if (scriptUrl != null)
				{
					if (verboselogging)
						this.LoggerWriteInvoke("preloadAndOpen: CXPSCRIPT found: " + scriptUrl);
					asyncPreload(scriptUrl, url);
					return;
				}
			}

			// otherwise, if there is no script data, just start the media.
			axWindowsMediaPlayer1.URL = (url.ToString()).Trim();
		}
		
		/// <summary>
		/// Download/read and parse WBV file.  Return the path to a Windows Media file if present.
		/// Return null if there's a problem.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		/// The input could be a local/unc (possibly relative), or http/ftp uri, etc.
		/// WebClient seems to handle all cases.
		private String parseWbv(String url)
		{
			System.Net.WebClient wc = new System.Net.WebClient();
			Stream stream;
			XmlTextReader rdr;
			String result = null;
			LoggerWriteInvoke("Opening: " + url);
			try 
			{
				stream = wc.OpenRead(url);
			}
			catch
			{
				LoggerWriteInvoke("Exception while opening WBV file: " + url);
				return null;
			}
			try
			{
				rdr = new XmlTextReader (stream);
			}
			catch
			{
				LoggerWriteInvoke("Exception while parsing WBV file" + url);
				stream.Close();
				return null;
			}
			try
			{
				while (rdr.Read())
				{
					if (rdr.NodeType == XmlNodeType.Element)
					{
						if (rdr.Name.ToLower() == "wmref")
						{
							rdr.MoveToNextAttribute();
							if ((rdr.Name.ToLower() == "href") && (rdr.Value != ""))
							{
								result = rdr.Value;
								break;
							}
						}
					}

				}
			}
			catch {}

			stream.Close();			
			return result;		
		}

		
		/// <summary>
		/// Download and parse a ASX file.  Return URL of script data if present, or null if it's not present,
		/// or if a problem was encountered.
		/// </summary>
		/// <param name="asxUrl"></param>
		/// <returns></returns>
		private string getScriptUrl(string asxUrl)
		{
			System.Net.WebClient wc = new System.Net.WebClient();
			Stream stream;
			XmlTextReader rdr;
			String result = null;
			try 
			{
				stream = wc.OpenRead(asxUrl);
			}
			catch
			{
				LoggerWriteInvoke("Exception while opening url: " + asxUrl.ToString());
				return null;
			}
			try
			{
				rdr = new XmlTextReader (stream);
			}
			catch
			{
				LoggerWriteInvoke("Exception while parsing XML file" + asxUrl.ToString());
				stream.Close();
				return null;
			}
			try
			{
				while (rdr.Read())
				{
					if (rdr.NodeType == XmlNodeType.Element)
					{
						if (rdr.Name == "PARAM")
						{
							rdr.MoveToNextAttribute();
							if ((rdr.Name.ToLower() == "name") && (rdr.Value == "CXP_SCRIPT"))
							{
								rdr.MoveToNextAttribute();
								if ((rdr.Name.ToLower() == "value") && (rdr.Value != ""))
								{
									result = rdr.Value;
									break;
								}
							}
						}
					}

				}
			}
			catch {}

			stream.Close();			
			return result;
		}

		/// <summary>
		/// Fire off a thread to preload script data.  When the thread completes, 
		/// initiate the stream.
		/// </summary>
		/// <param name="script"></param>
		/// <param name="asx"></param>
		private void asyncPreload(String script, String asx)
		{
			
			//disable selected menu controls
			this.Cursor = Cursors.WaitCursor;
			axWindowsMediaPlayer1.Ctlenabled = false;
			menuItem2.Enabled = false;
			menuItem3.Enabled = false;
			menuItem4.Enabled = false;
			this.buttonJumpForward.Enabled = false;
			this.buttonJumpBack.Enabled = false;

			currentUrl = asx;
			TOClistBox.Items.Clear();
			TOClistBox.Items.Add("Presentation data loading in progress .. ");
			ignoreInlineScripts = true;
			pDataStore = new PresenterDataStore(this,axWindowsMediaPlayer1);
			pDataStore.OnDisplayData += new PresenterDataStore.displayDataHandler(displayDataHandler);
			pDataStore.OnUpdateLoadProgress += new PresenterDataStore.updateLoadProgressCallback(updateLoadProgressHandler);
			pDataStore.OnInitialSlide += new PresenterDataStore.initialSlideReadyCallback(invokeDisplayInitialSlide);
			pDataStore.OnLoadComplete += new PresenterDataStore.loadCompleteHandler(invokeLoadCompleteHandler);
			pDataStore.OnTOCEntryChange += new PresenterDataStore.tocEntryChangeHandler(invokeTocEntryChange);
			pDataStore.AsynchronousLoad(script);
		}

		private delegate void LoadCompleteHandlerDelegate();
		public void invokeLoadCompleteHandler()
		{
			if (this.InvokeRequired)
			{
				try
				{
					this.BeginInvoke(new LoadCompleteHandlerDelegate(loadCompleteHandler));
				}
				catch
				{}
			}
			else
			{
				loadCompleteHandler();
			}
		}


		/// <summary>
		/// Script data preload is complete, so reenable user controls and open the media.
		/// </summary>
		public void loadCompleteHandler()
		{
			bool verboselogging = Constants.VerboseLogging;

#if DEBUG
			//verboselogging = true;
#endif
			if (verboselogging)
				this.LoggerWriteInvoke("Entering loadComplete callback.");

			CheckWebViewerVersion(); //Warn the user if an update is recommended.

			TOClistBox.Enabled = true;
			axWindowsMediaPlayer1.Ctlenabled = true;
			menuItem2.Enabled = true;
			menuItem3.Enabled = true;
			menuItem4.Enabled = true;
			this.buttonJumpForward.Enabled = true;
			this.buttonJumpBack.Enabled = true;
			this.Cursor = Cursors.Default;


			pDataStore.Play();			
			axWindowsMediaPlayer1.URL = currentUrl;

			if (verboselogging)
				this.LoggerWriteInvoke("Leaving loadComplete callback.");
		}


		private void CheckWebViewerVersion()
		{
			System.Reflection.Assembly mainAssembly = System.Reflection.Assembly.GetExecutingAssembly();
			System.Version version = mainAssembly.GetName().Version;
			if (version.CompareTo(pDataStore.PreferredWebViewerVersion)<0)
				MessageBox.Show("This archive will work best with WebViewer version " +
					pDataStore.PreferredWebViewerVersion.ToString() + " or later. \r\n" +
					"See Help Menu --> About for download URL.","WebViewer Upgrade Recommended");
		}

		public delegate void TocEntryChangeDelegate(Object o);
		private void invokeTocEntryChange(Object o)
		{
			if (this.InvokeRequired)
			{
				try
				{
					this.BeginInvoke(new TocEntryChangeDelegate(tocEntryChange),new object[] {o});
				}
				catch
				{}
			}
			else
			{
				tocEntryChange(o);
			}
		}
			
			

		public void tocEntryChange(Object o)
		{
			if (AutoTOC)
			{
				TOCIndex = (Int32)o;
				TOClistBox.SelectedIndex = TOCIndex;
			}
		}

		#endregion

		#region Misc UI

		private void buttonJumpBack_Click(object sender, System.EventArgs e)
		{
			jumpNSeconds(-jumpIncrement);
		}

		private void buttonJumpForward_Click(object sender, System.EventArgs e)
		{
			jumpNSeconds(jumpIncrement);
		}

		private void jumpNSeconds(double N)
		{
            
            int openState = (int)axWindowsMediaPlayer1.openState;
            int playState = (int)axWindowsMediaPlayer1.playState;
            if ((playState >= 1) && (playState <= 3)) //play, paused or stopped
            {
                double oldpos = axWindowsMediaPlayer1.Ctlcontrols.currentPosition;
                Debug.WriteLine("jumpNSeconds oldpos=" + oldpos.ToString() + ";openstate=" + openState.ToString());
                axWindowsMediaPlayer1.Ctlcontrols.currentPosition = oldpos + N;
            }
            else
                Debug.WriteLine("jumpNSeconds ignoring call due to playstate=" + playState.ToString());
		}



		//Adjust the video pane size after the splitter is moved
		private void splitter1_SplitterMoved(object sender, System.Windows.Forms.SplitterEventArgs e)
		{
			int newLeft = splitter1.Left - 10; //There are 10 pixels of padding on the left panel.
			panel3.Height = (240*newLeft/320) + 65;
		}

		private void splitter2_SplitterMoved(object sender, System.Windows.Forms.SplitterEventArgs e)
		{
			int newTop = splitter2.Location.Y - 10;
			panel1.Width = ((newTop-64)*320/240)+10;
		}

		#endregion

	}
}
