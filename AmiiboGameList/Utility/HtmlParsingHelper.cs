using HtmlAgilityPack;

namespace AmiiboGameList.Utility;

public static class HtmlParsingHelper
{
    public static string GetDirectInnerText(this HtmlNode node)
    {
        if (node == null)
            return string.Empty;
        System.Text.StringBuilder sb = new();
        foreach (HtmlNode child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                sb.Append(HtmlEntity.DeEntitize(child.InnerText));
            }
        }

        return sb.ToString();
    }
}