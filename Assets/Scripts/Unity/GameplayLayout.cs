using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // Which tray affordances an item exposes; shared by layout and controls so both agree.
    public static class TrayAffordances
    {
        public static bool HasRotate(ItemDefinition item) => item.AllowedRotations.Count > 1;
        public static bool HasFold(ItemDefinition item) => item.ShapeStates.Count > 1;
    }

    // Presentation sizes in dp (1080x1920 portrait reference). Never gameplay values.
    public static class GameplayMetrics
    {
        public const float ReferenceWidth = 1080f;
        public const float HudHeight = 112f;
        public const float HudGap = 12f;
        public const float TrayGap = 28f;
        public const float TrayBottomMargin = 20f;
        public const float TraySideMargin = 20f;
        public const float CardPadding = 14f;
        public const float CardGap = 14f;
        public const float RowGap = 16f;
        public const float LaneGap = 26f;
        public const float ChipHeight = 80f;
        public const float RotateChipWidth = 80f;
        public const float FoldChipWidth = 128f;
        public const float ChipSpacing = 44f;
        public const float ChipHitPadding = 10f;
        public const float ItemHitPadding = 12f;
        public const float MaxTrayScale = 0.62f;
        public const float TargetTrayScale = 0.46f;
        public const float MinTrayScale = 0.16f;

        // Short side relative to a 1080 px wide portrait phone; 1.0 on the target devices.
        public static float DpScale(float screenWidth, float screenHeight) =>
            Mathf.Min(screenWidth, screenHeight) / ReferenceWidth;

        public static float LaneWidth(bool rotate, bool fold) =>
            (rotate ? RotateChipWidth : 0f) + (fold ? FoldChipWidth : 0f) +
            (rotate && fold ? ChipSpacing : 0f);
    }

    // Item extents in item-local cells at scale 1 (footprint bbox united with visual bounds).
    public struct TrayEntry
    {
        public float MinX, MaxX, MinZ, MaxZ, Height;
        public bool Rotate, Fold;
        public float Width => MaxX - MinX;
        public float Depth => MaxZ - MinZ;
        public bool HasLane => Rotate || Fold;
    }

    // World XZ band on the y = 0 plane; ZTop > ZBottom.
    public struct TrayBand
    {
        public float XMin, XMax, ZTop, ZBottom;
        public float Width => XMax - XMin;
        public float Depth => ZTop - ZBottom;
    }

    public struct TrayFrame
    {
        public TrayBand Band;
        public float WorldPerDp;
        public float MaxScale;
    }

    public struct TraySlot
    {
        public float Scale;
        public Rect Card; // x = xMin, y = zBottom, height = depth along z
        public Vector3 Origin;
        public Vector3 RotateChip;
        public Vector3 FoldChip;
        public float HitPadding; // item-local cells
    }

    public struct GameplayFrame
    {
        public float OrthographicSize;
        public float CenterY; // camera-space vertical center of the view
        public float Aspect;
        public float DpScale;
        public float BoardCellPixels;
        public float TrayScale;
        public TrayFrame Tray;
        public Rect HudPixels;
        public Rect BoardPixels;
        public Rect TrayPixels;
    }

    // Pure presentation layout: portrait composition and tray slots. Never touches gameplay state.
    public static class GameplayLayout
    {
        public const float Pitch = 75f;
        private static readonly float Sin = Mathf.Sin(Pitch * Mathf.Deg2Rad);
        private static readonly float Cos = Mathf.Cos(Pitch * Mathf.Deg2Rad);
        private static readonly float Overhang = Cos / Sin;

        public static Vector3 CameraUp => new Vector3(0f, Cos, Sin);

        public static GameplayFrame Compose(Vector2 screen, Rect safe, Bounds boardVisual,
            Vector3 containerCenter, int outerWidth, IReadOnlyList<TrayEntry> entries)
        {
            if (screen.x <= 0f || screen.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(screen));
            var w = screen.x;
            var h = screen.y;
            var aspect = w / h;
            var dp = GameplayMetrics.DpScale(w, h);
            safe = Rect.MinMaxRect(Mathf.Clamp(safe.xMin, 0f, w), Mathf.Clamp(safe.yMin, 0f, h),
                Mathf.Clamp(safe.xMax, 0f, w), Mathf.Clamp(safe.yMax, 0f, h));
            if (safe.width <= 0f || safe.height <= 0f)
                safe = new Rect(0f, 0f, w, h);

            // Board visual extents in canonical camera space (container center at origin).
            var bx = 0f;
            var by0 = float.MaxValue;
            var by1 = float.MinValue;
            var min = boardVisual.min;
            var max = boardVisual.max;
            for (var i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z) - containerCenter;
                bx = Mathf.Max(bx, Mathf.Abs(p.x));
                var y = Vector3.Dot(p, CameraUp);
                by0 = Mathf.Min(by0, y);
                by1 = Mathf.Max(by1, y);
            }

            var sb = safe.yMin / h;
            var st = safe.yMax / h;
            var usable = Mathf.Max(0.2f, 2f * Mathf.Min(0.5f - safe.xMin / w, safe.xMax / w - 0.5f));
            var hudF = GameplayMetrics.HudHeight * dp / h;
            var hudGapF = GameplayMetrics.HudGap * dp / h;
            var trayGapF = GameplayMetrics.TrayGap * dp / h;
            var bottomF = GameplayMetrics.TrayBottomMargin * dp / h;
            var adr = FixedGameplayCamera.OrthographicSize(outerWidth, aspect);
            var horizontal = (bx + FixedGameplayCamera.HorizontalMargin) / (aspect * usable);

            var best = default(GameplayFrame);
            var bestScore = float.MinValue;
            for (var ft = 0.18f; ft <= 0.4201f; ft += 0.01f)
            {
                var lo = sb + ft + trayGapF;
                var hi = st - hudF - hudGapF;
                if (hi - lo < 0.15f)
                    continue;
                var size = Mathf.Max(adr, horizontal, (by1 - by0) / (2f * (hi - lo)));
                var centerY = (by0 + by1) * 0.5f + size - 2f * size * (lo + hi) * 0.5f;
                var boardBottomF = (by0 - centerY + size) / (2f * size);
                var boardTopF = (by1 - centerY + size) / (2f * size);
                var trayLo = sb + bottomF;
                var trayHi = boardBottomF - trayGapF;
                var worldPerPixel = 2f * size / h;
                var worldPerDp = worldPerPixel * dp;
                var halfWidth = size * aspect * usable - GameplayMetrics.TraySideMargin * worldPerDp;
                var band = new TrayBand
                {
                    XMin = containerCenter.x - halfWidth,
                    XMax = containerCenter.x + halfWidth,
                    ZTop = containerCenter.z + (centerY - size + trayHi * 2f * size) / Sin,
                    ZBottom = containerCenter.z + (centerY - size + trayLo * 2f * size) / Sin
                };
                var tray = new TrayFrame
                {
                    Band = band, WorldPerDp = worldPerDp, MaxScale = GameplayMetrics.MaxTrayScale
                };
                LayoutTray(entries, tray, out var scale);
                var cellPixels = h / (2f * size);
                var score = cellPixels * Mathf.Min(1f, scale / GameplayMetrics.TargetTrayScale);
                if (score <= bestScore + 0.001f)
                    continue;
                bestScore = score;
                tray.MaxScale = scale;
                best = new GameplayFrame
                {
                    OrthographicSize = size,
                    CenterY = centerY,
                    Aspect = aspect,
                    DpScale = dp,
                    BoardCellPixels = cellPixels,
                    TrayScale = scale,
                    Tray = tray,
                    HudPixels = Rect.MinMaxRect(safe.xMin, (st - hudF) * h, safe.xMax, st * h),
                    BoardPixels = Rect.MinMaxRect(0f, boardBottomF * h, w, boardTopF * h),
                    TrayPixels = Rect.MinMaxRect(safe.xMin, trayLo * h, safe.xMax, trayHi * h)
                };
            }
            if (bestScore == float.MinValue)
                throw new InvalidOperationException("Screen too small for gameplay composition.");
            return best;
        }

        public static TraySlot[] LayoutTray(IReadOnlyList<TrayEntry> entries, TrayFrame frame,
            out float scale)
        {
            var count = entries.Count;
            scale = frame.MaxScale;
            if (count == 0)
                return Array.Empty<TraySlot>();

            // Contiguous partitions into 1..3 rows; keep the one that allows the largest uniform scale.
            var candidates = new List<int[]> { new[] { count } };
            for (var a = 1; a < count; a++)
            {
                candidates.Add(new[] { a, count });
                for (var b = a + 1; b < count; b++)
                    candidates.Add(new[] { a, b, count });
            }

            int[] bestBreaks = null;
            var bestScale = -1f;
            var bestOverflow = float.MaxValue;
            foreach (var rowBreaks in candidates)
            {
                var fit = MaxFeasibleScale(entries, rowBreaks, frame);
                if (fit > bestScale + 0.0001f)
                {
                    bestScale = fit;
                    bestBreaks = rowBreaks;
                }
                else if (bestScale < 0f)
                {
                    var overflow = Overflow(entries, rowBreaks, frame, GameplayMetrics.MinTrayScale);
                    if (overflow < bestOverflow)
                    {
                        bestOverflow = overflow;
                        bestBreaks = rowBreaks;
                    }
                }
            }
            // ponytail: an over-full tray clips at MinTrayScale; add scrolling if content ever needs it.
            scale = bestScale > 0f ? bestScale : GameplayMetrics.MinTrayScale;
            return Place(entries, bestBreaks, frame, scale);
        }

        private static float MaxFeasibleScale(IReadOnlyList<TrayEntry> entries, int[] rowBreaks,
            TrayFrame frame)
        {
            if (Overflow(entries, rowBreaks, frame, GameplayMetrics.MinTrayScale) > 0f)
                return -1f;
            if (Overflow(entries, rowBreaks, frame, frame.MaxScale) <= 0f)
                return frame.MaxScale;
            var lo = GameplayMetrics.MinTrayScale;
            var hi = frame.MaxScale;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (Overflow(entries, rowBreaks, frame, mid) <= 0f) lo = mid; else hi = mid;
            }
            return lo;
        }

        private static float Overflow(IReadOnlyList<TrayEntry> entries, int[] rowBreaks,
            TrayFrame frame, float scale)
        {
            var gap = GameplayMetrics.CardGap * frame.WorldPerDp;
            var rowGap = GameplayMetrics.RowGap * frame.WorldPerDp / Sin;
            var widthOverflow = float.MinValue;
            var depth = 0f;
            var start = 0;
            for (var r = 0; r < rowBreaks.Length; r++)
            {
                var width = 0f;
                var rowDepth = 0f;
                for (var i = start; i < rowBreaks[r]; i++)
                {
                    width += CardWidth(entries[i], frame.WorldPerDp, scale) + (i > start ? gap : 0f);
                    rowDepth = Mathf.Max(rowDepth, CardDepth(entries[i], frame.WorldPerDp, scale));
                }
                widthOverflow = Mathf.Max(widthOverflow, width - frame.Band.Width);
                depth += rowDepth + (r > 0 ? rowGap : 0f);
                start = rowBreaks[r];
            }
            return Mathf.Max(widthOverflow, depth - frame.Band.Depth);
        }

        private static float CardWidth(TrayEntry entry, float worldPerDp, float scale) =>
            Mathf.Max(scale * entry.Width,
                GameplayMetrics.LaneWidth(entry.Rotate, entry.Fold) * worldPerDp) +
            2f * GameplayMetrics.CardPadding * worldPerDp;

        private static float CardDepth(TrayEntry entry, float worldPerDp, float scale) =>
            scale * (entry.Depth + Overhang * entry.Height) +
            (2f * GameplayMetrics.CardPadding +
             (entry.HasLane ? GameplayMetrics.LaneGap + GameplayMetrics.ChipHeight : 0f)) *
            worldPerDp / Sin;

        private static TraySlot[] Place(IReadOnlyList<TrayEntry> entries, int[] rowBreaks,
            TrayFrame frame, float scale)
        {
            var dp = frame.WorldPerDp;
            var gap = GameplayMetrics.CardGap * dp;
            var rowGap = GameplayMetrics.RowGap * dp / Sin;
            var padX = GameplayMetrics.CardPadding * dp;
            var padZ = padX / Sin;
            var laneZ = GameplayMetrics.ChipHeight * dp / Sin;
            var laneGapZ = GameplayMetrics.LaneGap * dp / Sin;
            var slots = new TraySlot[entries.Count];

            var rowDepths = new float[rowBreaks.Length];
            var total = 0f;
            var start = 0;
            for (var r = 0; r < rowBreaks.Length; r++)
            {
                for (var i = start; i < rowBreaks[r]; i++)
                    rowDepths[r] = Mathf.Max(rowDepths[r], CardDepth(entries[i], dp, scale));
                total += rowDepths[r] + (r > 0 ? rowGap : 0f);
                start = rowBreaks[r];
            }

            var z = frame.Band.ZTop - Mathf.Max(0f, frame.Band.Depth - total) * 0.5f;
            var centerX = (frame.Band.XMin + frame.Band.XMax) * 0.5f;
            start = 0;
            for (var r = 0; r < rowBreaks.Length; r++)
            {
                var rowWidth = 0f;
                for (var i = start; i < rowBreaks[r]; i++)
                    rowWidth += CardWidth(entries[i], dp, scale) + (i > start ? gap : 0f);
                var x = centerX - rowWidth * 0.5f;
                for (var i = start; i < rowBreaks[r]; i++)
                {
                    var entry = entries[i];
                    var cardWidth = CardWidth(entry, dp, scale);
                    var card = new Rect(x, z - rowDepths[r], cardWidth, rowDepths[r]);
                    var contentTop = card.yMax - padZ;
                    var contentBottom = card.yMin + padZ + (entry.HasLane ? laneZ + laneGapZ : 0f);
                    var visualDepth = scale * (entry.Depth + Overhang * entry.Height);
                    var visualTop = contentTop - Mathf.Max(0f, contentTop - contentBottom - visualDepth) * 0.5f;
                    var footprintTop = visualTop - scale * Overhang * entry.Height;
                    var cardCenter = card.center.x;
                    var slot = new TraySlot
                    {
                        Scale = scale,
                        Card = card,
                        Origin = new Vector3(cardCenter - scale * entry.Width * 0.5f - scale * entry.MinX,
                            0f, footprintTop - scale * entry.MaxZ),
                        HitPadding = Mathf.Min(0.2f, GameplayMetrics.ItemHitPadding * dp / scale)
                    };
                    var laneCenterZ = card.yMin + padZ + laneZ * 0.5f;
                    var laneWidth = GameplayMetrics.LaneWidth(entry.Rotate, entry.Fold) * dp;
                    var left = cardCenter - laneWidth * 0.5f;
                    if (entry.Rotate)
                        slot.RotateChip = new Vector3(left + GameplayMetrics.RotateChipWidth * dp * 0.5f,
                            0f, laneCenterZ);
                    if (entry.Fold)
                        slot.FoldChip = new Vector3(left + laneWidth - GameplayMetrics.FoldChipWidth * dp * 0.5f,
                            0f, laneCenterZ);
                    slots[i] = slot;
                    x += cardWidth + gap;
                }
                z -= rowDepths[r] + rowGap;
                start = rowBreaks[r];
            }
            return slots;
        }
    }
}
