#if UNITY_EDITOR
using StreetFight.Enum;
using StreetFight.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.EditorTools
{
    /// <summary>
    /// One-click generator for your existing Animator states (from the QuadPunch/ElbowPunch/
    /// CrossPunch/HookPunch/punch1 + LowKick/RoundKick/SideChick/KneeJab/FrontKicking set).
    /// Creates one AttackDataSO per state and pre-wires a default combo chain so you have
    /// something playable immediately — then tweak the chain in the Inspector to taste.
    ///
    /// Menu: Combat System > Generate Attack Data From Screenshot
    /// </summary>
    public static class ComboAssetGenerator
    {
        private struct Def
        {
            public string id;
            public string stateName;
            public AttackInputType type;
            public Def(string id, string stateName, AttackInputType type)
            {
                this.id = id; this.stateName = stateName; this.type = type;
            }
        }

        // Edit this list if your actual attack state names differ, or if FightIdel/Kicking
        // turn out to be real attacks in your project rather than idle poses.
        private static readonly Def[] Defs =
        {
            new Def("Punch1", "punch1",      AttackInputType.Punch),
            new Def("Punch2", "QuadPunch",   AttackInputType.Punch),
            new Def("Punch3", "ElbowPunch",  AttackInputType.Punch),
            new Def("Punch4", "CrossPunch",  AttackInputType.Punch),
            new Def("Punch5", "HookPunch",   AttackInputType.Punch),
            new Def("Kick1",  "LowKick",      AttackInputType.Kick),
            new Def("Kick2",  "RoundKick",    AttackInputType.Kick),
            new Def("Kick3",  "SideChick",    AttackInputType.Kick),
            new Def("Kick4",  "KneeJab",      AttackInputType.Kick),
            new Def("Kick5",  "FrontKicking", AttackInputType.Kick),
        };

        private const string Folder = "Assets/CombatSystem/Attacks";

        [MenuItem("Combat System/Generate Attack Data From Screenshot")]
        public static void Generate()
        {
            EnsureFolder();

            foreach (var def in Defs)
            {
                string path = $"{Folder}/{def.id}.asset";
                if (AssetDatabase.LoadAssetAtPath<AttackDataSO>(path) != null) continue;

                var asset = ScriptableObject.CreateInstance<AttackDataSO>();
                asset.attackId = def.id;
                asset.animatorStateName = def.stateName;
                asset.inputType = def.type;
                asset.transitionDuration = 0.08f;
                asset.safetyDuration = 1.0f;
                AssetDatabase.CreateAsset(asset, path);
            }
            AssetDatabase.SaveAssets();

            // Default chain: Punch1..5 in sequence, Kick1..5 in sequence, with a
            // punch<->kick cross-link back to each other's *first* hit at every step.
            LinkChain(new[] { "Punch1", "Punch2", "Punch3", "Punch4", "Punch5" }, AttackInputType.Kick, "Kick1");
            LinkChain(new[] { "Kick1", "Kick2", "Kick3", "Kick4", "Kick5" }, AttackInputType.Punch, "Punch1");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Combat System: generated/linked {Defs.Length} attacks in {Folder}");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CombatSystem"))
                AssetDatabase.CreateFolder("Assets", "CombatSystem");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/CombatSystem", "Attacks");
        }

        private static void LinkChain(string[] chainIds, AttackInputType crossType, string crossTargetId)
        {
            var sameType = crossType == AttackInputType.Punch ? AttackInputType.Kick : AttackInputType.Punch;
            var crossTarget = AssetDatabase.LoadAssetAtPath<AttackDataSO>($"{Folder}/{crossTargetId}.asset");

            for (int i = 0; i < chainIds.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<AttackDataSO>($"{Folder}/{chainIds[i]}.asset");
                if (asset == null) continue;

                asset.comboLinks.Clear();

                if (i < chainIds.Length - 1)
                {
                    var next = AssetDatabase.LoadAssetAtPath<AttackDataSO>($"{Folder}/{chainIds[i + 1]}.asset");
                    asset.comboLinks.Add(new ComboLink { requiredInput = sameType, nextAttack = next });
                }

                asset.comboLinks.Add(new ComboLink { requiredInput = crossType, nextAttack = crossTarget });
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
#endif
