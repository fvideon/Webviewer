using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Runtime.Serialization;

namespace ArchiveRTNav {
    [Serializable]
    public class RTQuickPoll : ISerializable {
        private Int32 version = 1;

        private int[] results;
        public int[] Results {
            get { return results; }
            set { results = value; }
        }

        private Guid deckGuid;
        public Guid DeckGuid {
            get { return this.deckGuid; }
            set { deckGuid = value; }
        }

        private Int32 slideIndex;
        public Int32 SlideIndex {
            get { return this.slideIndex; }
            set { slideIndex = value; }
        }

        private QuickPollStyle style;
        public QuickPollStyle Style {
            get { return this.style; }
            set { this.style = value; }
        }

        public RTQuickPoll(QuickPollStyle style, int[] results, Guid deckGuid, int slideIndex) {
            this.style = style;
            this.results = results;
            this.deckGuid = deckGuid;
            this.slideIndex = slideIndex;
        }

        protected RTQuickPoll(SerializationInfo info, StreamingContext context) {
            this.results = (int[])info.GetValue("results", typeof(int[]));
            this.deckGuid = new Guid(info.GetString("deckGuid"));
            this.slideIndex = info.GetInt32("slideIndex");
            this.style = (QuickPollStyle)info.GetInt32("style");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context) {
            info.AddValue("version", version);
            info.AddValue("deckGuid", deckGuid.ToString());
            info.AddValue("slideIndex", slideIndex);
            info.AddValue("style", (Int32)style);
            info.AddValue("results", this.results, this.results.GetType());
        }

    }

    public enum QuickPollStyle {
        Custom = 0,
        YesNo = 1,
        YesNoBoth = 2,
        YesNoNeither = 3,
        ABC = 4,
        ABCD = 5,
        ABCDE = 6,
        ABCDEF = 7
    }
}
