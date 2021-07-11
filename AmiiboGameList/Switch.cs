namespace AmiiboGameList
{
    /// <summary>Class containing an array of 3DS games</summary>
    [System.Serializable()]
    [System.ComponentModel.DesignerCategory("code")]
    [System.Xml.Serialization.XmlRoot("releases")]
    public class Switchreleases
    {

        private SwitchreleasesRelease[] releaseField;

        /// <summary>Gets or sets the games.</summary>
        /// <value>The release array.</value>
        [System.Xml.Serialization.XmlElement("release")]
        public SwitchreleasesRelease[] release
        {
            get => releaseField; set => releaseField = value;
        }
    }

    /// <summary>Class for each Switch game.</summary>
    [System.Serializable()]
    [System.ComponentModel.DesignerCategory("code")]
    [System.Xml.Serialization.XmlType(AnonymousType = true)]
    public class SwitchreleasesRelease
    {

        private ushort idField;

        private string nameField;

        private string titleidField;

        /// <summary>Gets or sets the identifier of the game.</summary>
        /// <value>The identifier.</value>
        public ushort id
        {
            get => idField; set => idField = value;
        }

        /// <summary>Gets or sets the name.</summary>
        /// <value>The name of the game.</value>
        public string name
        {
            get => nameField; set => nameField = value;
        }

        /// <summary>Gets or sets the titleid.</summary>
        /// <value>The titleid of the game.</value>
        public string titleid
        {
            get => titleidField; set => titleidField = value;
        }
    }


}
