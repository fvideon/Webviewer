using System;
using System.Runtime.Serialization;

namespace ArchiveRTNav
{
	[Serializable]
	public class RTRemoveDocument : ISerializable
	{
		private string document;
		public string Document {
			get { return document; }
		}

		public RTRemoveDocument(string document)
		{
			this.document = document;
		}

		public RTRemoveDocument() : this(""){
		}
		protected RTRemoveDocument(SerializationInfo info, StreamingContext context) 
		{	
			this.document = info.GetString("document");
		}

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Version", 1.0);
			info.AddValue("document",document);
		}
	}

}
