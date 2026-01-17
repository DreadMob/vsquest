using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class PlaySoundQuestAction : IQuestAction
    {
        public void Execute(ICoreServerAPI api, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length < 1)
            {
                throw new QuestException("The 'playsound' action requires at least 1 argument: soundLocation.");
            }

            string raw = args[0];
            var sound = new AssetLocation(raw);

            // args:
            // 0: soundLocation
            // 1: volume (optional)
            // 2: pitch  (optional)
            // Notes: Vintage Story exposes PlaySoundFor overloads with either (bool randomizePitch, float range, float volume)
            // or (float pitch, float range, float volume). Reflection-based selection is brittle across versions, so call directly.
            if (args.Length < 2)
            {
                api.World.PlaySoundFor(sound, byPlayer);
                return;
            }

            float volume = 1f;
            if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out volume))
            {
                volume = 1f;
            }

            if (args.Length >= 3 && float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float pitch))
            {
                api.World.PlaySoundFor(sound, byPlayer, pitch, 32f, volume);
                return;
            }

            api.World.PlaySoundFor(sound, byPlayer, randomizePitch: true, range: 32f, volume: volume);
        }
    }
}
