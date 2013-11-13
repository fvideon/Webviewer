using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ArchiveRTNav
{
	public class Helpers
	{
		public Helpers()
		{
		}
		public static byte[] ObjectToByteArray(Object b)
		{
			if (b==null)
				return new byte[0];
			BinaryFormatter bf = new BinaryFormatter();
			MemoryStream ms = new MemoryStream();
			bf.Serialize(ms,b);
			ms.Position = 0;//rewind
			byte[] ba = new byte[ms.Length];
			ms.Read(ba,0,(int)ms.Length);
			return ba;
		}
	
		public static Object ByteArrayToObject(byte[] ba)
		{
			BinaryFormatter bf = new BinaryFormatter();
			MemoryStream ms = new MemoryStream(ba);
			ms.Position = 0;
			try
			{
				return (Object) bf.Deserialize(ms);
			}
			catch(Exception e)
			{
				Console.WriteLine(e.ToString());
				return null;
			}
		}
	}

		public enum DeckTypeEnum
		{
			Undefined = 0,
			Presentation = 1,
			Whiteboard = 2,
			StudentSubmission = 3,
            QuickPoll = 4
		}
}
