using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.Drawing;

namespace ArchiveRTNav {
    /// A CP3 annotation refers to something dynamically added to or removed from a slide that is not ink
    /// and not a QuickPoll.
    /// We currently have Text and Images in CP 3.1.

    [Serializable]
    public class RTTextAnnotation : ISerializable {
        private Int32 version = 2;

        private Point origin;
        public Point Origin {
            get { return origin; }
            set { origin = value; }
        }

        private Color color;
        public Color Color {
            get { return color; }
            set { color = value; }
        }

        private Font font;
        public Font Font {
            get { return font; }
            set { font = value; }
        }

        private String text;
        public String Text {
            get { return text; }
            set { text = value; }
        }

        private Guid guid;
        public Guid Guid {
            get { return guid; }
            set { guid = value; }
        }

        private Guid deckGuid;
        public Guid DeckGuid {
            get { return this.deckGuid; }
            set { deckGuid = value; }
        }

        private Int32 slideIndex;
        public Int32 SlideIndex {
            get { return this.slideIndex; }
            set { slideIndex = value; }
        }

        /// <summary>
        /// Added for version 2.  If the value is -1 the object is from an earlier version and width should be ignored.
        /// </summary>
        private Int32 width;
        public Int32 Width {
            get { return this.width; }
            set { this.width = value; }
        }

        /// <summary>
        /// Added for version 2.  If the value is -1 the object is from an earlier version and width should be ignored.
        /// </summary>
        private Int32 height;
        public Int32 Height {
            get { return this.height; }
            set { this.height = value; }
        }

        public RTTextAnnotation(Point origin, Font font, Color color, String text, Guid guid,
                                Guid deckGuid, int slideIndex, int width, int height) {
            this.origin = origin;
            this.font = font;
            this.color = color;
            this.text = text;
            this.guid = guid;
            this.deckGuid = deckGuid;
            this.slideIndex = slideIndex;
            this.width = width;
            this.height = height;
        }

        protected RTTextAnnotation(SerializationInfo info, StreamingContext context) {
            this.origin = (Point)info.GetValue("origin", typeof(Point));
            this.font = (Font)info.GetValue("font", typeof(Font));
            this.color = (Color)info.GetValue("color", typeof(Color));
            this.text = info.GetString("text");
            this.guid = new Guid(info.GetString("guid"));
            this.deckGuid = new Guid(info.GetString("deckGuid"));
            this.slideIndex = info.GetInt32("slideIndex");
            this.version = info.GetInt32("version");
            if (version >= 2) {
                this.width = info.GetInt32("width");
                this.height = info.GetInt32("height");
            }
            else {
                width = -1;
                height = -1;
            }
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("version", version);
            info.AddValue("deckGuid", deckGuid.ToString());
            info.AddValue("slideIndex", slideIndex);
            info.AddValue("guid", this.guid.ToString());
            info.AddValue("origin", this.origin, this.origin.GetType());
            info.AddValue("font", this.font, this.font.GetType());
            info.AddValue("color", this.color, this.color.GetType());
            info.AddValue("text", this.text);
            info.AddValue("width", this.width);
            info.AddValue("height", this.height);
        }

    }


    [Serializable]
    public class RTImageAnnotation : ISerializable {
        private Int32 version = 1;

        private Point origin;
        public Point Origin {
            get { return origin; }
            set { origin = value; }
        }

        private Guid guid;
        public Guid Guid {
            get { return guid; }
            set { guid = value; }
        }

        private Guid deckGuid;
        public Guid DeckGuid {
            get { return this.deckGuid; }
            set { deckGuid = value; }
        }

        private Int32 slideIndex;
        public Int32 SlideIndex {
            get { return this.slideIndex; }
            set { slideIndex = value; }
        }

        /// <summary>
        /// Added for version 2.  If the value is -1 the object is from an earlier version and width should be ignored.
        /// </summary>
        private Int32 width;
        public Int32 Width {
            get { return this.width; }
            set { this.width = value; }
        }

        /// <summary>
        /// Added for version 2.  If the value is -1 the object is from an earlier version and width should be ignored.
        /// </summary>
        private Int32 height;
        public Int32 Height {
            get { return this.height; }
            set { this.height = value; }
        }

        private Image image;
        public Image Img {
            get { return this.image; }
            set { this.image = value; }
        }


        public RTImageAnnotation(Point origin, Guid guid,
                                Guid deckGuid, int slideIndex, int width, int height, Image image) {
            this.origin = origin;
            this.guid = guid;
            this.deckGuid = deckGuid;
            this.slideIndex = slideIndex;
            this.width = width;
            this.height = height;
            this.image = image;
        }

        protected RTImageAnnotation(SerializationInfo info, StreamingContext context) {
            this.origin = (Point)info.GetValue("origin", typeof(Point));
            this.guid = new Guid(info.GetString("guid"));
            this.deckGuid = new Guid(info.GetString("deckGuid"));
            this.slideIndex = info.GetInt32("slideIndex");
            this.width = info.GetInt32("width");
            this.height = info.GetInt32("height");
            this.image = (Image)info.GetValue("image", typeof(Image));
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("version", version);
            info.AddValue("deckGuid", deckGuid.ToString());
            info.AddValue("slideIndex", slideIndex);
            info.AddValue("guid", this.guid.ToString());
            info.AddValue("origin", this.origin, this.origin.GetType());
            info.AddValue("width", this.width);
            info.AddValue("height", this.height);
            info.AddValue("image", this.image, this.image.GetType());
        }

    }


    /// <summary>
    /// Remove a single annotation sheet.  Currently this could be a text annotation or an image that was
    /// added dynamically.   Some move operations may also be implemented with delete.
    /// </summary>
    [Serializable]
    public class RTDeleteAnnotation : ISerializable {
        private Int32 version = 1;

        private Guid guid;
        public Guid Guid {
            get { return guid; }
            set { guid = value; }
        }

        private Guid deckGuid;
        public Guid DeckGuid {
            get { return this.deckGuid; }
            set { deckGuid = value; }
        }

        private Int32 slideIndex;
        public Int32 SlideIndex {
            get { return this.slideIndex; }
            set { slideIndex = value; }
        }

        public RTDeleteAnnotation(Guid guid, Guid deckGuid, Int32 slideIndex) {
            this.guid = guid;
            this.deckGuid = deckGuid;
            this.slideIndex = slideIndex;
        }

        protected RTDeleteAnnotation(SerializationInfo info, StreamingContext context) {
            this.guid = new Guid(info.GetString("guid"));
            this.deckGuid = new Guid(info.GetString("deckGuid"));
            this.slideIndex = info.GetInt32("slideIndex");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("version", version);
            info.AddValue("guid", this.guid.ToString());
            info.AddValue("deckGuid", deckGuid.ToString());
            info.AddValue("slideIndex", slideIndex);
        }
    }

    #region RTDeleteTextAnnotation (Obsolete)

    /// <summary>
    /// This was used between CP3.0 and 3.1 when Text annotations were the only type of annotation sheet available.
    /// It is obsolete.  Future archives should not be created with this type.  For playback it carries the same
    /// meaning as RTDeleteAnnotation above.
    /// </summary>
    [Serializable]
    public class RTDeleteTextAnnotation : ISerializable {
        private Int32 version = 1;

        private Guid guid;
        public Guid Guid {
            get { return guid; }
            set { guid = value; }
        }

        private Guid deckGuid;
        public Guid DeckGuid {
            get { return this.deckGuid; }
            set { deckGuid = value; }
        }

        private Int32 slideIndex;
        public Int32 SlideIndex {
            get { return this.slideIndex; }
            set { slideIndex = value; }
        }

        public RTDeleteTextAnnotation(Guid guid, Guid deckGuid, Int32 slideIndex) {
            this.guid = guid;
            this.deckGuid = deckGuid;
            this.slideIndex = slideIndex;
        }

        protected RTDeleteTextAnnotation(SerializationInfo info, StreamingContext context) {
            this.guid = new Guid(info.GetString("guid"));
            this.deckGuid = new Guid(info.GetString("deckGuid"));
            this.slideIndex = info.GetInt32("slideIndex");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("version", version);
            info.AddValue("guid", this.guid.ToString());
            info.AddValue("deckGuid", deckGuid.ToString());
            info.AddValue("slideIndex", slideIndex);
        }
    }

    #endregion RTDeleteTextAnnotation (Obsolete)

}
