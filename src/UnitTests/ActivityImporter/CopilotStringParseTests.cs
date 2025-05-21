using ActivityImporter.Engine;

namespace UnitTests.ActivityImporter;

[TestClass]
public class CopilotStringParseTests : AbstractTest
{

    [TestMethod]
    public void IsValidUrlTests()
    {
        Assert.IsFalse(StringUtils.IsValidAbsoluteUrl(null));
        Assert.IsFalse(StringUtils.IsValidAbsoluteUrl(""));
        Assert.IsFalse(StringUtils.IsValidAbsoluteUrl("asdfasdf"));
        Assert.IsFalse(StringUtils.IsValidAbsoluteUrl("http://"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("http://asdfasdf"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("http://asdfasdf.com"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("http://asdfasdf.com/"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("http://asdfasdf.com/asdfasdf"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("http://asdfasdf.com/asdfasdf/"));
        Assert.IsTrue(StringUtils.IsValidAbsoluteUrl("https://contoso.sharepoint.com/sites/site1/_layouts/15/Doc.aspx?sourcedoc=%7BF2CB77E7-186C-4F9A-B949-FA078F48AA53%7D&file=RD%20Consejer%C3%ADa%20en%20el%20exterior%20v.1.docx&action=default&mobileredirect=true"));
    }

    [TestMethod]
    public void GetMeetingIdFragmentFromMeetingThreadUrl()
    {
        // Test with a valid URL
        Assert.AreEqual("19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2",
            StringUtils.GetMeetingIdFragmentFromMeetingThreadUrl("https://microsoft.teams.com/threads/19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2"));

        // Test with a URL that has extra segments and is HTTP encoded
        Assert.AreEqual("19:meeting_ODkxODBiMDEtOTk2Yi00M2RjLTgxOWItMWE0NmEwNjI4MWFm@thread.v2",
            StringUtils.GetMeetingIdFragmentFromMeetingThreadUrl("https://teams.microsoft.com/l/meetup-join/19%3ameeting_ODkxODBiMDEtOTk2Yi00M2RjLTgxOWItMWE0NmEwNjI4MWFm%40thread.v2/0?context=%7b%22Tid%22%3a%2250280b35-1c88-4a64-aaef-95357aad7204%22%2c%22Oid%22%3a%22981208f6-9303-4e8b-8a09-e8e2a9473b63%22%7d"));

        // Test with a URL that does not contain a meeting ID
        Assert.IsNull(StringUtils.GetMeetingIdFragmentFromMeetingThreadUrl("https://microsoft.teams.com/"));
    }

    [TestMethod]
    public void GetSiteUrlTests()
    {
        // My Site
        Assert.AreEqual("https://test.sharepoint.com/sites/test",
            StringUtils.GetSiteUrl("https://test.sharepoint.com/sites/test/Shared%20Documents/General/test.docx"));

        Assert.AreEqual("https://test.sharepoint.com/sites/test",
            StringUtils.GetSiteUrl("https://test.sharepoint.com/sites/test"));

        // If we're not passing a doc in the root-site, we should get the root site back
        Assert.IsNull(StringUtils.GetSiteUrl("https://test.sharepoint.com"));
        Assert.IsNull(StringUtils.GetSiteUrl("https://test.sharepoint.com/"));

        Assert.AreEqual("https://m365cp123890-my.sharepoint.com/personal/sambetts_m365cp123890_onmicrosoft_com",
            StringUtils.GetSiteUrl(
            "https://m365cp123890-my.sharepoint.com/personal/sambetts_m365cp123890_onmicrosoft_com/_layouts/15/Doc.aspx?sourcedoc=%7B0D86F64F-8435-430C-8979-FF46C00F7ACB%7D&file=Presentation.pptx&action=edit&mobileredirect=true")
            );

        // Root site doc
        Assert.AreEqual("https://m365cp123890.sharepoint.com",
            StringUtils.GetSiteUrl(
            "https://m365cp123890.sharepoint.com/_layouts/15/Doc.aspx?sourcedoc=%7B0D86F64F-8435-430C-8979-FF46C00F7ACB%7D&file=Presentation.pptx&action=edit&mobileredirect=true")
            );
        Assert.AreEqual("https://m365cp123890.sharepoint.com",
            StringUtils.GetSiteUrl("https://m365cp123890.sharepoint.com/Doc.docx"));

        // Non-root SP site doc
        Assert.AreEqual("https://m365x35901285.sharepoint.com/sites/Invoices",
            StringUtils.GetSiteUrl("https://m365x35901285.sharepoint.com/:x:/r/sites/Invoices/_layouts/15/Doc.aspx?sourcedoc=%7BF4372559-F4D8-44DD-BF9F-38C41BD8DBA0%7D&file=Invoices%20Scanned.xlsx&action=default&mobileredirect=true"));
    }

    [TestMethod]
    public void GetHostAndSiteRelativeUrl()
    {
        var subSiteResult = StringUtils.GetHostAndSiteRelativeUrl("https://test.sharepoint.com/sites/test");
        Assert.AreEqual("test.sharepoint.com:/sites/test", subSiteResult);

        var rootSiteResult = StringUtils.GetHostAndSiteRelativeUrl("https://test.sharepoint.com/");
        Assert.AreEqual("root", rootSiteResult);

        Assert.IsNull(StringUtils.GetHostAndSiteRelativeUrl("https://test.com/"));
    }

    [TestMethod]
    public void GetDriveItemId()
    {
        Assert.IsNull(StringUtils.GetDriveItemIdFromSourceDocParam("https://test.sharepoint.com/sites/test"));
        Assert.AreEqual(StringUtils.GetDriveItemIdFromSourceDocParam(
            "https://m365cp123890-my.sharepoint.com/personal/sambetts_m365cp123890_onmicrosoft_com/_layouts/15/Doc.aspx?sourcedoc=%7B0D86F64F-8435-430C-8979-FF46C00F7ACB%7D&file=Presentation.pptx&action=edit&mobileredirect=true"),
            "0D86F64F-8435-430C-8979-FF46C00F7ACB");
    }
}
