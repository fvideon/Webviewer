using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace SlideViewer {

    /// <summary>
    /// Contains the current information for one CP3 QuickPoll.  This is a property on SlideOverlay.
    /// The SlideViewLayer type DynamicElementsLayer contains the painting code.
    /// </summary>
    public class QuickPoll {
        private bool m_Enabled;
        private QuickPollStyle m_QuickPollStyle;
        private int[] m_Results;

        public QuickPoll() {
            m_Enabled = false;
            m_Results = new int[0];
            m_QuickPollStyle = QuickPollStyle.ABCD;
        }

        /// <summary>
        /// Make the QuickPoll visible or invisible
        /// </summary>
        public bool Enabled {
            get { return m_Enabled; }
            set { m_Enabled = value; }
        }

        public List<string> GetNames() {
            return QuickPoll.GetVoteStringsFromStyle(this.m_QuickPollStyle);
        }

        /// <summary>
        /// Return the current QuickPoll table
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, int> GetTable() {
            Dictionary<string, int> ret = new Dictionary<string, int>();
            lock (this) {
                List<string> names = GetNames();
                int index = 0;
                foreach (string s in names) {
                    if (m_Results.Length > index) {
                        ret.Add(s, m_Results[index]);
                    }
                    else {
                        ret.Add(s, 0);
                    }
                    index++;
                }
            }
            return ret;
        }

        /// <summary>
        /// Apply new results to the QuickPoll
        /// </summary>
        /// <param name="results"></param>
        public void Update(int styleAsInt, int[] results) {
            lock (this) {
                m_QuickPollStyle = (QuickPollStyle)styleAsInt;
                m_Results = results;
            }
        }


        #region Statics

        /// <summary>
        /// A helper member that specifies the brushes to use for the various columns
        /// </summary>
        public static System.Drawing.Brush[] ColumnBrushes = new System.Drawing.Brush[] { Brushes.Orange, 
                                                                            Brushes.Cyan, 
                                                                            Brushes.Magenta, 
                                                                            Brushes.Yellow, 
                                                                            Brushes.GreenYellow };

        /// <summary>
        /// Get the heading strings for a QuickPoll Style.
        /// </summary>
        /// <param name="style"></param>
        /// <returns></returns>
        public static List<string> GetVoteStringsFromStyle(QuickPollStyle style) {
            List<string> strings = new List<string>();
            switch (style) {
                case QuickPollStyle.YesNo:
                    strings.Add("Yes");
                    strings.Add("No");
                    break;
                case QuickPollStyle.YesNoBoth:
                    strings.Add("Yes");
                    strings.Add("No");
                    strings.Add("Both");
                    break;
                case QuickPollStyle.YesNoNeither:
                    strings.Add("Yes");
                    strings.Add("No");
                    strings.Add("Neither");
                    break;
                case QuickPollStyle.ABC:
                    strings.Add("A");
                    strings.Add("B");
                    strings.Add("C");
                    break;
                case QuickPollStyle.ABCD:
                    strings.Add("A");
                    strings.Add("B");
                    strings.Add("C");
                    strings.Add("D");
                    break;
                case QuickPollStyle.ABCDE:
                    strings.Add("A");
                    strings.Add("B");
                    strings.Add("C");
                    strings.Add("D");
                    strings.Add("E");
                    break;
                case QuickPollStyle.ABCDEF:
                    strings.Add("A");
                    strings.Add("B");
                    strings.Add("C");
                    strings.Add("D");
                    strings.Add("E");
                    strings.Add("F");
                    break;
                case QuickPollStyle.Custom:
                    // Do Nothing for now
                    break;
            }

            return strings;
        }

        #endregion Statics

        #region Enum

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

        #endregion Enum

    }
}
