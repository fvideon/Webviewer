using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WorkSpace
{
	/// <summary>
	/// Customized wrapper around SlideView which locks the ViewerForm object during
	/// paint/layout to avoid synchronization problems. 
	/// </summary>
	public class LockedSlideView : WorkSpace.SlideView {
		private object myLockObject;
		public object LockObject { set { myLockObject = value; } }

		protected override void OnPaint(PaintEventArgs args) {
			lock (this.myLockObject) {
				base.OnPaint(args);
			}
		}

 		protected override void OnLayout(LayoutEventArgs args) {			
			lock (this.myLockObject) {
				base.OnLayout(args);
			}
		}

		protected override void HandleLayerPaint(object sender, PaintEventArgs args) {
			lock (this.myLockObject) {
				base.HandleLayerPaint(sender, args);
			}
		}

		protected override void HandleScroll(object sender, ScrollEventArgs args) {
			lock (this.myLockObject) {
				base.HandleScroll(sender, args);
			}
		}

		protected override void HandleScrollBarScrollValueChanged(object sender, EventArgs args) {
			lock (this.myLockObject) {
				base.HandleScrollBarScrollValueChanged(sender, args);
			}
		}

		protected override void HandleLayerPanelBackColorChanged(object sender, EventArgs args) {
			lock (this.myLockObject) {
				base.HandleLayerPanelBackColorChanged(sender, args);
			}
		}

		private System.ComponentModel.IContainer components = null;

		public LockedSlideView() {
			// Until set by the outside world, lock on this.
			this.myLockObject = this;

			// This call is required by the Windows Form Designer.
			InitializeComponent();
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing ) {
			if( disposing ) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent() {
			components = new System.ComponentModel.Container();
		}
		#endregion
	}
}

