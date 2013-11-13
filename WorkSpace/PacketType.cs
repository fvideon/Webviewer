using System;

namespace WorkSpace
{
	// Indices used to identify packets
	public class PacketType {
		public const byte SlideIndex = 1;
		public const byte Slide = 2;
		public const byte Scribble = 3;
		public const byte RequestAllSlides = 4;
		public const byte NoOp = 5;
		public const byte Comment = 6;
		public const byte Highlight = 7;
		public const byte Pointer = 8;
		public const byte Scroll = 9;
		public const byte ClearAnnotations = 10;
		public const byte ResetSlides = 11;
		public const byte RequestSlide = 12;
		public const byte ScreenConfiguration = 13;
		public const byte ClearSlide = 14;
		public const byte Beacon = 15;
		public const byte ScribbleDelete = 16;
		public const byte TransferToken = 17;
		public const byte ID = 18;
		public const byte ClearScribble = 19;
		public const byte RequestMissingSlides = 20;
		public const byte DummySlide = 21;				// Empty slides for debugging

		public const byte RTUpdate = 23;
        public const byte RTText = 24;          //Text annotation
        public const byte RTDeleteText = 25;    //Delete text (or image) annotation.
        public const byte RTQuickPoll = 26;    //QuickPoll Message.
        public const byte RTImageAnnotation = 27;    //Dynamically added image.

	}
}
