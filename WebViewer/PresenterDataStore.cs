using System;
using System.Threading;
using System.Collections;
using System.Xml;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using WorkSpace;
using System.Runtime.Serialization.Formatters.Binary;
using ArchiveRTNav;


namespace UW.CSE.CXP
{
	/// <summary>
	/// Encapsulates presenter data, and methods for loading, playback and jumping in media
	/// </summary>
	/// One way to get presenter data (annotations, slides, etc) will be to preload it from a url or web service.
	/// The expectation is that references to such would be found in media metadata.  If webviewer determines
	/// that such data exists, this class would be instantiated, and passed a reference to data for asynchronous load. 
	/// A media player control may be tracked, and events may be raised as media position dictates that presenter data 
	/// should be displayed.  When the user initiates a change in media position, we will help the caller reestablish
	/// correct display state by returning the relevant subset of data.
	public class PresenterDataStore
	{
		private ArrayList	pData;					//The presentation data
		private Thread		PlayThread;				//Watch the media position and raise events
		private bool		endPlayThread;
		private WebViewerForm parent;				// 
		private AxWMPLib.AxWindowsMediaPlayer wmp;	//
		private double		lastPos;				//Last known Up-to-date media position
		private bool		stopPlayEvents = false;	//
		private SlideTitles slideTitles;			//slightly enhanced hashtable
		private Guid		lastTocDeckGuid;		//Most recent TOC change event was raised for this Deck
		private int			lastTocSlideIndex;		// .. and for this slide
		public Array		TOCArray;				//The TOC data
		private bool		NoAutoTOC;				//Disable Auto TOC building
		public Version		PreferredWebViewerVersion;//Prefer at least this version		

		public PresenterDataStore(WebViewerForm parent,AxWMPLib.AxWindowsMediaPlayer wmp)
		{
			this.wmp = wmp;
			this.parent = parent;
			pData = new ArrayList();
			PlayThread = null;
			lastPos = 0;
			baseUrl = extent = null;
			loadComplete = false;
			endPlayThread = false;
			slideTitles = new SlideTitles();
			TOCArray = null;
			lastTocDeckGuid = Guid.Empty;
			lastTocSlideIndex = -1;
			NoAutoTOC = false;
			PreferredWebViewerVersion = new Version(1,0);
		}

		/// <summary>
		/// url base for slide images
		/// </summary>
		string		baseUrl;
		public string BaseUrl 
		{
			get {return this.baseUrl;}
			set {this.baseUrl = value;}
		}

		/// <summary>
		/// file extension for slide images.. Eg. 'jpg'
		/// </summary>
		string		extent;
		public string Extent 
		{
			get {return this.extent;}
			set {this.extent = value;}
		}

		bool	loadComplete;
		public bool LoadComplete
		{
			get {return this.loadComplete;}
		}

		public void AsynchronousLoad(string url)
		{
			loadComplete = false;
			ThreadPool.QueueUserWorkItem(new WaitCallback(LoadThread),url);
		}

		public void Play()
		{
			// start the play thread
			endPlayThread = false;
			lastPos = wmp.Ctlcontrols.currentPosition;
			PlayThread = new Thread(new ThreadStart(playThread));
			PlayThread.Start();
		}

		public void Stop()
		{
			// stop the play thread
			endPlayThread = true;
			if (PlayThread != null) 
			{
				if (PlayThread.Join(2000))
				{
					Debug.WriteLine("PresenterDataStore.Stop: thread terminated.");
				}
				else
				{
					Debug.WriteLine("PresenterDataStore.Stop: thread termination timed out.");
					PlayThread.Abort();
				}
			}
		}

		/// <summary>
		/// Return the most recent ScreenConfiguration data, or a default if none are found.
		/// </summary>
		/// <param name="mTime">Media time from which to search</param>
		/// <returns></returns>
		/// The ScreenConfiguration packets control the layout of the slide within the viewer control.
		/// These packets do not have a slide number associated with them, so as the presenter advances
		/// slides, the screen configuration does not change.  After a position change, the dataStore client
		/// needs to get the currently applied ScreenConfiguration.
		public ScreenConfiguration GetScreenConfiguration(double mTime)
		{

			TimeSpan ts_mTime = new TimeSpan((long)(mTime*10000000));
			int index;
			ScreenConfiguration sc;
			index = pData.BinarySearch(new PresenterDataItem(ts_mTime,"NOOP",""),null);
			if (index < 0)
			{
				index = ~index;
			}
			while ((index<pData.Count) && 
				(((PresenterDataItem)pData[index]).TimeStamp == ts_mTime))
				index++;

			while (index > 0)
			{  
				index--;
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.ScreenConfiguration)
				{
					BinaryFormatter bitmapFormatter = new BinaryFormatter();
					MemoryStream memoryStream = new MemoryStream(((PresenterDataItem)pData[index]).Data, 
						1, ((PresenterDataItem)pData[index]).Data.Length - 1);
					return((ScreenConfiguration) bitmapFormatter.Deserialize(memoryStream));
				}
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.RTUpdate)
				{
					sc = new ScreenConfiguration();
					sc.SlideSize = ((ArchiveRTNav.RTUpdate)((PresenterDataItem)pData[index]).RTNav).SlideSize;
					return sc;
				}
			}

			// If none found, return a default.
			sc = new ScreenConfiguration();
			return sc;
		}


		/// <summary>
		/// Return the slide index active at the given media time
		/// </summary>
		/// <param name="mTime">time in seconds and fractional seconds</param>
		/// <param name="lookAhead">seconds to look ahead in the media for slide transitions</param>
		/// <returns>Active slide index, or -1 if none is found.  In the case of Presenter 2
		/// data, return the RTUpdate object</returns>
		/// 
		public object GetSlideNum(double mTime)
		{
			TimeSpan ts_mTime = new TimeSpan((long)((mTime)*10000000));
			int index, curIndex;
			index = pData.BinarySearch(new PresenterDataItem(ts_mTime,"NOOP",""),null);
			if (index < 0)
			{
				index = ~index;
			}
			while ((index<pData.Count) && 
				(((PresenterDataItem)pData[index]).TimeStamp == ts_mTime))
				index++;

			curIndex = index;

			//if there's a SlideIndex packet in the following 3 seconds, return it.
			while (index < pData.Count)
			{
				if ((((PresenterDataItem)pData[index]).TimeStamp-ts_mTime) > TimeSpan.FromSeconds(3))
					break;
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.SlideIndex)
				{
					return ((PresenterDataItem)pData[index]).Slide.ToString();
				}
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.RTUpdate)
				{
					return ((PresenterDataItem)pData[index]).RTNav;
				}
				index++;
			}

			//return the most recent past SlideIndex packet
			index = curIndex;
			while (index > 0)
			{  
				index--;
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.SlideIndex)
				{
					return ((PresenterDataItem)pData[index]).Slide.ToString();
				}
				if (((PresenterDataItem)pData[index]).Opcode == WorkSpace.PacketType.RTUpdate)
				{
					return ((PresenterDataItem)pData[index]).RTNav;
				}
			}

			return "-1";
		}

		/// <summary>
		/// After a jump, return the minimal set of data necessary to refresh the display.
		/// </summary>
		/// <param name="from">old media time</param>
		/// <param name="to">new media time</param>
		/// <returns>array of PresenterDataItem representing data to be replayed</returns>
		/// Given memory leaks in the Tablet SDK, we need to be sure that we only replay
		/// data when it is necessary.  To compute the minimal set we will begin by 
		/// building a set of slides which would be visited if the media were allowed to play
		/// continuously from the new media time to the end.  A stroke data item will be included
		/// in the result set only if it belongs to a slide in this set.
		/// Additionally, non-terminal strokes should be excluded from the result set.
		public PresenterDataItem[] Jump(double from, double to)
		{
			TimeSpan fromts, tots;
			fromts = new TimeSpan((long)(from*10000000));
			tots = new TimeSpan((long)(to*10000000));

			int fromidx, toidx;
			ArrayList tmpAL;
			Hashtable slidesYetToVisit;

			stopPlayEvents = true; //signal play thread to hurry up and get out of locked section.
			lock (this)
			{
				stopPlayEvents = false;
				lastPos = to;

				fromidx = pData.BinarySearch(new PresenterDataItem(fromts,"NOOP",""),null);
				if (fromidx < 0)
					fromidx = ~fromidx;

                if (fromidx >= pData.Count)
                    fromidx = pData.Count - 1;

                while ((fromidx > 0) && (((PresenterDataItem)pData[fromidx]).TimeStamp >= fromts)) {
                    fromidx--;
                }

				toidx = pData.BinarySearch(new PresenterDataItem(tots,"NOOP",""),null);
				if (toidx < 0)
				{
					toidx = ~toidx;
				}

				while ((toidx<pData.Count) && 
					(((PresenterDataItem)pData[toidx]).TimeStamp <= tots))
					toidx++;

				tmpAL = new ArrayList();

				//Get the set of slides yet to be visited at and after the new position
				slidesYetToVisit = GetSlidesToVisit(toidx);

				//Store references to relevant data items in return array
				String key;
				for (int i=fromidx;i<toidx;i++)
				{
					key = ((PresenterDataItem)pData[i]).DeckGuid.ToString() + "-" + ((PresenterDataItem)pData[i]).Slide.ToString();
					if ((slidesYetToVisit.ContainsKey(key)) ||
						((PresenterDataItem)pData[i]).Slide == -1)
					{
						if ((((PresenterDataItem)pData[i]).Opcode != PacketType.SlideIndex) &&
							(((PresenterDataItem)pData[i]).Opcode != PacketType.ScreenConfiguration) &&
							(((PresenterDataItem)pData[i]).Opcode != PacketType.RTUpdate))
						{
							tmpAL.Add((PresenterDataItem)pData[i]);
						}
					}
				}

				tmpAL = FilterDeletedStrokes(tmpAL);
				tmpAL = FilterNonTerminalStrokes(tmpAL);
			}

			return (PresenterDataItem[])tmpAL.ToArray(typeof(PresenterDataItem));

		}

		private void printAL(ArrayList al)
		{
			foreach (PresenterDataItem pdi in al)
				Console.WriteLine(pdi.ToString());
		}
		
		/// <summary>
		/// Remove the partial strokes from the replay set.
		/// </summary>
		/// <param name="pda"></param>
		/// <returns></returns>
		private ArrayList FilterNonTerminalStrokes(ArrayList pda)
		{
			ArchiveRTNav.RTDrawStroke rtds;
			ArrayList pda_out = new ArrayList();

			foreach (PresenterDataItem pdi in pda)
			{
				if ((pdi.Opcode == PacketType.Scribble) && (pdi.Type == "CXP3") && (pdi.RTNav != null))
				{
					rtds = (RTDrawStroke)pdi.RTNav;		
					if (rtds.StrokeFinished)
					{
						pda_out.Add(pdi);
					}
				}
				else
					pda_out.Add(pdi);
			}

			return pda_out;
		}

		/// <summary>
		/// Return TOC index (zero-based) corresponding to time given.
		/// If there is no exact match, return the index of the item just before the time given.
		/// </summary>
		public Int32 GetTocIndex(double time)
		{
			if (TOCArray == null)
				return 0;
			if (TOCArray.Length == 0)
				return 0;

			int i = Array.BinarySearch(TOCArray,new TOCEntry(TimeSpan.FromSeconds(time),"bogus",null));
			if (i<0) 
				i=(~i)-1; //We require that there be a TOC entry at time zero for this logic to work.

			return i;
		}

		/// <summary>
		/// Return the the TOC entry corresponding to the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public TOCEntry GetTocEntry(int index)
		{
			if (TOCArray == null)
				return null;
			if ((index >= TOCArray.Length) ||
				(index < 0))
				return null;
			return (TOCEntry)TOCArray.GetValue(index);
		}

		/// <summary>
		/// Filter unnecessary stroke data from the replay set.
		/// </summary>
		/// If an opcode causing a clear slide or clear deck operation occurs after the ink data,
		/// the ink data is irrelevant.  We'd also like to filter out individual strokes 
		/// which will be deleted, and non-terminal strokes, but we need to rehydrate to Ink objects
		/// before we can pull the GUIDs out.  We will do this with RTObjects.
        /// PRI2:  We should also filter deleted text annotations.
		/// <param name="pda"></param>
		/// <returns></returns>
		private ArrayList FilterDeletedStrokes(ArrayList pda)
		{	
			//Note: input dataset does not contain any RTUpdate, SlideIndex or ScreenConfiguration data.
			// It may contain scroll data.
			PresenterDataItem pdi;
			Hashtable clearSlideFound = new Hashtable();
			Hashtable clearDeckFound = new Hashtable();
			ArrayList tmpAL = (ArrayList)pda.Clone();
			int fcnt = 0;

			for(int i=tmpAL.Count-1;i>=0; i--)
			{
				pdi = (PresenterDataItem)tmpAL[i];

				if (clearDeckFound.ContainsKey(pdi.DeckGuid))
				{
					if ((pdi.Opcode == PacketType.ResetSlides) ||
						(pdi.Opcode == PacketType.ClearAnnotations) ||
						(pdi.Opcode == PacketType.ClearScribble) ||
						(pdi.Opcode == PacketType.ScribbleDelete) ||
						(pdi.Opcode == PacketType.Scribble))
					{
						pda.RemoveAt(i);
						fcnt++;
					}
					//Notably this leaves the scroll packets alone.  
					continue;
				}

				if ((pdi.Opcode == PacketType.ClearScribble) ||
					(pdi.Opcode == PacketType.ScribbleDelete) ||
					(pdi.Opcode == PacketType.Scribble))
				{
					if (clearSlideFound.ContainsKey(pdi.DeckGuid.ToString() + "-" + pdi.Slide.ToString()))
					{
						pda.RemoveAt(i);
						fcnt++;
						continue;
					}
				}

				if ((pdi.Opcode == PacketType.ResetSlides) ||
					(pdi.Opcode == PacketType.ClearAnnotations))
				{
					clearDeckFound.Add(pdi.DeckGuid,true); //don't remove the item causing the clear operation.
					continue;
				}

				if (pdi.Opcode == PacketType.ClearScribble)
				{
					clearSlideFound.Add(pdi.DeckGuid.ToString() + "-" + pdi.Slide.ToString(),true); 
					//don't remove the ClearScribble					
					continue;
				}

			}
			return pda;

		}


		/// <summary>
		/// Return a list of slides which appear in pData at or after the given index
		/// </summary>
		/// <param name="startidx"></param>
		/// <returns></returns>
		private Hashtable GetSlidesToVisit(int startidx)
		{
			Hashtable retHT = new Hashtable();
			String key;
			for (int i=startidx;i<pData.Count;i++)
			{
				key = ((PresenterDataItem)pData[i]).DeckGuid.ToString() + "-" + ((PresenterDataItem)pData[i]).Slide.ToString();
				if (!retHT.ContainsKey(key))
				{
					retHT.Add(key,true);
				}
			}

			//To make sure we have the current slide, we also need to scan backwards in pData 
			//until we find one rtUpdate
			for (int i=startidx-1;i>=0;i--)
			{
				if (((PresenterDataItem)pData[i]).Opcode == PacketType.RTUpdate)
				{
					key = ((PresenterDataItem)pData[i]).DeckGuid.ToString() + "-" + ((PresenterDataItem)pData[i]).Slide.ToString();
					if (!retHT.ContainsKey(key))
					{
						Debug.WriteLine("Looked back to add key:" + key);
						retHT.Add(key,true);
					}
					break;
				}
			}

			return retHT;
		}

		/// <summary>
		/// raise an event whenever the current media time passes an item in pData.
		/// </summary>
		/// look ahead in the data to guess at our sleep period.
		private void playThread()
		{
			double currentPos = lastPos;
			TimeSpan dtLastPos;
			TimeSpan dtCurPos;
			int playState = 0;
			int sleepPeriod = 1000;

			while (!endPlayThread)
			{
				try
				{
					if ((wmp.Disposing) || (wmp.IsDisposed))
					{
						Debug.WriteLine("playThread aborting: WMP disposing, or WMP is disposed");
						break;
					}
				}
				catch (Exception e)
				{
					Debug.WriteLine("Playthread exception while checking wmp.Disposing: " + e.ToString());
					break;
				}
				lock (this)
				{
					// the wmp stuff can except when we kill the app..
					try
					{
						currentPos = wmp.Ctlcontrols.currentPosition;
						dtCurPos = new TimeSpan((long)(currentPos*10000000));
						dtLastPos = new TimeSpan((long)(lastPos*10000000));
						playState = (int)wmp.playState;
						/// Note that the granularity of the WMP position data is not very fine.
						/// In particular it was observed that if the sleep was around 10 ms, the 
						/// WMP position was unchanged on the next iteration.  This can cause a little
						/// bit of jumpiness in the playback once in awhile.

						if ((playState >=1 ) && (playState <= 3 ) && (dtCurPos > dtLastPos)) //play, paused or stopped
						{
							sleepPeriod = RaisePresenterEvents(dtCurPos, dtLastPos,playState);
							lastPos = currentPos;
						}
						else
						{	
							sleepPeriod = 500;
						}

						/// In the case of Presenter2 data which has been filtered, we can get very long sleeps
						/// which will constipate the replay, so set a maximum.
						if (sleepPeriod > 1000)
							sleepPeriod = 1000;
					}
					catch (Exception e)
					{
						Debug.WriteLine("Playthread exception: " + e.ToString());
					}
				}

				try
				{
                    if (sleepPeriod > 0)
                        Thread.Sleep(sleepPeriod);
                    else
                        Debug.WriteLine("Playthread sleep period is negative.");
				}
				catch(Exception e)
				{
					Debug.WriteLine("Playthread exception while sleeping.  Sleep period=" + 
						sleepPeriod.ToString() + ";exception text: " + e.ToString());
				}
			}
			Debug.WriteLine("Playthread terminating.");
		}


		private int RaisePresenterEvents(TimeSpan current, TimeSpan last, int playstate)
		{
			int index = 0;;
			int ret;

			index = pData.BinarySearch(new PresenterDataItem(last,"NOOP",""),null);
			if (index < 0)
			{
				index = ~index;
			}
			while ((index<pData.Count) && 
				(((PresenterDataItem)pData[index]).TimeStamp == last))
				index++;
			while ((index<pData.Count) &&
				(((PresenterDataItem)pData[index]).TimeStamp <= current))
			{
                if (stopPlayEvents)
                {
                    Debug.WriteLine("RaisePresenterEvents: stopPlayEvents is true");
                    break; //someone wants us out of the locked block.
                }

				if (OnDisplayData != null)
				{
					OnDisplayData((pData[index]));
				}

				if ((TOCArray != null) && (TOCArray.Length > 0))
					RaiseTocChangeEvent((PresenterDataItem)pData[index]);

				index++;
			}
						
			if ((index!=0) && (index<pData.Count) && (playstate == 3))
			{
				ret = (int)(((PresenterDataItem)pData[index]).TimeStamp - current).TotalMilliseconds;
			}
			else
			{
				ret = 500;
			}
			return ret;
		}

		/// <summary>
		/// Raise event to indicate the current TOC entry should be changed.
		/// </summary>
		/// <param name="pdi"></param>
		private void RaiseTocChangeEvent(PresenterDataItem pdi)
		{
			if (pdi.Opcode != PacketType.RTUpdate)
				return;

			RTUpdate rtu = (RTUpdate)pdi.RTNav;
			if ((rtu.DeckGuid != lastTocDeckGuid) ||
				(rtu.SlideIndex != lastTocSlideIndex))
			{
				int i = Array.BinarySearch(TOCArray,new TOCEntry(pdi.TimeStamp,"bogus",null));
				if (i<0) 
					i=(~i)-1; //We require that there be a TOC entry at time zero for this logic to work.

				if (OnTOCEntryChange != null)
					OnTOCEntryChange(i);

				lastTocDeckGuid = rtu.DeckGuid;
				lastTocSlideIndex = rtu.SlideIndex;
			}

		}

		private void LoadThread(object url)
		{
			bool verboselogging = Constants.VerboseLogging;

			//The DateTimes in the file are unfortunately localized to en-US.. bad design decision ..
			IFormatProvider culture = new CultureInfo("en-US", false);

			if (verboselogging)
				parent.LoggerWriteInvoke("Starting presentation data load thread with " + url.ToString());

			// begin reading from the URL supplied.  Lock and fill pData incrementally so
			// that we don't hold it locked during a potentially long download period, and so
			// that we make the early bits of data available to the PlayThread as they arrive.
			System.Net.WebClient wc = new System.Net.WebClient();

			/// A few times I heard of corrupted data in IE's cache causing problems.
			/// The hope is that adding no-cache to the header here will prevent this.
			wc.Headers.Set("Cache-Control", "no-cache");
			string tp, cmd, tm, start, delta,deckguid,index,text;
			bool fragment = false; //true if current data is a fragment of a stroke.
			DateTime offset = DateTime.MinValue;
			TimeSpan relativeTime;
			Stream stream;
			int charsProcessed = 0, len;
			double percentProcessed = 0;
			int lastReported = -10;
			bool raiseInitialSlideCallback = true;
			XmlTextReader rdr;
			try 
			{
				stream = wc.OpenRead((string)url);
				len = Convert.ToInt32(wc.ResponseHeaders.Get("Content-Length"));
				if (verboselogging)
					parent.LoggerWriteInvoke("Successfully opened " + url.ToString());
			}
			catch
			{
				parent.LoggerWriteInvoke("Exception while opening url: " + url.ToString());
				if (OnLoadComplete != null)
				{
					OnLoadComplete();
				}
				loadComplete = true;
				return;
			}
			try
			{
				rdr = new XmlTextReader (stream);
				if (verboselogging)
					parent.LoggerWriteInvoke("Successfully parsed " + url.ToString());
			}
			catch
			{
				parent.LoggerWriteInvoke("Exception while parsing XML file" + url.ToString());
				stream.Close();
				if (OnLoadComplete != null)
				{
					OnLoadComplete();
				}
				loadComplete = true;
				return;
			}
			try
			{
				while (rdr.Read())
				{
					//Whitespace happens to work pretty well for keeping track of stream position to show loading progress.
					if (rdr.NodeType == XmlNodeType.Whitespace)
					{

						if (verboselogging)
							parent.LoggerWriteInvoke("Read XML Whitespace node.");

						charsProcessed += rdr.LinePosition + 1;
						percentProcessed = (((double)charsProcessed)/((double)len)*100);
						if (percentProcessed >= (lastReported + 10))
						{
							lastReported += 10;
							if (OnUpdateLoadProgress != null)
								OnUpdateLoadProgress(lastReported);
						}
					}

					if (rdr.NodeType == XmlNodeType.Element)
					{
						if (verboselogging)
							parent.LoggerWriteInvoke("Read XML Element node.");

						if (rdr.Name == "Slides")
						{
							//BaseURL and Extent are required for presenter 1.1.03 data.  They also appear with some
							// Presenter 2 data, but are not necessary for any WebViewer 1.9.4 or later. Beginning
							// with WebViewer 1.9.4, if present, they are used to override data found in RTUpdate
							// packets. For 1.9.0-1.9.3 they were necessary to enable the initial slide callback (a "bug").
							// Once 1.9.4 and later are deployed they should normally be omitted from script data.
							baseUrl = extent = null;
							while (rdr.MoveToNextAttribute())
							{
								if (rdr.Name == "BaseURL")
									baseUrl = rdr.Value;
								if (rdr.Name == "Extent")
									extent = rdr.Value;
							}
							if (verboselogging)
								parent.LoggerWriteInvoke("Read 'Slides' element.");
						}
						if (rdr.Name == "Options")
						{
							while (rdr.MoveToNextAttribute())
							{
								//A safety hatch to disable auto TOC building in case of an unforseen problem
								if (rdr.Name == "NoAutoTOC")
								{
									if (rdr.Value.ToLower() == "true")
										NoAutoTOC = true;
								}

								if (rdr.Name == "PreferredWebViewerVersion")
								{
									try
									{
										PreferredWebViewerVersion = new Version(rdr.Value);
									}
									catch {}
								}

							}
							if (verboselogging)
								parent.LoggerWriteInvoke("Read 'Options' element.");

						}
						if (rdr.Name == "Title")
						{
							deckguid=index=text=null;
							while (rdr.MoveToNextAttribute())
							{
								if (rdr.Name == "DeckGuid")
									deckguid = rdr.Value;
								if (rdr.Name == "Index")
									index = rdr.Value;
								if (rdr.Name == "Text")
									text = rdr.Value;							
							}
							slideTitles.Add(deckguid,index,text);
							if (verboselogging)
								parent.LoggerWriteInvoke("Read 'Titles' element.");
						}
						if (rdr.Name == "ScriptOffset")
						{
							start = delta = null;
							while (rdr.MoveToNextAttribute())
							{
								if (rdr.Name == "Start")
									start = rdr.Value;
								if (rdr.Name == "Delta")
									delta = rdr.Value;
							}
							if (start!=null)
							{
								offset = DateTime.Parse(start,culture,DateTimeStyles.None);
								if (delta != null)
									offset = offset + TimeSpan.Parse(delta);
							}
							if (verboselogging)
								parent.LoggerWriteInvoke("Read 'ScriptOffset' element.");
						}
						if (rdr.Name == "Script")
						{
							tp = cmd = tm = null;
							while (rdr.MoveToNextAttribute())
							{
								switch (rdr.Name)
								{
									case "Type":
										tp = rdr.Value;
										break;
									case "Command":
										cmd = rdr.Value;
										break;
									case "Time":
										tm = rdr.Value;
										break;
								}
							}
							if ((tp!=null) && (cmd!=null) && (tm!=null))
							{
								if ((tp == "CXP0")||(tp == "CXP1")||(tp == "CXP3"))
								{
									relativeTime = DateTime.Parse(tm,culture,DateTimeStyles.None) - offset;
									lock (pData)
									{
										pData.Add(new PresenterDataItem(relativeTime,tp,cmd,fragment));
									}
							
									//A base assumption is that after the first part of a 
									//fragmented stroke, the remaining pieces will follow
									//in order with no other script data interspersed.
									// Note: Presenter2 data uses type CXP3.  These messages are never fragmented.
									if (fragment) //previous data was a fragment
									{
										if (tp=="CXP0") //but this is the final piece
											fragment = false;
									} 
									else 
									{ 
										if (tp=="CXP1") //this is the first piece
											fragment = true;
									}
							
									if ((raiseInitialSlideCallback)&&(relativeTime >= TimeSpan.Zero))
									{
										//RTUpdate has baseUrl and extent built-in.
										if (((baseUrl!=null) && (extent!=null) &&
											(((PresenterDataItem)(pData[pData.Count - 1])).Opcode == PacketType.SlideIndex)) ||
											(((PresenterDataItem)(pData[pData.Count - 1])).Opcode == PacketType.RTUpdate))
										{
											if (OnInitialSlide != null)
											{
												if (((PresenterDataItem)(pData[pData.Count - 1])).Opcode == PacketType.SlideIndex)
													OnInitialSlide((((PresenterDataItem)(pData[pData.Count - 1])).Slide).ToString());
												else if (((PresenterDataItem)(pData[pData.Count - 1])).Opcode == PacketType.RTUpdate)
												{
													overrideBaseURLandExtent(baseUrl,extent);
													OnInitialSlide(((PresenterDataItem)(pData[pData.Count - 1])).RTNav);
												}
											}
											raiseInitialSlideCallback = false;
										}
									}
								}
							}
							if (verboselogging)
								parent.LoggerWriteInvoke("Read 'Script' element.");
						}
					}

				}
			}
			catch (Exception e)
			{
				parent.LoggerWriteInvoke("Exception while loading presentation data: " + e.ToString());
			}
			stream.Close();
			if (verboselogging)
				parent.LoggerWriteInvoke("Finished reading xml.  Beginning data filtering.");
			reassembleFragments(); //Presenter 1 data only
			filterData(); //remove data if timestamp < 0
			overrideBaseURLandExtent(baseUrl,extent); //Presenter2 data only
			if (verboselogging)
				parent.LoggerWriteInvoke("Finished data filtering.");

			buildTOC();

			if (OnUpdateLoadProgress != null)
				OnUpdateLoadProgress(100);

			if (OnLoadComplete != null)
			{
				OnLoadComplete();
			}
			loadComplete = true;

			parent.LoggerWriteInvoke("Presenter data preload complete.");
		}
		
		/// <summary>
		/// If Presenter2 data and baseURL and/or extent were specified, override the embedded values.
		/// </summary>
		/// <param name="?"></param>
		/// <param name="?"></param>
		private void overrideBaseURLandExtent(String baseUrl,String extent)
		{
			if (baseUrl != null)
			{
				lock (pData)
				{
					foreach(PresenterDataItem pdi in pData)
					{
						if (pdi.Opcode == PacketType.RTUpdate)
						{
							((ArchiveRTNav.RTUpdate)(pdi.RTNav)).BaseUrl = baseUrl;
						}
					}
				}
			}

			if (extent != null)
			{
				lock (pData)
				{
					foreach(PresenterDataItem pdi in pData)
					{
						if (pdi.Opcode == PacketType.RTUpdate)
						{
							((ArchiveRTNav.RTUpdate)(pdi.RTNav)).Extent = extent;
						}
					}
				}
			}
		}


		/// <summary>
		/// If Presenter2 data exists, use it to build a Table of Contents.
		/// </summary>
		/// If there is no Presenter2 data, TOCArray remains null.
		private void buildTOC()
		{
			//The null value indicates to caller to use markers to build the TOC.
			TOCArray = null;

			if (NoAutoTOC) //disabled by configuration
				return;

			ArrayList tmpAl = new ArrayList();
			Guid lastDeckGuid = Guid.Empty;
			Int32 lastSlideIndex = -1;

			/// Examine each item in pData.  Compare each 
			/// deckGuid and slideIndex to the last seen.  If a change is
			/// found, add a TOC entry containing timestamp, and the RTUpdate object
			foreach(PresenterDataItem pdi in pData)
			{
				if (pdi.Opcode == PacketType.RTUpdate)
				{
					if ((((ArchiveRTNav.RTUpdate)(pdi.RTNav)).SlideIndex != lastSlideIndex) ||
						(((ArchiveRTNav.RTUpdate)(pdi.RTNav)).DeckGuid != lastDeckGuid))
					{
						lastDeckGuid = ((ArchiveRTNav.RTUpdate)(pdi.RTNav)).DeckGuid;
						lastSlideIndex = ((ArchiveRTNav.RTUpdate)(pdi.RTNav)).SlideIndex;
						tmpAl.Add(new TOCEntry(pdi.TimeStamp,"",((ArchiveRTNav.RTUpdate)(pdi.RTNav))));
					}
				}
			}

			if (tmpAl.Count == 0)
				return;

			/// Make another pass through the TOC entries, removing any which didn't persist for at least
			/// N (3?) seconds.  The first one and the last are exempt from 
			/// this rule.  Also the first entry is moved to time zero.
			ArrayList tmpAl2 = new ArrayList();
			TOCEntry thisToc, nextToc, prevToc;
			((TOCEntry)tmpAl[0]).Time = TimeSpan.FromSeconds(0);
			tmpAl2.Add(tmpAl[0]);
			for (int i=1;i<(tmpAl.Count-1);i++)
			{
				thisToc = (TOCEntry)tmpAl[i];
				nextToc = (TOCEntry)tmpAl[i+1];
				if ((nextToc.Time - thisToc.Time) >= TimeSpan.FromSeconds(3))
				{
					tmpAl2.Add(thisToc);
				}
			}
			tmpAl2.Add(tmpAl[tmpAl.Count-1]); //also keep the last one in all cases.

			/// On the next pass, remove consecutive entries to the same slide, and 
			/// compile a list of deck guids for use when building the entry numbers.
			GuidToIndex guidMap = new GuidToIndex();
			tmpAl.Clear();
			tmpAl.Add(tmpAl2[0]);
			guidMap.Add(((TOCEntry)tmpAl[0]).RTUpdate.DeckGuid);
			for (int i=1;i<tmpAl2.Count; i++)
			{
				thisToc = (TOCEntry)tmpAl2[i];
				prevToc = (TOCEntry)tmpAl2[i-1];
				if ((thisToc.RTUpdate.DeckGuid != prevToc.RTUpdate.DeckGuid) ||
					(thisToc.RTUpdate.SlideIndex != prevToc.RTUpdate.SlideIndex))
				{
					tmpAl.Add(thisToc);
					guidMap.Add(thisToc.RTUpdate.DeckGuid);
				}			
			}

			/// On the final pass, build TOC text from timestamps, slidenumbers and titles.
			/// If there are multiple decks, construct slide numbers like 1.1, 2.3.  Identify
			/// Whiteboard and Student Submission by name.
			Int32 deckCount = guidMap.DeckCount;
			String currentText;
			foreach (TOCEntry t in tmpAl)
			{
				DateTime dt = DateTime.MinValue + t.Time;
				
				currentText = dt.ToString("H:mm:ss.f") + " - ";
				int slideNum = t.RTUpdate.SlideIndex + 1;

				if (deckCount > 1)
					currentText = currentText + guidMap.GetIndex(t.RTUpdate.DeckGuid).ToString() + "." +
						slideNum.ToString() + ". ";
				else
					currentText = currentText + slideNum.ToString() + ". ";

				if (t.RTUpdate.DeckType == (Int32)DeckTypeEnum.Presentation)
					currentText = currentText + slideTitles.Get(t.RTUpdate.DeckGuid,t.RTUpdate.SlideIndex);
				else if (t.RTUpdate.DeckType == (Int32)DeckTypeEnum.Whiteboard)
					currentText = currentText + "Whiteboard";
                else if (t.RTUpdate.DeckType == (Int32)DeckTypeEnum.StudentSubmission)
                    currentText = currentText + "Student Submission";
                else if (t.RTUpdate.DeckType == (Int32)DeckTypeEnum.QuickPoll)
                    currentText = currentText + "QuickPoll";

				t.EntryText = currentText;
			}

			TOCArray = tmpAl.ToArray(typeof(TOCEntry));
			
		}

		/// <summary>
		/// Maintain a map of DeckGuids to Index numbers
		/// </summary>
		private class GuidToIndex
		{
			Hashtable map;
			Int32 lastIndex;
			public GuidToIndex()
			{
				map = new Hashtable();	
				lastIndex = 0;
			}

			public void Add(Guid DeckGuid)
			{
				GetIndex(DeckGuid);
			}
	
			public Int32 GetIndex(Guid DeckGuid)
			{
				if (map.ContainsKey(DeckGuid))
					return (Int32)map[DeckGuid];
				else
				{
					lastIndex++;
					map.Add(DeckGuid,lastIndex);
					return lastIndex;
				}
			}

			public Int32 DeckCount
			{
				get {return map.Count;}
			}
		}

		/// <summary>
		/// remove data where timestamps are less than zero.
		/// </summary>
		private void filterData()
		{
			ArrayList pData2 = new ArrayList();
			PresenterDataItem pdi;
			for (int i=0;i<pData.Count;i++)
			{
				pdi = (PresenterDataItem)(pData[i]);
				if (pdi.TimeStamp >= TimeSpan.Zero)
				{
					pData2.Add(pdi);
				}
			}
			lock(pData)
			{
				pData = pData2;
			}
		}

		/// <summary>
		/// Reassemble fragemented packets
		/// </summary>
		/// Note: in the case of Presenter 2 data which is never fragmented, we 
		/// do some pointless work here.
		private void reassembleFragments()
		{
			ArrayList pData2 = new ArrayList();
			PresenterDataItem mpdi = null;
			PresenterDataItem pdi;

			for (int i=0;i<pData.Count;i++)
			{
				pdi = (PresenterDataItem)(pData[i]);
				if ((pdi.Type == "CXP0") || (pdi.Type == "CXP3"))
				{   // CXP0 and CXP3 indicates this is either a whole packet or the final piece of a fragmented packet.
				
					if (mpdi != null) 
					{
						byte[] tmpb = new byte[mpdi.Data.Length + pdi.Data.Length];
						mpdi.Data.CopyTo(tmpb,0);
						pdi.Data.CopyTo(tmpb,mpdi.Data.Length);
						mpdi.Data = tmpb;
						pData2.Add(mpdi);
						mpdi = null;
					}
					else
					{
						pData2.Add(pdi);
					}
				
				} 
				else if (pdi.Type == "CXP1")  //this is part of a fragmented packet, but not the final part.
				{
			
					if (mpdi == null)
					{
						mpdi = pdi.Copy();
						if (pdi.Type == "CXP1")
							mpdi.Type = "CXP0";
					}
					else
					{
						byte[] tmpb = new byte[mpdi.Data.Length + pdi.Data.Length];
						mpdi.Data.CopyTo(tmpb,0);
						pdi.Data.CopyTo(tmpb,mpdi.Data.Length);
						mpdi.Data = tmpb;
					}
				}
			}
			lock (pData)
			{
				pData = pData2;
			}
		}

		/// <summary>
		/// During presentation data preload, raise this event when
		/// all data necessary to form the initial slide url is available.
		/// </summary>
		public event initialSlideReadyCallback OnInitialSlide;
		public delegate void initialSlideReadyCallback(object slideNum);

		/// <summary>
		/// During presentation data preload, raise this event when
		/// to update the load progress indicator.
		/// </summary>
		public event updateLoadProgressCallback OnUpdateLoadProgress;
		public delegate void updateLoadProgressCallback(object percentLoaded);

		/// <summary>
		/// Indicates that some presentation data should be rendered now.
		/// </summary>
		public event displayDataHandler OnDisplayData;
		public delegate void displayDataHandler(object packet);
		
		/// <summary>
		/// Presentation data preload is finished.
		/// </summary>
		public event loadCompleteHandler OnLoadComplete;
		public delegate void loadCompleteHandler();

		/// <summary>
		/// If the TOC was built from presentation data, this event will be raised
		/// to indicate when the media has moved to a different entry.
		/// </summary>
		public event tocEntryChangeHandler OnTOCEntryChange;
		public delegate void tocEntryChangeHandler(object newIndex);

	}
}
