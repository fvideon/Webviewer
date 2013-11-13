using System;
using System.Runtime.Serialization;

namespace ArchiveRTNav
{
	/// <summary>
	/// Update scroll information.
	/// </summary>
	[Serializable]
	public class RTScrollLayer : ISerializable
	{
		private Int32 version = 1;

		private Double scrollPosition;
		public Double ScrollPosition
		{
			get { return this.scrollPosition; }
			set { this.scrollPosition = value; }
		}

		private Double scrollExtent;
		public Double ScrollExtent
		{
			get { return this.scrollExtent; }
			set { this.scrollExtent = value; }
		}

		private Guid deckGuid;
		public Guid DeckGuid
		{
			get { return this.deckGuid; }
			set { this.deckGuid = value; }
		}

		private Int32 slideIndex;
		public Int32 SlideIndex
		{
			get { return this.slideIndex; }
			set { this.slideIndex = value; }
		}

		public RTScrollLayer(Double position, Double extent, Guid deckGuid, Int32 slideIndex)
		{
			this.scrollPosition = position;
			this.scrollExtent = extent;
			this.deckGuid = deckGuid;
			this.slideIndex = slideIndex;
		}

		protected RTScrollLayer(SerializationInfo info, StreamingContext context) 
		{	
			this.scrollPosition = info.GetDouble("scrollPosition");
			this.scrollExtent = info.GetDouble("scrollExtent");
			this.deckGuid = new Guid(info.GetString("deckGuid"));
			this.slideIndex = info.GetInt32("slideIndex");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("version", version);
			info.AddValue("scrollPosition",scrollPosition);
			info.AddValue("scrollExtent",scrollExtent);
			info.AddValue("deckGuid",deckGuid.ToString());
			info.AddValue("slideIndex",slideIndex);
		}
	}
}
