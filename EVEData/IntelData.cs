//-----------------------------------------------------------------------
// Intel Data
//-----------------------------------------------------------------------

using System.ComponentModel;

namespace HISA.EVEData
{
    public enum IntelShipClass
    {
        UnknownHostile,
        Capsule,
        Corvette,
        Shuttle,
        Frigate,
        Destroyer,
        Cruiser,
        Battlecruiser,
        Battleship,
        Industrial,
        Mining,
        Freighter,
        Capital,
        Fighter,
        Structure
    }

    public enum IntelAlertIconType
    {
        HostileShip,
        Capital,
        Interdictor,
        Cyno,
        GateCamp,
        Fight,
        Pod,
        Clear,
        Unknown
    }

    /// <summary>
    /// Intel Data, Represents a single line of intel data
    /// </summary>
    public class IntelData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        ///  Initializes a new instance of the <see cref="IntelData" /> class
        /// </summary>
        /// <param name="intelText">the raw line of text from the log file</param>
        public IntelData(string intelText, string intelChannel)
        {
            RawIntelString = intelText;
            // text will be in the format ?[ 2017.05.01 18:24:28 ] Charname > blah, blah blah
            int start = intelText.IndexOf('>') + 1;
            IntelString = intelText.Substring(start);
            IntelTime = DateTime.Now;
            Systems = new List<string>();
            ReportedShips = new List<string>();
            ReportedPilots = new List<string>();
            ReportedShipClasses = new List<IntelShipClass>();
            AlertIcons = new List<IntelAlertIconType>();
            ClearNotification = false;
            IntelChannel = intelChannel;
        }

        public bool ClearNotification { get; set; }

        public string IntelChannel { get; set; }

        /// <summary>
        /// Gets or sets the intel substring (minus time stamp and character name)
        /// </summary>
        public string IntelString { get; set; }

        /// <summary>
        /// Gets or sets the time we parsed the intel (note this is not in eve time)
        /// </summary>
        public DateTime IntelTime { get; set; }

        /// <summary>
        /// Gets or sets the raw line of text (incase we need to do anything else with it)
        /// </summary>
        public string RawIntelString { get; set; }

        /// <summary>
        /// Gets or sets the list of systems we matched when parsing this string
        /// </summary>
        public List<string> Systems { get; set; }

        /// <summary>
        /// Gets or sets the list of detected ship names mentioned in this intel line
        /// </summary>
        public List<string> ReportedShips { get; set; }

        /// <summary>
        /// Gets or sets the list of detected pilot names mentioned in this intel line
        /// </summary>
        public List<string> ReportedPilots { get; set; }

        /// <summary>
        /// Gets or sets detected ship classes mentioned in this intel line
        /// </summary>
        public List<IntelShipClass> ReportedShipClasses { get; set; }

        /// <summary>
        /// Gets or sets parsed intel markers used to render map alert badges
        /// </summary>
        public List<IntelAlertIconType> AlertIcons { get; set; }

        // public override string ToString() => "[" + IntelTime.ToString("HH:mm") + "] " + IntelString;
    }
}

