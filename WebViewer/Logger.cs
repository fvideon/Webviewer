using System;
using System.IO;
using System.Windows.Forms;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Writes messages to a TextBox and/or a file
	/// </summary>
	public class Logger
	{
		private String[]		sa;						//content of the textbox
		private int				maxlines = 100;			//max number of lines in textbox
		private int				cline = 0;				//next line to write into sa
		private int				displayLines = 0;		//current number of lines in textbox
		private TextBox			logTextBox = null;		//The textbox itself
		private string			logFileName = null;		//file name
		private StreamWriter	logStreamWriter = null;	//for file logging 

		public Logger(TextBox txtBox, String filename,bool showLog, bool useLogFile)
		{
			logTextBox = null;
			logFileName = null;
			if (showLog)
			{
				logTextBox = txtBox;
				sa = new String[maxlines];
			}
			if (useLogFile)
			{
				logFileName = filename;
				CreateOpenLogFile();
			}
		}


		public void Close()
		{
			if (logStreamWriter != null)
			{
				CloseLogFile();
			}
		}

		private void CreateOpenLogFile() 
		{
			logStreamWriter = null;

			FileStream dLogFileStream = new FileStream(logFileName,
				FileMode.Append, FileAccess.Write, FileShare.None);
         
			logStreamWriter = new StreamWriter(dLogFileStream);
		}

		private void CloseLogFile()
		{
			if (logStreamWriter != null)
			{
				logStreamWriter.Flush();
				logStreamWriter.Close();
			}
		}

		private void FlushLogFile()
		{
			if (logStreamWriter != null)
			{
				logStreamWriter.Flush();
			}
		}

		/// <summary>
		/// Write to diagnostic log and textbox
		/// </summary>
		/// <param name="s">String to add to log</param>
		/// Can we redo this with an ArrayList?  Would that be easier?
		public void Write(String s)
		{
			string timeNow = DateTime.Now.ToString("HH:mm:ss.ff");
			lock (this) 
			{
				int	oldest;	//index to oldest item in sa.			

				if (logTextBox != null)
				{
					if (cline==maxlines)
					{
						cline=0;
					}

					displayLines++; // how many lines to display
					
					if (displayLines>maxlines) 
					{
						oldest=cline+1;
						if (oldest == maxlines)
						{
							oldest = 0;
						}
						displayLines = maxlines;
					}
					else
					{
						oldest = 0;
					}

					sa[cline] = timeNow + " " + s;

					String [] tmpsa = new String[displayLines];
					
					if (oldest == 0)
					{
						Array.Copy(sa,tmpsa,displayLines);
					}
					else
					{
						Array.Copy(sa,oldest,tmpsa,0,maxlines-oldest);
						Array.Copy(sa,0,tmpsa,maxlines-oldest,oldest);	
					}

					cline++;

					try //can except during application shutdown.
					{
						logTextBox.Lines = tmpsa;		
						logTextBox.Select(logTextBox.TextLength - tmpsa[displayLines-1].Length,0);
						logTextBox.ScrollToCaret();	
					}
					catch {}
				}

				if (logStreamWriter != null) 
				{
					try
					{
						logStreamWriter.WriteLine(timeNow + " " + s);

						if (Constants.VerboseLogging)
							logStreamWriter.Flush();
					}
					catch{} //Can except if we write while exiting app.
				}
			}	
		}

	}
}
