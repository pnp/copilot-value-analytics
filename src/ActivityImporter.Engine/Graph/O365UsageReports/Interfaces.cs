using ActivityImporter.Engine.Graph.O365UsageReports.Models;

namespace ActivityImporter.Engine.Graph.O365UsageReports;


public interface IUserActivityLoader
{
    Task<List<TAbstractActivityRecord>> LoadReport<TAbstractActivityRecord>(DateTime dt, string reportGraphURL) where TAbstractActivityRecord : AbstractActivityRecord;
}
