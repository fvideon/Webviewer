using System;
using System.Collections;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Represents an entry in the Table of Contents.
	/// </summary>
	public class TOCEntry : IComparable
	{
		private TimeSpan time;
		private String entryText;
		private ArchiveRTNav.RTUpdate rtUpdate;

		public TOCEntry(TimeSpan Time, String EntryText, ArchiveRTNav.RTUpdate RTUpdate)
		{
			this.time = Time;
			this.entryText = EntryText;
			this.rtUpdate = RTUpdate;
		}

		public TimeSpan Time
		{
			get {return time;}
			set {time = value;}
		}

		public String EntryText
		{
			get {return entryText;}
			set {entryText = value;}
		}

		public ArchiveRTNav.RTUpdate RTUpdate
		{
			get {return rtUpdate;}
		}

		public override string ToString()
		{
			return entryText;
		}

		//implement sorting/comparing by timestamp
		public int CompareTo(object rh)
		{
			return this.time.CompareTo(((TOCEntry)rh).time);
		}

		public class EntryComparer: IComparer
		{
			public int Compare(object lh, object rh)
			{
				return ((TOCEntry)lh).CompareTo(rh);
			}
		}
	}
}
