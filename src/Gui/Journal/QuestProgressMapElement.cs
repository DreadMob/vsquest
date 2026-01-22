using System;
using System.Collections.Generic;
using Vintagestory.API.Client;

namespace VsQuest.Gui.Journal
{
    public enum QuestMapNodeStatus
    {
        Locked,
        Available,
        Completed,
        Current
    }

    public enum QuestMapNodeKind
    {
        Quest,
        Npc
    }

    public class QuestMapNode
    {
        public string QuestId { get; set; }
        public string TargetId { get; set; }
        public string Title { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public QuestMapNodeStatus Status { get; set; }
        public QuestMapNodeKind Kind { get; set; }
    }

    public class QuestMapEdge
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
    }

    public class QuestProgressMapElement : GuiElement
    {
        private List<QuestMapNode> nodes;
        private List<QuestMapEdge> edges;
        private readonly Action<string> onNodeClicked;
        private LoadedTexture mapTexture;
        private string hoveredQuestId;
        private string selectedQuestId;

        public QuestProgressMapElement(ICoreClientAPI capi, ElementBounds bounds, List<QuestMapNode> nodes, List<QuestMapEdge> edges, Action<string> onNodeClicked)
            : base(capi, bounds)
        {
            this.nodes = nodes ?? new List<QuestMapNode>();
            this.edges = edges ?? new List<QuestMapEdge>();
            this.onNodeClicked = onNodeClicked;
            mapTexture = new LoadedTexture(capi);
        }

        public void SetData(List<QuestMapNode> nodes, List<QuestMapEdge> edges)
        {
            this.nodes = nodes ?? new List<QuestMapNode>();
            this.edges = edges ?? new List<QuestMapEdge>();
            hoveredQuestId = null;
            if (selectedQuestId != null && !this.nodes.Exists(n => string.Equals(n?.QuestId, selectedQuestId, StringComparison.Ordinal)))
            {
                selectedQuestId = null;
            }
            RegenerateTexture();
        }

        public override void ComposeElements(Cairo.Context ctxStatic, Cairo.ImageSurface surfaceStatic)
        {
            RegenerateTexture();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (mapTexture == null) return;
            string newHover = Bounds.ParentBounds.PointInside(api.Input.MouseX, api.Input.MouseY)
                ? TryGetQuestIdAt(api.Input.MouseX, api.Input.MouseY)
                : null;
            if (!string.Equals(newHover, hoveredQuestId, StringComparison.Ordinal))
            {
                hoveredQuestId = newHover;
                RegenerateTexture();
            }
            api.Render.Render2DLoadedTexture(mapTexture, (float)Bounds.absX, (float)Bounds.absY);
        }

        public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
        {
            if (!Bounds.ParentBounds.PointInside(args.X, args.Y)) return;

            string questId = TryGetQuestIdAt(args.X, args.Y);
            if (!string.IsNullOrWhiteSpace(questId))
            {
                if (!string.Equals(selectedQuestId, questId, StringComparison.Ordinal))
                {
                    selectedQuestId = questId;
                    RegenerateTexture();
                }
                onNodeClicked?.Invoke(questId);
                args.Handled = true;
                return;
            }

            base.OnMouseUpOnElement(api, args);
        }

        private string TryGetQuestIdAt(int mouseX, int mouseY)
        {
            if (nodes == null || nodes.Count == 0) return null;

            Bounds.CalcWorldBounds();
            double pad = GuiElement.scaled(14.0);
            double radius = GuiElement.scaled(6.0);
            double npcRadius = GuiElement.scaled(7.5);

            double plotWidth = Math.Max(1.0, Bounds.InnerWidth - pad * 2.0);
            double plotHeight = Math.Max(1.0, Bounds.InnerHeight - pad * 2.0);

            double NodeX(float x) => Bounds.absX + pad + plotWidth * x;
            double NodeY(float y) => Bounds.absY + pad + plotHeight * y;

            foreach (var node in nodes)
            {
                double nodeRadius = node.Kind == QuestMapNodeKind.Npc ? npcRadius : radius;
                double hitRadius = nodeRadius * 1.6;
                double hitRadiusSq = hitRadius * hitRadius;
                double dx = mouseX - NodeX(node.X);
                double dy = mouseY - NodeY(node.Y);
                if (dx * dx + dy * dy <= hitRadiusSq)
                {
                    return node.QuestId;
                }
            }

            return null;
        }

        private void RegenerateTexture()
        {
            Bounds.CalcWorldBounds();

            int width = Math.Max(1, (int)Bounds.InnerWidth);
            int height = Math.Max(1, (int)Bounds.InnerHeight);

            var surface = new Cairo.ImageSurface(Cairo.Format.Argb32, width, height);
            var context = new Cairo.Context(surface);

            context.SetSourceRGBA(0, 0, 0, 0);
            context.Paint();

            double pad = GuiElement.scaled(14.0);
            double radius = GuiElement.scaled(6.0);
            double hoverRadius = radius + GuiElement.scaled(3.0);
            double selectedRadius = radius + GuiElement.scaled(4.5);
            double npcRadius = GuiElement.scaled(7.5);
            double npcHoverRadius = npcRadius + GuiElement.scaled(3.0);
            double npcSelectedRadius = npcRadius + GuiElement.scaled(4.5);

            double plotWidth = Math.Max(1, width - pad * 2.0);
            double plotHeight = Math.Max(1, height - pad * 2.0);

            double NodeX(float x) => pad + plotWidth * x;
            double NodeY(float y) => pad + plotHeight * y;

            context.LineWidth = GuiElement.scaled(2.0);
            context.SetSourceRGBA(0.8, 0.8, 0.8, 0.65);

            foreach (var edge in edges)
            {
                if (edge.FromIndex < 0 || edge.FromIndex >= nodes.Count) continue;
                if (edge.ToIndex < 0 || edge.ToIndex >= nodes.Count) continue;

                var from = nodes[edge.FromIndex];
                var to = nodes[edge.ToIndex];

                context.MoveTo(NodeX(from.X), NodeY(from.Y));
                context.LineTo(NodeX(to.X), NodeY(to.Y));
                context.Stroke();
            }

            context.SelectFontFace("Sans", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            context.SetFontSize(GuiElement.scaled(12.0));

            foreach (var node in nodes)
            {
                double x = NodeX(node.X);
                double y = NodeY(node.Y);
                bool isNpc = node.Kind == QuestMapNodeKind.Npc;
                double nodeRadius = isNpc ? npcRadius : radius;
                double nodeHoverRadius = isNpc ? npcHoverRadius : hoverRadius;
                double nodeSelectedRadius = isNpc ? npcSelectedRadius : selectedRadius;

                bool isSelected = !string.IsNullOrWhiteSpace(selectedQuestId)
                    && string.Equals(selectedQuestId, node.QuestId, StringComparison.Ordinal);
                bool isHovered = !string.IsNullOrWhiteSpace(hoveredQuestId)
                    && string.Equals(hoveredQuestId, node.QuestId, StringComparison.Ordinal);

                if (isSelected)
                {
                    context.SetSourceRGBA(0.25, 0.65, 1.0, 0.9);
                    context.Arc(x, y, nodeSelectedRadius, 0, Math.PI * 2.0);
                    context.Fill();
                }
                else if (isHovered)
                {
                    context.SetSourceRGBA(1.0, 1.0, 1.0, 0.65);
                    context.Arc(x, y, nodeHoverRadius, 0, Math.PI * 2.0);
                    context.Fill();
                }

                var fill = GetNodeColor(node.Status, node.Kind);
                context.SetSourceRGBA(fill.Item1, fill.Item2, fill.Item3, 0.95);
                context.Arc(x, y, nodeRadius, 0, Math.PI * 2.0);
                context.FillPreserve();

                context.SetSourceRGBA(0.1, 0.1, 0.1, 0.85);
                context.LineWidth = GuiElement.scaled(1.0);
                context.Stroke();

                bool shouldShowLabel = !string.IsNullOrWhiteSpace(node.Title)
                    && (isSelected || isNpc);

                if (shouldShowLabel)
                {
                    string label = FitLabelText(context, node.Title, GuiElement.scaled(140.0));
                    var extents = context.TextExtents(label);
                    double textX = x - extents.Width / 2.0;
                    double textY = y + nodeRadius + GuiElement.scaled(14.0);

                    context.SetSourceRGBA(1.0, 1.0, 1.0, 0.95);
                    context.MoveTo(textX, textY);
                    context.ShowText(label);
                }
            }

            generateTexture(surface, ref mapTexture);
            context.Dispose();
            surface.Dispose();
        }

        private static Tuple<double, double, double> GetNodeColor(QuestMapNodeStatus status, QuestMapNodeKind kind)
        {
            if (kind == QuestMapNodeKind.Npc)
            {
                return Tuple.Create(0.75, 0.8, 0.9);
            }
            return status switch
            {
                QuestMapNodeStatus.Completed => Tuple.Create(0.25, 0.8, 0.35),
                QuestMapNodeStatus.Available => Tuple.Create(0.95, 0.75, 0.25),
                QuestMapNodeStatus.Current => Tuple.Create(0.3, 0.6, 1.0),
                _ => Tuple.Create(0.4, 0.4, 0.4)
            };
        }

        private static string FitLabelText(Cairo.Context context, string text, double maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (maxWidth <= 0) return text;

            string result = text;
            var extents = context.TextExtents(result);
            if (extents.Width <= maxWidth) return result;

            const string suffix = "...";
            int maxChars = Math.Max(1, text.Length);
            while (maxChars > 1)
            {
                maxChars--;
                result = text.Substring(0, maxChars) + suffix;
                extents = context.TextExtents(result);
                if (extents.Width <= maxWidth) break;
            }

            return result;
        }

        public override void Dispose()
        {
            mapTexture?.Dispose();
            mapTexture = null;
            base.Dispose();
        }
    }
}
