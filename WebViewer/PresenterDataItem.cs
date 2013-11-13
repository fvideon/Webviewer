using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


using WorkSpace;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Data for one presenter packet.
	/// </summary>
	/// The data will be received as a script type, a timestamp and a string,
	/// possibly base64 encoded. When instantiated we will
	/// decode the data string, and also extract the Presenter opcode (first byte), and slide number if any.
	public class PresenterDataItem : IComparable
	{
		byte[] data;
		public byte[] Data
		{
			get { return this.data; }
			set { this.data = value; }
		}

		TimeSpan timestamp;
		public TimeSpan TimeStamp
		{
			get { return this.timestamp; }
			set { this.timestamp = value; }
		}

		string type;
		public string Type
		{
			get { return this.type; }
			set { this.type = value; }
		}

		string url;
		public string Url
		{
			get { return this.url; }
			set { this.url = value; }
		}

		byte opcode;
		public byte Opcode
		{
			get { return this.opcode; }
			set { this.opcode = value; }
		}

		int slide;
		public int Slide
		{
			get { return this.slide; }
			set { this.slide = value; }
		}

		Guid deckGuid;
		public Guid DeckGuid
		{
			get { return this.deckGuid; }
			set { this.deckGuid = value; }
		}

		object rtnav;
		public object RTNav
		{
			get { return this.rtnav; }
			set { this.rtnav = value; }
		}

		public PresenterDataItem(TimeSpan timestamp, string type, string data)
		{
			Construct(timestamp,type,data,false);
		}

		public PresenterDataItem(TimeSpan timestamp, string type, string data, bool fragment)
		{
			Construct(timestamp,type,data,fragment);
		}

		private void Construct(TimeSpan timestamp, string type, string data, bool fragment)
		{
			this.type = type;
			this.timestamp = timestamp;
			this.data = null;
			this.slide = -1;
			this.deckGuid = Guid.Empty;
			this.opcode = 0;
			this.url = null;
			this.RTNav = null;

			if ((type == "CXP0") || (type == "CXP1"))
			{
				this.data = Convert.FromBase64String(data);

				//If this is a piece of a fragmented stroke, and not the first piece, 
				//meta-data will not be available.  When we reassemble fragments, the 
				//meta-data for the first will be used.
				if (!fragment) 
				{
					this.opcode = this.data[0]; 

					//some opcodes apply to all slides:
					if ((this.opcode != PacketType.ClearAnnotations) &&
						(this.opcode != PacketType.ResetSlides) &&
						(this.opcode != PacketType.ScreenConfiguration))
					{
						if ((this.opcode == PacketType.Scribble) || (this.opcode == PacketType.ScribbleDelete))
						{
							BinaryFormatter formatter = new BinaryFormatter();
							MemoryStream memoryStream = new MemoryStream(this.data, 1, this.data.Length - 1);
							this.slide = (int) formatter.Deserialize(memoryStream);
						}
						else 
						{
							this.slide = 256 * this.data[2] + this.data[1];  
						}
					}
				}
			}
			else if (type == "CXP3")
			{
				this.data = Convert.FromBase64String(data);
				/// Presenter 2 data is never fragmented.
				Helpers.DeserializeRTNav(this);
			}
			else if (type == "CXP2")
			{
				this.url = data;
			}
			else if (type == "URL")
			{
				this.url = data;
			}

		}

		/// <summary>
		/// return a copy of myself.
		/// </summary>
		/// <returns></returns>
		public PresenterDataItem Copy()
		{
			PresenterDataItem pdi = new PresenterDataItem(new TimeSpan(this.timestamp.Ticks),"NOOP",null);
			pdi.opcode = this.opcode;
			pdi.slide = this.slide;
			pdi.type = this.type;
			pdi.url = this.url;
			pdi.timestamp = this.timestamp;
			pdi.data = new byte[this.data.Length];
			Array.Copy(this.data,0,pdi.data,0,this.data.Length);

			return pdi;
		}

		public override string ToString()
		{
			return "Time: " + this.TimeStamp.ToString() +
				" Type: " + this.Type +
				" Opcode: " + this.Opcode.ToString() +
				" Slide: " + this.Slide.ToString() +
				" Deck: " + this.DeckGuid.ToString(); 
		}

		//implement sorting/comparing by timestamp
		public int CompareTo(object rh)
		{
			return this.timestamp.CompareTo(((PresenterDataItem)rh).timestamp);
		}

		public class DataItemComparer: IComparer
		{
			public int Compare(object lh, object rh)
			{
				return ((PresenterDataItem)lh).CompareTo(rh);
			}
		}
	}
}
