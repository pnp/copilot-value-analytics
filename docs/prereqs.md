# Prerequisites
To deploy this solution you need the following ready before. 

## Deployment PC
* [Docker desktop](https://docs.docker.com/desktop/setup/install/windows-install/) + [firewall exceptions](https://docs.docker.com/desktop/setup/allow-list/)
* PowerShell
  * [PowerShell 7.5](https://learn.microsoft.com/en-gb/powershell/scripting/install/installing-powershell-on-windows)
  * [Az PowerShell](https://learn.microsoft.com/en-us/powershell/azure/install-azure-powershell) version 13.2.0 or above. 
  * Modules:
    * SqlServer
    * Az.Apps
* Firewall connectivity to your created Azure SQL Server port 1433, once back-end is created.
* At least 50GB of free space. 

## Cloud Rights for Setup
* Contributor rights to an Azure subscription.
* Team admin right to publish new applications.
* Global admin rights to create Teams bot and consent to bot/app API permissions.

## Cloud Rights for Runtime
De-identified information on Office 365 needs to be disabled for activity tracking to work.	If "de-identified reports" setting is enabled, the import cannot match Teams or Activity usage reports. https://docs.microsoft.com/en-us/office365/troubleshoot/miscellaneous/reports-show-anonymous-user-name

## Application Permissions for App ID
Graph permissions needed (application):
* User.Read.All - for looking up user metadata, allowing statistic slicing by user demographics (job title, location etc)
* Reports.Read.All - for reading activity data so we can cross-check who's active/inactive in Office and using/not-using copilot.

If the copilot feedback bot is used and enabled for auto-install:
* TeamsAppInstallation.ReadWriteForUser.All - so the bot can proactively install itself into users Teams, to start a new conversation. 

*Recommended:*
When the activity import detects copilot events that have a context (a meeting/file), it'll try and load the metadata about that context if permissions are in place:
* OnlineMeetings.Read.All - read meeting info for meetings with copilot interactions.
* Files.Read.All (or Sites.Selected if fine-tuned permissions are required) - read file info for copilot related files.

The system and stats still work high-level without these permissions, but the deep filtering and reporting isn't possible without this extra metadata.

Office 365 Management APIs (Required)
* ActivityFeed.Read - for detecting copilot and Office 365 audit events. 

All these permissions need administrator consent to be effective. 
