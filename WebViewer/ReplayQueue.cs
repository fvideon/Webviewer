using System;
using System.Collections;
using System.Threading;

namespace UW.CSE.CXP
{
	/// <summary>
	/// Assure correct ordering, and avoid loss of presentation data.
	/// </summary>
	/// Presententation data playback occurs in two distinct scenarios:  1) After a media position change, relevant
	/// presentation data needs to be played back as fast as possible to restore current state. 2) As media is 
	/// playing events are raised by PresenterDataStore which track the media.  All data of the second type
	/// pass through this class.  During normal playback the queue will be nearly empty.  
	/// During playback after a media position change, the start 
	/// of which is indicated by a call of the Hold method, incoming data will be enqueued.  After
	/// the type 1 data playback is finished, a call to the Release method will cause enqueued data to be
	/// played until the queue is empty.    
	public class ReplayQueue
	{

		private Queue	myQueue;			//Queue for data items incoming while a replay thread is underway.
		private Queue	syncQueue;			//thread-safe wrapper for myQueue

		private Thread dequeueThread;		//thread to do the dequeueing
		private ManualResetEvent playNow;	//indicate to the dequeueThread that there is work to do.
		private bool hold;					//indicate that we are in hold state

		public event playItem OnPlayItem;	//event and delegate to raise to dequeue.
		public delegate void playItem(object data);

		public ReplayQueue() 
		{
			myQueue = new Queue();
			syncQueue = Queue.Synchronized(myQueue);
			hold = false;
			playNow = new ManualResetEvent(true);
			dequeueThread = new Thread(new ThreadStart(DequeueThread));
			dequeueThread.Start();
		}

		public void Stop()
		{
			if (dequeueThread != null)
				dequeueThread.Abort();
		}

		public void Hold()
		{
			hold = true;
		}

		public void Release()
		{
			hold = false;
			playNow.Set();
		}

		public void Clear()
		{
			syncQueue.Clear();
		}


		public void Enqueue(object data)
		{
			syncQueue.Enqueue(data);
			if (!hold)
				playNow.Set();
		}

		void DequeueThread()
		{
			object data;
			while(true)
			{
				while ((!hold) && (syncQueue.Count>0))
				{
					try
					{
						data = syncQueue.Dequeue();
					}
					catch (InvalidOperationException)
					{
						//queue empty
						break;
					}
					
					if (OnPlayItem != null)
						OnPlayItem(data);
					
				}
				playNow.Reset();
				playNow.WaitOne(5000,false);
			}
		}

	}
}
