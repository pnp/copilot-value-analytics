﻿using AdaptiveCards;
using Common.DataUtils;
using Common.Engine.Bot;
using Entities.DB.Entities.AuditLog;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace Common.Engine.Surveys;

/// <summary>
/// Base implementation for any of the adaptive cards sent
/// </summary>
public abstract class BaseAdaptiveCard
{

    public abstract string GetCardContent();

    protected string ReadResource(string resourcePath)
    {
        return ResourceUtils.ReadResource(Assembly.GetExecutingAssembly(), resourcePath);
    }
    public Attachment GetCardAttachment()
    {
        dynamic cardJson = JsonConvert.DeserializeObject(GetCardContent()) ?? new { };

        return new Attachment
        {
            ContentType = AdaptiveCard.ContentType,
            Content = cardJson,
        };
    }
}

public abstract class SurveyCard : BaseAdaptiveCard
{
    public SurveyCard()
    {
        // Replace in JSON the variables with the actual values
        CardBody = ReadResource(BotConstants.SurveyOverrallSatisfactionCommonBody)
            .Replace(GetJsonVarName(nameof(BotConstants.SurveyAnswerRating1)), BotConstants.SurveyAnswerRating1)
            .Replace(GetJsonVarName(nameof(BotConstants.SurveyAnswerRating2)), BotConstants.SurveyAnswerRating2)
            .Replace(GetJsonVarName(nameof(BotConstants.SurveyAnswerRating3)), BotConstants.SurveyAnswerRating3)
            .Replace(GetJsonVarName(nameof(BotConstants.SurveyAnswerRating4)), BotConstants.SurveyAnswerRating4)
            .Replace(GetJsonVarName(nameof(BotConstants.SurveyAnswerRating5)), BotConstants.SurveyAnswerRating5)
            .Replace(BotConstants.FIELD_NAME_SURVEY_STOP, BotConstants.SurveyAnswerStop);
    }
    string GetJsonVarName(string name) => "${" + name + "}";

    public string CardBody { get; set; }

    protected string GetMergedCardContent(string cardSpecific)
    {
        var cardBody = JObject.Parse(cardSpecific);
        cardBody.Merge(JObject.Parse(CardBody), new JsonMergeSettings
        {
            // Avoid duplicates
            MergeArrayHandling = MergeArrayHandling.Union
        });

        return cardBody.ToString();
    }
}


public class CopilotTeamsActionSurveyCard : SurveyCard
{
    private readonly CopilotEventMetadataMeeting _baseCopilotEvent;

    public CopilotTeamsActionSurveyCard(CopilotEventMetadataMeeting baseCopilotEvent)
    {
        _baseCopilotEvent = baseCopilotEvent;
    }

    public override string GetCardContent()
    {
        var json = ReadResource(BotConstants.CardFileNameCopilotTeamsActionSurvey);
        json = json.Replace(BotConstants.FIELD_NAME_RESOURCE_NAME, _baseCopilotEvent.GetEventDescription());
        return GetMergedCardContent(json);
    }
}

public class CopilotFileActionSurveyCard : SurveyCard
{
    private readonly CopilotEventMetadataFile _baseCopilotEvent;

    public CopilotFileActionSurveyCard(CopilotEventMetadataFile baseCopilotEvent)
    {
        _baseCopilotEvent = baseCopilotEvent;
    }

    public override string GetCardContent()
    {
        var json = ReadResource(BotConstants.CardFileNameCopilotFileActionSurvey);
        json = json.Replace(BotConstants.FIELD_NAME_RESOURCE_NAME, _baseCopilotEvent.GetEventDescription());
        return GetMergedCardContent(json);
    }
}

public class SurveyNotForSpecificAction : SurveyCard
{
    public SurveyNotForSpecificAction()
    {
    }

    public override string GetCardContent()
    {
        var json = ReadResource(BotConstants.CardFileNameSurveyNoActionCard);
        return GetMergedCardContent(json);
    }
}
