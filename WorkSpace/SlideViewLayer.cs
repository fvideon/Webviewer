using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using SlideViewer;
using System.Drawing.Drawing2D;
using Ink = Microsoft.Ink;

namespace WorkSpace {
	/// <summary>
	/// A slide view layer is one element of the view of a slide. 
	/// It should know how to paint itself based on the data from 
	/// its slideview (which is always available to it). 
	/// </summary>
	public class SlideViewLayer : IDisposable {
		/// <summary>
		/// A layer may only (correctly) use the SourceCopy composite mode of
		/// a Graphics object if it sets this property to true. If it does not
		/// need this, it should set the property to false. 
		/// </summary>
		/// <remarks>This value should never change over the course of an 
		/// object's lifetime.</remarks>
		public virtual bool UsesNonOpaqueOverwrite { get { return false; } }

		/// <summary>
		/// Layers that declare themselves as visible will receive paint calls and
		/// incur the performance penalties associated with painting. Otherwise, this
		/// resource use/calling will be avoided.
		/// </summary>
		/// <remarks>If this value changes, the layer should call Invalidate with no arguments.</remarks>
		public virtual bool VisibleLayer { get { return true; } }

		/// <summary>
		/// Invalidates <em>this</em> layer's entire extent. (In 1x1 space.)
		/// </summary>
		public void Invalidate() {
			Region r = new Region();
			r.MakeInfinite();
			this.Invalidate(r);
			r.Dispose();
		}

		/// <summary>
		/// Invalidates <em>this</em> layer over the given Region. (In 1x1 space.)
		/// </summary>
        public void Invalidate(Region region) {
            if (this.Substrate != null) {
                System.Drawing.Region r = null;
                try {
                    r = new System.Drawing.Region(region.GetRegionData());
                    r.Transform(InvertMatrix(this.To1x1Transform));
                    if (this.Substrate.InvokeRequired) {
                        this.Substrate.Invoke(new SubstrateInvalidateDelegate(SubstrateInvalidate), new object[] { r });
                    }
                    else {
                        this.Substrate.Invalidate(r);
                    }
                }
                catch (System.ArgumentException ex) {
                    // This happened once during live stream playback 
                    // apparently inside the Invert operation above.
                    Debug.WriteLine(ex.ToString());
                }
                catch (InvalidOperationException ex) {
                    // This can happen due to a GDI+ threading issue when trying to clone a Transform. "Object is in use elsewhere."
                    Debug.WriteLine(ex.ToString());
                }
                finally { 
                    if (r != null) r.Dispose();
                }
            }
            this.OnInvalidated(new RegionEventArgs(region));
        }
		

        private delegate void SubstrateInvalidateDelegate(Region r);

        private void SubstrateInvalidate(Region r)
        {
            this.Substrate.Invalidate(r);
        }

		public event RegionEventHandler Invalidated;
		protected virtual void OnInvalidated(RegionEventArgs args) {
			if (this.Invalidated != null)
				this.Invalidated(this, args);
		}

		/// <summary>
		/// Convenience method, takes a point and transforms it according to the
		/// given matrix. Returns the transformed point.
		/// </summary>
		public static PointF TransformPoint(PointF point, Matrix transform) {
			PointF[] points = new PointF[] { point };
			transform.TransformPoints(points);
			return points[0];
		}

		/// <summary>
		/// Convenience method, takes a point and transforms it according to the
		/// given matrix. Returns the transformed point.
		/// </summary>
		public static Point TransformPoint(Point point, Matrix transform) {
			Point[] points = new Point[] { point };
			transform.TransformPoints(points);
			return points[0];
		}

		/// <summary>
		/// Convenience method, takes a matrix and returns its inverse
		/// </summary>
		public static Matrix InvertMatrix(Matrix matrix) {
			matrix.Invert();
			return matrix;
		}

		/// <summary>
		/// A transform from the substrate coordinates to the 1x1 coordinates
		/// layers draw onto. To get the inverse transform, invert the matrix.
		/// <para>Note: returns identity if the substrate is currently null.</para>
		/// </summary>
		public virtual Matrix To1x1Transform {
			get {
				Matrix transform = new Matrix();
                if (Substrate != null)
                    transform.Scale(1f / Substrate.ClientSize.Width, 1f / Substrate.ClientSize.Height, MatrixOrder.Prepend);
				Matrix m = this.Transform;
				transform.Multiply(m, MatrixOrder.Prepend);
				m.Dispose();
				return transform;
			}
		}

		private Matrix myTransform = new Matrix();

		/// <summary>
		/// A transform applied after scaling from the substrate to a 1x1 canvas.
		/// Can be used to alter the drawing appearance of layers.
		/// </summary>
		public virtual Matrix Transform {
			get { return myTransform == null ? null : (Matrix)myTransform.Clone(); }
			set {
				this.Invalidate();
				if (myTransform != null)
					myTransform.Dispose();
				myTransform = (Matrix)value.Clone();
				this.Invalidate();
			}
		}

		/// <summary>
		/// Detach from the existing ViewData. Detach will only be called (by the base) 
		/// when something is currently attached (that is, the # of detach calls
		/// since construction is one less than the number of attach calls).
		/// <para>ViewData may or may not be the same ViewData object which is 
		/// being detached (i.e., it may already have changed). Subclasses should
		/// not count on using ViewData for their Detachment.</para>
		/// <para>Subclasses that override this method should call the base.</para>
		/// </summary>
		/// <remarks>It will often be necessary for subclasses to cache data to
		/// make their detach work properly (as there is no guarantee that the
		/// ViewData at the time of a call to DetachData is, in fact, the ViewData
		/// being detached). Subclasses should be very careful about when caching
		/// occurs and when the use of that cached data occurs. In particular,
		/// consider caching <em>before</em> calling the base class but using
		/// the data afterward so that your caches and the base class's caches
		/// are both updated when you start doing processing.</remarks>
		protected virtual void DetachData() {
		}

		/// <summary>
		/// Attach to new ViewData. Attach will only be called (by the base) 
		/// when nothing is currently attached (that is, the # of detach calls
		/// since construction is equal to the number of attach calls).
		/// <para>It is a precondition that ViewData is non-null and that
		/// it is the ViewData which is being attached.</para>
		/// <para>Subclasses that override this method should call the base.</para>
		/// </summary>
		protected virtual void AttachData() {
		}

		/// <summary>
		/// Detach from the existing Substrate. Detach will only be called (by the base) 
		/// when something is currently attached (that is, the # of detach calls
		/// since construction is one less than the number of attach calls).
		/// <para>Substrate is guaranteed to be the substrate being detached.</para>
		/// <para>Subclasses that override this method should call the base.</para>
		/// </summary>
		protected virtual void DetachSubstrate() {
		}

		/// <summary>
		/// Attach to new Substrate. Attach will only be called (by the base) 
		/// when nothing is currently attached (that is, the # of detach calls
		/// since construction is equal to the number of attach calls).
		/// <para>It is a precondition that Substrate is non-null and that
		/// it is the substrate which is being attached.</para>
		/// <para>Subclasses that override this method should call the base.</para>
		/// </summary>
		protected virtual void AttachSubstrate() {
		}

		private SlideViewData myViewData;
		protected virtual SlideViewData ViewData {
			get { return myViewData; }
			set {
				if (myViewData != value) {
					if (myViewData != null) {
						ViewData.Changed -= new EventHandler(this.HandleChangedData);
						this.DetachData();
					}
					myViewData = value;
					if (myViewData != null) {
						ViewData.Changed += new EventHandler(this.HandleChangedData);
						this.AttachData();
					}
				}
			}
		}

		private Control mySubstrate;

		/// <summary>
		/// A reference to the control on which this layer appears. This should
		/// be used to handle input events and to leverage existing C# windowing
		/// constructs to reduce effort in layers.
		/// </summary>
		protected virtual Control Substrate {
			get { return mySubstrate; }
			set {
				if (mySubstrate != value) {
					if (mySubstrate != null)
						this.DetachSubstrate();
					mySubstrate = value;
					if (mySubstrate != null)
						this.AttachSubstrate();
				}
			}
		}

		public SlideViewData GetData() { return this.ViewData; }
		public Control GetSubstrate() { return this.Substrate; }

		/// <summary>
		/// Paints this object into the given Graphics context. 
		/// All SlideViewLayer objects look (to the outside world) as if
		/// they paint to a 1x1 square. Note that scrolled SlideViewLayers
		/// will automatically shift this graphics context to account for
		/// the position of the scrollBar.
		/// </summary>
		public void Paint(Graphics g) { 
			Matrix old = g.Transform;
			Matrix m = g.Transform;
			m.Multiply(InvertMatrix(this.To1x1Transform), MatrixOrder.Prepend);
			g.Transform = m;
			this.InternalPaint(g);
			g.Transform = old;
			m.Dispose();
			old.Dispose();
		}

		/// <summary>
		/// Locked, overridable version of Paint. Subclasses should override
		/// this method to get the behavior they want. Subclasses should call
		/// the base class's InternalPaint method.
		/// </summary>
		protected virtual void InternalPaint(Graphics g) {
		}

		private void HandleNewData(object sender, EventArgs args) {
			ViewData = ((SlideView)sender).Data;
		}

		private void HandleChangedData(object sender, EventArgs args) {
			this.HandleChangedData();
		}

		/// <summary>
		/// Handles changed data by detaching and attaching.
		/// </summary>
		private void HandleChangedData() {
			DetachData();
			AttachData();
		}

		private SlideView myView = null;
		public void Detach() {
			SlideView view = myView;
			myView = null;
			if (view != null) {
				if (view.HasLayer(this))
					view.RemoveLayer(this);
				view.DataChanged -= new EventHandler(this.HandleNewData);	
				ViewData = null;
				Substrate = null;
			}
		}

		public void Attach(SlideView view) {
			this.Detach();
			myView = view;
			if (myView != null) {
				myView.DataChanged += new EventHandler(this.HandleNewData);
				ViewData = myView.Data;
				Substrate = myView.LayerPanel;
			}
		}

		public SlideViewLayer() { }

		/// <param name="view">An initial view to attach this layer to (it takes its
		/// data and substrate from the view)</param>
		public SlideViewLayer(SlideView view) { 
			this.Attach(view);
		}

		public virtual void Dispose() {
			if (this.myTransform != null) {
				this.myTransform.Dispose();
				this.myTransform = null;
			}
		}
	}

	public class ScrolledSlideViewLayer : SlideViewLayer {
		public ScrolledSlideViewLayer() : base() { }
		public ScrolledSlideViewLayer(SlideView view) : base(view) { }
	
		/// <summary>
		/// Moves a 1x1 "window" from the top of the overlay to the current scroll position.
		/// </summary>
		public Matrix ToOverlayTransform {
			get {
				float scroll;
				scroll = (this.ViewData != null && this.ViewData.Overlay != null) ?
					this.ViewData.Overlay.MylarScrollPosition : 0;
				Matrix transform = new Matrix();
				transform.Translate(0, scroll, MatrixOrder.Prepend);
				return transform;
			}
		}

		public override Matrix To1x1Transform {
			get {
				Matrix transform = new Matrix();
				transform.Multiply(this.ToOverlayTransform, MatrixOrder.Prepend);
				if (Substrate != null)
					transform.Scale(1f / Substrate.ClientSize.Width, 1f / Substrate.ClientSize.Height, MatrixOrder.Prepend);
				Matrix m = this.Transform;
				transform.Multiply(m, MatrixOrder.Prepend);
				m.Dispose();
				return transform;
			}
		}

		private SlideOverlay myOverlay = null;
		protected SlideOverlay Overlay { get { return myOverlay; } }

		protected override void DetachData() {
			if (myOverlay != null) {
				myOverlay.Scrolled -= new EventHandler(this.HandleScroll);
				myOverlay = null;
			}
			base.DetachData();
		}

		protected override void AttachData() {
			if (this.ViewData.Overlay != null) {
				myOverlay = this.ViewData.Overlay;
				myOverlay.Scrolled += new EventHandler(this.HandleScroll);
			}
			base.AttachData();
		}

		private void HandleScroll(object sender, EventArgs args) {
			this.HandleScroll();
		}
			
		/// <summary>
		/// Convenience method for subclasses.
		/// Called when the mylar scrolls.
		/// Default does nothing; overrides should fill in.
		/// </summary>
		protected virtual void HandleScroll() {
		}
	}

	/// <summary>
	/// An ink-based scribble viewing layer. 
	/// </summary>
	public class InkLayer : ScrolledSlideViewLayer {
		public readonly static Size CANONICAL_SIZE = new Size(500, 500);
        
		public override bool VisibleLayer { get { return false; } }

		protected Ink.Renderer myRenderer = new Ink.Renderer();
		private InkScribble myScribble = null;

		public InkLayer() : base() { Initialize(); }
		public InkLayer(SlideView view) : base(view) { Initialize(); }

		private void Initialize() {
		}

		protected override void AttachSubstrate() {
			base.AttachSubstrate();

			this.Substrate.SizeChanged += new EventHandler(this.HandleSizeChanged);
			this.Substrate.Paint += new PaintEventHandler(this.HandleSubstratePaint);

			if (this.ViewData != null)
				this.HandleScroll();
		}

		protected override void DetachSubstrate() {
			base.DetachSubstrate();
			this.Substrate.SizeChanged -= new EventHandler(this.HandleSizeChanged);
			this.Substrate.Paint -= new PaintEventHandler(this.HandleSubstratePaint);
		}

		protected override void AttachData() {
			this.myScribble = null;
			if (this.ViewData.Overlay != null &&
				this.ViewData.Overlay.Scribble != null &&
				this.ViewData.Overlay.Scribble is InkScribble)
				myScribble = (InkScribble)this.ViewData.Overlay.Scribble;
			base.AttachData();
			if (this.myScribble != null) {
				this.myScribble.Cleared += new EventHandler(this.HandleCleared);
				this.myScribble.InkAdded += new NumericEventHandler(this.HandleInkAdded);
				this.myScribble.StrokeDeleted += new NumericEventHandler(this.HandleStrokeDeleted);
				this.myScribble.Stroke += new NumericEventHandler(this.HandleStroke);
			}
			if (this.Substrate != null)
				this.HandleScroll();
		}

		protected override void DetachData() {
			if (this.myScribble != null) {
				try 
				{
					this.myScribble.Cleared -= new EventHandler(this.HandleCleared);
					this.myScribble.InkAdded -= new NumericEventHandler(this.HandleInkAdded);
					this.myScribble.StrokeDeleted -= new NumericEventHandler(this.HandleStrokeDeleted);
					this.myScribble.Stroke -= new NumericEventHandler(this.HandleStroke);
					this.myScribble = null;
				}
				catch (Exception e)
				{
					Trace.WriteLine(e.ToString());
				}
			}
			base.DetachData();
		}

		protected virtual void HandleSubstratePaint(object sender, PaintEventArgs args) {
			if (this.myScribble != null)
				this.myScribble.Draw(args.Graphics, this.myRenderer);
		}

		protected virtual void HandleInkAdded(object sender, NumericEventArgs args) {
			this.Substrate.Invalidate();
		}

		protected virtual void HandleStrokeDeleted(object sender, NumericEventArgs args) {
			this.Substrate.Invalidate();
		}

		private void HandleSizeChanged(object sender, EventArgs args) {
			this.HandleScroll();
		}

        /// <summary>
        /// The name is misleading.  CP used to support mylar scroll, but no longer. 
        /// However this is still called and used to do all the ink scaling.
        /// </summary>
		protected override void HandleScroll() {
			// Ignore resizes leading to zero area.
			if (this.Substrate.ClientSize.Height == 0 || 
				this.Substrate.ClientSize.Width == 0)
				return;
			
			// Find the ink space lower right corner of the canvas.
			Point lr = new Point(CANONICAL_SIZE);
			Graphics tempG = this.Substrate.CreateGraphics();
			(new Ink.Renderer()).PixelToInkSpace(tempG, ref lr);
			tempG.Dispose();

			// Create a matrix which will translate by MylarScrollPosition screenfuls and
			// scale to the canonical size.
			Matrix m = new Matrix();
			m.Translate(0, (this.Overlay != null ? -this.Overlay.MylarScrollPosition : 0) * lr.Y, MatrixOrder.Append);
			m.Scale(1f / CANONICAL_SIZE.Width, 1f / CANONICAL_SIZE.Height, MatrixOrder.Append);
			m.Scale(this.Substrate.ClientSize.Width, this.Substrate.ClientSize.Height, MatrixOrder.Append);

            // If the slide is wide screen format, then the image will be placed in the top of the 4x3 display area.
            // Push the ink up so it will align.
            if ((this.ViewData != null) &&
                (this.ViewData.Slide != null) &&
                (this.ViewData.Slide.Image != null)) {
                    try {
                        if (((float)this.ViewData.Slide.Image.Width / (float)this.ViewData.Slide.Image.Height) > 1.33334f) {
                            float h = 1.333333f * (float)(this.ViewData.Slide.Image.Height) / (float)(this.ViewData.Slide.Image.Width);
                            m.Scale(1f, h, MatrixOrder.Append);
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine(ex.ToString());
                        // We can get InvalidAccessException: "Object is in use elsewhere" on Image.Width.
                    }
            }

			Matrix thisTransform = this.Transform; // accounts for the 'shrink slide' feature.
			m.Multiply(InvertMatrix(thisTransform), MatrixOrder.Append);
			thisTransform.Dispose();
			
			this.myRenderer.SetViewTransform(m);
			m.Dispose();
			this.Substrate.Invalidate();
		}

		protected virtual void HandleStroke(object sender, NumericEventArgs args) {
			this.Substrate.Invalidate();
		}

		protected virtual void HandleCleared(object sender, EventArgs args) {
			this.Substrate.Invalidate();
		}

		protected override void InternalPaint(Graphics g) {
			base.InternalPaint(g);
		}

		public override Matrix Transform {
			get { return base.Transform; }
			set {
				base.Transform = value;
				this.HandleScroll();
			}
		}
	}

    /// <summary>
    /// Layer for dynamic images, quickpolls and text annotations
    /// </summary>
    public class DynamicElementsLayer : SlideViewLayer {

        public DynamicElementsLayer() : base() { }
        public DynamicElementsLayer(SlideView view) : base(view) { }
        private Dictionary<Guid, TextAnnotation> myAnnotations;
        private Dictionary<Guid, DynamicImage> myDynamicImages;
        private QuickPoll myQuickPoll;
        private Matrix myTransform;
        private SlideOverlay mySlideOverlay;

        //Like Ink, text annotations use this 500x500 canonical size.
        public readonly static Size CANONICAL_SIZE = new Size(500, 500);

        /// <summary>
        /// Layers that declare themselves as visible will receive paint calls and
        /// incur the performance penalties associated with painting. Otherwise, this
        /// resource use/calling will be avoided.
        /// </summary>
		public override bool VisibleLayer { get { return false; } }

        /// <summary>
        /// The substrate is the control itself.
        /// </summary>
		protected override void AttachSubstrate() {
			base.AttachSubstrate();
            myTransform = new Matrix();
            SetTransform();
			this.Substrate.SizeChanged += new EventHandler(this.HandleSizeChanged);
			this.Substrate.Paint += new PaintEventHandler(this.HandleSubstratePaint);
		}

		protected override void DetachSubstrate() {
			base.DetachSubstrate();
			this.Substrate.SizeChanged -= new EventHandler(this.HandleSizeChanged);
			this.Substrate.Paint -= new PaintEventHandler(this.HandleSubstratePaint);
            myTransform.Dispose();
		}


        /// <summary>
        /// This is where all the action happens.  Scale and draw each dynamic element 
        /// (text annotation or quickpoll) on the slide.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
		protected virtual void HandleSubstratePaint(object sender, PaintEventArgs args) {
            if ((this.myQuickPoll != null) && (this.myQuickPoll.Enabled)) { 
                //Draw the QuickPoll graphic on the slide
                PaintQuickPoll(args.Graphics);
            }

            if ((this.myDynamicImages != null) && (this.myDynamicImages.Count > 0)) {
                PaintDynamicImages(args.Graphics);
            }
            

            if ((this.myAnnotations != null) && (myAnnotations.Count > 0)) {
                Matrix slideTransform = this.Transform; //this.Transform scales between large and small slide views
                Matrix thisTransform = myTransform.Clone(); //myTransform scales from the 500x500 canonical size
                thisTransform.Multiply(slideTransform, MatrixOrder.Append);

                lock (this.myAnnotations) {
                    foreach (TextAnnotation ta in myAnnotations.Values) {
                        float x = (float)ta.Origin.X / thisTransform.Elements[0];
                        float y = (float)ta.Origin.Y / thisTransform.Elements[3];
                        float fontSize = ta.Font.Size / thisTransform.Elements[0];
                        Font f = new Font(ta.Font.FontFamily, fontSize, ta.Font.Style);
                        if ((ta.Width > 0) && (ta.Height > 0)) {
                            //Archives corresponding to CP3.1 or later have width and height to support text wrap.
                            float textWidth = (float)ta.Width / thisTransform.Elements[0];
                            float textHeight = (float)ta.Height / thisTransform.Elements[3];
                            Rectangle bounds = new Rectangle((int)x, (int)y, (int)textWidth, (int)textHeight);
                            args.Graphics.DrawString(ta.Text, f, new SolidBrush(ta.Color), bounds);
                        }
                        else {
                            //CP3.0 archives only support origin for text annotations and do not word wrap.
                            args.Graphics.DrawString(ta.Text, f, new SolidBrush(ta.Color),x,y);                           
                        }
                    }
                }
            }

        }

        private void PaintDynamicImages(Graphics graphics) {
            Matrix slideTransform = this.Transform; //this.Transform scales between large and small slide views
            Matrix thisTransform = myTransform.Clone(); //myTransform scales from the 500x500 canonical size
            thisTransform.Multiply(slideTransform, MatrixOrder.Append);

            lock (this.myDynamicImages) {
                foreach (DynamicImage di in this.myDynamicImages.Values) {
                    float x = (float)di.Origin.X / thisTransform.Elements[0];
                    float y = (float)di.Origin.Y / thisTransform.Elements[3];
                    float w = (float)di.Width / thisTransform.Elements[0];
                    float h = (float)di.Height / thisTransform.Elements[3];
                    Rectangle bounds = new Rectangle((int)x, (int)y, (int)w, (int)h);
                    graphics.DrawImage(di.Img, bounds);
                }
            }
        }


        public void PaintQuickPoll(Graphics g) {
            Matrix slideTransform = this.Transform; //this.Transform scales between large and small slide views
            Matrix thisTransform = myTransform.Clone(); //myTransform scales from the 500x500 canonical size
            thisTransform.Multiply(slideTransform, MatrixOrder.Append);

            int startX, startY, endX, endY, width, height;
            RectangleF finalLocation;
            Font writingFont = new Font(FontFamily.GenericSansSerif, 10.0f / thisTransform.Elements[0]);
            StringFormat format = new StringFormat(StringFormat.GenericDefault);
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;

            startX = (int)((500 * 0.5f) / thisTransform.Elements[0]);
            endX = (int)((500 * 0.95f) / thisTransform.Elements[0]);
            startY = (int)((500 * 0.3f) / thisTransform.Elements[3]);
            endY = (int)((500 * 0.85f) / thisTransform.Elements[3]);
            width = endX - startX;
            height = endY - startY;
            finalLocation = new RectangleF(startX, startY, width, height);

            List<string> names = myQuickPoll.GetNames();
            Dictionary<string, int> table = myQuickPoll.GetTable();

            // Draw the outline
            g.FillRectangle(Brushes.White, startX - 1, startY - 1, width, height);
            g.DrawRectangle(Pens.Black, startX - 1, startY - 1, width, height);

            // Count the total number of results
            int totalVotes = 0;
            foreach (int i in table.Values) {
                totalVotes += i;
            }

            // Draw the choices
            float columnWidth = width / names.Count;
            int columnStartY = (int)((height * 0.9f) + startY);
            int columnTotalHeight = columnStartY - startY;
            for (int i = 0; i < names.Count; i++) {
                // Draw the column
                int columnHeight = 0;
                if (totalVotes != 0) {
                    columnHeight = (int)Math.Round((float)columnTotalHeight * ((int)table[names[i]] / (float)totalVotes));
                }
                if (columnHeight == 0) {
                    columnHeight = 1;
                }
                g.FillRectangle(QuickPoll.ColumnBrushes[i], (int)(i * columnWidth) + startX, columnStartY - columnHeight, (int)columnWidth, columnHeight);

                // Draw the label
                g.DrawString(names[i],
                              writingFont,
                              Brushes.Black,
                              new RectangleF((i * columnWidth) + startX, columnStartY, columnWidth, endY - columnStartY),
                              format);

                // Draw the number
                string percentage = String.Format("{0:0%}", (totalVotes == 0) ? 0 : (float)(((int)table[names[i]] / (float)totalVotes)));
                int numberHeight = (endY - columnStartY) * 2;
                RectangleF numberRectangle = new RectangleF((i * columnWidth) + startX,
                                                             (numberHeight > columnHeight) ? (columnStartY - columnHeight - numberHeight) : (columnStartY - columnHeight),
                                                             columnWidth,
                                                             numberHeight);
                string numberString = percentage + System.Environment.NewLine + "(" + table[names[i]].ToString() + ")";
                g.DrawString(numberString, writingFont, Brushes.Black, numberRectangle, format);
            }
        }


        /// <summary>
        /// Update the matrix to scale from the 500x500 canonical size to the size of the current client area of the control.
        /// </summary>
        private void SetTransform() { 
            if (this.Substrate == null ||
                this.Substrate.ClientSize.Height == 0 ||
                this.Substrate.ClientSize.Width == 0)
                return;

            myTransform.Reset();
            myTransform.Scale((float)CANONICAL_SIZE.Width / (float)this.Substrate.ClientSize.Width, 
                (float)CANONICAL_SIZE.Height / (float)this.Substrate.ClientSize.Height);
        
        }

        /// <summary>
        /// This happens when the user changes the window size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void HandleSizeChanged(object sender, EventArgs args) {
            SetTransform();
            this.Substrate.Invalidate();
        }

        /// <summary>
        /// Detach and attach occur in response to various events such as slide transitions.
        /// </summary>
        protected override void AttachData() {
            mySlideOverlay = null;
            base.AttachData();

            if (this.ViewData.Overlay != null) {
                mySlideOverlay = this.ViewData.Overlay;
                mySlideOverlay.DynamicElementChanged += new EventHandler(OnDynamicElementChanged);

                // Note in practice every overlay should have a TextAnnotations and a QuickPoll event
                // if they are empty and disabled.

                if (this.ViewData.Overlay.TextAnnotations != null) {
                    this.myAnnotations = this.ViewData.Overlay.TextAnnotations;
                }

                if (this.ViewData.Overlay.DynamicImages != null) {
                    this.myDynamicImages = this.ViewData.Overlay.DynamicImages;
                }

                if (this.ViewData.Overlay.QuickPoll != null) {
                    this.myQuickPoll = this.ViewData.Overlay.QuickPoll;
                }

            }

            if ((this.myAnnotations != null) ||
                (this.myQuickPoll != null)) {
                this.Invalidate();
            }
        }

        protected override void DetachData() {
            if (myAnnotations != null) myAnnotations = null;
            if (myQuickPoll != null) myQuickPoll = null;
            if (myDynamicImages != null) myDynamicImages = null;

            if (mySlideOverlay != null) {
                mySlideOverlay.DynamicElementChanged -= new EventHandler(OnDynamicElementChanged);
                mySlideOverlay = null;
            }

            base.DetachData();
            this.Invalidate();
        }

        /// <summary>
        /// This does nothing here. It is used for slide image painting.
        /// </summary>
        /// <param name="g"></param>
        protected override void InternalPaint(Graphics g) {
            base.InternalPaint(g);
        }

        /// <summary>
        /// Respond to the SlideOverlay event indicating that a text annotation or a QuickPoll 
        /// or some other dynamic element on this slide was added or updated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected void OnDynamicElementChanged(object sender, EventArgs args) {
            this.Substrate.Invalidate();
        }
    }

    /// <summary>
    /// Layer for the main static slide image
    /// </summary>
	public class ImageLayer : SlideViewLayer {
		public ImageLayer() : base() { }
		public ImageLayer(SlideView view) : base(view) { }

		private Image myImage;
		protected override void DetachData() {
			if (myImage != null)
				myImage = null;
			base.DetachData();
			this.Invalidate();
		}

		protected override void AttachData() {
			if (this.ViewData.Slide != null && this.ViewData.Slide.Image != null)
				this.myImage = this.ViewData.Slide.Image;
			base.AttachData();
			if (this.myImage != null)
				this.Invalidate();
		}

		protected override void InternalPaint(Graphics g) {
			base.InternalPaint(g);

            try {
                if (this.myImage != null) {
                    float w = 1.0f;
                    float h = 1.0f;

                    /// If the image is wide screen format, push it up to the top of the
                    /// display area.  We don't mess with the dimensions of the underlying control
                    /// because whiteboards are always 4x3.  
                    if (((float)this.myImage.Width / (float)this.myImage.Height) > 1.33334f) {
                        h = 1.333333f * (float)(this.myImage.Height) / (float)(this.myImage.Width);
                    }

                    g.DrawImage(this.myImage, 0.0f, 0.0f, w, h);
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex.ToString());
            }
		}
	}
  
}
