using System;
using SlideViewer;

namespace WorkSpace
{
	/// <summary>
	/// The data associated with a slide view.
	/// </summary>
	public class SlideViewData
	{
		private Slide mySlide;
		public Slide Slide {
			get { return mySlide; }
		}

		private SlideOverlay myOverlay;
		public SlideOverlay Overlay {
			get { return myOverlay; }
		}

		public void ChangeSlide(Slide newSlide, SlideOverlay newOverlay) {
			if (newSlide != Slide || newOverlay != Overlay) {
				this.mySlide = newSlide;
				this.myOverlay = newOverlay;
				OnChange();
			}
		}

		public SlideViewData()
		{
			mySlide = null;
			myOverlay = null;
		}

		public event EventHandler Changed;

		private void OnChange() {
			this.OnChange(this, EventArgs.Empty);
		}

		private void OnChange(object sender, EventArgs args) {
			if (this.Changed != null)
				this.Changed(sender, args);
		}
	}
}
