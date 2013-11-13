using System;
using System.Collections;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Summary description for SlideTitles.
	/// </summary>
	public class SlideTitles
	{
		private Hashtable titles;

		public SlideTitles()
		{
			titles = new Hashtable();
		}

		public void Add(String DeckGuid, String Index, String Text)
		{
			if ((DeckGuid == null) 
				|| (Index == null) 
				|| (Text == null))
			{
				return;
			}

			Guid guid;
			Int32 index;
			try
			{
				guid = new Guid(DeckGuid);
				index = Convert.ToInt32(Index);
			}
			catch
			{
				return;
			}
			
			if (index < 0)
				return;

			String key = DeckGuid+"-"+Index;
			if (titles.ContainsKey(key))
			{
				titles[key] = Text;
				return;
			}

			titles.Add(key,Text);
		}

		public String Get(Guid DeckGuid, Int32 Index)
		{
			String key = DeckGuid.ToString()+"-"+Index.ToString();
			if (titles.ContainsKey(key))
			{
				return (String)titles[key];
			}
			return null;
		}

	}
}
