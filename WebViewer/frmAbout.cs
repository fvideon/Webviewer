using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Reflection;

namespace UW.CSE.CXP
{
	/// <summary>
	/// About box
	/// </summary>
	public class frmAbout : System.Windows.Forms.Form
	{
		private String myName;
		private String myVersion;
		private String myCopyright;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.LinkLabel linkLabel1;
		private System.Windows.Forms.Label lblTitle;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public frmAbout()
		{
			InitializeComponent();

			myName = "CXP Web Viewer";
			myVersion = "Unknown";
			myCopyright = "";

			Assembly mainAssembly = Assembly.GetExecutingAssembly();
			myVersion =  mainAssembly.GetName().Version.ToString();

			AssemblyName [] ar = mainAssembly.GetReferencedAssemblies();

			AssemblyCopyrightAttribute [] attribarray = 
				(AssemblyCopyrightAttribute [])mainAssembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute),true);
			if (attribarray.Length != 0)
			{
				myCopyright = '\u00A9' + attribarray[0].Copyright;
			}

			AssemblyTitleAttribute [] attribarray2 = 
				(AssemblyTitleAttribute [])mainAssembly.GetCustomAttributes(typeof(AssemblyTitleAttribute),true);
			if (attribarray2.Length != 0)
			{
				myName = attribarray2[0].Title;
			}

			lblTitle.Text = myName;
			this.Text = "About " + myName;
			label2.Text = "Version: " + myVersion + "   " + myCopyright;
			label3.Text = "";
			label4.Text = "For more information, please visit:";

		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
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
			this.button1 = new System.Windows.Forms.Button();
			this.linkLabel1 = new System.Windows.Forms.LinkLabel();
			this.lblTitle = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// button1
			// 
			this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.button1.Location = new System.Drawing.Point(352, 136);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(72, 24);
			this.button1.TabIndex = 0;
			this.button1.Text = "OK";
			// 
			// linkLabel1
			// 
			this.linkLabel1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.linkLabel1.Location = new System.Drawing.Point(40, 112);
			this.linkLabel1.Name = "linkLabel1";
			this.linkLabel1.Size = new System.Drawing.Size(392, 16);
			this.linkLabel1.TabIndex = 1;
			this.linkLabel1.TabStop = true;
			this.linkLabel1.Text = "www.cs.washington.edu/education/dl/confxp/webviewer.html";
			this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
			// 
			// lblTitle
			// 
			this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.lblTitle.ForeColor = System.Drawing.Color.DarkBlue;
			this.lblTitle.Location = new System.Drawing.Point(16, 16);
			this.lblTitle.Name = "lblTitle";
			this.lblTitle.Size = new System.Drawing.Size(408, 24);
			this.lblTitle.TabIndex = 2;
			this.lblTitle.Text = "Title";
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(24, 48);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(408, 16);
			this.label2.TabIndex = 3;
			this.label2.Text = "Line 2";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(24, 64);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(408, 16);
			this.label3.TabIndex = 4;
			this.label3.Text = "Line 3";
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(24, 96);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(408, 16);
			this.label4.TabIndex = 5;
			this.label4.Text = "Line 4";
			// 
			// frmAbout
			// 
			this.AcceptButton = this.button1;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(442, 168);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.label4,
																		  this.label3,
																		  this.label2,
																		  this.lblTitle,
																		  this.linkLabel1,
																		  this.button1});
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "frmAbout";
			this.Text = "About CXP Web Viewer";
			this.ResumeLayout(false);

		}
		#endregion

		private void linkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
		{
			linkLabel1.LinkVisited = true;
			System.Diagnostics.Process.Start(linkLabel1.Text);

		}
	}
}
