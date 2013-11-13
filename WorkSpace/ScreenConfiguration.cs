using System;
using System.Drawing;

namespace WorkSpace
{
	/// <summary>
	/// Dimensions of work space and slide.  
	/// Aspect Ratio - width/height ratio of the work space.  On resize, the largest work space fitting 
	/// the viewer form is created
	/// Slide size - the maximum slide size is the largest 4:3 rectangle fitting inside the workspace.  The 
	/// slide size is the fraction of the largest slide size
	/// Slide centering (float, float) - the percentage of the white space above and right of the slide
	/// </summary>
	[Serializable]
	public class ScreenConfiguration
	{

		public const int BorderLeft = 8;
		public const int BorderRight = 8;
		public const int BorderTop = 8;	
		public const int BorderBottom = 8;

		public const int MinDisplayWidth = 100;

		private double aspectRatio;
		public double AspectRatio{
			get { return aspectRatio; }
			set { aspectRatio = value; }
		}

		private double slideSize;
		public double SlideSize {
			get { return slideSize; }
			set { slideSize = value; }
		}
	
		private double slideHorizontal;
		public double SlideHorizontal {
			get { return slideHorizontal; }
			set { slideHorizontal = value; }
		}

		private double slideVertical;
		public double SlideVertical { 
			get { return slideVertical; }
			set { slideVertical = value; }
		}

		public ScreenConfiguration()
		{
			AspectRatio = 1.33333;
			SlideSize = 1.0;
			SlideHorizontal = 0.5;
			SlideVertical = 0.5;
			
		}

		public ScreenConfiguration(ScreenConfiguration other){
			this.aspectRatio = other.aspectRatio;
			this.slideSize = other.slideSize;
			this.slideHorizontal = other.slideHorizontal;
			this.slideVertical = other.slideVertical;
		}

		// Compute the size available for the work space - this is the largest rectangle with the given
		// aspect ratio that will fit inside the boders.  An optional arguement takes into account the width
		// of the scroll bar
		public Size WorkSpaceSize(Size viewerSize){
			return WorkSpaceSize(viewerSize, 0);
		}

		public Size WorkSpaceSize(Size viewerSize, int scrollBarWidth){
			int maxDisplayHeight = viewerSize.Height - BorderTop - BorderBottom;
			int maxDisplayWidth = viewerSize.Width - BorderLeft - BorderRight - scrollBarWidth;
	
									// If it's too small, just return the minimum size and see what happens
			if (maxDisplayWidth < MinDisplayWidth || AspectRatio * maxDisplayHeight < MinDisplayWidth)
				return new Size(MinDisplayWidth, (int) (MinDisplayWidth / AspectRatio));

			if (maxDisplayWidth > AspectRatio * maxDisplayHeight){			// Height constrained
				return new Size((int)(AspectRatio * maxDisplayHeight), maxDisplayHeight);
			}
			else {															// Width constrained
				return new Size(maxDisplayWidth, (int)(maxDisplayWidth / AspectRatio));
			}
		}

		public Rectangle WorkSpaceRectangle(Size viewerSize, int scrollBarWidth){
			Size wsSize = WorkSpaceSize(viewerSize, scrollBarWidth);
											// Center in the viewer area
			int maxDisplayHeight = viewerSize.Height - BorderTop - BorderBottom;
			int maxDisplayWidth = viewerSize.Width - BorderLeft - BorderRight;
			int top = BorderTop + (maxDisplayHeight - wsSize.Height) / 2;
			int left = BorderLeft + (maxDisplayWidth - (wsSize.Width + scrollBarWidth)) / 2;

			return new Rectangle(new Point(left, top), wsSize);

		}

		public Rectangle SlideRectangle(Size workSpaceSize){
			double maxWidth;
			double maxHeight;
			if (workSpaceSize.Width > 1.333333 * workSpaceSize.Height){
				maxWidth = 1.333333 * workSpaceSize.Height;
				maxHeight = workSpaceSize.Height;
			}
			else {
				maxWidth = workSpaceSize.Width;
				maxHeight = workSpaceSize.Width / 1.333333;
			}

			double width = maxWidth * SlideSize;
			double height = maxHeight * SlideSize;

			double x = (workSpaceSize.Width - width) * SlideHorizontal;
			double y = (workSpaceSize.Height - height) * SlideVertical;

			return new Rectangle((int) x, (int) y, (int) width, (int) height);
		}

	}
}
