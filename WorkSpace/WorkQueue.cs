using System;

namespace WorkSpace
{
	/// <summary>
	/// Maintain work queue for update tasks
	/// 
	/// For now, the work queue handles scrolling events.  We want to make sure that scroll messages don't queue up.
	/// To handle this, the queue only stores a single scroll event, if a new one comes in, the current one is overwritten
	/// (so this really isn't a queue at all).
	/// Coverage for slide indices added
	/// </summary>
	public class WorkQueue
	{
		private bool scrollEmpty;
		private float scrollValue;
		private float scrollExtent;
		private bool scrollBusy;
		private int slideIndex;

		private object myLockObject;
		public object LockObject { set { myLockObject = value; } }

		public WorkQueue()
		{
			this.LockObject = this;

			scrollEmpty = true;
			scrollValue = 0.0f;
			scrollExtent = 1.5f;
			scrollBusy = false;

			slideIndex = 0;
		}

		// Control access to scrolling.  We need to ensure there is at most one thread scrolling, and that everything
		// eventually gets scrolled.
		// Access to the work queue is guarded by a lock, so at most one thread can have access.
		// To gain access in order to process scroll messages, a version of test and set is used.
		// We can start scrolling if the queue is not busy and if there is work to do.
		// Start scrolling, called by a thread that is not processing scroll messages
		// Atomic actions:
		//       if  (!scrollEmpty && ! scrollBusy) then {scrollBusy = true;  return true;}
		//                                          else return false;
		public bool StartScrolling(){
			lock(this.myLockObject){
				if (!scrollEmpty && ! scrollBusy){
					scrollBusy = true;
					return true;
				}
				else
					return false;
			}

		}

		// Continue scrolling, called by a thread processing scroll messages (so scrollBusy == true)
		// Atomic actions:
		//       if (! scrollEmpty ) then return true;
		//                           else { scrollBusy = false; return false; }
		public bool ContinueScrolling(){
			lock(this.myLockObject){
				if (! scrollEmpty)
					return true;
				else {
					scrollBusy = false;
					return false;
				}
			}
		}

		public bool GetScrollPos(out int slideIndex, out float scrollValue, out float scrollExtent){
			lock(this.myLockObject){
				slideIndex = this.slideIndex;
				scrollValue = this.scrollValue;
				scrollExtent = this.scrollExtent;
				if (scrollEmpty)
					return false;
				else {
					scrollEmpty = true;
					return true;
				}
			}
		}

		public void SetScrollPos(int slideIndex, float scrollValue, float scrollExtent){
			lock(this.myLockObject){
				this.slideIndex = slideIndex;
				this.scrollValue = scrollValue;
				this.scrollExtent = scrollExtent;
				scrollEmpty = false;
			}
		}
	}
}
