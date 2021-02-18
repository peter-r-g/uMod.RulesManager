//#define RulesManager_DEBUG
// Uncomment above to enable debug statements. Will only be useful for developers or when debugging a problem.

using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

#if RUST
using Oxide.Core;
using Oxide.Game.Rust.Libraries;
#endif

namespace Oxide.Plugins
{
    [Info("Rules Manager", "gunman435", "1.0.0")]
    [Description("Easy to use and configurable rules management tool.")]
    class RulesManager : CovalencePlugin
    {
        #region Fields
        // List for storing the cached rule texts for minimal performance hit on large servers.
        private List<string> ruleCache;

#if RUST
        // Include server library for Rust so we can use AvatarIDs.
        protected Server Server = Interface.Oxide.GetLibrary<Server>();
#endif
        #endregion

        #region Config
        // Config variable.
        private Configuration config;

        // Class for holding config elements.
        class Configuration
        {
            // Holds the rules.
            [JsonProperty(PropertyName = "Rules")]
            public List<string> rules;

            // The header to all rules.
            [JsonProperty(PropertyName = "Rules Header")]
            public string rulesHeader;

            // The footer to all rules.
            [JsonProperty(PropertyName = "Rules Footer")]
            public string rulesFooter;

            // The format for rules.
            [JsonProperty(PropertyName = "Rule Format")]
            public string ruleFormat;

            // The SteamID64 of the Avatar to use in broadcasted messages.
            [JsonProperty(PropertyName = "Scheduled Messages Avatar ID")]
            public ulong rulesAvatarID;
        }

        /// <summary>
        /// Loads the plugin configuration.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<Configuration>();

            // No existing config found, load default one and save it.
            if (config == null)
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        /// <summary>
        /// Loads the default plugin configuration.
        /// </summary>
        protected override void LoadDefaultConfig() => config = new Configuration
        {
            rules = new List<string>
            {
                "No Rules!"
            },
            rulesHeader = "These are our rules",
            rulesFooter = "",
            ruleFormat = "{RULENUMBER}. {RULE}",
            rulesAvatarID = 0
        };

        /// <summary>
        /// Saves the plugin configuration.
        /// </summary>
        protected override void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Language/Localization
        /// <summary>
        /// Loads the plugin localization messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            // English translation.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // For when someone is missing permission to use a command.
                ["MissingPermission"] = "You do not have permission to use the '{0}' command!",
                // 
                ["RulesManagerHelp"] = "These are the commands available:\n{0}",
                // 
                ["RulesManagerRuleNotFound"] = "Rule #{0} does not exist!",
                // 
                ["RulesManagerSpecificRule"] = "Rule #{0} states that: {1}",

                // 
                ["RulesManagerRuleAdded"] = "The rule '{0}' has been added!",
                // 
                ["RulesManagerRuleRemoved"] = "Rule #{0} has been removed!",
                // 
                ["RulesManagerRuleEdited"] = "Rule #{0} is now '{1}'",
                // 
                ["RulesManagerRulesShown"] = "Rules have been shown to {0}!",
                // 
                ["RulesManagerRuleShown"] = "Rule #{0} has been shown to {1}!",
                // 
                ["RulesManagerAvatarChanged"] = "Rules avatar has been changed to {0}!",
                // 
                ["RulesManagerRulesFooterChanged"] = "Rules footer has been changed to '{0}'",
                // 
                ["RulesManagerRuleFormatChanged"] = "Rule format has been changed to '{0}'",
                // 
                ["RulesManagerRulesHeaderChanged"] = "Rules header has been changed to '{0}'",

                // 
                ["RulesManagerAddUsage"] = "Usage: <rulesmanager/rman> <add/a> <rule>",
                // 
                ["RulesManagerRemoveUsage"] = "Usage: <rulesmanager/rman> <remove/r> <rule number>",
                // 
                ["RulesManagerEditUsage"] = "Usage: <rulesmanager/rman> <edit/e> <rule number> <rule>",
                // 
                ["RulesManagerShowUsage"] = "Usage: <rulesmanager/rman> <show/s> <player> [rule number]",
                // 
                ["RulesManagerSetAvatarUsage"] = "Usage: <rulesmanager/rman> <setavatar/sa> <steamid64>",
                // 
                ["RulesManagerSetFooterUsage"] = "Usage: <rulesmanager/rman> <setfooter/sf> <footer>",
                // 
                ["RulesManagerSetRuleFormatUsage"] = "Usage: <rulesmanager/rman> <setruleformat/srf> <format>",
                // 
                ["RulesManagerSetHeaderUsage"] = "Usage: <rulesmanager/rman> <setheader/sh> <header>"
            }, this);

            // Add other languages here.
        }
        #endregion

        #region Plugin Events
        /// <summary>
        /// Called when the server has finished startup or this plugin has been hotloaded.
        /// </summary>
        /// <param name="initial">Whether this is being called on server finishing startup or not.</param>
        void OnServerInitialized(bool initial)
        {
            // Register the permissions we need.
            permission.RegisterPermission("rulesmanager.add", this);
            permission.RegisterPermission("rulesmanager.remove", this);
            permission.RegisterPermission("rulesmanager.edit", this);
            permission.RegisterPermission("rulesmanager.show", this);
            permission.RegisterPermission("rulesmanager.setavatar", this);
            permission.RegisterPermission("rulesmanager.setfooter", this);
            permission.RegisterPermission("rulesmanager.setruleformat", this);
            permission.RegisterPermission("rulesmanager.setheader", this);

            // Build the rule text cache.
            ruleCache = new List<string>();
            BuildRuleCache();
        }
        #endregion

        #region Commands
        /// <summary>
        /// Executes a players command for rules.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        [Command("rules")]
        private void RulesCommand(IPlayer ply, string command, string[] args)
        {
            // If no arguments are passed then just display rules.
            if (args.Length == 0)
            {
                API_DisplayRules(ply);
                return;
            }

            try
            {
                // Try to parse the input.
                int ruleNumber = int.Parse(args[0]);
                // Check if the input is valid.
                if (!API_DisplayRule(ply, ruleNumber))
                    PrintToChat(ply, Lang("RulesManagerRuleNotFound", ply.Id, ruleNumber));
            }
            catch (Exception)
            {
                // Something went wrong, just show all rules.
                API_DisplayRules(ply);
            }
        }

        /// <summary>
        /// Executes a players command for rules.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        [Command("rulesmanager", "rman"), Permission("rulesmanager.cmd")]
        private void RulesManagerCommand(IPlayer ply, string command, string[] args)
        {
            // If no arguments are passed then just display help section.
            if (args.Length == 0)
            {
                HelpCommand(ply, command, args);
                return;
            }

            // Boolean for whether the config has been edited during this function.
            bool configEdited = false;

            switch (args[0])
            {
                // Add command.
                case "add":
                case "a":
                    configEdited = AddCommand(ply, command, args);
                    break;
                // Remove command.
                case "remove":
                case "r":
                    configEdited = RemoveCommand(ply, command, args);
                    break;
                // Edit command.
                case "edit":
                case "e":
                    configEdited = EditCommand(ply, command, args);
                    break;
                // Show command.
                case "show":
                    configEdited = ShowCommand(ply, command, args);
                    break;
                // Set avatar command.
                case "setavatar":
                case "sa":
                    configEdited = SetAvatarCommand(ply, command, args);
                    break;
                // Set footer command.
                case "setfooter":
                case "sf":
                    configEdited = SetFooterCommand(ply, command, args);
                    break;
                // Set rule format command.
                case "setruleformat":
                case "srf":
                    configEdited = SetRuleFormatCommand(ply, command, args);
                    break;
                // Set header command.
                case "setheader":
                case "sh":
                    configEdited = SetHeaderCommand(ply, command, args);
                    break;
                // Unknown command.
                default:
                    // Show help text.
                    configEdited = HelpCommand(ply, command, args);
                    break;
            }

            // If the config has been edited, save the changes.
            if (configEdited)
                SaveConfig();
        }

        /// <summary>
        /// Sub-command for adding a rule.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        private bool AddCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.add"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }

            // Build the rule the user tried to make.
            string newRule = Combine(args, 1);

            // Check to make sure it isn't still blank.
            if (newRule == "")
            {
                PrintToChat(ply, Lang("RulesManagerAddUsage", ply.Id));
                return false;
            }
            // Add the new rule.
            else
            {
                config.rules.Add(newRule);
                // Make the rule cache for the new rule.
                BuildRuleCache(config.rules.Count);
                // Call the OnRuleAdded hook.
                plugins.CallHook("OnRuleRemoved", config.rules.Count, newRule);
                // Let the player know the rule was added.
                PrintToChat(ply, Lang("RulesManagerRuleAdded", ply.Id, newRule));
                return true;
            }
        }

        /// <summary>
        /// Sub-command for removing a rule.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        private bool RemoveCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.remove"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }

            try
            {
                // Attempt to parse the rule number passed.
                int ruleNumber = int.Parse(args[1]);
                string rule = config.rules[ruleNumber-1];
                // Attempt to remove the rule at the parsed index.
                config.rules.RemoveAt(ruleNumber-1);
                // Rebuild the main rule cache and the possible new rule at that index.
                BuildRuleCache(ruleNumber);
                // Let the player know the message at that index+1 was removed.
                PrintToChat(ply, Lang("RulesManagerRuleRemoved", ply.Id, args[1]));
                // Call the OnRuleRemoved hook.
                plugins.CallHook("OnRuleRemoved", ruleNumber, rule);

                return true;
            }
            catch (Exception)
            {
                // Something went wrong, let the player know how to use the command.
                PrintToChat(ply, Lang("RulesManagerRemoveUsage", ply.Id));
                return false;
            }
        }

        /// <summary>
        /// Sub-command for editing a rule.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.<param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool EditCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.edit"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }

            try
            {
                // Attempt to parse the rule number passed and build arguments.
                int ruleNumber = int.Parse(args[1]);
                string oldRule = config.rules[ruleNumber-1];
                string newRule = Combine(args, 2);

                // Check that the new rule we got isn't empty.
                if (newRule != "")
                {
                    // Edit the rule at the parsed index.
                    config.rules[ruleNumber-1] = newRule;
                    // Remake the cached message for this rule.
                    BuildRuleCache(ruleNumber);
                    // Let the player know the message at that index was edited.
                    PrintToChat(ply, Lang("RulesManagerRuleEdited", ply.Id, args[1], newRule));
                    // Call the OnRuleRemoved hook.
                    plugins.CallHook("OnRuleEdited", ruleNumber, oldRule, newRule);

                    return true;
                }
                else
                {
                    PrintToChat(ply, Lang("RulesManagerEditUsage", ply.Id));
                    return false;
                }   
            }
            catch (Exception)
            {
                // Something went wrong, let the player know how to use the command.
                PrintToChat(ply, Lang("RulesManagerEditUsage", ply.Id));
                return false;
            }
        }

        /// <summary>
        /// Sub-command for showing the rules to another player.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool ShowCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.show"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }
            // Check if we got an argument for the player to find.
            else if (args.Length < 2)
            {
                PrintToChat(ply, Lang("RulesManagerShowUsage", ply.Id));
                return false;
            }

            // Try to find the player that is requested.
            IPlayer foundPlayer = players.FindPlayer(args[1]);
            // Make sure we have a player, if not send usage text to player.
            if (foundPlayer != null)
            {
                // Display all rules to the found player.
                if (args.Length < 3)
                {
                    API_DisplayRules(foundPlayer);
                    PrintToChat(ply, Lang("RulesManagerRulesShown", ply.Id, foundPlayer.Name));
                }
                // Try to display specific rule to player.
                else
                {
                    try
                    {
                        // Attempt to parse the rule number passed.
                        int ruleNumber = int.Parse(args[2]);
                        // Display the rule if it exists.
                        if (API_DisplayRule(foundPlayer, ruleNumber))
                            PrintToChat(ply, Lang("RulesManagerRuleShown", ply.Id, ruleNumber, foundPlayer.Name));
                        else
                            PrintToChat(ply, Lang("RulesManagerShowUsage", ply.Id));
                    }
                    catch (Exception)
                    {
                        PrintToChat(ply, Lang("RulesManagerShowUsage", ply.Id));
                    }
                }
            }
            // Display usage text for the player.
            else
                PrintToChat(ply, Lang("RulesManagerShowUsage", ply.Id));

            return false;
        }

        /// <summary>
        /// Sub-command for changing the avatar of messages in this plugin.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool SetAvatarCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.setavatar"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }
            // Check if the argument is exactly 17 characters long (the size of all SteamID64s) and make sure it isn't the default avatarID.
            else if (args[1].Length != 17 && args[1] != "0")
            {
                PrintToChat(ply, Lang("RulesManagerSetAvatarUsage", ply.Id));
                return false;
            }

            try
            {
                // Attempt to parse the new avatar.
                config.rulesAvatarID = ulong.Parse(args[1]);
                // Let the player know the avatar has been changed.
                PrintToChat(ply, Lang("RulesManagerAvatarChanged", ply.Id, args[1]));
                return true;
            }
            catch (Exception)
            {
                // Something went wrong, let the player know how to use the command.
                PrintToChat(ply, Lang("RulesManagerSetAvatarUsage", ply.Id));
                return false;
            }
        }

        /// <summary>
        /// Sub-command for changing rules footer.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool SetFooterCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.setfooter"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }

            // Create the new header.
            string newFooter = Combine(args, 1);

            // Set the new footer and let the player know about it.
            config.rulesFooter = newFooter;
            // Rebuild the main rules cache.
            BuildRuleCache(-1);
            PrintToChat(ply, Lang("RulesManagerRulesFooterChanged", ply.Id, newFooter));
            return true;
        }

        /// <summary>
        /// Sub-command for changing the rule format.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool SetRuleFormatCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.setformat")) 
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }
            // Check if we got an argument for the new format.
            else if (args.Length < 2)
            {
                PrintToChat(ply, Lang("RulesManagerSetFormatUsage", ply.Id));
                return false;
            }

            // Create the new format.
            string newFormat = Combine(args, 1);

            // Check to make sure it isn't still blank.
            if (newFormat == "")
            {
                PrintToChat(ply, Lang("RulesManagerSetFormatUsage", ply.Id));
                return false;
            }
            // Set the new header.
            else
            {
                // Set the new rule format and let the player know about it.
                config.ruleFormat = newFormat;
                // Rebuild the main rules cache.
                BuildRuleCache(-1);
                PrintToChat(ply, Lang("RulesManagerRuleFormatChanged", ply.Id, newFormat));
                return true;
            }
        }

        /// <summary>
        /// Sub-command for changing the rules header.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool SetHeaderCommand(IPlayer ply, string command, string[] args)
        {
            // Check if the player has the specific permission.
            if (!ply.HasPermission("rulesmanager.setheader"))
            {
                PrintToChat(ply, Lang("MissingPermission", ply.Id, $"{command} {args[0]}"));
                return false;
            }
            // Check if we got an argument for the new header.
            else if (args.Length < 2)
            {
                PrintToChat(ply, Lang("RulesManagerSetHeaderUsage", ply.Id));
                return false;
            }

            // Create the new header.
            string newHeader = Combine(args, 1);

            // Check to make sure it isn't still blank.
            if (newHeader == "")
            {
                PrintToChat(ply, Lang("RulesManagerSetHeaderUsage", ply.Id));
                return false;
            }
            // Set the new header.
            else
            {
                // Set the new header and let the player know about it.
                config.rulesHeader = newHeader;
                // Rebuild the main rules cache.
                BuildRuleCache(-1);
                PrintToChat(ply, Lang("RulesManagerRulesHeaderChanged", ply.Id, newHeader));
                return true;
            }
        }

        /// <summary>
        /// Sub-command for displaying command usage.
        /// </summary>
        /// <param name="ply">The player executing the command.</param>
        /// <param name="command">The command used.</param>
        /// <param name="args">The arguments passed in the message.</param>
        /// <returns>Whether the config has been edited or not.</returns>
        private bool HelpCommand(IPlayer ply, string command, string[] args)
        {
            // Send the player the help text.
            PrintToChat(ply, Lang("RulesManagerHelp", ply.Id,
                $"Add - {Lang("RulesManagerAddUsage", ply.Id)}\n" +
                $"Remove - {Lang("RulesManagerRemoveUsage", ply.Id)}\n" +
                $"Edit - {Lang("RulesManagerEditUsage", ply.Id)}\n" +
                $"Show - {Lang("RulesManagerShowUsage", ply.Id)}\n" +
                $"Set Avatar - {Lang("RulesManagerSetAvatarUsage", ply.Id)}\n" +
                $"Set Format - {Lang("RulesManagerSetFormatUsage", ply.Id)}\n" +
                $"Set Header - {Lang("RulesManagerSetHeaderUsage", ply.Id)}"));

            return false;
        }
        #endregion

        #region API Functions
        /// <summary>
        /// Displayed a single rule to the player passed.
        /// </summary>
        /// <param name="ply">The player to display the rule to.</param>
        /// <param name="ruleNumber">The actual rule number, this does not start from 0.</param>
        /// <returns>Whether the rule as displayed or not.</returns>
        public bool API_DisplayRule(IPlayer ply, int ruleNumber)
        {
            bool isValid = API_IsValidRule(ruleNumber);
            // Only do this if the rule number is within valid bounds.
            if (isValid)
                PrintToChat(ply, ruleCache[ruleNumber]);

            return isValid;
        }

        /// <summary>
        /// Displays the rules to the player passed.
        /// </summary>
        /// <param name="ply">The player to display the rules to.</param>
        public void API_DisplayRules(IPlayer ply)
        {
            // Display the rules in players chat.
            PrintToChat(ply, ruleCache[0]);
        }

        /// <summary>
        /// Checks whether the rule number given is within valid bounds of the rule list.
        /// </summary>
        /// <param name="ruleNumber">The rule number to check.</param>
        /// <returns>Whether the number is in valid bounds.</returns>
        public bool API_IsValidRule(int ruleNumber)
        {
            if (config.rules.Count != 0 && (ruleNumber >= 1 && ruleNumber <= config.rules.Count))
                return true;
            else
                return false;
        }

        /// <summary>
        /// Gets the rules currently registered within this system.
        /// </summary>
        /// <returns>A list of all the rules.</returns>
        public List<string> API_GetRulesList() { return config.rules; }
        /// <summary>
        /// Gets the rules currently registered within this system as an array.
        /// </summary>
        /// <returns>A list of all the rules as an array.</returns>
        public string[] API_GetRules() { return config.rules.ToArray(); }
        #endregion

        #region API Hooks
        /// <summary>
        /// Called when a rule is added.
        /// </summary>
        /// <param name="ruleNumber">The number that this rule was given. This is not zero based.</param>
        /// <param name="rule">The rule text.</param>
        private void OnRuleAdded(int ruleNumber, string rule) { }
        /// <summary>
        /// Called when a rule is removed.
        /// </summary>
        /// <param name="ruleNumber">The number that this rule was given. This is not zero based.</param>
        /// <param name="rule">The rule text.</param>
        private void OnRuleRemoved(int ruleNumber, string rule) { }
        /// <summary>
        /// Called when a rule is edited.
        /// </summary>
        /// <param name="ruleNumber">The number that this rule was given. This is not zero based.</param>
        /// <param name="oldRule">The old rule text.</param>
        /// <param name="newRule">The new rule text.</param>
        private void OnRuleEdited(int ruleNumber, string oldRule, string newRule) { }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Helper function to get a localized string and place arguments within it.
        /// </summary>
        /// <param name="key">The localization key.</param>
        /// <param name="id">The UserID.</param>
        /// <param name="args">Any arguments to pass to the formatter.</param>
        /// <returns></returns>
        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        /// <summary>
        /// Prints a message to a players chat.
        /// </summary>
        /// <param name="ply">The player to send the message to.</param>
        /// <param name="message">The message to send, supports formatting.</param>
        /// <param name="args">The variables to pass to string.Format if message needs formatting.</param>
        private void PrintToChat(IPlayer ply, string message, params object[] args)
        {
#if RUST
            // Use console command so we can include a different avatarID.
            ply.Command("chat.add", 2, config.rulesAvatarID, args.Length > 0 ? string.Format(message, args) : message);
#else
            // Just use default replying if we're not in Rust.
            ply.Reply(args.Length > 0 ? string.Format(message, args) : message);
#endif
        }

        /// <summary>
        /// Builds the cache for rule texts.
        /// </summary>
        /// <param name="ruleNumber">If zero(0), builds all rules. Otherwise attempts to build the specified rule.</param>
        private void BuildRuleCache(int ruleNumber=0)
        {
            // Check that our cache size is enough to hold it all.
            if (ruleCache.Count < config.rules.Count + 1)
                // Create any missing space.
                for (int i=ruleCache.Count-1; i<config.rules.Count; i++)
                    ruleCache.Add("");

            string rules = "";
            // Add each rule with a br after so they can show as seperate lines.
            for (int i = 0; i < config.rules.Count; i++)
                rules += $"{config.ruleFormat.Replace("{RULENUMBER}", (i + 1).ToString()).Replace("{RULE}", config.rules[i])}<br>";
            // Add rules text to cache.
            ruleCache[0] = $"{config.rulesHeader}<br>{rules}{config.rulesFooter}";

            // Build all rule texts.
            if (ruleNumber == 0)
                for (int i=0; i<config.rules.Count; i++)
                    ruleCache[i+1] = Lang("RulesManagerSpecificRule", null, i+1, config.rules[i]);
            // Build specific rule text.
            else if (API_IsValidRule(ruleNumber))
                ruleCache[ruleNumber] = Lang("RulesManagerSpecificRule", null, ruleNumber, config.rules[ruleNumber-1]);
        }

        /// <summary>
        /// Helper function to combine an array of strings.
        /// </summary>
        /// <param name="strings">The array of strings to combine.</param>
        /// <param name="startIndex">The index at which to start combining.</param>
        /// <returns>The combined string from the array.</returns>
        private string Combine(string[] strings, int startIndex=0)
        {
            string newString = "";
            for (int i=startIndex; i<strings.Length; i++)
                newString += $" {strings[i]}";
            newString = newString.Trim();

            return newString;
        }
        #endregion
    }
}
