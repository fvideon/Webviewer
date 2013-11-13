using System;
using System.Runtime.Serialization;
using Microsoft.Ink;
/// The archival ink add and remove types follow.
/// As with the other archive types, these have been flattened to make them serialize 
/// to smaller byte arrays.
/// 
/// Notes about archiving ink:
/// As far as I know there will never be more than one ink layer on a slide, so specifying both
/// a layer index and a slide index may be redundant.  For now we will leave them both.
/// 
namespace ArchiveRTNav {
	/// <summary>
	/// Draw a stroke.
	/// </summary>
	[Serializable]
	public class RTDrawStroke : ISerializable 
	{
		private Int32 version = 1;

		private Microsoft.Ink.Ink ink;
		public Microsoft.Ink.Ink Ink 
		{ 
			get { return ink;}
			set { ink = value; }
		}
		
		// stroke is terminal
		private Boolean strokeFinished;
		public Boolean StrokeFinished 
		{
			get { return strokeFinished; }
			set { strokeFinished = value; }
		}

		private Guid guid;
		public Guid Guid 
		{
			get { return guid; }
			set { guid = value; }
		}


		private Guid deckGuid;
		public Guid DeckGuid 
		{ 
			get { return this.deckGuid; } 
			set { deckGuid = value; }
		}

		private Int32 slideIndex;
		public Int32 SlideIndex 
		{ 
			get { return this.slideIndex; } 
			set { slideIndex = value; }
		}

		public RTDrawStroke(Microsoft.Ink.Ink ink, Guid guid, bool strokeFinished, Guid deckGuid, int slideIndex) 
		{
			this.ink = ink;
			this.guid = guid;
			this.strokeFinished = strokeFinished;
			this.deckGuid = deckGuid;
			this.slideIndex = slideIndex;
		}

		protected RTDrawStroke(SerializationInfo info, StreamingContext context) 
		{
			this.ink = new Microsoft.Ink.Ink();
			this.ink.Load((byte[])info.GetValue("ink", typeof(byte[])));
			this.guid = new Guid(info.GetString("guid"));
			this.strokeFinished = info.GetBoolean("strokeFinished");
			this.deckGuid = new Guid(info.GetString("deckGuid"));
			this.slideIndex = info.GetInt32("slideIndex");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context) 
		{
			info.AddValue("version", version);
			info.AddValue("deckGuid",deckGuid.ToString());
			info.AddValue("slideIndex",slideIndex);
			info.AddValue("guid", this.guid.ToString());
			info.AddValue("strokeFinished", this.strokeFinished);
			info.AddValue("ink", this.ink.Save(PersistenceFormat.InkSerializedFormat));
		}

	}

	/// <summary>
	/// Remove a single stroke.
	/// </summary>
	[Serializable]
	public class RTDeleteStroke  : ISerializable
	{
		private Int32 version = 1;

		private Guid guid;
		public Guid Guid {
			get { return guid; }
			set { guid = value; }
		}

		private Guid deckGuid;
		public Guid DeckGuid 
		{ 
			get { return this.deckGuid; } 
			set { deckGuid = value; }
		}

		private Int32 slideIndex;
		public Int32 SlideIndex 
		{ 
			get { return this.slideIndex; } 
			set { slideIndex = value; }
		}

		public RTDeleteStroke(Guid guid, Guid deckGuid, Int32 slideIndex)
		{
			this.guid = guid;
			this.deckGuid = deckGuid;
			this.slideIndex = slideIndex;
		}

		protected RTDeleteStroke(SerializationInfo info, StreamingContext context) 
		{	
			this.guid = new Guid(info.GetString("guid"));
			this.deckGuid = new Guid(info.GetString("deckGuid"));
			this.slideIndex = info.GetInt32("slideIndex");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("version", version);
			info.AddValue("guid", this.guid.ToString());
			info.AddValue("deckGuid",deckGuid.ToString());
			info.AddValue("slideIndex",slideIndex);
		}
	}

	/// <summary>
	/// Remove all the ink from a layer on one slide
	/// </summary>
	[Serializable]
	public class RTEraseLayer  : ISerializable
	{
		private Int32 version = 1;

		private Guid deckGuid;
		public Guid DeckGuid 
		{ 
			get { return this.deckGuid; } 
			set { deckGuid = value; }
		}

		private Int32 slideIndex;
		public Int32 SlideIndex 
		{ 
			get { return this.slideIndex; } 
			set { slideIndex = value; }
		}

		public RTEraseLayer(Guid deckGuid, Int32 slideIndex){
			this.deckGuid = deckGuid;
			this.slideIndex = slideIndex;
		}

		protected RTEraseLayer(SerializationInfo info, StreamingContext context) 
		{	
			this.deckGuid = new Guid(info.GetString("deckGuid"));
			this.slideIndex = info.GetInt32("slideIndex");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("version", version);
			info.AddValue("deckGuid",deckGuid.ToString());
			info.AddValue("slideIndex",slideIndex);
		}
	}

	/// <summary>
	/// Erase all ink layers for all slides in one deck.
	/// </summary>
	[Serializable]
	public class RTEraseAllLayers  : ISerializable
	{
		private Int32 version = 1;

		private Guid deckGuid;
		public Guid DeckGuid 
		{ 
			get { return this.deckGuid; } 
			set { deckGuid = value; }
		}

		public RTEraseAllLayers(Guid deckGuid) 
		{
			this.deckGuid = deckGuid;
		}

		protected RTEraseAllLayers(SerializationInfo info, StreamingContext context) 
		{	
			this.deckGuid = new Guid(info.GetString("deckGuid"));
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("version", version);
			info.AddValue("deckGuid",deckGuid.ToString());
		}
	}
}
