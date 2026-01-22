using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest.Gui.Journal
{
    public class QuestProgressMapPage : JournalPage
    {
        private class QuestEdgeRef
        {
            public string FromId { get; set; }
            public string ToId { get; set; }
        }

        private readonly string title;
        private readonly List<QuestMapNode> npcNodes;
        private readonly List<QuestMapNode> allQuestNodes;
        private readonly List<QuestEdgeRef> questEdges;
        private readonly Dictionary<string, HashSet<string>> questIdsByNpc;
        private Dictionary<string, QuestMapNode> currentNodesById = new Dictionary<string, QuestMapNode>(StringComparer.OrdinalIgnoreCase);
        private QuestProgressMapElement mapElement;
        private GuiComposer composer;
        private ActionConsumable<string> openDetailPageFor;
        private string selectedQuestId;
        private string selectedNpcId;

        public override string PageCode => "quest-map";
        public override string CategoryCode => "map";

        public QuestProgressMapPage(
            ICoreClientAPI capi,
            string title,
            List<QuestMapNode> npcNodes,
            List<QuestMapNode> questNodes,
            List<(string FromId, string ToId)> questEdges,
            Dictionary<string, HashSet<string>> questIdsByNpc) : base(capi)
        {
            this.title = title;
            this.npcNodes = npcNodes ?? new List<QuestMapNode>();
            this.allQuestNodes = questNodes ?? new List<QuestMapNode>();
            this.questEdges = questEdges?.Select(edge => new QuestEdgeRef { FromId = edge.FromId, ToId = edge.ToId }).ToList()
                ?? new List<QuestEdgeRef>();
            this.questIdsByNpc = questIdsByNpc ?? new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            this.titleCached = title?.ToLowerInvariant() ?? "";
        }

        public override void RenderListEntryTo(ICoreClientAPI capi, float dt, double x, double y, double cellWidth, double cellHeight)
        {
            if (Texture == null)
            {
                Texture = new TextTextureUtil(capi).GenTextTexture(title, CairoFont.WhiteSmallText());
            }
            RenderTextureIfExists(x, y);
        }

        public override float GetTextMatchWeight(string searchText)
        {
            if (string.IsNullOrEmpty(searchText)) return 1f;
            if (titleCached.Equals(searchText, StringComparison.OrdinalIgnoreCase)) return 4f;
            if (titleCached.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) return 3f;
            if (titleCached.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return 2f;
            return 0f;
        }

        public override void ComposePage(GuiComposer composer, ElementBounds textBounds, ActionConsumable<string> openDetailPageFor)
        {
            this.composer = composer;
            this.openDetailPageFor = openDetailPageFor;
            selectedNpcId = null;
            selectedQuestId = null;

            double mapHeight = Math.Max(180.0, textBounds.fixedHeight * 0.6);
            double detailSpacing = 8.0;
            double detailHeight = Math.Max(0.0, textBounds.fixedHeight - mapHeight - detailSpacing);
            double buttonHeight = 24.0;
            double buttonSpacing = 6.0;

            var mapBounds = textBounds.FlatCopy().WithFixedOffset(0.0, 0.0);
            mapBounds.fixedHeight = mapHeight;

            var detailBounds = textBounds.FlatCopy().WithFixedOffset(0.0, mapHeight + detailSpacing);
            detailBounds.fixedHeight = detailHeight;

            var detailTextBounds = detailBounds.FlatCopy();
            detailTextBounds.fixedHeight = Math.Max(0.0, detailBounds.fixedHeight - buttonHeight - buttonSpacing);

            var openButtonBounds = detailBounds.FlatCopy();
            openButtonBounds.fixedY = detailBounds.fixedY + detailTextBounds.fixedHeight + buttonSpacing;
            openButtonBounds.fixedHeight = buttonHeight;

            mapElement = new QuestProgressMapElement(capi, mapBounds, BuildVisibleNodes(), BuildVisibleEdges(), questId =>
            {
                if (string.IsNullOrWhiteSpace(questId)) return;
                HandleNodeClick(questId);
            });
            composer.AddInteractiveElement(mapElement, "questmap");
            composer.AddRichtext(BuildDetailText(), CairoFont.WhiteSmallishText(), detailTextBounds, "mapdetailtext");
            composer.AddSmallButton(Lang.Get("alegacyvsquest:map-open-entry"), OnOpenEntry, openButtonBounds, EnumButtonStyle.Normal, "mapopenbutton");
            RefreshMapData();
            UpdateButtonState();
        }

        public override void Dispose()
        {
            base.Dispose();
            mapElement?.Dispose();
            mapElement = null;
            composer = null;
            openDetailPageFor = null;
        }

        private void HandleNodeClick(string nodeId)
        {
            if (!currentNodesById.TryGetValue(nodeId, out var node) || node == null) return;

            if (node.Kind == QuestMapNodeKind.Npc)
            {
                selectedNpcId = node.TargetId;
                selectedQuestId = null;
                RefreshMapData();
                UpdateDetailText();
                UpdateButtonState();
                return;
            }

            selectedQuestId = node.QuestId;
            UpdateDetailText();
            UpdateButtonState();
        }

        private string BuildDetailText()
        {
            if (string.IsNullOrWhiteSpace(selectedQuestId) || !currentNodesById.TryGetValue(selectedQuestId, out var node) || node == null)
            {
                return Lang.Get("alegacyvsquest:map-detail-none");
            }

            string titleText = string.IsNullOrWhiteSpace(node.Title) ? node.QuestId : node.Title;
            string description = ExtractDescription(node.QuestId);

            var lines = new List<string>
            {
                Lang.Get("alegacyvsquest:map-detail-title", titleText)
            };

            if (node.Kind == QuestMapNodeKind.Npc)
            {
                return string.Join("\n", lines);
            }

            if (node.Status != QuestMapNodeStatus.Locked)
            {
                string statusText = GetStatusText(node.Status);
                lines.Add(Lang.Get("alegacyvsquest:map-detail-status", statusText));
                if (!string.IsNullOrWhiteSpace(description))
                {
                    lines.Add("");
                    lines.Add(Lang.Get("alegacyvsquest:map-detail-desc", description));
                }
            }

            return string.Join("\n", lines);
        }

        private void UpdateDetailText()
        {
            var text = composer?.GetRichtext("mapdetailtext");
            if (text != null)
            {
                text.SetNewText(BuildDetailText(), CairoFont.WhiteSmallishText());
            }
        }

        private void RefreshMapData()
        {
            var nodes = BuildVisibleNodes();
            var edges = BuildVisibleEdges(nodes);
            mapElement?.SetData(nodes, edges);
            currentNodesById = nodes
                .Where(node => node?.QuestId != null)
                .ToDictionary(node => node.QuestId, node => node, StringComparer.OrdinalIgnoreCase);
        }

        private List<QuestMapNode> BuildVisibleNodes()
        {
            var nodes = new List<QuestMapNode>();
            nodes.AddRange(npcNodes);

            if (string.IsNullOrWhiteSpace(selectedNpcId))
            {
                return nodes;
            }

            if (questIdsByNpc.TryGetValue(selectedNpcId, out var questIds) && questIds != null)
            {
                nodes.AddRange(allQuestNodes.Where(node => questIds.Contains(node.QuestId)));
            }

            return nodes;
        }

        private List<QuestMapEdge> BuildVisibleEdges()
        {
            return BuildVisibleEdges(BuildVisibleNodes());
        }

        private List<QuestMapEdge> BuildVisibleEdges(List<QuestMapNode> nodes)
        {
            var edges = new List<QuestMapEdge>();
            var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(nodes[i]?.QuestId))
                {
                    indexById[nodes[i].QuestId] = i;
                }
            }

            foreach (var edge in questEdges)
            {
                if (!indexById.TryGetValue(edge.FromId, out int fromIndex)) continue;
                if (!indexById.TryGetValue(edge.ToId, out int toIndex)) continue;
                edges.Add(new QuestMapEdge { FromIndex = fromIndex, ToIndex = toIndex });
            }

            return edges;
        }

        private void UpdateButtonState()
        {
            var button = composer?.GetButton("mapopenbutton");
            if (button != null)
            {
                button.Enabled = !string.IsNullOrWhiteSpace(selectedQuestId);
            }
        }

        private bool OnOpenEntry()
        {
            if (string.IsNullOrWhiteSpace(selectedQuestId)) return true;
            openDetailPageFor?.Invoke("entry-" + selectedQuestId);
            return true;
        }

        private static string ExtractDescription(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId)) return null;

            string raw = Lang.Get(questId + "-desc");
            if (string.IsNullOrWhiteSpace(raw) || raw == questId + "-desc") return null;

            string cleaned = raw
                .Replace("<br />", "\n")
                .Replace("<br/>", "\n")
                .Replace("<br>", "\n");

            foreach (var line in cleaned.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (trimmed.Length > 160) trimmed = trimmed.Substring(0, 157) + "...";
                return trimmed;
            }

            return null;
        }

        private static string GetStatusText(QuestMapNodeStatus status)
        {
            return status switch
            {
                QuestMapNodeStatus.Completed => Lang.Get("alegacyvsquest:map-status-completed"),
                QuestMapNodeStatus.Available => Lang.Get("alegacyvsquest:map-status-available"),
                QuestMapNodeStatus.Current => Lang.Get("alegacyvsquest:map-status-current"),
                _ => Lang.Get("alegacyvsquest:map-status-locked")
            };
        }
    }
}
