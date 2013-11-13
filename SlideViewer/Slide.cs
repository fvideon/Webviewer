using System;
using System.Drawing;

namespace SlideViewer
{
	/// <summary>
	/// Represent a single slide.
	/// </summary>
	[Serializable]	
	public class Slide
	{
		// Modes for comment menu and display
		private int commentMenuType;
		public int CommentMenuType {
			get { return commentMenuType; }
			set { commentMenuType = value; }
		}

		private string title;
		public string Title {
			get { return title; }
		}

		private Bitmap image;
		public Bitmap Image {
			get { return image; }
		}

		public Slide(){
			this.image = null;
			this.title = "";
		}

		public Slide(Bitmap image) : this(image, "") {
		}

		public Slide(Bitmap image, string title)
		{
			this.image = image;
			this.title = title;
		}

        /// <summary>
        /// clean up the image before deleting a slide.
        /// </summary>
        public void Dispose() {
            if (image != null) {
                image.Dispose();
                image = null;
            }
        }
	}
}
