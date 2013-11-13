using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;

namespace SlideViewer
{
    /// <summary>
    /// SlideDeck is composed of several colletions to enable the features we want. 
    /// We have dictionaries of Slide and SlideOverlay keyed by index.  We'll keep them 
    /// in sync so that if we create a slide, an overlay also exists at that index
    /// and vice versa.  If we delete a slide, the overlay is also deleted.
    /// Support tracking of the Least Recently Used Slide and a maximum
    /// slide count such that adding a slide/overlay beyond the threshold will
    /// cause the LRU to be removed.  Also support reverse lookup by Slide or by SlideOverlay.
    /// Internally we may not have any Slide/Overlay at some indices, but this is
    /// transparent to the user since we create a new Slide and Overlay as needed such
    /// that GetSlide/GetOverlay never return null.
    /// </summary>
    public class SlideDeck {
        private Dictionary<int, LinkedListNode<Slide>> slides;
        private Dictionary<Slide, int> slidesRev;
        private LinkedList<Slide> slidesByAccess;
        private Dictionary<int, SlideOverlay> overlays;
        private Dictionary<SlideOverlay, int> overlaysRev;
        private int MAX_STORED_SLIDES = 50;

        /// <summary>
        /// Constructor for an empty deck.
        /// </summary>
        public SlideDeck() {
            Reset();
        }

        /// <summary>
        ///  Remove a slide and overlay at this index
        /// </summary>
        /// <param name="i"></param>
        private void Remove(int i) {
            if (slides.ContainsKey(i)) {
                this.slidesByAccess.Remove(slides[i]);
                this.slidesRev.Remove(slides[i].Value);
                slides[i].Value.Dispose();
                slides.Remove(i);
            }

            if (overlays.ContainsKey(i)) {
                overlaysRev.Remove(overlays[i]);
                overlays.Remove(i);
            }
        }

        // Reset the deck to contain one slide and one overlay
        public void Reset() {
            lock (this) {
                // clean up any previous contents
                if (slides != null) {
                    foreach (int i in slides.Keys) {
                        slides[i].Value.Dispose();
                    }
                }
                slides = new Dictionary<int, LinkedListNode<Slide>>();
                slidesRev = new Dictionary<Slide, int>();
                slidesByAccess = new LinkedList<Slide>();
                overlays = new Dictionary<int, SlideOverlay>();
                overlaysRev = new Dictionary<SlideOverlay, int>();

                // A new deck has one empty slide and overlay
                SetSlide(0, new Slide());
            }
        }

        // Clear all overlays by assigning fresh overlays for all existing keys.
        public void ClearAnnotations() {
            lock (this) {
                overlaysRev = new Dictionary<SlideOverlay, int>();
                Dictionary<int, SlideOverlay> newOverlays = new Dictionary<int, SlideOverlay>();
                foreach (int i in overlays.Keys) {
                    newOverlays.Add(i, new SlideOverlay());
                    overlaysRev.Add(newOverlays[i], i);
                }
                overlays = newOverlays;
            }
        }

        public Slide GetSlide(int index) {
            lock (this) {
                if (!slides.ContainsKey(index)) {
                    Slide s = new Slide();
                    SetSlide(index, s);
                    return s;
                }
                else {
                    slidesByAccess.Remove(slides[index]);
                    slidesByAccess.AddFirst(slides[index]);
                    return slides[index].Value;
                }
            }
        }

        public void SetSlide(int index, Slide slide) {
            lock (this) {
                if (slides.ContainsKey(index)) {
                    //replace existing slide
                    Remove(index);
                }
                else {
                    if (slidesByAccess.Count >= MAX_STORED_SLIDES) {
                        //Over threshold, remove least recently used
                        Slide lru = slidesByAccess.Last.Value;
                        Debug.WriteLine("Removing slide index: " + slidesRev[lru].ToString());
                        Remove(slidesRev[lru]);

                    }
                }

                slides.Add(index, slidesByAccess.AddFirst(slide));
                slidesRev.Add(slides[index].Value, index);
                if (!overlays.ContainsKey(index)) {
                    overlays.Add(index, new SlideOverlay());
                    overlaysRev.Add(overlays[index], index);
                }
            }
        }

        public SlideOverlay GetOverlay(int index) {
            lock (this) {
                if (!overlays.ContainsKey(index)) {
                    SetOverlay(index, new SlideOverlay());
                }
                return overlays[index];
            }
        }

        private void SetOverlay(int index, SlideOverlay overlay) {
            if (overlays.ContainsKey(index)) {
                if (overlaysRev.ContainsKey(overlays[index])) {
                    overlaysRev.Remove(overlays[index]);
                }
                overlays.Remove(index);
            }
            overlays.Add(index, overlay);
            overlaysRev.Add(overlays[index], index);
            if (!slides.ContainsKey(index)) {
                slides.Add(index, slidesByAccess.AddFirst(new Slide()));
                slidesRev.Add(slides[index].Value, index);
            }
        }

        public int FindSlide(Slide slide) {
            lock (this) {
                if (slidesRev.ContainsKey(slide)) {
                    return slidesRev[slide];
                }
                return -1;
            }
        }

        public int FindOverlay(SlideOverlay slideOverlay) {
            lock (this) {
                if (overlaysRev.ContainsKey(slideOverlay)) {
                    return overlaysRev[slideOverlay];
                }
                return -1;
            }
        }
    }
}
