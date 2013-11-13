using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using WorkSpace;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Summary description for Helpers.
	/// </summary>
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

		/// <summary>
		/// Deserialize CXP3 data and set opcodes and slide numbers.
		/// </summary>
		public static void DeserializeRTNav(PresenterDataItem pdi)
		{
			if (pdi.Type == "CXP3")
			{
				pdi.RTNav = Helpers.ByteArrayToObject(pdi.Data);
				if (pdi.RTNav == null)
					return;

				if (pdi.RTNav is ArchiveRTNav.RTDeleteStroke) //delete one stroke
				{
					pdi.Opcode = PacketType.ScribbleDelete;
					pdi.Slide = ((ArchiveRTNav.RTDeleteStroke)pdi.RTNav).SlideIndex;
					pdi.DeckGuid = ((ArchiveRTNav.RTDeleteStroke)pdi.RTNav).DeckGuid;
				}
				else if (pdi.RTNav is ArchiveRTNav.RTEraseLayer)  //delete strokes on current slide
				{
					pdi.Opcode = PacketType.ClearScribble;
					pdi.Slide = ((ArchiveRTNav.RTEraseLayer)pdi.RTNav).SlideIndex;
					pdi.DeckGuid = ((ArchiveRTNav.RTEraseLayer)pdi.RTNav).DeckGuid;
				}
				else if (pdi.RTNav is ArchiveRTNav.RTEraseAllLayers) //delete all strokes on all slides
				{
					pdi.Opcode = PacketType.ClearAnnotations;
					pdi.DeckGuid = ((ArchiveRTNav.RTEraseAllLayers)pdi.RTNav).DeckGuid;
				}
				else if (pdi.RTNav is ArchiveRTNav.RTUpdate)
				{
					pdi.Opcode = PacketType.RTUpdate;
					pdi.Slide = ((ArchiveRTNav.RTUpdate)pdi.RTNav).SlideIndex;
					pdi.DeckGuid = ((ArchiveRTNav.RTUpdate)pdi.RTNav).DeckGuid;
				}
				else if (pdi.RTNav is ArchiveRTNav.RTDrawStroke)
				{
					pdi.Opcode = PacketType.Scribble;
					pdi.Slide = ((ArchiveRTNav.RTDrawStroke)pdi.RTNav).SlideIndex;
					pdi.DeckGuid = ((ArchiveRTNav.RTDrawStroke)pdi.RTNav).DeckGuid;
				}
				else if (pdi.RTNav is ArchiveRTNav.RTScrollLayer)
				{
					pdi.Opcode = PacketType.Scroll;
					pdi.Slide = ((ArchiveRTNav.RTScrollLayer)pdi.RTNav).SlideIndex;
					pdi.DeckGuid = ((ArchiveRTNav.RTScrollLayer)pdi.RTNav).DeckGuid;
				}
                else if (pdi.RTNav is ArchiveRTNav.RTTextAnnotation) {
                    pdi.Opcode = PacketType.RTText;
                    pdi.Slide = ((ArchiveRTNav.RTTextAnnotation)pdi.RTNav).SlideIndex;
                    pdi.DeckGuid = ((ArchiveRTNav.RTTextAnnotation)pdi.RTNav).DeckGuid;
                }
                else if (pdi.RTNav is ArchiveRTNav.RTImageAnnotation) {
                    pdi.Opcode = PacketType.RTImageAnnotation;
                    pdi.Slide = ((ArchiveRTNav.RTImageAnnotation)pdi.RTNav).SlideIndex;
                    pdi.DeckGuid = ((ArchiveRTNav.RTImageAnnotation)pdi.RTNav).DeckGuid;
                }
                else if (pdi.RTNav is ArchiveRTNav.RTDeleteTextAnnotation) {
                    pdi.Opcode = PacketType.RTDeleteText;
                    pdi.Slide = ((ArchiveRTNav.RTDeleteTextAnnotation)pdi.RTNav).SlideIndex;
                    pdi.DeckGuid = ((ArchiveRTNav.RTDeleteTextAnnotation)pdi.RTNav).DeckGuid;
                }
                else if (pdi.RTNav is ArchiveRTNav.RTDeleteAnnotation) {
                    //TODO: this might delete a text or an image.  
                    pdi.Opcode = PacketType.RTDeleteText;
                    pdi.Slide = ((ArchiveRTNav.RTDeleteAnnotation)pdi.RTNav).SlideIndex;
                    pdi.DeckGuid = ((ArchiveRTNav.RTDeleteAnnotation)pdi.RTNav).DeckGuid;
                }
                else if (pdi.RTNav is ArchiveRTNav.RTQuickPoll) {
                    pdi.Opcode = PacketType.RTQuickPoll;
                    pdi.Slide = ((ArchiveRTNav.RTQuickPoll)pdi.RTNav).SlideIndex;
                    pdi.DeckGuid = ((ArchiveRTNav.RTQuickPoll)pdi.RTNav).DeckGuid;
                }
                else {
                    //Type t = rtobj.GetType();
                    //parent.LoggerWriteInvoke("**Unhandled RTObject Type:" + t.ToString());
                }
			}
		}
	}
}
