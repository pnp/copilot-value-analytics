using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Text.RegularExpressions;
using System.Web;

namespace ActivityImporter.Engine;

public class StringUtils
{
    public static string GetOnlineMeetingId(string copilotDocContextId, string userGuid)
    {
        var meetingIdFragment = StringUtils.GetMeetingIdFragmentFromMeetingThreadUrl(copilotDocContextId);
        if (meetingIdFragment == null)
        {
            throw new Exception($"Could not parse meeting id from url {copilotDocContextId}");
        }

        // Get/create meeting in DB
        var meetingId = $"{userGuid}_{meetingIdFragment}";
        return meetingId;
    }

    // Example copilotDocContextId in: https://contoso-my.sharepoint.com/personal/alex_contoso_onmicrosoft_com/_layouts/15/Doc.aspx?sourcedoc=%7B1F9103E2-34CF-4560-8458-BD3296201FA9%7D&file=Document19.docx&action=default&mobileredirect=true
    // Out: 1F9103E2-34CF-4560-8458-BD3296201FA9 - the drive item ID
    public static string? GetDriveItemIdFromSourceDocParam(string copilotDocContextId)
    {
        var uri = new Uri(copilotDocContextId);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var sourcedoc = query["sourcedoc"];
        if (string.IsNullOrEmpty(sourcedoc))
        {
            return null;
        }
        return sourcedoc.Replace("{", "").Replace("}", "");
    }


    /// <summary>
    /// Hack for https://github.com/dotnet/runtime/issues/21626
    /// </summary>
    public static bool IsValidAbsoluteUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    /// <summary>
    /// Returns the root site URL from a Copilot context ID.
    /// </summary>
    public static string? GetSiteUrl(string copilotDocContextId)
    {
        if (!IsValidAbsoluteUrl(copilotDocContextId))
        {
            return null;
        }
        var uri = new Uri(copilotDocContextId);

        const string SEP = "/";

        // Office Online URLs have a different format, so we need to handle them separately.
        // Example: https://contoso.sharepoint.com/:x:/r/sites/Invoices -> https://contoso.sharepoint.com/sites/Invoices
        const string OFFICE_SEGMENT = @":(\w):/r/";
        var officeOnlineMatch = Regex.Match(copilotDocContextId, OFFICE_SEGMENT);

        var urlToProcess = uri.AbsolutePath;
        if (officeOnlineMatch.Success)
        {
            urlToProcess = urlToProcess.Replace(officeOnlineMatch.Value, string.Empty);
        }

        
        // Remove SharePoint app pages (layouts)
        var absoluteUriMinusQueryAndAppPages = urlToProcess.Replace("/_layouts/15", "");

        var urlSegments = absoluteUriMinusQueryAndAppPages.Split(SEP, StringSplitOptions.RemoveEmptyEntries).ToList();

        var siteRelativeUrl = string.Empty;
        if (urlSegments.Count < 2)
        {
            if (urlSegments.Count == 0)
            {
                // URL is literally just a root SC URL like https://test.sharepoint.com
                return null;
            }
        }
        else
        {
            siteRelativeUrl = "/" + string.Join(SEP, urlSegments.Take(2).ToArray());
        }

        var siteUrl = $"{uri.Scheme}://{uri.DnsSafeHost}{siteRelativeUrl}";
        return siteUrl;
    }
    public static bool IsMySiteUrl(string copilotDocContextId)
    {
        return copilotDocContextId.Contains("-my.sharepoint.com");
    }

    /// <summary>
    /// To form path required by https://learn.microsoft.com/en-us/graph/api/site-get
    /// From "https://test.sharepoint.com/sites/test" returns "test.sharepoint.com:/sites/test"
    /// Or, from root site "https://test.sharepoint.com" returns "root"
    /// Or null if none of the above
    /// </summary>
    public static string? GetHostAndSiteRelativeUrl(string siteRootUrl)
    {
        const string SEP = "/";
        var urlSegments = siteRootUrl.Split(SEP, StringSplitOptions.RemoveEmptyEntries);
        if (urlSegments.Length < 4)
        {
            if (urlSegments.Length == 2 && urlSegments[1].ToLower().Contains("sharepoint.com"))
                return "root"; // URL is literally just a root SC URL like https://test.sharepoint.com
            else return null;
        }

        var host = urlSegments[1];
        var siteRelativeUrl = string.Join(SEP, urlSegments.Skip(2).ToArray());

        return $"{host}:/{siteRelativeUrl}";
    }

    /// <summary>
    /// Example: https://microsoft.teams.com/threads/19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2 -> 
    ///          19:meeting_NDQ4MGRhYjgtMzc5MS00ZWMxLWJiZjEtOTIxZmM5Mzg3ZGFi@thread.v2
    /// </summary>
    public static string? GetMeetingIdFragmentFromMeetingThreadUrl(string copilotDocContextId)
    {
        var decodedUrl = HttpUtility.UrlDecode(copilotDocContextId);
        const string START = "19:meeting_";
        var start = decodedUrl.IndexOf(START);
        if (start == -1)
        {
            return null;
        }

        var stringStart = decodedUrl.Substring(start, decodedUrl.Length - start);

        // Find "v2" 
        const string END = "@thread.v2";
        var end = stringStart.IndexOf(END, start);
        if (end == -1)
        {
            return null;
        }

        var meetingId = stringStart.Substring(0, end + END.Length);
        return meetingId;
    }


    public static string EnsureMaxLength(string? potentiallyLongString, int maxLength)
    {
        if (string.IsNullOrEmpty(potentiallyLongString))
        {
            return string.Empty;
        }
        const string END = "...";

        if (maxLength < END.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        if (potentiallyLongString.Length > maxLength)
        {
            return potentiallyLongString.Substring(0, maxLength - END.Length) + END;
        }
        else
        {
            return potentiallyLongString;
        }
    }
}
