## Features
* Able to temporarily enable/disable the scheduled messages from broadcasting.
* Fully customizable both in and out of the game.
* Rich text support, see [here](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html).
* (Rust only) Customizable avatar image that shows in place of the Rust logo in scheduled messages.
* Developer API for broadcasting scheduled messages whenever you want and knowing when a message has been broadcasted.

## Notes
* This has only been tested within a Rust dedicated server, although the code should be supported universally. If any problems occur, let me know.

## Configuration
```json  
{
  "Rules": [
    "No Rules!"
  ],
  "Rules Header": "These are our rules",
  "Rules Footer": "",
  "Rule Format": "{RULENUMBER}. {RULE}",
  "Scheduled Messages Avatar ID": 0
}
```

## Localization
```json
{
  "MissingPermission": "You do not have permission to use the '{0}' command!",
  "RulesManagerHelp": "These are the commands available:\n{0}",
  "RulesManagerRuleNotFound": "Rule #{0} does not exist!",
  "RulesManagerSpecificRule": "Rule #{0} states that: {1}",
  "RulesManagerRuleAdded": "The rule '{0}' has been added!",
  "RulesManagerRuleRemoved": "Rule #{0} has been removed!",
  "RulesManagerRuleEdited": "Rule #{0} is now '{1}'",
  "RulesManagerRulesShown": "Rules have been shown to {0}!",
  "RulesManagerRuleShown": "Rule #{0} has been shown to {1}!",
  "RulesManagerAvatarChanged": "Rules avatar has been changed to {0}!",
  "RulesManagerRulesFooterChanged": "Rules footer has been changed to '{0}'",
  "RulesManagerRuleFormatChanged": "Rule format has been changed to '{0}'",
  "RulesManagerRulesHeaderChanged": "Rules header has been changed to '{0}'",
  "RulesManagerAddUsage": "Usage: <rulesmanager/rman> <add/a> <rule>",
  "RulesManagerRemoveUsage": "Usage: <rulesmanager/rman> <remove/r> <rule number>",
  "RulesManagerEditUsage": "Usage: <rulesmanager/rman> <edit/e> <rule number> <rule>",
  "RulesManagerShowUsage": "Usage: <rulesmanager/rman> <show/s> <player> [rule number]",
  "RulesManagerSetAvatarUsage": "Usage: <rulesmanager/rman> <setavatar/sa> <steamid64>",
  "RulesManagerSetFooterUsage": "Usage: <rulesmanager/rman> <setfooter/sf> <footer>",
  "RulesManagerSetRuleFormatUsage": "Usage: <rulesmanager/rman> <setruleformat/srf> <format>",
  "RulesManagerSetHeaderUsage": "Usage: <rulesmanager/rman> <setheader/sh> <header>"
}
```

## Developers
### Hooks
```C#
void OnRuleAdded(int ruleNumber, string rule)
```

```C#
void OnRuleRemoved(int ruleNumber, string rule)
```

```C#
void OnRuleEdited(int ruleNumber, string oldRule, string newRule)
```

### Functions
```C#
API_DisplayRule(IPlayer ply, int ruleNumber) : bool
```

```C#
API_DisplayRules(IPlayer ply) : void
```

```C#
API_IsValidRule(int ruleNumber) : bool
```

```C#
API_GetRulesList() : List<string>
```

```C#
API_GetRules() : string[]
```
