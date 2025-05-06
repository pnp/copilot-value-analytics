using Common.Engine;
using Common.Engine.Bot;
using Common.Engine.Bot.Cards;
using Common.Engine.Config;
using Common.Engine.Notifications;
using Common.Engine.Surveys;
using Common.Engine.Surveys.Model;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Graph;
using System.Text.Json;
using Web.Server.Bots.Dialogues.Abstract;

namespace Web.Server.Bots.Dialogues;


/// <summary>
/// Entrypoint to all new conversations
/// </summary>
public class SurveyDialogue : StoppableDialogue
{
    private readonly ILogger<SurveyDialogue> _tracer;
    private readonly BotActionsHelper _botActionsHelper;
    private readonly UserState _userState;
    private readonly IConversationResumeHandler _conversationResumeHandler;

    const string CACHE_NAME_CONVO_STATE = "CACHE_NAME_CONVO_STATE";

    const string BTN_SEND_SURVEY = "Go on then";

    /// <summary>
    /// Setup dialogue flow
    /// </summary>
    public SurveyDialogue(StopBotheringMeDialogue stopBotheringMeDialogue, BotConfig configuration, BotConversationCache botConversationCache, ILogger<SurveyDialogue> tracer,
        BotActionsHelper botActionsHelper,
        UserState userState, IServiceProvider services, GraphServiceClient graphServiceClient, IConversationResumeHandler conversationResumeHandler)
        : base(nameof(SurveyDialogue), botConversationCache, configuration, services, graphServiceClient)
    {
        _tracer = tracer;
        _botActionsHelper = botActionsHelper;
        _userState = userState;
        _conversationResumeHandler = conversationResumeHandler;
        AddDialog(new TextPrompt(nameof(TextPrompt)));
        AddDialog(stopBotheringMeDialogue);

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog),
        [
            NewChat,
            SendSurveyOrNot,
            ProcessSurveyResponse,
            ProcessFollowUp
        ]));
        AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
        InitialDialogId = nameof(WaterfallDialog);
    }

    /// <summary>
    /// Main entry-point for bot new chat. User is either responding to the intro card or has said something to the bot.
    /// </summary>
    private async Task<DialogTurnResult> NewChat(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get/set state
        var convoState = await GetConvoStateAsync(stepContext.Context);

        // Check if user wants to stop the bot from bothering them
        var cancel = await InterruptAsync(stepContext, cancellationToken);
        if (cancel != null)
            return cancel;

        // Get cached user and conversation. We do it here too in case the conversation cache has been cleared.
        var botUser = await BotUserUtils.GetBotUserAsync(stepContext.Context, _botConfig, _graphServiceClient);
        var cachedUserAndConversation = await GetCachedUser(botUser);
        if (cachedUserAndConversation == null || cachedUserAndConversation.UserPrincipalName == null)
        {
            // This may be the first time we've met this user if they've spoken to the bot, but the conversation cache has been cleared.
            // Add them to the cache and try again.
            await _botConversationCache.AddConversationReferenceToCache(stepContext.Context.Activity, botUser);
            cachedUserAndConversation = await GetCachedUser(botUser);

            // Now we've tried to add their user to the conversation cache, check again
            if (cachedUserAndConversation == null || cachedUserAndConversation.UserPrincipalName == null)
            {
                // Should never happen. End the conversation.
                await SendMsg(stepContext.Context!,
                    "Hi! It's the copilot feedback bot. For some reason I can't find your login name in our chat history, which is very odd. Are you a guest? " +
                    "It looks like I can't do much without this so, bye!");
                return await stepContext.EndDialogAsync();
            }
        }

        var prevActionText = stepContext.Context?.Activity?.Text;

        // Figure out why we're here. 
        var surveyInitialResponse = GetSurveyInitialResponseFrom(stepContext.Context?.Activity?.Text);
        if (surveyInitialResponse != null)
        {
            // User has responded to the initial survey card probably from the bot new thread event, so we can skip the intro card
            convoState.LastResponse = surveyInitialResponse;
            return await stepContext.NextAsync();
        }

        if (prevActionText == "{}" || prevActionText == "Start Survey")              // In teams, this is empty JSon for some reason, unlike in Bot Framework SDK client
        {
            // User has started the dialogue with "start survey", probably from the intro card on bot 1st join a new thread.
            return await stepContext.NextAsync(new FoundChoice() { Value = BTN_SEND_SURVEY });
        }
        else
        {
            // We're here because the user said something to the bot outside this dialogue flow, and it wasn't a response to the intro card.
            // Probably because they said "sup" or something to us, randomly
            await SendMsg(stepContext.Context!, "Oh heeeyyy, you're back! I assume you want another chance to rate copilot if you're talking to me...");

            var nextActionAndResumeCard = await _conversationResumeHandler.GetProactiveConversationResumeConversationCard(cachedUserAndConversation.UserPrincipalName);
            return await PromptWithCard(stepContext, nextActionAndResumeCard.Item2);
        }
    }

    /// <summary>
    /// User has chosen to fill out a survey or not
    /// </summary>
    private async Task<DialogTurnResult> SendSurveyOrNot(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get/set state
        var convoState = await GetConvoStateAsync(stepContext.Context);

        if (convoState.LastResponse != null)
        {
            // User has responded to the initial survey card outside of the dialogue (bot new thread event probably).
            return await stepContext.NextAsync(convoState.LastResponse);
        }
        else if (stepContext.Result is string resultString)
        {
            // Response survey sent by previous step
            var suveyResponse = GetSurveyInitialResponseFrom(resultString);
            if (suveyResponse != null)
            {
                return await stepContext.NextAsync(stepContext.Result);
            }
        }
        else if (stepContext.Result is FoundChoice response)
        {
            if (response.Value == BTN_SEND_SURVEY)
            {
                var botUser = await BotUserUtils.GetBotUserAsync(stepContext.Context, _botConfig, _graphServiceClient);
                var chatUserAndConversation = await GetCachedUser(botUser);
                if (chatUserAndConversation == null || chatUserAndConversation.UserPrincipalName == null)
                {
                    // We really shouldn't get here, but just in case...
                    await SendMsg(stepContext.Context, "Oops, can't send you a survey if I don't know who you are. Sorry about that...");
                    return await stepContext.EndDialogAsync();
                }

                var (nextCopilotEvent, surveyCard) = await _conversationResumeHandler.GetProactiveConversationResumeConversationCard(chatUserAndConversation.UserPrincipalName);

                if (nextCopilotEvent != null)
                {
                    // Remember copilot action user is being surveyed for
                    convoState.CopilotEventForSurveyResult = nextCopilotEvent;
                }

                return await PromptWithCard(stepContext, surveyCard);
            }
            else
            {
                await SendMsg(stepContext.Context, "No worries. Ping me if you change your mind later.");
                return await stepContext.EndDialogAsync();
            }
        }

        // We're here because we don't know what the user is responding to
        await SendMsg(stepContext.Context, "Oops, I wasn't expecting that response. Let's pretend this didn't happen...");
        return await stepContext.EndDialogAsync();

    }

    /// <summary>
    /// User responds to initial survey card, or we're in a loop showing custom survey pages
    /// </summary>
    private async Task<DialogTurnResult> ProcessSurveyResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get/set state
        var convoState = await GetConvoStateAsync(stepContext.Context);

        // Save survey data?
        var botUser = await BotUserUtils.GetBotUserAsync(stepContext.Context, _botConfig, _graphServiceClient);
        SurveyPage? surveyPage = null;
        var chatUserAndConvo = await GetCachedUser(botUser);
        if (chatUserAndConvo == null || chatUserAndConvo.UserPrincipalName == null)
            await SendMsg(stepContext.Context, "Oops, can't report feedback for anonymous users. Your login is needed even if we don't report on it. Thanks for letting me know anyway.");
        else
        {
            BaseAdaptiveCard? responseCard = null;
            var endDiagWithResponseCard = false;
            await GetSurveyManagerService(async surveyManager =>
            {
                var initialSurveyRatingResponse = JsonSerializer.Deserialize<InitialSurveyResponse>(stepContext.Context.Activity.Text);
                if (initialSurveyRatingResponse?.Response != null)
                {
                    // We're here because the user has responded to the initial overral rating survey card

                    // Start custom surveys flow
                    surveyPage = await surveyManager.GetSurveyPage(0);
                    if (surveyPage != null)
                    {
                        surveyPage.SurveyPageCommonAdaptiveCardTemplateJson = Utils.ReadResource(BotConstants.SurveyCustomPageCommon);
                    }
                    else
                    {
                        await SendMsg(stepContext.Context, "It looks like there's no more questions to ask.");
                        endDiagWithResponseCard = true;
                    }
                    if (convoState.CopilotEventForSurveyResult != null)
                    {
                        // Log survey result for specific copilot event
                        try
                        {
                            convoState.SurveyIdUpdatedOrCreated = await surveyManager.Loader.UpdateSurveyResultWithInitialScore(convoState.CopilotEventForSurveyResult.RelatedChat.AuditEvent, initialSurveyRatingResponse.Response.ScoreGiven);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            _tracer.LogWarning($"Survey record doesn't exist for event {convoState.CopilotEventForSurveyResult.RelatedChat.AuditEvent.Id} to update with score.");
                            // Survey record doesn't exist for this event, for some reason. Ignore and end the conversation.
                        }
                        convoState.CopilotEventForSurveyResult = null;
                    }
                    else
                    {
                        // Log survey result for general survey
                        convoState.SurveyIdUpdatedOrCreated = await surveyManager.Loader.LogDisconnectedSurveyResultWithInitialScore(initialSurveyRatingResponse.Response.ScoreGiven, chatUserAndConvo.UserPrincipalName);
                    }

                    responseCard = new BotReactionCard(initialSurveyRatingResponse.Response.ReplyToFeedback, initialSurveyRatingResponse.Response.IsHappy, surveyPage);

                    // Cycle through custom survey pages, starting at 0
                    convoState.NextCopilotCustomPage = 0;
                }
                else if (initialSurveyRatingResponse?.Response == null && convoState.NextCopilotCustomPage.HasValue)
                {
                    // We're here because we're on a loop through custom survey pages. Get next page and send
                    surveyPage = await surveyManager.GetSurveyPage(convoState.NextCopilotCustomPage.Value);
                    if (surveyPage != null)
                    {
                        surveyPage.SurveyPageCommonAdaptiveCardTemplateJson = Utils.ReadResource(BotConstants.SurveyCustomPageCommon);
                        responseCard = new CustomSurveyPageCard(surveyPage);
                    }
                    else
                    {
                        // Send "all done" card
                        endDiagWithResponseCard = true;
                        responseCard = null;
                    }
                }
            });


            // Send reaction card to initial survey with follow-up form for more details
            if (responseCard != null)
            {
                if (!endDiagWithResponseCard)
                {
                    // Show card and wait for response
                    return await PromptWithCard(stepContext, responseCard);
                }
                else
                {
                    // Just send card
                    await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(responseCard.GetCardAttachment()), cancellationToken);
                }
            }
        }

        // If we're here, we must be done
        await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(new BotDiagFinishedCard().GetCardAttachment()), cancellationToken);
        return await stepContext.EndDialogAsync();
    }

    private async Task<DialogTurnResult> ProcessFollowUp(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        // Get/set state
        var convoState = await GetConvoStateAsync(stepContext.Context);

        var result = stepContext.Context.Activity.Text;
        var botUser = await BotUserUtils.GetBotUserAsync(stepContext.Context, _botConfig, _graphServiceClient);

        var chatUserAndConvo = await GetCachedUser(botUser);

        if (chatUserAndConvo?.UserPrincipalName != null)
        {
            var surveyResponse = new SurveyPageUserResponse(result, chatUserAndConvo.UserPrincipalName);

            if (convoState.SurveyIdUpdatedOrCreated > 0 && surveyResponse.IsValid)
            {
                var goAroundAgain = false;
                await GetSurveyManagerService(async surveyManager =>
                {
                    // Add follow-up survey data
                    await surveyManager.SaveCustomSurveyResponse(surveyResponse, convoState.SurveyIdUpdatedOrCreated.Value);

                    // Get next survey page
                    var nextPageNumber = (convoState.NextCopilotCustomPage ?? 0) + 1;
                    var surveyPage = await surveyManager.GetSurveyPage(nextPageNumber);
                    if (surveyPage != null)
                    {
                        convoState.NextCopilotCustomPage = nextPageNumber;
                        goAroundAgain = true;
                    }
                    else
                    {
                        // Sign off & say thanks
                        var allDoneCard = new BotDiagFinishedCard().GetCardAttachment();
                        goAroundAgain = false;
                        await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(allDoneCard), cancellationToken);
                    }
                });

                if (goAroundAgain)
                {
                    // "Thanks for that. Let's move on to the next question..." - diag will send next card on previous step
                    return await stepContext.ReplaceDialogAsync(nameof(WaterfallDialog));
                }
            }
            else
            {
                await SendMsg(stepContext.Context, "Oops, didn't get that for some reason. Oh well; everything else seemed to work. Thanks & byee!");
            }

        }
        else
        {
            await SendMsg(stepContext.Context, "Oops, can't report feedback for anonymous users. Your login is needed even if we don't report on it. Thanks for letting me know anyway.");
        }

        return await stepContext.EndDialogAsync();
    }
    async Task<SurveyDialogueConvoState> GetConvoStateAsync(ITurnContext context)
    {
        var convoStateProp = _userState.CreateProperty<SurveyDialogueConvoState>(CACHE_NAME_CONVO_STATE);
        var convoState = await convoStateProp.GetAsync(context);
        if (convoState == null)
        {
            convoState = new SurveyDialogueConvoState();
            await convoStateProp.SetAsync(context, convoState);
        }
        return convoState;
    }

}
