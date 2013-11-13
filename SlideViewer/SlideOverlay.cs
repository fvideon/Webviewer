using System;
using System.Runtime.Serialization;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace SlideViewer
{
	/// <summary>
	/// Annotations associated with a slide - this includes ink, text, dynamic images, quickPolls, etc.
	/// </summary>
	[Serializable]
	public class SlideOverlay : ISerializable
	{
		// Simple custom serialization to avoid serializing events.
		protected SlideOverlay(SerializationInfo info, StreamingContext context) {
			//	: base(info, context)
			Type t = typeof(SlideOverlay);
			ArrayList events = new ArrayList();
			foreach (EventInfo o in t.GetEvents(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				events.Add(o.Name);
			foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				if (!events.Contains(field.Name))
					field.SetValue(this, info.GetValue(field.Name, field.FieldType));
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type t = typeof(SlideOverlay);
			ArrayList events = new ArrayList();
			foreach (EventInfo o in t.GetEvents(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				events.Add(o.Name);
			foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				if (!events.Contains(field.Name))
					info.AddValue(field.Name, field.GetValue(this), field.FieldType);
		}

        private Dictionary<Guid,TextAnnotation> textAnnotations;
        public Dictionary<Guid, TextAnnotation> TextAnnotations {
            get { return textAnnotations; }
        }

        private Dictionary<Guid, DynamicImage> dynamicImages;
        public Dictionary<Guid, DynamicImage> DynamicImages {
            get { return dynamicImages; }
        }

        private QuickPoll quickPoll;
        /// <summary>
        /// Every SlideOverlay has exactly one QuickPoll object for its lifetime.
        /// The QuickPoll is initially disabled. 
        /// It can be made visible and caused to change appearance by setting its properties.
        /// </summary>
        public QuickPoll QuickPoll {
            get { return quickPoll; }
        }

		private Scribble scribble;
		public Scribble Scribble {
			get { return scribble; }
		}

		public SlideOverlay()
		{
			scribble = SlideViewer.Scribble.NewScribble();
            textAnnotations = new Dictionary<Guid,TextAnnotation>();
            quickPoll = new QuickPoll();
            dynamicImages = new Dictionary<Guid, DynamicImage>();
		}

		private float myMaxMylarScrollPosition = 1.5f;

		/// <summary>
		/// The maximum possible value (absolute) of the scroll position. So, 
		/// the mylar extends, positively and negatively, from zero to 
		/// MaxMylarScrollPosition.
		/// </summary>
		public float MaxMylarScrollPosition {
			get { return this.myMaxMylarScrollPosition; }
			set {
				if (value < 0)
					throw new ArgumentOutOfRangeException("value", value, "must be positive");
				if (value != this.myMaxMylarScrollPosition) {
					if (this.MylarScrollPosition > value)
						this.MylarScrollPosition = value;
					else if (this.MylarScrollPosition < -value)
						this.MylarScrollPosition = -value;
					this.myMaxMylarScrollPosition = value;
					this.OnMylarSizeChanged(EventArgs.Empty);
				}
			}
		}

		private float myMylarScrollPosition = 0;

		/// <summary>
		/// The position of the mylar overlay. Centered is 0. The extent of the overlay
		/// is from -MaxMylarScrollPosition to +MaxMylarScrollPosition.
		/// </summary>
		public float MylarScrollPosition {
			get { return this.myMylarScrollPosition; }
			set {
				float trueValue = value;
				if (Math.Abs(trueValue) > this.MaxMylarScrollPosition)
					trueValue = Math.Sign(trueValue) * this.MaxMylarScrollPosition;
				if (this.myMylarScrollPosition != trueValue) {
					this.myMylarScrollPosition = trueValue;
					this.OnScrolled(EventArgs.Empty);
				}
			}
		}

		public event EventHandler Scrolled;
		protected virtual void OnScrolled(EventArgs args) {
			if (this.Scrolled != null)
				this.Scrolled(this, args);
		}

		public event EventHandler MylarSizeChanged;
		protected virtual void OnMylarSizeChanged(EventArgs args) {
			if (this.MylarSizeChanged != null)
				this.MylarSizeChanged(this, args);
		}

        public event EventHandler DynamicElementChanged;
        /// <summary>
        /// Signal the appropriate layer to invalidate and redraw.  This is for elements such
        /// as QuickPolls and Text Annotations which are not immutable on the slide.
        /// </summary>
        public void RefreshDynamicElements() {
            if (this.DynamicElementChanged != null) {
                this.DynamicElementChanged(this, EventArgs.Empty);
            }
        }
    }
}
