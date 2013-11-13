using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Runtime.Serialization;
using System.Reflection;
using Ink = Microsoft.Ink;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SlideViewer
{

	[Serializable]
	public abstract class Scribble : ISerializable {

		/// <summary>
		/// Create a new scribble object of the type dictated by UseInk.
		/// </summary>
		public static Scribble NewScribble() {
			return new InkScribble();
		}

		// simple custom serialization to avoid serializing events.
		protected Scribble(SerializationInfo info, StreamingContext context) {
			//	: base(info, context)
			Type t = typeof(Scribble);
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
			Type t = typeof(Scribble);
			ArrayList events = new ArrayList();
			foreach (EventInfo o in t.GetEvents(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				events.Add(o.Name);
			foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly
				| BindingFlags.Public | BindingFlags.NonPublic))
				if (!events.Contains(field.Name))
					info.AddValue(field.Name, field.GetValue(this), field.FieldType);
		}

		public Scribble() { }

		public abstract void Clear();

		public abstract int Count { get; }

		/// <summary>
		/// Called for points added to the scribble with the number of points
		/// added in the last block of adds.
		/// <para>Must be called separately for each stroke added even if
		/// all strokes are added in a block.</para>
		/// </summary>
		public event NumericEventHandler InkAdded;

		public event NumericEventHandler Stroke;

		/// <summary>
		/// Fired when a stroke is deleted with the id (not index!) of the stroke
		/// that was deleted.
		/// </summary>
		public event NumericEventHandler StrokeDeleted;
		
		public event EventHandler Cleared;
		
		public abstract void Draw(Graphics g);

		protected virtual void OnStroke(NumericEventArgs args) {
			if (this.Stroke != null)
				this.Stroke(this, args);
		}

		protected virtual void OnStrokeDeleted(NumericEventArgs args) {
			if (this.StrokeDeleted != null)
				this.StrokeDeleted(this, args);
		}

		protected virtual void OnInkAdded(NumericEventArgs args) {
			if (this.InkAdded != null)
				this.InkAdded(this, args);
		}

		protected virtual void OnCleared(EventArgs args) {
			if (this.Cleared != null)
				this.Cleared(this, args);
		}

		public abstract void SerializeAllPoints(Stream stream);
		public abstract void SerializePoints(Stream stream, int numPoints);
		public abstract void DeserializePoints(Stream stream);
	}

	[Serializable]
	public class InkScribble : Scribble, ISerializable, IDeserializationCallback {
		protected InkScribble(SerializationInfo info, StreamingContext context)
			: base(info, context) {
			this.myInk = new Ink.Ink();
			this.myInk.Load((byte[])info.GetValue("ink", typeof(byte[])));
		}

		public void OnDeserialization(object sender) {
			this.Initialize();
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context) {
			byte[] bytes = this.myInk.Save(
				Microsoft.Ink.PersistenceFormat.InkSerializedFormat,
				Microsoft.Ink.CompressionMode.Maximum);
			info.AddValue("ink", bytes);
		}

		protected ArrayList /* of Ink.InkCollector or Ink.InkOverlay */ myInkSources = 
			new ArrayList();
		
		public InkScribble() { 
			myInk = new Ink.Ink();
			Initialize();
		}

		private void Initialize() {
			myInk.InkAdded += new Ink.StrokesEventHandler(this.HandleInkAdded);
			myInk.InkDeleted += new Ink.StrokesEventHandler(this.HandleInkDeleted);
			foreach (Microsoft.Ink.Stroke stroke in myInk.Strokes)
				this.HandleInkAdded(stroke.Id);
		}

		protected virtual void HandleInkAdded(object sender, Microsoft.Ink.StrokesEventArgs args) {
			foreach (int id in args.StrokeIds)
				this.HandleInkAdded(id);
		}

		protected virtual void HandleInkDeleted(object sender, Microsoft.Ink.StrokesEventArgs args) {
			foreach (int id in args.StrokeIds)
				this.OnStrokeDeleted(new NumericEventArgs(id));
		}

		protected virtual void HandleInkAdded(int id) {
			this.myNumDelayedPackets = 0;
			this.OnStroke(new NumericEventArgs(id));
		}

		public override int Count { get { return this.myInk.Strokes.Count; } }

		/// <remarks>Check enabled status before altering properties that require source to be
		/// disabled.</remarks>
		public void SetData(Ink.InkCollector inkSource) {
			if (!this.myInkSources.Contains(inkSource)) {
				if (!inkSource.Enabled)
					inkSource.Ink = myInk;
				inkSource.NewPackets += new Ink.InkCollectorNewPacketsEventHandler(this.HandleNewPackets);

				this.myInkSources.Add(inkSource);			
			}
		}

		/// <remarks>Check enabled status before altering properties that require source to be
		/// disabled.</remarks>
		public void SetData(Ink.InkOverlay inkSource) {
			if (!this.myInkSources.Contains(inkSource)) {
				if (!inkSource.Enabled)
					inkSource.Ink = myInk;
				inkSource.NewPackets += new Ink.InkCollectorNewPacketsEventHandler(this.HandleNewPackets);

				this.myInkSources.Add(inkSource);
			}
		}

		/// <remarks>Check enabled status before altering properties that require source to be
		/// disabled.</remarks>
		public void UnsetData(Ink.InkCollector inkSource) {
			if (this.myInkSources.Contains(inkSource)) {
				this.myInkSources.Remove(inkSource);
				inkSource.NewPackets -= new Ink.InkCollectorNewPacketsEventHandler(this.HandleNewPackets);
			}
		}

		/// <remarks>Check enabled status before altering properties that require source to be
		/// disabled.</remarks>
		public void UnsetData(Ink.InkOverlay inkSource) {
			if (this.myInkSources.Contains(inkSource)) {
				this.myInkSources.Remove(inkSource);
				inkSource.NewPackets -= new Ink.InkCollectorNewPacketsEventHandler(this.HandleNewPackets);
			}
		}

		public const int PACKET_REPORT_COUNT = 30;
		private int myNumDelayedPackets = 0;

		protected virtual void HandleNewPackets(object sender, Ink.InkCollectorNewPacketsEventArgs args) {
			if (args.Stroke.DrawingAttributes.Transparency < 255) {
				this.myNumDelayedPackets += args.PacketCount;
				if (this.myNumDelayedPackets >= PACKET_REPORT_COUNT) {
					this.OnInkAdded(new NumericEventArgs(this.myNumDelayedPackets));
					this.myNumDelayedPackets = 0;
				}
			}
		}

		protected Ink.Ink myInk = null;
		public Ink.Ink Ink { get { return this.myInk; } }

		public override void Clear() {
			// Cannot clear if anything is currently collecting.
			foreach (object o in this.myInkSources)
				if ((bool)o.GetType().GetProperty("CollectingInk").GetValue(o, null))
					return;

			try {
				myInk.InkDeleted -= new Ink.StrokesEventHandler(this.HandleInkDeleted);
				try {
					myInk.DeleteStrokes();
				}
				finally {
					myInk.InkDeleted += new Ink.StrokesEventHandler(this.HandleInkDeleted);
				}
			}
			catch (System.InvalidOperationException e) {
				// The call to delete failed.
				System.Diagnostics.Debug.WriteLine("Delete call failed with " + e);
				return;
			}
			catch (System.Runtime.InteropServices.COMException e) {
				// The call to delete failed.
				System.Diagnostics.Debug.WriteLine("Delete call failed with " + e);
				return;
			}

			this.myNumDelayedPackets = 0;
			this.OnCleared(EventArgs.Empty);
		}

		
		private const int NUM_DRAW_ATTEMPTS = 10;
		public void Draw(Graphics g, Ink.Renderer renderer) {
			if (renderer == null)
				throw new NullReferenceException("cannot draw with null renderer");
			for (int i = 0; i < NUM_DRAW_ATTEMPTS; i++) {
				try {
					renderer.Draw(g, myInk.Strokes);
					break;
				}
				catch (Exception e) {
					System.Diagnostics.Debug.WriteLine("Exception occurred in InkScribble.Draw: " + e);
					// Ignore exception.
					System.Threading.Thread.Sleep(100);
				}
			}
		}

		public override void Draw(Graphics g) {
			if (this.myInkSources.Count == 0)
				throw new NullReferenceException("must have some ink source set (with SetData) before drawing");
			this.Draw(g, (Ink.Renderer)(this.myInkSources[0].GetType().GetProperty("Renderer").GetValue(this.myInkSources[0], null)));
		}

		public override void SerializeAllPoints(Stream stream) {
			int count = 0;
			foreach (Ink.Stroke s in this.myInk.Strokes)
				count += s.GetPoints().Length;
			this.SerializePoints(stream, count);
		}

		public override void SerializePoints(Stream stream, int numPoints) {
			IFormatter formatter = new BinaryFormatter();

			// Calculate the number of strokes to serialize
			int index = this.myInk.Strokes.Count;
			int countLeft = numPoints;
			while (countLeft > 0 && index > 0) {
				index--;
				countLeft -= this.myInk.Strokes[index].GetPoints().Length;
			}
			// Index is now -1 or the first stroke to send.
			if (index < 0)
				index = 0;

			if (countLeft < 0)
				countLeft += this.myInk.Strokes[index].GetPoints().Length;

			// If there is a partial stroke, serialize it specially
			if (countLeft > 0) {
				formatter.Serialize(stream, true); // Flag that there is a partial stroke.
			}
			else
				formatter.Serialize(stream, false); // Flag that there is no partial stroke.

			// Serialize just a zero (length) for an empty scribble.
			if (this.myInk.Strokes.Count - index == 0) {
				formatter.Serialize(stream, 0);
			}
			else {
				// Serialize all remaining strokes completely
				Ink.Ink ink = new Ink.Ink();
				int[] ids = new int[this.myInk.Strokes.Count - index];
				for (int i = index; i < this.myInk.Strokes.Count; i++)
					ids[i-index] = this.myInk.Strokes[i].Id;
				// Note: AddStrokesAtRectangle is used since the documentation for AddStrokes
				// states that the strokes must *already* be in the ink object (so, it's only
				// used for updating custom strokes collections??).
				Ink.Strokes strokes = this.myInk.CreateStrokes(ids); 
				ink.AddStrokesAtRectangle(strokes, strokes.GetBoundingBox());
				byte[] inkBytes = ink.Save();
				formatter.Serialize(stream, inkBytes.Length);
				stream.Write(inkBytes, 0, inkBytes.Length);
			}
		}

		public override void DeserializePoints(Stream stream) {
			// Check if there was a partial stroke; if so, delete the current last stroke.
			IFormatter formatter = new BinaryFormatter();
			bool partial = (bool)formatter.Deserialize(stream);
			if (partial) {
				if (this.myInk.Strokes.Count == 0)
					System.Diagnostics.Debug.WriteLine("no partial stroke to delete", "WARNING");
				else
					this.myInk.DeleteStroke(this.myInk.Strokes[this.myInk.Strokes.Count - 1]);
			}

			// Deserialize all remaining strokes.
			int length = (int)formatter.Deserialize(stream);

			if (length > 0) {
				byte[] inkBytes = new byte[length];
				stream.Read(inkBytes, 0, length);
				Ink.Ink ink = new Ink.Ink();
				ink.Load(inkBytes);
				// Note: AddStrokesAtRectangle is used since the documentation for AddStrokes
				// states that the strokes must *already* be in the ink object (so, it's only
				// used for updating custom strokes collections??).
				if (ink.Strokes.Count > 0)
					this.myInk.AddStrokesAtRectangle(ink.Strokes, ink.Strokes.GetBoundingBox());
			}

			foreach (Ink.Stroke stroke in this.myInk.Strokes)
				this.OnStroke(new NumericEventArgs(stroke.Id));
		}
	}

}
