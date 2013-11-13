using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Drawing;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

using WorkSpace;
using SlideViewer;
using MSR.LST;
using Ink = Microsoft.Ink;
using ArchiveRTNav;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Accept messages, and reflect changes to the slide display. 
    /// This contains support for two distinct archive formats,
    /// the older of which is no longer produced.  The playback code
    /// is kept around so that there may be some chance that the old
    /// archives from the era could still work.
	/// </summary>
	public class SlideViewerGlue {

        #region Private Members

        private System.Net.WebClient webClient;
		private LockedSlideView mySlideView;
		private ScreenConfiguration screenConfiguration;
		private WorkQueue workQueue;
		private WebViewerForm parent;
		private SlideMap slideMap;
		private Color currentBackgroundColor;
		private IDictionary myGuidReverseTable = new Hashtable(); // Guid -> ScribbleIntPair
		private IDictionary myGuidTable = new Hashtable(); // ScribbleIntPair -> Guid
		private Slide currentSlide;
		private SlideOverlay currentOverlay;		
		private double XinkScaleFactor; //With other than 96DPI we need to scale ink
		private double YinkScaleFactor;
        private double currentSlideSize;
        private int currentSlideIndex;
        private SlideDeck slideDeck;
        private string baseURL;
        private string extent;

        private struct ScribbleIntPair {
            public ScribbleIntPair(Scribble scribble, int id) { this.scribble = scribble; this.id = id; }
            public Scribble scribble;
            public int id;
        }

        #endregion Private Members

        #region Properties

        public int CurrentSlideIndex {
            get { return currentSlideIndex; }
            set { lock (this) { currentSlideIndex = value; } }
        }

		public SlideDeck SlideDeck 
		{
			get { return slideDeck; }
			set { slideDeck = value; }
		}

		public string BaseURL
		{
			get { return this.baseURL; }
			set { this.baseURL = value; }
		}

		public string Extent
		{
			get { return this.extent; }
			set { this.extent = value; }
		}

        #endregion Properties

        #region Static

		public readonly static Guid GUID_TAG = 
			new Guid ("{BAF82C27-E0CA-64B7-EDAA-E5D9E62118EA}"); //Presenter 1.1.03
		public readonly static Guid NEW_GUID_TAG = 
			new Guid ("{179222D6-BCC1-4570-8D8F-7E8834C1DD2A}"); //Presenter 2.0

        #endregion Static

        #region Constructor
        /// <summary>
		/// Construct
		/// </summary>
		/// <param name="view">The control where we display slides and ink</param>
		/// <param name="form">The top level form for the app</param>
		public SlideViewerGlue(LockedSlideView view, WebViewerForm form)
		{
			mySlideView = view;
			parent = form;

			mySlideView.LockObject = form;
			mySlideView.Data = new SlideViewData();

			currentBackgroundColor = mySlideView.LayerPanel.BackColor;

			this.webClient = new System.Net.WebClient();
			this.webClient.BaseAddress = "";
			this.webClient.Credentials = null;

			//Scale factors to make ink appear at the right place on this display.
			Graphics g = form.CreateGraphics();
			XinkScaleFactor = 96.0/g.DpiX;
			YinkScaleFactor = 96.0/g.DpiY;
			g.Dispose();

			slideDeck = new SlideDeck(); //Create deck with one blank slide
			currentSlideIndex = 0;	
			currentSlideSize = 1;

			//Put that slide and overlay into SlideViewData:
			mySlideView.Data.ChangeSlide(slideDeck.GetSlide(0), slideDeck.GetOverlay(0)); 

			//Prepare layers for the SlideView control
			mySlideView.ClearLayers();
			mySlideView.AddLayer(new ImageLayer());
            mySlideView.AddLayer(new DynamicElementsLayer());
			mySlideView.AddLayer(new InkLayer());

			mySlideView.Scrollable = false;  //The app can scroll, but the user can't
			
			//need this for scrolling:
			workQueue = new WorkQueue();
			workQueue.LockObject = form;

			baseURL = null;
			extent = null;

			//Map Presenter2 decks to a flat array.
			slideMap = new SlideMap();
		}

        #endregion Constructor

        #region "Old" Message Handling

        /// <summary>
		/// Wrap the Receive method, creating the slide object first if necessary.
		/// </summary>
		/// In asynchronous playback, we may need to replay slide-specific Presenter data
		/// before the slides have been created.  One case where this occurs is if the user jumps 
		/// ahead in media.  Here we want to build overlays for all slide objects even though
		/// they may not have been displayed yet. 
		/// <param name="slideNum"></param>
		/// <param name="data"></param>
		public void SpReceive(int slideNum, byte[] data)
		{
			Receive(data);
		}

        /// <summary>
        /// Act upon a subset of presenter opcodes generated by Presenter 1.1.03.
        /// </summary>
        /// <param name="data">The data in the wire format</param>
        public void Receive(byte[] data) {
            byte opCode = data[0];
            BufferChunk bc = new BufferChunk(data, data.Length);
            // lock?
            switch (opCode) {
                case PacketType.SlideIndex:
                    WebViewerReceiveSlideIndex(bc);
                    break;
                case PacketType.Scribble: //add a stroke
                    ReceiveStroke(bc);
                    break;
                case PacketType.ScribbleDelete:  //delete one stroke
                    ReceiveDeleteStroke(bc);
                    break;
                case PacketType.Scroll:  //scroll
                    ReceiveScrollPosition(bc);
                    break;
                case PacketType.ClearAnnotations: //clear deck operation
                    ClearAnnotations();
                    break;
                case PacketType.ClearSlide:
                    //Unused
                    break;
                case PacketType.ClearScribble: //clear all strokes from the slide
                    ReceiveClearScribble(bc);
                    break;
                case PacketType.ResetSlides: //empty deck operation
                    Reset();
                    break;
                case PacketType.ScreenConfiguration: //minimize/maximize buttons
                    ReceiveScreenConfiguration(bc);
                    break;
                default:
                    parent.LoggerWriteInvoke("Unhandled opcode: " + opCode.ToString());
                    break;
            }
        }

        /// <summary>
        /// Attempt to display the indicated slide
        /// </summary>
        /// <param name="num">slide number</param>
        /// If base url and extent are not null, attempt to load and display the slide.
        public void ShowSlide(int num) {
            if (num != 0) {
                Slide tmpSlide = slideDeck.GetSlide(num);

                if ((tmpSlide == null) || (tmpSlide.Image == null)) {
                    ThreadPool.QueueUserWorkItem(new WaitCallback(GetWebImageThread), num.ToString());
                    return;
                }
            }
            DisplaySlideIndex(num);
        }

        public void SetScreenConfiguration(ScreenConfiguration sc) {
            if (sc != null) {
                ReceiveScreenConfiguration(sc);
            }
        }

        private void GetWebImageThread(object o) {
            if ((baseURL == null) || (extent == null))
                return;

            System.Drawing.Image tmpImage;
            string imgURL = baseURL + (string)o + "." + extent;
            try {
                tmpImage = System.Drawing.Image.FromStream(webClient.OpenRead(imgURL));
            }
            catch {
                parent.LoggerWriteInvoke("Exception while opening image url: " + imgURL);
                return;
            }
            //put the new image in the SlideDeck.
            int newIndex = Convert.ToInt32((string)o);
            slideDeck.SetSlide(newIndex, new Slide(new Bitmap(tmpImage), "Slide " + (string)o));

            // display the newly loaded slide.
            DisplaySlideIndex(Convert.ToInt32(o));  // the latter two params are not relevant here.
        }

        /// <summary>
        /// WebViewer wrapper for ReceiveSlideIndex
        /// </summary>
        /// <param name="bc"></param>
        /// If this isn't slide zero, and if we don't already have the slide bitmap,
        /// fire off a thread to fetch the image and store it.  Otherwise, pass control
        /// to ReceiveSlideIndex.
        private void WebViewerReceiveSlideIndex(BufferChunk bc) {
            int newSlideIndex = UnpackShort(bc, 1);
            int slidesInDeck = UnpackShort(bc, 3);
            UpdateType updateType = (UpdateType)bc[5];

            WebViewerReceiveSlideIndex(newSlideIndex, slidesInDeck, updateType);
        }

        // Receive SlideIndex for Presenter 1.  Slide zero was the whiteboard, and there was
        // always exactly one deck.
        private void WebViewerReceiveSlideIndex(int newSlideIndex, int slidesInDeck, UpdateType updateType) {
            if (newSlideIndex != 0) {
                Slide tmpSlide = slideDeck.GetSlide(newSlideIndex);

                if ((tmpSlide == null) || (tmpSlide.Image == null)) {
                    ThreadPool.QueueUserWorkItem(new WaitCallback(GetWebImageThread), newSlideIndex.ToString());
                    return;
                }
            }
            DisplaySlideIndex(newSlideIndex);
        }

        private void ReceiveDeleteStroke(BufferChunk bc) {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream(bc.Buffer, 1, bc.Length - 1);
            int slideIndex = (int)formatter.Deserialize(memoryStream);
            // Get the guid to delete.
            Guid guid = (Guid)formatter.Deserialize(memoryStream);

            ReceiveDeleteStroke(slideIndex, guid, GUID_TAG);
        }

        private void ReceiveClearScribble(BufferChunk bc) {
            int slideIndex = UnpackShort(bc, 1);

            SlideOverlay overlay = slideDeck.GetOverlay(slideIndex);
            if (overlay != null && overlay.Scribble != null)
                overlay.Scribble.Clear();
        }

        /// <summary>
        /// Receive stroke for Presenter 1.
        /// </summary>
        /// <param name="bc"></param>
        private void ReceiveStroke(BufferChunk bc) {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream(bc.Buffer, 1, bc.Length - 1);
            int slideIndex = (int)formatter.Deserialize(memoryStream);

            if (this.SlideDeck != null && slideIndex >= 0) {
                InkScribble s = (InkScribble)this.SlideDeck.GetOverlay(slideIndex).Scribble;

                // Restore the bytes to ink.
                byte[] bytes = new byte[memoryStream.Length - memoryStream.Position];
                memoryStream.Read(bytes, 0, bytes.Length);

                Ink.Ink ink = new Ink.Ink();
                ink.Load(bytes);

                // Delete the previous version of this stroke if necessary.
                foreach (Ink.Stroke stroke in ink.Strokes)
                    if (stroke.ExtendedProperties.DoesPropertyExist(GUID_TAG)) {
                        Ink.Stroke foundStroke;
                        bool found = FastFindStrokeFromGuid(s,
                            new Guid((string)stroke.ExtendedProperties[GUID_TAG].Data),
                            out foundStroke, GUID_TAG);
                        if (found) {
                            s.Ink.DeleteStroke(foundStroke);
                        }
                    }

                lock (this) {
                    Rectangle r = ink.Strokes.GetBoundingBox();
                    r.X = (int)(r.X * XinkScaleFactor);
                    r.Y = (int)(r.Y * YinkScaleFactor);
                    r.Width = (int)(r.Width * XinkScaleFactor);
                    r.Height = (int)(r.Height * YinkScaleFactor);
                    s.Ink.AddStrokesAtRectangle(ink.Strokes, r);

                    // Make note of the ink's strokes in the guidtable.
                    // ASSUME that these strokes went on the end.
                    int sCount = s.Ink.Strokes.Count;
                    int newCount = ink.Strokes.Count;
                    for (int i = sCount - newCount; i < sCount; i++) {
                        Ink.Stroke stroke = s.Ink.Strokes[i];
                        if (stroke.ExtendedProperties.Contains(GUID_TAG)) {
                            Guid guid = new Guid((string)stroke.ExtendedProperties[GUID_TAG].Data);
                            this.myGuidTable[new ScribbleIntPair(s, stroke.Id)] = guid;
                            this.myGuidReverseTable[guid] = new ScribbleIntPair(s, stroke.Id);
                        }
                    }
                }
            }
        }

        private void ReceiveScrollPosition(BufferChunk bc) {
            int slideIndex = (int)UnpackInt(bc, 1);
            float scrollExtent = (float)(UnpackInt(bc, 9) / 65536.0);
            float scrollPosition = (float)(UnpackSignedInt(bc, 5) * scrollExtent / int.MaxValue);
            workQueue.SetScrollPos(slideIndex, scrollPosition, scrollExtent);
            if (workQueue.StartScrolling())
                ThreadPool.QueueUserWorkItem(new WaitCallback(ScrollWorker));
        }

        private void Reset() {
            slideDeck.Reset();
            this.UpdateView();
            // Set all views to look at the zeroth slide.
            Slide slide = slideDeck.GetSlide(0) == null ? new Slide() : slideDeck.GetSlide(0);
            SlideOverlay overlay = slideDeck.GetOverlay(0) == null ? new SlideOverlay() : slideDeck.GetOverlay(0);
            this.mySlideView.Data.ChangeSlide(slide, overlay);
        }

        private void ReceiveScreenConfiguration(BufferChunk bc) {
            BinaryFormatter bitmapFormatter = new BinaryFormatter();

            MemoryStream memoryStream = new MemoryStream(bc.Buffer, 1, bc.Length - 1);

            this.ReceiveScreenConfiguration((ScreenConfiguration)bitmapFormatter.Deserialize(memoryStream));
        }

        private void ReceiveScreenConfiguration(ScreenConfiguration screenConfiguration) {
            this.currentSlideSize = screenConfiguration.SlideSize;
            lock (this) {
                this.screenConfiguration = screenConfiguration;
                this.AcceptScreenConfiguration();
            }
        }

        #endregion "Old" Message Handling

        #region "New" Message Handling

        /// <summary>
		/// This one is for the case where there is no preloaded data.  We receive
		/// a raw byte[] from the stream.  We have verified that it is a CXP3 type.
		/// </summary>
		/// 
		public void ReceiveRTNav(byte[] data)
		{
			object rtobj = Helpers.ByteArrayToObject(data); 
			if (rtobj != null)
				ReceiveRTNav(rtobj);
		}

		/// <summary>
		/// Process the data accompanying a CXP3 script type.  This data is generated for Classroom Presenter 2.0 and later
		/// </summary>
		/// <param name="data"></param>
		public void ReceiveRTNav(object rtobj)
		{
			if (rtobj == null)
				return;

			if (rtobj is ArchiveRTNav.RTUpdate) //change slide, slide size or background color.
			{
				WebViewerReceiveUpdate((ArchiveRTNav.RTUpdate)rtobj);
			}
			else if (rtobj is ArchiveRTNav.RTDrawStroke) //stroke
			{
				ReceiveStroke((ArchiveRTNav.RTDrawStroke)rtobj);
			}
			else if (rtobj is ArchiveRTNav.RTScrollLayer) //scroll
			{
				ReceiveScrollPosition((ArchiveRTNav.RTScrollLayer)rtobj);
			}
			else if (rtobj is ArchiveRTNav.RTDeleteStroke) //delete one stroke
			{
				ReceiveDeleteStroke((ArchiveRTNav.RTDeleteStroke)rtobj);
			}
			else if (rtobj is ArchiveRTNav.RTEraseLayer)  //delete strokes and text annotations on current slide
			{
				ReceiveClearScribble((ArchiveRTNav.RTEraseLayer)rtobj);
			}
			else if (rtobj is ArchiveRTNav.RTEraseAllLayers) //delete all strokes and text annotations on all slides in one deck
			{
				ClearAnnotations((ArchiveRTNav.RTEraseAllLayers)rtobj);
			}
            else if (rtobj is ArchiveRTNav.RTTextAnnotation) { 
                ReceiveTextAnnotation((ArchiveRTNav.RTTextAnnotation)rtobj);
            }
            else if (rtobj is ArchiveRTNav.RTDeleteTextAnnotation) {
                ReceiveDeleteTextAnnotation((ArchiveRTNav.RTDeleteTextAnnotation)rtobj); //Obsolete
            }
            else if (rtobj is ArchiveRTNav.RTDeleteAnnotation) {
                ReceiveDeleteAnnotation((ArchiveRTNav.RTDeleteAnnotation)rtobj);
            }
            else if (rtobj is ArchiveRTNav.RTQuickPoll) {
                ReceiveQuickPoll((ArchiveRTNav.RTQuickPoll)rtobj);
            }
            else if (rtobj is ArchiveRTNav.RTImageAnnotation) {
                ReceiveImageAnnotation((ArchiveRTNav.RTImageAnnotation)rtobj);
            }
            else {
                Type t = rtobj.GetType();
                parent.LoggerWriteInvoke("**Unhandled RTObject Type:" + t.ToString());
            }

		}

        /// <summary>
        /// These are images that are added to slides "on-the-fly".
        /// </summary>
        /// <param name="rtia"></param>
        private void ReceiveImageAnnotation(RTImageAnnotation rtia) {
            //Get the internal slide index:
            int slideIndex = slideMap.GetMapping(rtia.SlideIndex, rtia.DeckGuid);

            //Get the slide and overlay
            Slide s = SlideDeck.GetSlide(slideIndex);
            SlideOverlay so = SlideDeck.GetOverlay(slideIndex);

            lock (so.DynamicImages) {
                //If the annotation is already on the slide, replace, otherwise add.
                if (so.DynamicImages.ContainsKey(rtia.Guid)) {
                    so.DynamicImages[rtia.Guid] = new DynamicImage(rtia.Guid, rtia.Origin, rtia.Width, rtia.Height, rtia.Img);
                }
                else {
                    so.DynamicImages.Add(rtia.Guid, new DynamicImage(rtia.Guid, rtia.Origin, rtia.Width, rtia.Height, rtia.Img));
                }
            }
            so.RefreshDynamicElements();

        }

        private void ReceiveQuickPoll(RTQuickPoll rtqp) {
            int slideIndex;

            //If we have already created the QuickPoll slide, get its index:
            slideIndex = slideMap.GetMapping(rtqp.SlideIndex, rtqp.DeckGuid);

            //Get the slide and overlay
            Slide s = SlideDeck.GetSlide(slideIndex);
            SlideOverlay so = SlideDeck.GetOverlay(slideIndex);

            so.QuickPoll.Update((int)rtqp.Style, rtqp.Results); 
            so.QuickPoll.Enabled = true;

            so.RefreshDynamicElements();
        }

        /// <summary>
        /// This is an obsolete message type which was used to delete text annotations.
        /// RTDeleteAnnotation is currently used to delete different types of annotation sheets
        /// including text and image.
        /// </summary>
        /// <param name="rtdta"></param>
        private void ReceiveDeleteTextAnnotation(RTDeleteTextAnnotation rtdta) {
            int slideIndex;
            slideIndex = slideMap.GetMapping(rtdta.SlideIndex, rtdta.DeckGuid);

            //Get the slide
            Slide s = SlideDeck.GetSlide(slideIndex);
            SlideOverlay so = SlideDeck.GetOverlay(slideIndex);

            //If the annotation is on the slide, delete it.
            lock (so.TextAnnotations) {
                if (so.TextAnnotations.ContainsKey(rtdta.Guid)) {
                    so.TextAnnotations.Remove(rtdta.Guid);
                }
            }

            so.RefreshDynamicElements();
        }

        /// <summary>
        /// Delete a text or image annotation.
        /// </summary>
        /// <param name="rtda"></param>
        private void ReceiveDeleteAnnotation(RTDeleteAnnotation rtda) {
            int slideIndex;
            slideIndex = slideMap.GetMapping(rtda.SlideIndex, rtda.DeckGuid);

            //Get the slide
            Slide s = SlideDeck.GetSlide(slideIndex);
            SlideOverlay so = SlideDeck.GetOverlay(slideIndex);

            //If the annotation is on the slide, delete it.
            lock (so.TextAnnotations) {
                if (so.TextAnnotations.ContainsKey(rtda.Guid)) {
                    so.TextAnnotations.Remove(rtda.Guid);
                }
            }

            lock (so.DynamicImages) {
                if (so.DynamicImages.ContainsKey(rtda.Guid)) {
                    so.DynamicImages.Remove(rtda.Guid);
                }
            }

            so.RefreshDynamicElements();
        }

        private void ReceiveTextAnnotation(RTTextAnnotation rtta) {
            int slideIndex;
            slideIndex = slideMap.GetMapping(rtta.SlideIndex, rtta.DeckGuid);

            //Get the slide and overlay
            Slide s = SlideDeck.GetSlide(slideIndex);
            SlideOverlay so = SlideDeck.GetOverlay(slideIndex);

            lock (so.TextAnnotations) {
                //If the annotation is already on the slide, replace, otherwise add.
                if (so.TextAnnotations.ContainsKey(rtta.Guid)) {
                    so.TextAnnotations[rtta.Guid] = new TextAnnotation(rtta.Guid, rtta.Text, rtta.Color, rtta.Font, rtta.Origin, rtta.Width, rtta.Height);
                }
                else {
                    so.TextAnnotations.Add(rtta.Guid, new TextAnnotation(rtta.Guid, rtta.Text, rtta.Color, rtta.Font, rtta.Origin, rtta.Width, rtta.Height));
                } 
            }
            so.RefreshDynamicElements();

        }

		/// <summary>
		/// The Presenter 2 version of ShowSlide
		/// </summary>
		/// <param name="rtu"></param>
		public void ShowSlide(ArchiveRTNav.RTUpdate rtu)
		{
			WebViewerReceiveUpdate(rtu);
		}
        
		/// <summary>
		/// Fetch image from web for Presenter 2 data
		/// </summary>
		/// <param name="o"></param>
		private void GetWebImageThread2(object o)
		{
			RTUpdate rtu = (RTUpdate)o;

			if ((rtu.BaseUrl == null) || (rtu.Extent == null))
				return;

			if (rtu.DeckGuid == Guid.Empty)
				return;

			System.Drawing.Image tmpImage;

			string imgURL;

			Guid pDeckGuid = rtu.DeckGuid;
			int pSlideNumber = rtu.SlideIndex + 1; 

			/// Student submissions need to map to a presentation deck and slide.
			if ((rtu.DeckType == (Int32)DeckTypeEnum.StudentSubmission) ||
                (rtu.DeckType == (Int32)DeckTypeEnum.QuickPoll))
			{
				pDeckGuid = rtu.DeckAssociation;
				pSlideNumber = rtu.SlideAssociation + 1;
			}

			//implicit assumption that rtu.BaseUrl includes the trailing '/'.
			imgURL = rtu.BaseUrl + pDeckGuid.ToString() + "/slide" + pSlideNumber.ToString() + "." + rtu.Extent;
			
			try 
			{
				tmpImage = System.Drawing.Image.FromStream(webClient.OpenRead(imgURL));
			}
			catch 
			{
				parent.LoggerWriteInvoke("Exception while opening image url: " + imgURL);
				return;
			}
			//put the new image in the SlideDeck.
			int internalIndex = slideMap.GetMapping(rtu.SlideIndex,rtu.DeckGuid);
			slideDeck.SetSlide(internalIndex,new Slide(new Bitmap(tmpImage),"Slide " + internalIndex.ToString()));

            // display the newly loaded slide.
            DisplaySlideIndex(internalIndex);  // the latter two params are not relevant here.
		}

		/// <summary>
		/// Handle RTUpdate -- one of the repeating messages emitted by Presenter 2.
		/// </summary>
		/// <param name="rtu"></param>
		private void WebViewerReceiveUpdate(ArchiveRTNav.RTUpdate rtu)
		{
			int internalSlideIndex = slideMap.GetMapping(rtu.SlideIndex,rtu.DeckGuid);

			/// Fetch slide images for any slide in a Presentation deck
			/// and any slide in a StudentSubmission deck.
            /// Note: it used to be that rtu.SlideIndex==0 in the SS case was
            /// a whiteboard slide.  This does not seem to be true anymore.  For CP3 we use explicit DeckTypeAssociation.
			if ((rtu.DeckType == (Int32)DeckTypeEnum.Presentation) ||
				((rtu.DeckType == (Int32)DeckTypeEnum.StudentSubmission) && (rtu.DeckTypeAssociation != (Int32)DeckTypeEnum.Whiteboard)) ||
                ((rtu.DeckType == (Int32)DeckTypeEnum.QuickPoll) && (rtu.DeckTypeAssociation != (Int32)DeckTypeEnum.Whiteboard)))
			{
				Slide tmpSlide = slideDeck.GetSlide(internalSlideIndex);

				if ((tmpSlide == null) || (tmpSlide.Image == null))
				{
					ThreadPool.QueueUserWorkItem(new WaitCallback(GetWebImageThread2),rtu);
				}
				else
                    DisplaySlideIndex(internalSlideIndex);
			}
			else //No background image -- this is a whiteboard slide
			{
                DisplaySlideIndex(internalSlideIndex);
			}

			//if slide size is not up to date, fix that.
			//Console.WriteLine("WebviewerReceiveRTUpdate new slide size=" + rtu.SlideSize.ToString() +
			//	" current slide size=" + currentSlideSize.ToString());
			if (currentSlideSize != rtu.SlideSize)
			{
				currentSlideSize = rtu.SlideSize;
				this.screenConfiguration = new ScreenConfiguration();
				this.screenConfiguration.SlideSize = rtu.SlideSize;
				AcceptScreenConfiguration();
			}

			//Update background color if necessary.
			if (currentBackgroundColor != rtu.BackgroundColor)
			{
				mySlideView.LayerPanel.BackColor = rtu.BackgroundColor;
				currentBackgroundColor = rtu.BackgroundColor;
			}

			//Update scroll position
			workQueue.SetScrollPos(internalSlideIndex, (float)rtu.ScrollPosition, (float)rtu.ScrollExtent);
			if (workQueue.StartScrolling())
				ThreadPool.QueueUserWorkItem (new WaitCallback( ScrollWorker));
		}


        /// <summary>
        /// Delete Stroke -- Presenter2
        /// </summary>
        /// <param name="rtds"></param>
        private void ReceiveDeleteStroke(ArchiveRTNav.RTDeleteStroke rtds) {
            Int32 internalSlideIndex = slideMap.GetMapping(rtds.SlideIndex, rtds.DeckGuid);
            ReceiveDeleteStroke(internalSlideIndex, rtds.Guid, NEW_GUID_TAG);
        }

        /// <summary>
        /// Delete all ink and text annotations on a slide -- Presenter 2
        /// </summary>
        /// <param name="rtel"></param>
        private void ReceiveClearScribble(ArchiveRTNav.RTEraseLayer rtel) {
            Int32 internalSlideIndex = slideMap.GetMapping(rtel.SlideIndex, rtel.DeckGuid);
            SlideOverlay overlay = slideDeck.GetOverlay(internalSlideIndex);
            if (overlay != null) {
                if (overlay.Scribble != null) {
                    overlay.Scribble.Clear();
                }
                if ((overlay.TextAnnotations != null) && (overlay.TextAnnotations.Count > 0)) {
                    lock (overlay.TextAnnotations) {
                        overlay.TextAnnotations.Clear();
                    }
                    overlay.RefreshDynamicElements();
                }
            }
        }

        /// <summary>
        /// Receive Presenter2 stroke
        /// </summary>
        /// <param name="drawStroke"></param>
        public void ReceiveStroke(ArchiveRTNav.RTDrawStroke drawStroke) {
            int slideIndex;
            slideIndex = slideMap.GetMapping(drawStroke.SlideIndex, drawStroke.DeckGuid);
            InkScribble s = (InkScribble)this.SlideDeck.GetOverlay(slideIndex).Scribble;
            Ink.Stroke foundStroke;
            bool found = FastFindStrokeFromGuid(s, drawStroke.Guid, out foundStroke, NEW_GUID_TAG);
            if (found)
                s.Ink.DeleteStroke(foundStroke);

            Ink.Ink ink = drawStroke.Ink;

            lock (this) {
                Rectangle r = ink.Strokes.GetBoundingBox();
                r.X = (int)(r.X * XinkScaleFactor);
                r.Y = (int)(r.Y * YinkScaleFactor);
                r.Width = (int)(r.Width * XinkScaleFactor);
                r.Height = (int)(r.Height * YinkScaleFactor);
                s.Ink.AddStrokesAtRectangle(ink.Strokes, r);

                // Make note of the ink's strokes in the guidtable.
                // ASSUME that these strokes went on the end.
                int sCount = s.Ink.Strokes.Count;
                int newCount = ink.Strokes.Count;
                for (int i = sCount - newCount; i < sCount; i++) {
                    Ink.Stroke stroke = s.Ink.Strokes[i];
                    if (stroke.ExtendedProperties.Contains(NEW_GUID_TAG)) {
                        Guid guid = Guid.Empty;
                        try {	//Saw this except once.  Couldn't repro.  Suspect corrupted data.
                            guid = new Guid((string)stroke.ExtendedProperties[NEW_GUID_TAG].Data);
                        }
                        catch {
                            continue;
                        }
                        this.myGuidTable[new ScribbleIntPair(s, stroke.Id)] = guid;
                        this.myGuidReverseTable[guid] = new ScribbleIntPair(s, stroke.Id);
                    }
                }
            }

        }

        private void ReceiveScrollPosition(ArchiveRTNav.RTScrollLayer rtsl) {
            Int32 internalSlideIndex = slideMap.GetMapping(rtsl.SlideIndex, rtsl.DeckGuid);

            workQueue.SetScrollPos(internalSlideIndex, (float)rtsl.ScrollPosition, (float)rtsl.ScrollExtent);
            if (workQueue.StartScrolling())
                ThreadPool.QueueUserWorkItem(new WaitCallback(ScrollWorker));
        }

        public void ClearAnnotations(ArchiveRTNav.RTEraseAllLayers rteal) {
            ArrayList slides = slideMap.GetSlidesInDeck(rteal.DeckGuid);
            foreach (int internalSlideIndex in slides) {
                SlideOverlay overlay = slideDeck.GetOverlay(internalSlideIndex);
                if (overlay != null) {
                    if (overlay.Scribble != null) {
                        overlay.Scribble.Clear();
                    }
                    if ((overlay.TextAnnotations != null) && (overlay.TextAnnotations.Count > 0)) {
                        lock (overlay.TextAnnotations) {
                            overlay.TextAnnotations.Clear();
                        }
                    }
                }
            }
        }

        #endregion "New" Message Handling

		#region Core Message Handling and Utility

        /// <summary>
        /// Update the displayed slide index.
        /// currentSlideIndex should ONLY be changed in
        /// this method (and when constructing the object).
        /// </summary>
        private void DisplaySlideIndex(int index) {
            this.UpdateView();
            if (index != currentSlideIndex ||
                this.SlideDeck.GetSlide(index) != currentSlide ||
                this.SlideDeck.GetOverlay(index) != currentOverlay) {
                currentSlideIndex = index;

                // Refresh the slide display.
                NewSlide();

                this.mySlideView.Data.ChangeSlide(this.SlideDeck.GetSlide(currentSlideIndex),
                    this.SlideDeck.GetOverlay(currentSlideIndex));
            }
        }

		private void ReceiveDeleteStroke(int slideIndex, Guid strokeGuid, Guid guidTag)
		{
			if (this.SlideDeck != null && slideIndex >= 0) 
			{
				InkScribble s = (InkScribble)this.SlideDeck.GetOverlay(slideIndex).Scribble;

				// Find the stroke to delete.
				Ink.Stroke stroke;
				bool found = this.FindStrokeFromGuid(s, strokeGuid, out stroke, guidTag);

				lock (this) 
				{
					// Delete the stroke if found.
					if (found)
						s.Ink.DeleteStroke(stroke);
				}
			}
		}


		/// <summary>
		/// Find Stroke from Guid
		/// </summary>
		/// The original version was much too slow for the archive replay scenario.
		/// <param name="scribble"></param>
		/// <param name="guid"></param>
		/// <param name="stroke"></param>
		/// <param name="guidTag"></param>
		/// <returns></returns>
		private bool FastFindStrokeFromGuid(InkScribble scribble, Guid guid, out Ink.Stroke stroke, Guid guidTag) 
		{
			Ink.Ink ink = scribble.Ink;

			if (this.myGuidReverseTable.Contains(guid))
			{
				if (((ScribbleIntPair)this.myGuidReverseTable[guid]).scribble == scribble)  
				{
					ScribbleIntPair pair = (ScribbleIntPair)this.myGuidReverseTable[guid];

					/// The count can be zero right after a clear slide or clear deck operation
					/// if count is zero, CreateStrokes will except.
					if (pair.scribble.Count == 0)
					{
						stroke = null;
						return false;
					}

					Ink.Strokes targetStrokes = null;
					try 
					{
						targetStrokes = ink.CreateStrokes(new int[] { pair.id });
					}
					catch (Exception e)
					{ //not clear when this would happen?
						stroke = null;
						Trace.WriteLine("FastFindStrokeFromGuid: Exception during CreateStrokes: " + e.ToString());
						return false;
					}

					if (targetStrokes.Count < 1) //not clear when this would happen?
					{
						stroke = null;
						Trace.WriteLine("FastFindStrokeFromGuid: Found target with zero stroke count.");
						return false;
					}

					// Found it.
					stroke = targetStrokes[0];
					return true;

				}
				else
				{   //This happens when we revisit slides we've been on previously
					this.myGuidReverseTable.Remove(guid);
					stroke = null;
					return false;
				}
			}
			else
			{
				stroke = null;
				return false;
			}
		}

		private bool FindStrokeFromGuid(InkScribble scribble, Guid guid, out Ink.Stroke stroke, Guid guidTag) 
		{
			Ink.Ink ink = scribble.Ink;

			// Check if the guid tables are out of date. This might be indicated by either:
			// * The guid is not in the reverse table.
			// * The reverse table entry's scribble does not match the given scribble.
			// If the tables are out of date, update the entries for this scribble object.
			if (!this.myGuidReverseTable.Contains(guid) ||
				((ScribbleIntPair)this.myGuidReverseTable[guid]).scribble != scribble) 
			{
				// Ensure that, at the least, the stale entry is removed.
				this.myGuidReverseTable.Remove(guid);

				// Refresh entries for all strokes in this scribble.
				foreach (Ink.Stroke s in ink.Strokes) 
				{
					try 
					{
						if (s.ExtendedProperties.DoesPropertyExist(guidTag)) 
						{
							Guid strokeGuid = new Guid((string)s.ExtendedProperties[guidTag].Data);
							this.myGuidReverseTable[strokeGuid] = new ScribbleIntPair(scribble, s.Id);
							this.myGuidTable[new ScribbleIntPair(scribble, s.Id)] = strokeGuid;
						}
					}
					catch 
					{
						//this try/catch because I once saw an exception thrown here.  Not sure if it was a bug
						// in the data, the TPC SDK, or in my code..  Anyway it seemed safe to ignore.
					}

				}
			}

			// If the guid is still not in the table, it is not found.
			if (!this.myGuidReverseTable.Contains(guid)) 
			{
				stroke = null;
				return false;
			}

			// Can we find the specific id sought?
			ScribbleIntPair pair = (ScribbleIntPair)this.myGuidReverseTable[guid];

			//This try/catch is a hack to deal with multiple-play (archive) scenario.
			//I believe the real fix is to catch WMP stop and postion change events and
			//to rebuild guid tables and InkScribble appropriately for the new media position.
			Ink.Strokes targetStrokes = null;
			try 
			{
				targetStrokes = ink.CreateStrokes(new int[] { pair.id });
			}
			catch
			{
				stroke = null;
				return false;
			}

			if (targetStrokes.Count < 1) 
			{
				stroke = null;
				return false;
			}

			// Found it.
			stroke = targetStrokes[0];
			return true;
		}
		
		private void ScrollWorker( object o)
		{
			float scrollPos;
			float scrollExt;
			int slideIndex;

			while (workQueue.ContinueScrolling())
			{
				if (workQueue.GetScrollPos(out slideIndex, out scrollPos, out scrollExt))
				{  // This must succeed, since queue is not empty, and this
					// is the only worker

						if (this.SlideDeck != null && 
							slideIndex >= 0 && (this.SlideDeck.GetOverlay(slideIndex) != null)) 
						{
							this.SlideDeck.GetOverlay(slideIndex).MaxMylarScrollPosition = scrollExt;
							this.SlideDeck.GetOverlay(slideIndex).MylarScrollPosition = scrollPos;
						}

				}
				else 
				{
					throw new Exception("Impossible case in scroll worker, queue is empty");
				}
			}
		}

		public void ClearAnnotations()
		{
			slideDeck.ClearAnnotations();
			this.UpdateView();
			this.DisplaySlideIndex(this.CurrentSlideIndex);
		}

		/// <summary>
		/// Update the view of the slide to reflect the currentSlideIndex.
		/// </summary>
		public void NewSlide() 
		{
			this.UpdateView();
			currentOverlay = slideDeck.GetOverlay(currentSlideIndex);
			currentSlide = slideDeck.GetSlide(currentSlideIndex);
		}

        /// <summary>
        /// Ugly code alert:  After a slide change we appear to need this to 
        /// keep overlay in sync with the slide.
        /// </summary>
		private void UpdateView() 
		{
            if (slideDeck == null) return;

            int i;
            i = slideDeck.FindSlide(this.mySlideView.Data.Slide);
            if (i >= 0) {
                if (this.mySlideView.Data.Overlay != slideDeck.GetOverlay(i))
                    this.mySlideView.Data.ChangeSlide(
                        slideDeck.GetSlide(i),
                        slideDeck.GetOverlay(i));
            }
            else {
                i = slideDeck.FindOverlay(this.mySlideView.Data.Overlay);
                if (i >= 0) {
                    if (this.mySlideView.Data.Slide != slideDeck.GetSlide(i))
                        this.mySlideView.Data.ChangeSlide(
                            slideDeck.GetSlide(i),
                            slideDeck.GetOverlay(i));
                }
            }
                
		}

		private void AcceptScreenConfiguration() 
		{
			SlideView view = this.mySlideView;

			//For some reason, this caused a hang if script data was preloaded.
			// for now we just assume it's always 1.3333.  The assignment caused the 
			// aspect ration to go from 1.333337 to 1.3333.
			//view.AspectRatio = (float)screenConfiguration.AspectRatio;

			System.Drawing.Drawing2D.Matrix m = new System.Drawing.Drawing2D.Matrix();
			m.Scale(1 / (float)screenConfiguration.SlideSize, 
				1 / (float)screenConfiguration.SlideSize);
			foreach (SlideViewLayer layer in 
				this.FetchLayersOfType(view, typeof(SlideViewLayer))) 
			{
				layer.Transform = m;
			}
			m.Dispose();
		}

		/// <summary>
		/// Get a list of layers of the given type contained in the given view.
		/// </summary>
		private ArrayList FetchLayersOfType(SlideView view, Type layerType) 
		{
			ArrayList hits = new ArrayList();
			for (int i = 0; i < view.LayerCount; i++)
				if (layerType.IsInstanceOfType(view.GetLayer(i)))
					hits.Add(view.GetLayer(i));
			return hits;
		}

		// Unpack a short from a buffer chunk
		private static ushort UnpackShort(BufferChunk bc, int index)
		{
			return (ushort)(256 * bc.Buffer[index + 1] + bc.Buffer[index]);
		}

		private static int UnpackSignedInt(BufferChunk bc, int index)
		{
			uint val = UnpackInt(bc, index);
			int rVal;
			if (val > int.MaxValue)
				rVal = -(int)(~val) - 1;
			else
				rVal = (int)val;

			return rVal;
		}

		// Unpack an int from a buffer chunk
		private static uint UnpackInt(BufferChunk bc, int index)
		{
			uint v0 = bc.Buffer[index];
			uint v1 = bc.Buffer[index + 1];
			uint v2 = bc.Buffer[index + 2];
			uint v3 = bc.Buffer[index + 3];
			uint rVal = (v3 << 24) + (v2 << 16) + (v1 << 8) + v0;
			return rVal;
		}

		public enum UpdateType { FORCED, NORMAL };

		#endregion Core Message Handling and Utility
	}

}
