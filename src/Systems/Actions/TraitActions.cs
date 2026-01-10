using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public static class TraitActions
    {
        public static void AddTraits(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            traits.AddRange(args);
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }

        public static void RemoveTraits(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            args.Foreach(trait => traits.Remove(trait));
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }
    }
}
