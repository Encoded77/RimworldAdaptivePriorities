using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using AdaptivePriorities.Core;
using UnityEngine;
using Verse;

namespace AdaptivePriorities.Debug
{
    /// <summary>
    /// Dev-mode balance report: runs the real assignment pipeline with tracing on and renders every input
    /// each decision used, so a run can be read away from the save - hence the tuning snapshot and
    /// formula legend, which the reader would otherwise lack. Nothing is written to the pawns.
    ///
    /// Output goes to the clipboard and a file, not the log: the dev console truncates long messages and
    /// cannot be selected out of.
    ///
    /// Grouped by work type rather than pawn because rank, scoreCutoff, minWorkers and maxWorkersFraction
    /// all apply per work type - what explains a pawn missing a cut lives in the other pawns' rows.
    /// </summary>
    public static class PriorityReportDebugActions
    {
        private const string ReportFileName = "AdaptivePriorities_Report.txt";

        [DebugAction("Adaptive Priorities", "Priority report: full", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportFull() => Emit("full", onlyPawn: null, diffOnly: false);

        [DebugAction("Adaptive Priorities", "Priority report: differences only", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportDifferences() => Emit("differences only", onlyPawn: null, diffOnly: true);

        [DebugAction("Adaptive Priorities", "Priority report: this pawn", actionType = DebugActionType.ToolMapForPawns,
                     allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReportPawn(Pawn pawn)
        {
            if (pawn == null)
                return;
            Emit("pawn " + pawn.LabelShortCap, pawn, diffOnly: false);
        }

        private static void Emit(string mode, Pawn onlyPawn, bool diffOnly)
        {
            var map = Find.CurrentMap;
            if (map == null)
                return;

            var trace = new List<AssignmentTrace>();
            ColonyPriorityAssigner.ComputeProposal(map, trace);

            string report;
            try
            {
                report = Build(map, mode, trace, onlyPawn, diffOnly);
            }
            catch (Exception e)
            {
                Log.Error($"[Adaptive Priorities] Report failed to render: {e}");
                return;
            }

            string path = null;
            try
            {
                path = Path.Combine(GenFilePaths.DevOutputFolderPath, ReportFileName);
                File.WriteAllText(path, report);
            }
            catch (Exception e)
            {
                path = null;
                Log.Warning($"[Adaptive Priorities] Could not write the report file; it is still on the clipboard. {e}");
            }

            try
            {
                GUIUtility.systemCopyBuffer = report;
            }
            catch (Exception e)
            {
                Log.Warning($"[Adaptive Priorities] Could not copy the report to the clipboard. {e}");
            }

            // A receipt only; the report itself would be truncated here.
            Log.Message($"[Adaptive Priorities] Report ready ({mode}): {report.Length} chars, copied to clipboard"
                        + (path != null ? $" and written to {path}" : "")
                        + ". No priorities were changed.");
        }

        private static string Build(Map map, string mode, List<AssignmentTrace> trace, Pawn onlyPawn, bool diffOnly)
        {
            var rows = trace.Where(t => onlyPawn == null || t.pawn == onlyPawn)
                            .Where(t => !diffOnly || t.proposed != t.current)
                            .ToList();

            var sb = new StringBuilder();
            AppendHeader(sb, map, mode, trace, rows);
            AppendTuning(sb);
            AppendPassions(sb, trace);
            AppendLegend(sb);

            if (onlyPawn != null)
                AppendByPawn(sb, rows);
            else
                AppendByWorkType(sb, rows, diffOnly);

            AppendCoverage(sb, trace);
            sb.AppendLine("=== END REPORT ===");
            return sb.ToString();
        }

        private static void AppendHeader(StringBuilder sb, Map map, string mode, List<AssignmentTrace> trace,
                                         List<AssignmentTrace> rows)
        {
            int pawnCount = trace.Select(t => t.pawn).Distinct().Count();
            int differing = trace.Count(t => t.proposed != t.current);

            sb.AppendLine("=== ADAPTIVE PRIORITIES REPORT ===");
            sb.AppendLine($"mode: {mode}   generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"RimWorld {VersionControl.CurrentVersionStringWithRev}   map: {map.info?.parent?.Label ?? "?"}");
            sb.AppendLine($"colonists scored: {pawnCount}   cells: {trace.Count}   differing from current: {differing}   rows shown: {rows.Count}");
            sb.AppendLine($"priority range: {PriorityRangeService.HighestPriority}..{PriorityRangeService.LowestPriority}"
                          + (PriorityRangeService.LowestPriority == 4 ? " (vanilla)" : " (extended by a mod)"));
            sb.AppendLine($"manual priorities on: {Find.PlaySettings.useWorkPriorities}");

            var relevant = new List<string>();
            foreach (var m in ModsConfig.ActiveModsInLoadOrder)
            {
                string id = m.PackageIdPlayerFacing?.ToLowerInvariant() ?? "";
                if (id.Contains("skills") || id.Contains("worktab") || id.Contains("fluffy")
                    || id.Contains("ludeon.rimworld") || id.Contains("adaptivepriorities") || id.Contains("numbers"))
                    relevant.Add(m.Name);
            }
            sb.AppendLine($"relevant mods ({ModsConfig.ActiveModsInLoadOrder.Count()} active total): {string.Join(", ", relevant)}");
            sb.AppendLine();
        }

        private static void AppendTuning(StringBuilder sb)
        {
            sb.AppendLine("--- TUNING (* = overridden in the settings tab, otherwise the Def value) ---");
            sb.AppendLine(string.Join("  ", F("skillWeight", ScoringConfig.SkillWeight),
                                            F("minorPassionBonus", ScoringConfig.MinorPassionBonus),
                                            F("majorPassionBonus", ScoringConfig.MajorPassionBonus),
                                            F("badPassionPenalty", ScoringConfig.BadPassionPenalty),
                                            F("maxPassionBonus", ScoringConfig.MaxPassionBonus)));
            sb.AppendLine(string.Join("  ", F("noSkillWorkScore", ScoringConfig.NoSkillWorkScore),
                                            F("relativeWeight", ScoringConfig.RelativeWeight),
                                            F("qualityWeight", ScoringConfig.QualityWeight),
                                            F("workStatWeight", ScoringConfig.WorkStatWeight)));
            sb.AppendLine(string.Join("  ", F("opposedWorkFactor", ScoringConfig.OpposedWorkFactor),
                                            B("assignOpposedWhenNeeded", ScoringConfig.AssignOpposedWhenNeeded),
                                            B("inspirationBonusEnabled", ScoringConfig.InspirationBonusEnabled),
                                            F("inspirationBonus", ScoringConfig.InspirationBonus),
                                            F("inspirationUrgency", ScoringConfig.InspirationUrgency)));
            sb.AppendLine();
        }

        /// <summary>Every distinct passion seen on a scored skill, and the bonus it resolved to.</summary>
        private static void AppendPassions(StringBuilder sb, List<AssignmentTrace> trace)
        {
            var seen = new Dictionary<string, float>();
            foreach (var t in trace)
            {
                if (t.breakdown.passionSkill == null || t.breakdown.noRelevantSkills)
                    continue;
                seen[PassionScoreService.DebugNameFor(t.breakdown.passion)] = t.breakdown.passionBonus;
            }

            sb.AppendLine("--- PASSIONS IN PLAY (resolved bonus; * = pinned by a PassionScoreDef) ---");
            if (seen.Count == 0)
                sb.AppendLine("(none)");
            else
                foreach (var kvp in seen.OrderByDescending(k => k.Value))
                    sb.AppendLine($"  {kvp.Key,-32} {kvp.Value,6:+0.00;-0.00;+0.00}");
            sb.AppendLine();
        }

        private static void AppendLegend(StringBuilder sb)
        {
            sb.AppendLine("--- HOW A NUMBER BECOMES A PRIORITY ---");
            sb.AppendLine("  raw   = (skillWeight*avgLvl01 + passion) * statF + insp   (0 at worst, deliberately not capped at 1)");
            sb.AppendLine("  blend = clamp01((1-relativeWeight)*raw + relativeWeight*(raw/colonyBest))   <- ranking + cutoff use this");
            sb.AppendLine("  norm  = urgency + qualityWeight*(blend-0.5)   (pinned/everyone jobs: norm = urgency)");
            sb.AppendLine("          inspired pawns floor norm at inspirationUrgency; opposed pawns scale blend by opposedWorkFactor first");
            sb.AppendLine("  prio  = clamp(fromNormalized(norm) + falloff, highest, lowest)   (0 = not assigned)");
            sb.AppendLine("  statF = 1 + workStatWeight*weightFactor*(avgStat-1), from the work type's WorkTypeStatsDef,");
            sb.AppendLine("          where avgStat averages each stat over its baseline (shown as Stat/baseline when not 1).");
            sb.AppendLine("          Each stat has the pawn's own skill divided back out, so statF is trait/gene/bionic/health");
            sb.AppendLine("          only and sits near 1.00 for an ordinary pawn. A stat marked (!skill) folds skill in as an");
            sb.AppendLine("          offset, which cannot be divided out - that one still double-counts its skill.");
            sb.AppendLine("  flags: O=ideo-opposed  I=inspired  L=locked cell  D=differs from current");
            sb.AppendLine();
        }

        private static void AppendByWorkType(StringBuilder sb, List<AssignmentTrace> rows, bool diffOnly)
        {
            foreach (var group in rows.GroupBy(r => r.workType).OrderBy(g => g.Key.defName))
            {
                var any = group.First();
                var p = any.policy;
                var config = WorkTypeStatsDef.For(group.Key);
                string statNames = config.stats == null || config.stats.Count == 0
                    ? "none"
                    : string.Join("+", config.stats.Select(s =>
                          s.stat.defName
                          + (s.baseline != 1f ? $"/{s.baseline:0.##}" : "")
                          + (PawnPriorityScorer.SkillDrivenByOffset(s.stat) ? "(!skill)" : "")))
                      + (config.weightFactor != 1f ? $"  weightFactor={config.weightFactor:0.##}" : "");

                sb.AppendLine($"### {group.Key.defName}  ({group.Key.labelShort})");
                sb.AppendLine($"    policy: urgency={p.urgency:0.00} cutoff={p.scoreCutoff:0.00} minWorkers={p.minWorkers} "
                              + $"maxFrac={p.maxWorkersFraction:0.00} everyone={YN(p.assignEveryone)} pin={YN(p.pinPriority)} falloff={YN(p.priorityFalloff)}");
                sb.AppendLine($"    capable={any.capableCount} maxWorkers={any.maxWorkers} colonyBest={any.colonyBest:0.000} stats={statNames}");
                if (any.externalMode != ExternalOffloadMode.Off && any.externalCapacity > 0f)
                    sb.AppendLine($"    external: capacity={any.externalCapacity:0.00} x uptime {any.externalUptime:0.##} "
                                  + $"-> -{any.externalReduction} workers (mode={any.externalMode}, floor={any.externalFloor})");
                sb.AppendLine("    rank pawn         cur prop    raw   blend  avgLvl  passion                          statF  insp   norm  flg  reason");

                foreach (var r in group.OrderBy(r => r.rank))
                    sb.AppendLine("    " + Row(r));

                if (diffOnly)
                    sb.AppendLine($"    (only differing rows shown; {any.capableCount} pawns were ranked)");
                sb.AppendLine();
            }
        }

        private static void AppendByPawn(StringBuilder sb, List<AssignmentTrace> rows)
        {
            foreach (var group in rows.GroupBy(r => r.pawn))
            {
                sb.AppendLine($"### PAWN: {group.Key.LabelShortCap}");
                sb.AppendLine("    work                 cur prop    raw   blend rank/cap  avgLvl  passion                          statF  insp   norm  flg  reason");
                foreach (var r in group.OrderBy(r => r.workType.defName))
                {
                    var p = r.policy;
                    sb.AppendLine($"    {Trunc(r.workType.defName, 20),-20} {Cur(r.current),3} {r.proposed,4}  {r.breakdown.raw,6:0.000} "
                                  + $"{r.blended,6:0.000} {r.rank + 1,4}/{r.capableCount,-4} {Levels(r.breakdown),7} "
                                  + $"{Trunc(PassionCell(r.breakdown), 32),-32} {r.breakdown.statFactor,5:0.00} {r.breakdown.inspirationBonus,5:0.00} "
                                  + $"{Norm(r),6} {Flags(r),-4} {r.reason}");
                    sb.AppendLine($"                         ^ policy: urgency={p.urgency:0.00} cutoff={p.scoreCutoff:0.00} minW={p.minWorkers} "
                                  + $"maxFrac={p.maxWorkersFraction:0.00} everyone={YN(p.assignEveryone)} pin={YN(p.pinPriority)} colonyBest={r.colonyBest:0.000}");
                }
                sb.AppendLine();
            }
        }

        private static string Row(AssignmentTrace r) =>
            $"{r.rank + 1,4} {Trunc(r.pawn.LabelShortCap, 12),-12} {Cur(r.current),3} {r.proposed,4}  "
            + $"{r.breakdown.raw,6:0.000} {r.blended,6:0.000} {Levels(r.breakdown),7} "
            + $"{Trunc(PassionCell(r.breakdown), 32),-32} {r.breakdown.statFactor,5:0.00} {r.breakdown.inspirationBonus,5:0.00} "
            + $"{Norm(r),6} {Flags(r),-4} {r.reason}";

        private static void AppendCoverage(StringBuilder sb, List<AssignmentTrace> trace)
        {
            sb.AppendLine("--- COVERAGE / LOCKS ---");
            bool anyNote = false;

            foreach (var group in trace.GroupBy(t => t.workType).OrderBy(g => g.Key.defName))
            {
                if (!group.Any(t => t.proposed > 0))
                {
                    var first = group.First();
                    if (first.externalReduction > 0)
                        sb.AppendLine($"  OFFLOADED: {group.Key.defName} - no colonist assigned; "
                                      + $"{first.externalCapacity:0.0} external capacity (mode {first.externalMode}).");
                    else
                        sb.AppendLine($"  NOT COVERED: {group.Key.defName} - {group.Count()} capable colonist(s) but none assigned.");
                    anyNote = true;
                }
            }

            foreach (var wt in WorkTypeDiscoveryService.GetAllWorkTypes())
            {
                if (PriorityLockManager.IsWorkTypeLocked(wt))
                {
                    sb.AppendLine($"  LOCKED work type (not recomputed): {wt.defName}");
                    anyNote = true;
                }
                else if (!trace.Any(t => t.workType == wt))
                {
                    sb.AppendLine($"  NOT COVERABLE: {wt.defName} - no scored colonist can do this work.");
                    anyNote = true;
                }
            }

            int lockedCells = trace.Count(t => t.cellLocked);
            if (lockedCells > 0)
            {
                sb.AppendLine($"  LOCKED cells kept at their current value: {lockedCells}");
                anyNote = true;
            }

            if (!anyNote)
                sb.AppendLine("  nothing to report.");
            sb.AppendLine();
        }

        private static string PassionCell(in ScoreBreakdown b)
        {
            if (b.noRelevantSkills)
                return $"(no skills; base {ScoringConfig.NoSkillWorkScore:0.00})";
            if (b.passionSkill == null)
                return "(no usable skill)";
            return $"{b.passionSkill.defName}:{PassionScoreService.DebugNameFor(b.passion)}{b.passionBonus:+0.00;-0.00;+0.00}";
        }

        private static string Levels(in ScoreBreakdown b) =>
            b.noRelevantSkills || b.relevantSkillCount == 0
                ? "-"
                : $"{b.totalLevel / b.relevantSkillCount:0.0}/20";

        private static string Norm(in AssignmentTrace r) => r.assigned ? r.normalized.ToString("0.000") : "-";

        private static string Cur(int current) => current < 0 ? "?" : current.ToString();

        private static string Flags(in AssignmentTrace r)
        {
            var s = new StringBuilder(4);
            if (r.opposed) s.Append('O');
            if (r.inspired) s.Append('I');
            if (r.cellLocked) s.Append('L');
            if (r.proposed != r.current) s.Append('D');
            return s.Length == 0 ? "-" : s.ToString();
        }

        private static string F(string key, float value) => $"{key}={value:0.00}{Star(key)}";
        private static string B(string key, bool value) => $"{key}={YN(value)}{Star(key)}";
        private static string Star(string key) => AdaptivePrioritiesMod.Settings?.IsOverridden(key) == true ? "*" : "";
        private static string YN(bool b) => b ? "y" : "n";
        private static string Trunc(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "~";
    }
}
