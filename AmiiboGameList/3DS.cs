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

        private ushort idField;

        private string nameField;

        private string publisherField;

        private string regionField;

        private string languagesField;

        private string groupField;

        private ushort imagesizeField;

        private string serialField;

        private string titleidField;

        private string imgcrcField;

        private string filenameField;

        private string releasenameField;

        private uint trimmedsizeField;

        private string firmwareField;

        private byte typeField;

        private byte cardField;

        /// <remarks/>
        public ushort id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

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
        public string publisher
        {
            get
            {
                return this.publisherField;
            }
            set
            {
                this.publisherField = value;
            }
        }

        /// <remarks/>
        public string region
        {
            get
            {
                return this.regionField;
            }
            set
            {
                this.regionField = value;
            }
        }

        /// <remarks/>
        public string languages
        {
            get
            {
                return this.languagesField;
            }
            set
            {
                this.languagesField = value;
            }
        }

        /// <remarks/>
        public string group
        {
            get
            {
                return this.groupField;
            }
            set
            {
                this.groupField = value;
            }
        }

        /// <remarks/>
        public ushort imagesize
        {
            get
            {
                return this.imagesizeField;
            }
            set
            {
                this.imagesizeField = value;
            }
        }

        /// <remarks/>
        public string serial
        {
            get
            {
                return this.serialField;
            }
            set
            {
                this.serialField = value;
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

        /// <remarks/>
        public string imgcrc
        {
            get
            {
                return this.imgcrcField;
            }
            set
            {
                this.imgcrcField = value;
            }
        }

        /// <remarks/>
        public string filename
        {
            get
            {
                return this.filenameField;
            }
            set
            {
                this.filenameField = value;
            }
        }

        /// <remarks/>
        public string releasename
        {
            get
            {
                return this.releasenameField;
            }
            set
            {
                this.releasenameField = value;
            }
        }

        /// <remarks/>
        public uint trimmedsize
        {
            get
            {
                return this.trimmedsizeField;
            }
            set
            {
                this.trimmedsizeField = value;
            }
        }

        /// <remarks/>
        public string firmware
        {
            get
            {
                return this.firmwareField;
            }
            set
            {
                this.firmwareField = value;
            }
        }

        /// <remarks/>
        public byte type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        /// <remarks/>
        public byte card
        {
            get
            {
                return this.cardField;
            }
            set
            {
                this.cardField = value;
            }
        }
    }


}
