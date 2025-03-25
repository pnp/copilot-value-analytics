# Copilot Value Analytics
Onboarding users that would most benefit from copilot and measure value once deployed.

## Onboarding
Identify users and groups for copilot adoption with advanced analytics.
[Usage report example](imgs/usage_report.jpg)

Tailor adoption plans per user demographic based on hard usage data.

## Measure Copilot Feedback
For users that use copilot, get real feedback from your organisations users about how copilot is helping, with "Copilot Feedback Bot".

![Copilot Feedback Bot says 'Hiii!'](imgs/bot-salute-small.png)

This is a bot that collects user feedback about copilot use within M365 so you can see exactly what your users think about it. Do your users love copilot? Does it really save them time? Doing what actions? Do some groups of people like it more than others?

More importantly: is it worth the investment? This is the question this system seeks to quantify in more detail than anything else. 

![User Satisfaction Dashboard](imgs/report1.png)

Copilot events are detected automatically, and each user that used copilot will be surveyed about how that interaction went through a Teams bot. 

![Teams Prompt from Bot](imgs/botconvo.jpg)

User responses are stored in a database and visualised in Power BI.

![User Satisfaction Report](imgs/report2.png)
This is an automated way to get real feedback from your users about what they think of copilot in Office 365. 

All data is stored in your own SQL Server.

## Prerequisites
Check the [prerequisites](docs/prereqs.md) document to see everything you'll need to get the system running. 

## Usage
**Quick version**: run the activity import and let the bot do the rest. The functions app will look for users that have used copilot and then survey them.

When deployed, a web-job will read the Office 365 Activity API to determine copilot interactions - whom, and what. It runs automatically once a day. 

A functions app will then find users that have new activity and start a new conversation with them to ask if they could review the activity in question with copilot. 

Once surveyed they won't be surveyed again for that specific interaction (even if they don't answer). 

If the user doesn't have the bot installed in Teams already, it'll be installed automatically.

If the user says anything to the bot outside the normal dialogue flow, the assumption is they want to leave a copilot review. Surveys don't necessarily have to correlate to a specific interaction, the user can just leave general feedback too. 

### Testing the Bot
Copilot activity can be faked by sending a **POST** to ```/api/Triggers/GenerateFakeActivityFor?upn={**UPN**}``` (no body required). This will create a fake meeting & file activity for that user.

The bot can also be forced to check and send for pending surveys to be sent everyone with a **POST** to ```/api/Triggers/SendSurveys``` (no body required).

To test-install the bot for a user and force a "hi!" from the bot, independently of if they have pending surveys or not, **POST** to ```/api/Triggers/InstallBotForUser?upn={**UPN**}``` (no body required).

Otherwise, the longer version is to use copilot in a file or team meeting, wait a few minutes, run (manually or automatically) the import job and then wait for the timer function to run the "_FindAndProcessNewSurveyEventsAllUsers_" method.

## How Does it Work?
Easy. There's a web-job/process that reads O365 audit events for SharePoint and copilot activity. We read SharePoint to know what happened with files (edit/view/etc) and the copilot to know what users have done with copilot. The information we get from copilot events is limited; "User A chatted to copilot in an app, and it involved this file/meeting", so hence we read SP logs too to link what actually was going on.

    Note: SharePoint events that are saved can be filtered so we don't accidentally store sensitive file activity data in table "import_url_filter".

In addition we also read aggregated activity reports for users for OneDrive, SharePoint, Outlook and Teams. That way we can find users that are active in O365 but not using Copilot. 

Finally the solution reads user metadata from Entra ID (Azure AD) copilot activity & satisfaction by demographics can be analysed - job title, office location, etc. 

# Setup Steps
PowerShell and ARM deployment options avaialble. See the [Setup guide](docs/setup.md).