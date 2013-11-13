using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;

namespace WorkSpace
{
	/// <summary>
	/// A view of a slide: shows all the layers of content on the slide
	/// as well as allowing scrolling and supporting commenting, scribbling,
	/// and highlighting.
	/// </summary>
	public class SlideView : System.Windows.Forms.UserControl
	{
		private bool myFitToSlide = false;
		
		[Category("Layout")]
		public bool FitToSlide {
			get { return this.myFitToSlide; }
			set {
				if (this.myFitToSlide != value) {
					this.myFitToSlide = value;
					this.OnResize(EventArgs.Empty);
				}
			}
		}

		private float myAspectRatio = 4f / 3f;
		
		/// <summary>
		/// The aspect ratio of the layer panel on this view.
		/// </summary>
		public float AspectRatio {
			get { return myAspectRatio; }
			set { 
				if (value <= 0)
					throw new ArgumentOutOfRangeException("value", value, "must be > 0");
				if (value != myAspectRatio) {
					myAspectRatio = value; 
					this.PerformLayout();
				}
			}
		}

		public SlideView() {
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			this.LayerPanel.Paint += new PaintEventHandler(this.HandleLayerPaint);
			this.LayerPanel.BackColorChanged += new EventHandler(this.HandleLayerPanelBackColorChanged);
			this.LayerPanel.Click += new EventHandler(this.HandleLayerPanelClick);
		}

		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
		private System.Windows.Forms.Panel myPanel;

		#region Data

		/// <summary>
		/// The slide data associated with this view. Changes to this fire a DataChanged event.
		/// </summary>
		private SlideViewData myData;

		private SlideViewer.SlideOverlay myOverlay;
		/// <summary>
		/// Data is guaranteed to be non-null and to be the data which is being
		/// detached. However, its fields may have changed since it was attached.
		/// </summary>
		private void DetachData() {
			this.Data.Changed -= new EventHandler(this.HandleDataChanged);
			if (myOverlay != null) {
				myOverlay.MylarSizeChanged -= new EventHandler(this.HandleMylarSizeChanged);
				myOverlay.Scrolled -= new EventHandler(this.HandleMylarScrollValueChanged);
				myOverlay = null;
			}
		}

		/// <summary>
		/// Data is guaranteed to be non-null and to be the data which is being
		/// attached.
		/// </summary>
		private void AttachData() {
			this.Data.Changed += new EventHandler(this.HandleDataChanged);
			if (this.Data.Overlay != null) {
				myOverlay = this.Data.Overlay;
				myOverlay.MylarSizeChanged += new EventHandler(this.HandleMylarSizeChanged);
				myOverlay.Scrolled += new EventHandler(this.HandleMylarScrollValueChanged);
				this.AlignScrollBar();
			}
		}

		[Category("Data")]
		public SlideViewData Data {
			get { return myData; }
			set {
				if (myData != value) {
					if (myData != null)
						DetachData();
					myData = value;
					if (myData != null)
						AttachData();
				}
				OnDataChanged(EventArgs.Empty);
			}
		}

		public event EventHandler DataChanged;
		protected virtual void OnDataChanged(EventArgs args) {
			if (this.DataChanged != null)
				this.DataChanged(this, args);
		}

		protected virtual void HandleDataChanged(object sender, EventArgs args) {
			this.DetachData();
			this.AttachData();
			
		}

		#endregion

		#region Layers

		public event EventHandler LayerAdded;
		public event EventHandler LayerRemoved;

		private bool LayerDirty(int index) {
			Debug.Assert(this.myLayerDirtyRegions != null && index >= 0 && index < this.myLayerDirtyRegions.Count);
			return this.myLayerDirtyRegions[index] != null;
		}

		private ArrayList /* of SlideViewLayer */ myLayers = new ArrayList();

		/// <summary>
		/// A list of Regions recording which portion of any given layer is "dirty", i.e.,
		/// needs to be redrawn on the next paint. Null if the layer is "clean".
		/// 
		/// <para>These regions are in 1x1-space coordinates.</para>
		/// </summary>
		private ArrayList /* of Region */ myLayerDirtyRegions = new ArrayList();

		/// <summary>
		/// A cache of the renderings of the various layers. This should be cleared
		/// whenever a layer is added or removed. Each entry in the cache represents 
		/// a series of contiguous layers with at most one layer which 
		/// UsesNonOpaqueOverwrite and is visible (which must be the first in the series). The last
		/// entry of each series is one less than the minimum of the smallest entry 
		/// larger than the series's start from myLastChangedLayers, the smallest entry 
		/// larger than the series's start which has UsesNonOpaqueOverwrite == true and is visible, and 
		/// the length of myLayers. Layers in myLastChangedLayers are kept as series of 
		/// one.
		/// 
		/// <para>These caches are in layerPanel-space coordinates.</para>
		/// </summary>
		private ArrayList /* of Image */ myCachedLayerImages = new ArrayList();

		/// <summary>
		/// The set of layers which were the last to change.
		/// </summary>
		private ArrayList /* of int */ myLastChangedLayers = new ArrayList();

		public int LayerCount { get { return myLayers.Count; } }
		public SlideViewLayer GetLayer(int index) {
			return (SlideViewLayer)myLayers[index];
		}

		private void MarkForFullRedraw() {
			foreach (Image i in this.myCachedLayerImages)
				if (i != null)
					i.Dispose();
			this.myCachedLayerImages.Clear();
			// Mark the whole 1x1 canvas dirty.
			for (int i = 0; i < this.myLayerDirtyRegions.Count; i++) {
				System.Drawing.Region r = new System.Drawing.Region();
				r.MakeInfinite();
				this.myLayerDirtyRegions[i] = r;
			}
		}

		protected virtual void HandleInvalidatedLayer(object sender, SlideViewer.RegionEventArgs args) {
			SlideViewLayer layer = (SlideViewLayer)sender;
			int index = this.myLayers.IndexOf(layer);
			Debug.Assert(index >= 0 && index < this.myLayerDirtyRegions.Count);
			Debug.Assert(this.myLayerDirtyRegions.Count == this.myLayers.Count);
			if (this.myLayerDirtyRegions[index] == null)
				this.myLayerDirtyRegions[index] = new Region();
            try {
                ((System.Drawing.Region)this.myLayerDirtyRegions[index]).Union(args.Region);
            }
            catch (Exception ex) {
                Debug.WriteLine(ex.ToString());
            }
		}

		public bool HasLayer(SlideViewLayer layer) {
			return this.myLayers.Contains(layer);
		}

		public void AddLayer(SlideViewLayer layer) {
			if (layer == null)
				throw new ArgumentNullException("layer", "layer may not be null");

			this.myLayers.Add(layer);
			this.myLayerDirtyRegions.Add(new Region(new RectangleF(0,0,1,1)));
			// Redraw everything since we expect add events to be rare, anyway.
			this.MarkForFullRedraw();
			this.myLayerPanel.Invalidate();
			layer.Attach(this);
			layer.Invalidated += new SlideViewer.RegionEventHandler(this.HandleInvalidatedLayer);

			if (this.LayerAdded != null)
				this.LayerAdded(this, EventArgs.Empty);
		}

		/// <remarks>Disposes of layers as it removes them (unlike RemoveLayer)</remarks>
		public void ClearLayers() {
			while (this.LayerCount > 0) {
				SlideViewLayer layer = this.GetLayer(0);
				this.RemoveLayer(0);
				layer.Dispose();
			}
		}

		public void RemoveLayer(int layer) {
			if (layer < 0 || layer >= this.myLayers.Count)
				throw new ArgumentOutOfRangeException("layer", layer, "must be >= 0 and < LayerCount");

			this.RemoveLayer(this.GetLayer(layer));
		}

		public void RemoveLayer(SlideViewLayer layer) {
			Debug.Assert(layer != null);

			if (!this.myLayers.Contains(layer))
				throw new ArgumentException("this object does not contain the given layer", "layer");

			int index = this.myLayers.IndexOf(layer);
			layer.Invalidated -= new SlideViewer.RegionEventHandler(this.HandleInvalidatedLayer);
			this.myLayers.RemoveAt(index);
			if (this.myLayerDirtyRegions[index] != null)
				((System.Drawing.Region)this.myLayerDirtyRegions[index]).Dispose();
			this.myLayerDirtyRegions.RemoveAt(index);
			layer.Detach();

			// Redraw everything since we expect remove events to be rare, anyway.
			this.MarkForFullRedraw();
			this.myLayerPanel.Invalidate();
			if (this.LayerRemoved != null)
				this.LayerRemoved(this, EventArgs.Empty);
		}

		/// <summary>
		/// Determines whether the given layer begins a new layer series.
		/// A layer series ends if the next layer is either changed or
		/// uses non-opaque overwrite (which can't be stacked on top of
		/// anything directly) OR if the previous layer was changed. Therefore,
		/// this layer begins a new layer if any of those conditions hold OR
		/// if the layer is the zeroth layer.
		/// </summary>
		private bool BeginsLayerSeries(int layer) {
			Debug.Assert(layer >= 0 && layer < this.myLayers.Count);
			return layer == 0 ||
				this.myLastChangedLayers.Contains(layer) ||
				this.myLastChangedLayers.Contains(layer-1) || 
				(((SlideViewLayer)this.myLayers[layer]).UsesNonOpaqueOverwrite &&
				((SlideViewLayer)this.myLayers[layer]).VisibleLayer);
		}

		/// <summary>
		/// Recache just the last changed layers in the cache.
		/// </summary>
		private void CacheChangedLayers() {
			if (this.myLayerDirtyRegions.Count == 0)
				return;

			int cacheIndex = -1; // cacheIndex will be incremented at the 0th layer
			for (int i = 0; i < this.myLayers.Count; i++) {
				if (this.BeginsLayerSeries(i))
					cacheIndex++;

				if (this.myLastChangedLayers.Contains(i)) {
					// Repaint this layer.
					Debug.Assert(cacheIndex < this.myCachedLayerImages.Count);
					Image image = (Image)this.myCachedLayerImages[cacheIndex];
					this.LayerSeriesPaint(i, 1, image);
				}
			}
		}

		/// <summary>
		/// Establish the cache to meet the criteria described at the definition of
		/// myCachedLayerImages.
		/// </summary>
		private void CacheAllLayers() {
			if (this.myLayerDirtyRegions.Count == 0)
				return;

			foreach (Image i in this.myCachedLayerImages)
				if (i != null)
					i.Dispose();
			this.myCachedLayerImages.Clear();
			int first = 0;

			// Paint each layer series to the cache.
			for (int i = 1; i < this.myLayers.Count; i++) {
				if (this.BeginsLayerSeries(i)) {
					this.myCachedLayerImages.Add(this.LayerSeriesPaint(first, i - first));
					first = i;
				}
			}
			this.myCachedLayerImages.Add(this.LayerSeriesPaint(first, this.myLayers.Count - first));
		}

		private void CachedLayerPaint(Graphics g) {
			// Just layer the cached images onto the graphics context.
			g.Clear(this.LayerPanel.BackColor);
			foreach (Image i in this.myCachedLayerImages)
				g.DrawImage(i, this.LayerPanel.Bounds);
		}

		/// <summary>
		/// On a redraw, the following occurs: If the cache is not empty and the changed
		/// layers are the same as the layers that last changed, only the layers that
		/// changed will be redrawn, and they will be composed with the cached
		/// layers to create the new image. Otherwise, the entire stack of layers is
		/// redrawn with the minimum number of series (i.e., each series starts with
		/// a UseNonOpaqueOverwrite layer (except the first)) except that each layer
		/// that changed this time will get its own series.
		/// </summary>
		protected virtual void HandleLayerPaint(object sender, PaintEventArgs args) {
            //This is where slide image painting happens.  Ink painting happens in HandleSubstratePaint.
			if (this.LayerPanel.Width == 0 || this.LayerPanel.Height == 0)
				return;

			if (this.myLayers.Count == 0)
				args.Graphics.Clear(this.LayerPanel.BackColor);
			else {
				ArrayList newChangedLayers = new ArrayList();
				bool match = true;
				for (int i = 0; i < this.myLayerDirtyRegions.Count; i++) {
					// Build up the new changed regions.
					if (this.LayerDirty(i))
						newChangedLayers.Add(i);
					// Check for a match between the new and old changed regions.
					if (newChangedLayers.Contains(i) != this.myLastChangedLayers.Contains(i))
						match = false;
				}

				this.myLastChangedLayers = newChangedLayers;

				// Freshen the cache where necessary.
				if (!match || this.myCachedLayerImages.Count == 0) {
					// Complete freshen.
					this.MarkForFullRedraw();
					this.CacheAllLayers();
				}
				else
					this.CacheChangedLayers();

				this.CachedLayerPaint(args.Graphics);
			}
		}

		/// <remarks>caller must dispose of returned Image</remarks>
		private Image LayerSeriesPaint(int start, int length) {
			Image image = new Bitmap(this.LayerPanel.Width, this.LayerPanel.Height);
			LayerSeriesPaint(start, length, image);
			return image;
		}

		/// <summary>
		/// Paints to one image the series of layers between start and length
		/// and returns that image. Of all these layers, only start should have
		/// the UsesNonOpaqueOverwrite property true (and it need not have that
		/// property set to true).
		/// </summary>
		private void LayerSeriesPaint(int start, int length, Image image) {
			Debug.Assert(start >= 0 && start + length <= this.myLayers.Count && length > 0);

			// Establish the region that needs to be painted for all layers.
			// The region to be painted is union of all regions' dirty areas.
			System.Drawing.Region region = new System.Drawing.Region();
			for (int i = start; i < start + length; i++) {
				Debug.Assert(i == start || 
					!((SlideViewLayer)this.myLayers[i]).UsesNonOpaqueOverwrite ||
					!((SlideViewLayer)this.myLayers[i]).VisibleLayer);
				if (this.myLayerDirtyRegions[i] != null)
					region.Union((System.Drawing.Region)this.myLayerDirtyRegions[i]);
			}

			// Set up the graphics context.
			Graphics g = Graphics.FromImage(image);
			g.Clip = region;

			// Clear the canvas for painting.
			g.Clear(Color.Transparent);

			// Paint the layers
			for (int i = start; i < start + length; i++) {
				if (((SlideViewLayer)this.myLayers[i]).VisibleLayer)
					((SlideViewLayer)this.myLayers[i]).Paint(g);
				if (this.myLayerDirtyRegions[i] != null)
					((System.Drawing.Region)this.myLayerDirtyRegions[i]).Dispose();
				this.myLayerDirtyRegions[i] = null;
			}
				
			g.Dispose();
			region.Dispose();
		}

		#endregion

		#region Scrolling

		private System.Windows.Forms.VScrollBar myScrollBar;

		/// <summary>
		/// DO NOT change this directly. Go through the property.
		/// </summary>
		private float myTrueScrollBarPos = 0; 

		/// <summary>
		/// The location of the scroll bar if it could be a float (i.e.,
		/// in mylar coordinates and exactly reflecting the mylar position).
		/// This automatically updates the scrollBar.
		/// </summary>
		private float TrueScrollBarPos {
			get { return myTrueScrollBarPos; }
			set {
				if (this.myTrueScrollBarPos != value) {
					myTrueScrollBarPos = value;
					int mylarPos = this.MylarPosToScrollBarPos(this.myTrueScrollBarPos);
					if (this.myScrollBar.Value != mylarPos)
						this.myScrollBar.Value = mylarPos;
				}
			}
		}

		/// <summary>
		/// Set the true scrollBar pos based on the scrollBar.
		/// </summary>
		private void UpdateTrueScrollBarPos() {
			if (this.MylarPosToScrollBarPos(this.TrueScrollBarPos) != this.myScrollBar.Value)
				this.TrueScrollBarPos = this.ScrollBarPosToMylarPos(this.myScrollBar.Value);
		}

		// apparently this cannot be changed while the inkcollector is enabled (?)
		public bool Scrollable
		{
			get { return myScrollBar.Visible; }
			set { myScrollBar.Visible = value; }
		}


		private const float SCROLL_MYLAR_SCALE_FACTOR = 30f;
		/// <remarks>of course, SBPToMP(MPToSBP(x)) is not necessarily equal to x.</remarks>
		/// <remarks>MPToSBP(-x) == -MPToSBP(x)</remarks>
		private int MylarPosToScrollBarPos(float mylarPos) {
			return (int)(Math.Abs(mylarPos) * SCROLL_MYLAR_SCALE_FACTOR) * Math.Sign(mylarPos);
		}

		/// <remarks>of course, SBPToMP(MPToSBP(x)) is not necessarily equal to x.</remarks>
		private float ScrollBarPosToMylarPos(int scrollPos) {
			return scrollPos / SCROLL_MYLAR_SCALE_FACTOR;
		}

		///<remarks>From the .NET documentation: 
		///Note: The value of a scroll bar cannot reach its maximum value through 
		///user interaction at run time. The maximum value that can be reached is 
		///equal to the Maximum property value minus the LargeChange property value 
		///plus one. The maximum value can only be reached programmatically.
		///</remarks>
		protected virtual void HandleScroll(object sender, ScrollEventArgs args) {
			if (args.Type == ScrollEventType.EndScroll &&
				(args.NewValue == (this.myScrollBar.Maximum - this.myScrollBar.LargeChange + 1) ||
				args.NewValue == this.myScrollBar.Minimum) &&
				this.myOverlay != null) {
				this.myOverlay.MaxMylarScrollPosition +=
					this.ScrollBarPosToMylarPos(this.myScrollBar.LargeChange * 4);
			}		
		}

		protected virtual void HandleScrollBarScrollValueChanged(object sender, EventArgs args) {
			this.UpdateTrueScrollBarPos();
			if (this.myOverlay != null &&
				this.TrueScrollBarPos != this.myOverlay.MylarScrollPosition)
				this.myOverlay.MylarScrollPosition = this.TrueScrollBarPos;
		}

		protected virtual void HandleMylarScrollValueChanged(object sender, EventArgs args) {
			if (this.myOverlay != null &&
				this.TrueScrollBarPos != this.myOverlay.MylarScrollPosition) {
				this.TrueScrollBarPos = this.myOverlay.MylarScrollPosition;
			}
		}

		protected virtual void HandleMylarSizeChanged(object sender, EventArgs args) {
			this.myScrollBar.Maximum = this.MylarPosToScrollBarPos(this.myOverlay.MaxMylarScrollPosition);
			this.myScrollBar.Minimum = this.MylarPosToScrollBarPos(-this.myOverlay.MaxMylarScrollPosition);
		}

		/// <summary>
		/// Sets all scrollBar parameters to match the data parameters.
		/// <para>Precondition: myOverlay != null</para>
		/// </summary>
		private void AlignScrollBar() {
			float maximum = this.myOverlay.MaxMylarScrollPosition;
			float position = this.myOverlay.MylarScrollPosition;
			this.myScrollBar.Maximum = this.MylarPosToScrollBarPos(maximum);
			this.myScrollBar.Minimum = this.MylarPosToScrollBarPos(-maximum);
			this.myScrollBar.Value = this.MylarPosToScrollBarPos(position);
		}

		#endregion

		private WorkSpace.SlideView.DoubleBufferPanel myLayerPanel;

		#region LayerPanel

		public Panel LayerPanel { get { return myLayerPanel; } }
		
		/// <summary>
		/// A cache of the last dimensions of the slide itself within this view.
		/// </summary>
		private Size myAspect = new Size(0,0);

		private class DoubleBufferPanel : Panel {
			public DoubleBufferPanel() : base() {
				this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
				this.SetStyle(ControlStyles.UserPaint, true);
				this.SetStyle(ControlStyles.DoubleBuffer, true);
				this.SetStyle(ControlStyles.ResizeRedraw, true);
			}
		}

		protected virtual void HandleLayerPanelClick(object sender, EventArgs args) {
			this.OnClick(args);
		}

		protected virtual void HandleLayerPanelBackColorChanged(object sender, EventArgs args) {
			this.MarkForFullRedraw();
			this.LayerPanel.Invalidate();
		}

		/// <returns>A size object which represents the dimensions of the area
		/// not usable for displaying the slide in this view.</returns>
		protected virtual Size GetUnusableSize() {
			return this.myPanel.Size - this.LayerPanel.ClientSize;
		}

		// Need to force a layout - I'm guessing on hte event args
		public void ForceLayout(){
			OnLayout(new LayoutEventArgs(this, "bounds"));
		}

		/// <summary>
		/// Resizes the internal panel on each layout so that it 
		/// has the slide aspect ratio (SlideView.ASPECT_RATIO,
		/// currently).
		/// </summary>
 		protected override void OnLayout(LayoutEventArgs args) {
			base.OnLayout(args);

			// Calculate the "fudge factor" for vertical and horizontal axes:
			// the amount of space not available for the canvas.
			System.Drawing.Size fudge = this.GetUnusableSize();
			myAspect = this.ClientSize - fudge;

			if (myAspect.Width / (float)myAspect.Height > this.AspectRatio)
				myAspect.Width = (int)(myAspect.Height * this.AspectRatio);
			else
				myAspect.Height = (int)(myAspect.Width / this.AspectRatio);

			this.myPanel.Size = myAspect + fudge;

			if (this.FitToSlide) {
				this.SuspendLayout();
				this.ClientSize = this.myPanel.Size;
				this.ResumeLayout();
			}
			this.MarkForFullRedraw();
		}


		#endregion

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

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.myPanel = new System.Windows.Forms.Panel();
			this.myLayerPanel = new WorkSpace.SlideView.DoubleBufferPanel();
			this.myScrollBar = new System.Windows.Forms.VScrollBar();
			this.myPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// myPanel
			// 
			this.myPanel.BackColor = System.Drawing.Color.Transparent;
			this.myPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
			this.myPanel.Controls.AddRange(new System.Windows.Forms.Control[] {
																				  this.myLayerPanel,
																				  this.myScrollBar});
			this.myPanel.Name = "myPanel";
			this.myPanel.Size = new System.Drawing.Size(150, 104);
			this.myPanel.TabIndex = 0;
			// 
			// myLayerPanel
			// 
			this.myLayerPanel.AccessibleDescription = "myLayerPanel";
			this.myLayerPanel.AccessibleName = "myLayerPanel";
			this.myLayerPanel.BackColor = System.Drawing.Color.FromArgb(((System.Byte)(255)), ((System.Byte)(192)), ((System.Byte)(192)));
			this.myLayerPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.myLayerPanel.Name = "myLayerPanel";
			this.myLayerPanel.Size = new System.Drawing.Size(114, 100);
			this.myLayerPanel.TabIndex = 1;
			// 
			// myScrollBar
			// 
			this.myScrollBar.AccessibleDescription = "myScrollBar";
			this.myScrollBar.AccessibleName = "myScrollBar";
			this.myScrollBar.Dock = System.Windows.Forms.DockStyle.Right;
			this.myScrollBar.Location = new System.Drawing.Point(114, 0);
			this.myScrollBar.Name = "myScrollBar";
			this.myScrollBar.Size = new System.Drawing.Size(32, 100);
			this.myScrollBar.TabIndex = 0;
			this.myScrollBar.ValueChanged += new System.EventHandler(this.HandleScrollBarScrollValueChanged);
			this.myScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(this.HandleScroll);
			// 
			// SlideView
			// 
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.myPanel});
			this.Name = "SlideView";
			this.myPanel.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		#endregion
	}
}
