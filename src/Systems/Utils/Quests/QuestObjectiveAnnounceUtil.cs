using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VsQuest
{
    public static class QuestObjectiveAnnounceUtil
    {
        private static string GetWalkLabel()
        {
            return Lang.HasTranslation("objective-walkdistance")
                ? Lang.Get("objective-walkdistance")
                : (Lang.HasTranslation("vsquest:objective-walkdistance") ? Lang.Get("vsquest:objective-walkdistance") : "Walk");
        }

        private static string GetMeterUnit()
        {
            return Lang.HasTranslation("unit-meter-short")
                ? Lang.Get("unit-meter-short")
                : (Lang.HasTranslation("vsquest:unit-meter-short") ? Lang.Get("vsquest:unit-meter-short") : "m");
        }

        public static void AnnounceOnAccept(IServerPlayer player, QuestAcceptedMessage message, ICoreServerAPI sapi, Quest quest)
        {
            if (player == null || sapi == null || quest == null) return;

            AnnounceRandomKillTargets(player, message, sapi, quest);

            AnnounceTimeOfDayGate(player, sapi, quest);

            // walkdistance
            if (quest.actionObjectives != null)
            {
                foreach (var obj in quest.actionObjectives)
                {
                    if (obj?.id != "walkdistance") continue;
                    if (obj.args == null || obj.args.Length < 2) continue;

                    int needMeters = 0;
                    if (obj.args.Length >= 3)
                    {
                        int.TryParse(obj.args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out needMeters);
                    }
                    else
                    {
                        int.TryParse(obj.args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out needMeters);
                    }

                    if (needMeters < 0) needMeters = 0;
                    if (needMeters == 0) continue;

                    sapi.SendMessage(player, GlobalConstants.GeneralChatGroup, $"{GetWalkLabel()}: 0/{needMeters} {GetMeterUnit()}", EnumChatType.Notification);
                }
            }

            // killObjectives
            if (quest.killObjectives != null)
            {
                foreach (var ko in quest.killObjectives)
                {
                    if (ko == null) continue;
                    int need = ko.demand;
                    if (need <= 0) continue;

                    string code = null;
                    if (ko.validCodes != null && ko.validCodes.Count > 0) code = ko.validCodes[0];
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    // Client-side localization (server language may be EN)
                    sapi.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage()
                    {
                        Template = "kill-notify",
                        Need = need,
                        MobCode = code,
                        Notification = null
                    }, player);
                }
            }
        }

        private static void AnnounceTimeOfDayGate(IServerPlayer player, ICoreServerAPI sapi, Quest quest)
        {
            if (quest.actionObjectives == null) return;

            foreach (var obj in quest.actionObjectives)
            {
                if (obj?.id != "timeofday") continue;
                if (obj.args == null || obj.args.Length < 1) continue;

                string mode = obj.args[0];
                bool night = string.Equals(mode, "night", System.StringComparison.OrdinalIgnoreCase);
                string key = night ? "timeofday-notify-night" : "timeofday-notify-day";

                sapi.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage()
                {
                    Notification = key
                }, player);
            }
        }

        private static void AnnounceRandomKillTargets(IServerPlayer player, QuestAcceptedMessage message, ICoreServerAPI sapi, Quest quest)
        {
            var wa = player.Entity?.WatchedAttributes;
            if (wa == null) return;

            string questId = message?.questId;
            if (string.IsNullOrWhiteSpace(questId)) return;

            int slots = wa.GetInt(RandomKillQuestUtils.SlotsKey(questId), 0);
            if (slots <= 0) return;

            string template = null;
            if (quest.onAcceptedActions != null)
            {
                foreach (var action in quest.onAcceptedActions)
                {
                    if (action?.id != "randomkill") continue;
                    if (action.args == null || action.args.Length < 4) continue;

                    var args = action.args;
                    RandomKillQuestUtils.ParseRollArgsMulti(args, out _, out _, out _, out template, out _, out _, out _);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(template)) return;

            for (int slot = 0; slot < slots; slot++)
            {
                string code = wa.GetString(RandomKillQuestUtils.SlotCodeKey(questId, slot), null);
                int need = wa.GetInt(RandomKillQuestUtils.SlotNeedKey(questId, slot), 0);
                if (string.IsNullOrWhiteSpace(code) || need <= 0) continue;

                // Client-side localization for the template
                sapi.Network.GetChannel("vsquest").SendPacket(new ShowNotificationMessage()
                {
                    Template = template,
                    Need = need,
                    MobCode = code,
                    Notification = null
                }, player);
            }
        }
    }
}
