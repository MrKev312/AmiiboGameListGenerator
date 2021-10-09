namespace AmiiboGameList
{

    // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlRoot("releases")]
    public partial class DSreleases
    {

        private DSreleasesRelease[] releaseField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("release")]
        public DSreleasesRelease[] release
        {
            get
            {
                return this.releaseField;
            }
            set
            {
                this.releaseField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class DSreleasesRelease
    {
        private string nameField;

        private string titleidField;

        /// <remarks/>
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        public string titleid
        {
            get
            {
                return this.titleidField;
            }
            set
            {
                this.titleidField = value;
            }
        }
    }
}
