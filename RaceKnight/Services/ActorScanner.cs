using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using RaceFilter.Model;

namespace RaceFilter.Services;

/// <summary>
/// 每帧遍历场景对象，同时按两类来源识别需要改动的 actor：
///   1) 种族规则（RaceRule）：按 Customize 的种族/性别/部族匹配；
///   2) 手动目标（ManualTarget）：按显示名匹配。
/// 命中后产出统一 MatchSpec 交给 Intervention。
/// 匹配以 "R:规则下标" / "M:手动目标下标" 为稳定标识，避免配置每帧重载导致引用不相等而闪烁。
/// </summary>
public sealed class ActorScanner : IDisposable
{
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IRaceIntervention intervention;

    private readonly Dictionary<ulong, string> matched = new();

    public ActorScanner(IObjectTable objectTable, IClientState clientState, IFramework framework, IRaceIntervention intervention)
    {
        this.objectTable = objectTable;
        this.clientState = clientState;
        this.framework = framework;
        this.intervention = intervention;
        framework.Update += OnUpdate;
    }

    public void Dispose() => framework.Update -= OnUpdate;

    private void OnUpdate(IFramework _)
    {
        var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var rules = config.Rules.Where(r => r.Enabled).ToList();
        var manuals = config.ManualTargets.Where(m => m.Enabled).ToList();

        var localId = objectTable.LocalPlayer?.GameObjectId ?? 0xFFFFFFFFFFFFFFFF;
        var seen = new HashSet<ulong>();

        for (var i = 0; i < objectTable.Length; i++)
        {
            var obj = objectTable[i];
            if (obj == null || obj is not ICharacter) // 只处理可读取 Customize 的角色类
                continue;
            if (obj.GameObjectId == localId) // 永不影响自己
                continue;

            var key = MatchKeyFor(obj, obj.ObjectKind, rules, manuals);
            if (key == null)
                continue;

            seen.Add(obj.GameObjectId);

            if (matched.TryGetValue(obj.GameObjectId, out var prev))
            {
                if (prev != key) // 命中来源变了：先还原再按新指令应用
                {
                    intervention.OnActorUnmatched(obj);
                    matched[obj.GameObjectId] = key;
                    intervention.OnActorMatched(obj, SpecFor(key, rules, manuals));
                }
            }
            else if (matched.TryAdd(obj.GameObjectId, key))
            {
                intervention.OnActorMatched(obj, SpecFor(key, rules, manuals));
            }
        }

        // 不再匹配到的（离开范围/种族变化/配置变更/名字消失）统一还原
        foreach (var kv in matched)
        {
            if (!seen.Contains(kv.Key))
            {
                var actor = FindByObjectId(kv.Key);
                if (actor != null)
                    intervention.OnActorUnmatched(actor);
                matched.Remove(kv.Key);
            }
        }
    }

    private static string? MatchKeyFor(IGameObject obj, ObjectKind kind, List<RaceRule> rules, List<ManualTarget> manuals)
    {
        // 1) 种族规则
        for (var idx = 0; idx < rules.Count; idx++)
        {
            var r = rules[idx];
            if (!ScopeCovers(r.Scope, kind))
                continue;
            var c = ((ICharacter)obj).Customize;
            if ((byte)r.TargetRace != c[(int)CustomizeIndex.Race])
                continue;
            if (r.TargetGender.HasValue && (byte)r.TargetGender.Value != c[(int)CustomizeIndex.Gender])
                continue;
            if (r.TargetTribe.HasValue && r.TargetTribe.Value != c[(int)CustomizeIndex.Tribe])
                continue;
            return "R:" + idx;
        }

        // 2) 手动目标（按名字）
        var name = obj.Name.TextValue.Trim();
        for (var idx = 0; idx < manuals.Count; idx++)
        {
            var m = manuals[idx];
            if (!ScopeCovers(m.Scope, kind))
                continue;
            if (string.Equals(name, m.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                return "M:" + idx;
        }

        return null;
    }

    private static MatchSpec SpecFor(string key, List<RaceRule> rules, List<ManualTarget> manuals)
    {
        var idx = int.Parse(key[2..]);
        if (key.StartsWith("R:"))
        {
            var r = rules[idx];
            return new MatchSpec(r.Action, r.ReplacementRace, r.ReplacementGender, r.ReplacementTribe);
        }

        var m = manuals[idx];
        return new MatchSpec(m.Action, m.ReplacementRace, m.ReplacementGender, m.ReplacementTribe);
    }

    private static bool ScopeCovers(TargetScope scope, ObjectKind kind)
    {
        var isPlayer = (int)kind == 1; // ObjectKind.Player == 1
        return scope switch
        {
            TargetScope.PlayersAndNpcs => true,
            TargetScope.PlayersOnly => isPlayer,
            TargetScope.NpcsOnly => !isPlayer,
            _ => false,
        };
    }

    private IGameObject? FindByObjectId(ulong id)
    {
        for (var i = 0; i < objectTable.Length; i++)
        {
            var o = objectTable[i];
            if (o != null && o.GameObjectId == id)
                return o;
        }

        return null;
    }
}
