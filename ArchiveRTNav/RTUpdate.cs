using System;
using System.Drawing;
using System.Runtime.Serialization;


namespace ArchiveRTNav
{
	/// <summary>
	/// Encapsulate all the things that need frequent periodic updating and which are "global"
	/// in nature.  Information specific to a slide such as scroll position needs a separate type.
	/// We want this to be small when serialized.  
	/// </summary>
	[Serializable]
	public class RTUpdate : ISerializable
	{

		private UInt16 version = 2;

		private Int32 slideIndex;
		public Int32  SlideIndex
		{
			get { return slideIndex; }
			set { slideIndex = value; }
		}

		// Eg Presentation, Whiteboard, StudentSubmission, etc.
		private Int32 deckType;
		public Int32  DeckType
		{
			get { return deckType; }
			set { deckType= value; }
		}

		// Size of slide in the display area.  A number between 0 and 1 inclusive.
		private Double slideSize; 
		public Double  SlideSize
		{
			get { return slideSize; }
			set {slideSize = value; }
		}

		/// Some deck types will refer to images from other decks.
		/// In this case, deckAssociation and slideAssociation define the reference.
		private Guid deckAssociation;
		public Guid  DeckAssociation
		{
			get { return deckAssociation; }
			set { deckAssociation= value; }
		}

		private Int32 slideAssociation;
		public Int32  SlideAssociation
		{
			get { return slideAssociation; }
			set { slideAssociation= value; }
		}

        private Int32 deckTypeAssociation;
        public Int32 DeckTypeAssociation {
            get { return deckTypeAssociation; }
            set { deckTypeAssociation = value; }
        }

		private Color backgroundColor;
		public Color BackgroundColor 
		{ 
			get { return backgroundColor; } 
			set { backgroundColor = value; }
		}

		//Each presentation deck should have a unique guid.
		//If this message is for a StudentSubmission, the guid should be
		//the associated presentation deck guid.  Otherwise it can be empty.
		//As a special case, a presentation deck guid can be empty if only
		//one presentation deck is used.
		private Guid deckGuid;
		public Guid DeckGuid
		{
			get { return deckGuid; }
			set { deckGuid = value; }
		}
		
		private String baseUrl;
		public String BaseUrl
		{
			get { return baseUrl; }
			set { baseUrl = value; }
		}

		private String extent;
		public String Extent
		{
			get { return extent; }
			set { extent = value; }
		}

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


		public RTUpdate()
		{
			slideIndex = 0;
			deckType = 0;
			slideSize = 0;
			slideAssociation = 0;
			deckAssociation = Guid.Empty;
            deckTypeAssociation = 0;
			backgroundColor = Color.Wheat;
			deckGuid = Guid.Empty;
			baseUrl = "";
			extent = "";
			scrollPosition = 0.0;
			scrollExtent = 0.0;
		}
		
		protected RTUpdate(SerializationInfo info, StreamingContext context) 
		{
            this.version = info.GetUInt16("version");
			this.slideIndex = info.GetInt32("slideIndex");
			this.deckType = info.GetInt32("deckType");
			this.slideSize = info.GetDouble("slideSize");
			this.slideAssociation = info.GetInt32("slideAssociation");
			this.deckAssociation = new Guid(info.GetString("deckAssociation"));
            if (this.version >= 2) {
                this.deckTypeAssociation = info.GetInt32("deckTypeAssociation");
            }
            else {
                this.deckTypeAssociation = this.deckType;
            }
            this.backgroundColor = (Color)Helpers.ByteArrayToObject((byte[])info.GetValue("backgroundColor", typeof(byte[])));
			this.deckGuid = new Guid(info.GetString("deckGuid"));
			this.baseUrl = info.GetString("baseUrl");
			this.extent = info.GetString("extent");
			this.scrollPosition = info.GetDouble("scrollPosition");
			this.scrollExtent = info.GetDouble("scrollExtent");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("version", version);
			info.AddValue("slideIndex",slideIndex);
			info.AddValue("deckType",deckType);
			info.AddValue("slideSize",slideSize);
			info.AddValue("slideAssociation",slideAssociation);
			info.AddValue("deckAssociation",deckAssociation.ToString());
            info.AddValue("deckTypeAssociation", deckTypeAssociation);
			info.AddValue("backgroundColor",Helpers.ObjectToByteArray(backgroundColor));
			info.AddValue("deckGuid",this.deckGuid.ToString());
			info.AddValue("baseUrl",baseUrl);
			info.AddValue("extent",extent);
			info.AddValue("scrollPosition",scrollPosition);
			info.AddValue("scrollExtent",scrollExtent);
		}

	}

}
