using System;
using System.Collections;

namespace SlideViewer {
	public class NumericEventArgs : EventArgs {
		int myNumber;
		public int Number { get { return myNumber; } }

		public NumericEventArgs(int val) : base() {
			this.myNumber = val;
		}
	}

	public delegate void NumericEventHandler(object sender, NumericEventArgs args);

	public delegate void RegionEventHandler(object sender, RegionEventArgs args);
	public class RegionEventArgs : EventArgs {
		private System.Drawing.Region myRegion;
		public System.Drawing.Region Region { get { return myRegion; } }
		public RegionEventArgs(System.Drawing.Region region) : base() { this.myRegion = region; }
	}
}