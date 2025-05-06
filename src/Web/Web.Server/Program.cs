using Common.Engine;
using Common.Engine.Notifications;
using Common.Engine.Surveys;
using Common.Engine.UsageStats;
using Entities.DB;
using Entities.DB.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Bot.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Web.Server.Bots;
using Web.Server.Bots.Dialogues;

namespace Web.Server;

public class Program
{
    public async static Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();


        var config = DependencyInjection.AddBotServices(builder.Services, builder.Configuration);

        if (config.AppInsightsConnectionString != null)
        {
            builder.Services.AddApplicationInsightsTelemetry((appInsightConfig) =>
                    appInsightConfig.ConnectionString = config.AppInsightsConnectionString
                );
        }


        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(ops =>
            {
                ops.Audience = config.WebAuthConfig.ApiAudience;
            }, o =>
            {
                o.ClientId = config.WebAuthConfig.ClientId;
                o.TenantId = config.WebAuthConfig.TenantId;
                o.ClientSecret = config.WebAuthConfig.ClientSecret;
                o.Authority = config.WebAuthConfig.Authority;
                o.Instance = "https://login.microsoftonline.com/";
            });

        // UsageStatsReport
        builder.Services.AddDbContext<DataContext>(options =>
            options.UseSqlServer(config.ConnectionStrings.SQL));
        builder.Services.AddDbContext<ProfilingContext>(options =>
            options.UseSqlServer(config.ConnectionStrings.SQL));
        builder.Services.AddScoped<ReportManager>();
        builder.Services.AddScoped<IUsageDataLoader, SqlUsageDataLoader>();

        // Bot services --->
        // Create the Bot Framework Adapter with error handling enabled.

        // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
        builder.Services.AddSingleton<IStorage, MemoryStorage>();

        // Create the User state. (Used in this bot's Dialog implementation.)
        builder.Services.AddSingleton<UserState>();

        // Create the Conversation state. (Used by the Dialog system itself.)
        builder.Services.AddSingleton<ConversationState>();

        // Bot diags
        builder.Services.AddSingleton<SurveyDialogue>();
        builder.Services.AddSingleton<StopBotheringMeDialogue>();

        builder.Services.AddSingleton<BotActionsHelper>();
        builder.Services.AddSingleton<BotAppInstallHelper>();

        builder.Services.AddTransient<IBot, CopilotFeedbackBot<SurveyDialogue>>();
        builder.Services.AddSingleton<IConversationResumeHandler, SurveyConversationResumeHandler>();

        var app = builder.Build();

#if DEBUG

        // Clear out the bot cache for dev testing. That way we get 1st time user experience every time.
        var graph = app.Services.GetRequiredService<Microsoft.Graph.GraphServiceClient>();
        var botCache = new BotConversationCache(graph, config);
        var allUsers = await botCache.GetCachedUsers();
        foreach (var user in allUsers)
        {
            await botCache.RemoveFromCache(user.RowKey);
        }
#endif

        // Ensure DB
        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        optionsBuilder.UseSqlServer(config.ConnectionStrings.SQL);

        using (var db = new DataContext(optionsBuilder.Options))
        {
            var logger = LoggerFactory.Create(c =>
            {
                c.AddConsole();

                if (config.AppInsightsConnectionString != null)
                {
                    c.AddApplicationInsights(configureTelemetryConfiguration: (appInsightConfig) =>
                        appInsightConfig.ConnectionString = config.AppInsightsConnectionString,
                        configureApplicationInsightsLoggerOptions: (options) => { }
                    );
                }
            }).CreateLogger("DB init");
            logger.LogInformation($"Using SQL connection-string: {config.ConnectionStrings.SQL}");
            await DbInitialiser.EnsureInitialised(db, logger, config.TestUPN, config.DevMode);

        }

        // https://learn.microsoft.com/en-us/visualstudio/javascript/tutorial-asp-net-core-with-react?view=vs-2022#publish-the-project
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
