using System;
using System.Collections;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Map multiple decks to a single virtual deck
	/// </summary>
	public class SlideMap
	{
		private Hashtable slideMap;
		private int index;

		public SlideMap()
		{
			slideMap = new Hashtable();
			index = -1;	
		}

		/// <summary>
		/// Return an array of internal indices for all slides belonging to the deckGuid
		/// </summary>
		/// <param name="DeckGuid"></param>
		/// <returns></returns>
		public ArrayList GetSlidesInDeck(Guid DeckGuid)
		{
			ArrayList al = new ArrayList();
			lock (this)
			{
				foreach (SlideData sd in slideMap.Values)
				{
					if (sd.DeckGuid == DeckGuid)
						al.Add(sd.PrivateIndex);
				}
			}
			return al;
		}

		/// <summary>
		/// Return the internal index for slide index and deckGuid.  If there is not
        /// yet an entry in the map, create one and return the new index.
		/// </summary>
		/// <param name="SlideIndex"></param>
		/// <param name="DeckGuid"></param>
		/// <returns></returns>
		public int GetMapping(int SlideIndex, Guid DeckGuid)
		{
			String key = DeckGuid.ToString()+ "-" + SlideIndex.ToString();

			if (slideMap.ContainsKey(key))
			{
				return ((SlideData)slideMap[key]).PrivateIndex;
			}

			index++;
			SlideData sd = new SlideData();
			sd.DeckGuid = DeckGuid;
			sd.PublicIndex = SlideIndex;
			sd.PrivateIndex = index;
			lock (this)
				slideMap.Add(key,sd);

			return index;
		}

		private struct SlideData
		{
			private Guid deckGuid;
			private int publicIndex;
			private int privateIndex;
			public Guid DeckGuid
			{
				get {return deckGuid;}
				set {deckGuid = value;}
			}
			public int PublicIndex
			{
				get {return publicIndex;}
				set {publicIndex = value;}
			}
			public int PrivateIndex
			{
				get {return privateIndex;}
				set {privateIndex = value;}
			}
		}

	}
}
