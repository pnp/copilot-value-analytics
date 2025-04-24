using Common.Engine.Config;
using Common.Engine.Surveys;
using Entities.DB;
using Entities.DB.DbContexts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Functions;

public class TimerFunctions(ILogger<TimerFunctions> tracer, ILogger<SurveyManager> loggerSM, ISurveyManagerDataLoader surveyManagerDataLoader, 
    ISurveyEventsProcessor surveyProcessor, TeamsAppConfig config, DataContext dataContext)
{
    const string CRON_TIME_HOURLY = "0 0 * * * *";       // Every hour for debugging
    const string CRON_TIME_DAILY = "0 0 0 * * *";       // Every day at midnight

    /// <summary>
    /// Trigger notifications. Find people that've done copilot things, and prompt for survey
    /// </summary>
    [Function(nameof(TriggerNotifications))]
    public async Task TriggerNotifications([TimerTrigger(CRON_TIME_HOURLY)] TimerJobRefreshInfo timerInfo)
    {
        tracer.LogInformation($"{nameof(TriggerNotifications)} function executed at: {DateTime.Now}");
        var sm = new SurveyManager(surveyManagerDataLoader, surveyProcessor, loggerSM);
        await sm.FindAndProcessNewSurveyEventsAllUsers();
        tracer.LogInformation($"Next timer schedule at: {timerInfo.ScheduleStatus?.Next}");
    }

    [Function(nameof(GenerateFakeActivityForAllUsers))]
    public async Task GenerateFakeActivityForAllUsers([TimerTrigger(CRON_TIME_DAILY)] TimerJobRefreshInfo timerInfo)
    {
        if (config.DevMode)
        {
            tracer.LogInformation($"{nameof(GenerateFakeActivityForAllUsers)} function executed at: {DateTime.Now}");
            await FakeDataGen.GenerateFakeActivityForAllUsers(dataContext, loggerSM);
            tracer.LogInformation($"Next timer schedule at: {timerInfo.ScheduleStatus?.Next}");
        }
        else
        {
            tracer.LogWarning($"{nameof(GenerateFakeActivityForAllUsers)} function executed in non-dev mode, skipping.");
        }
    }
}

public class TimerJobRefreshInfo
{
    public TimerJobRefreshScheduleStatus? ScheduleStatus { get; set; } = null!;
    public bool IsPastDue { get; set; }
}

public class TimerJobRefreshScheduleStatus
{
    public DateTime Last { get; set; }
    public DateTime Next { get; set; }
    public DateTime LastUpdated { get; set; }
}
