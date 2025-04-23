using Azure.Identity;
using Common.Engine.Config;
using Common.Engine.Notifications;
using Common.Engine.Surveys;
using Entities.DB.DbContexts;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace Common.Engine;

public class DependencyInjection
{

    public static BotConfig AddBotServices(IServiceCollection services, IConfiguration configuration)
    {
        var config = new BotConfig(configuration);
        services.AddSingleton(config);
        services.AddSingleton<AppConfig>(config);
        services.AddSingleton<TeamsAppConfig>(config);

        AddCommon(services, config);

        return config;
    }
    public static TeamsAppConfig AddTeamsAppServices(IServiceCollection services, IConfiguration configuration)
    {
        var config = new TeamsAppConfig(configuration);
        services.AddSingleton(config);
        services.AddSingleton<AppConfig>(config);


        AddCommon(services, config);

        return config;
    }

    public static void AddCommon(IServiceCollection services, AppConfig config)
    {

        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        var options = new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud };
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var clientSecretCredential = new ClientSecretCredential(config.ImportAuthConfig.TenantId, config.ImportAuthConfig.ClientId, config.ImportAuthConfig.ClientSecret, options);
        services.AddSingleton(sp => new GraphServiceClient(clientSecretCredential, scopes));

        // Create the Bot Framework Authentication to be used with the Bot Adapter.
        services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

        services.AddSingleton<IBotConvoResumeManager, BotConvoResumeManager>();
        services.AddSingleton<BotConversationCache>();
        services.AddSingleton<ISurveyEventsProcessor, TeamsBotSendSurveyProcessor>();

        services.AddTransient<SurveyManager>();
        services.AddTransient<ISurveyManagerDataLoader, SqlSurveyManagerDataLoader>();

        services.AddDbContext<DataContext>(options => options
            .UseSqlServer(config.ConnectionStrings.SQL)
        );
    }
}
