using System.Xml.Serialization;

namespace AmiiboGameList.Models.PlatformSpecific;

[Serializable]
[XmlRoot("releases")]
public class ThreeDsReleaseList
{
    [XmlElement("release")]
    public ThreeDsRelease[] Releases { get; set; }
}

[Serializable]
public class ThreeDsRelease
{
    [XmlElement("name")]
    public string Name { get; set; }

    [XmlElement("titleid")]
    public string TitleId { get; set; }
}