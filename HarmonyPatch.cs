using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Fashion_Wardrobe
{
    [StaticConstructorOnStartup]
    public static class FWModHarmonyPatch
    {
        internal static MethodInfo getPawn = null;
        internal static Type RPGInvType = null;
        private static readonly Type patch = typeof(FWModHarmonyPatch);
        private static readonly Type renderTree = typeof(PawnRenderTree);
        private static readonly Type renderSetup = typeof(DynamicPawnRenderNodeSetup_Apparel);
        private static FieldInfo nodeSetupPwan = null;

        static FWModHarmonyPatch()
        {
            Harmony harmony = new Harmony("aedbia.fashionwardrobe");
            BindingFlags flag = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = renderSetup.GetNestedTypes(BindingFlags.NonPublic)?.Where(a => a.GetMethods(flag).Any(m => m.Name == "MoveNext") && a.Name.IndexOf("GetDynamicNodes") != -1 && a.Name.IndexOf("d__3") != -1).FirstOrDefault();
            MethodInfo setupApparel = null;
            if (type != null)
            {
                setupApparel = type.GetMethod("MoveNext", flag);
                nodeSetupPwan = type.GetField("pawn", flag);
            }
            if (setupApparel != null && nodeSetupPwan != null)
            {
                harmony.Patch(setupApparel, transpiler: new HarmonyMethod(patch, nameof(TranGetDynamicNodes)));
            }
            MethodInfo ApparelWearGetter = AccessTools.PropertyGetter(typeof(Apparel), "Wearer");
            if (ApparelWearGetter != null)
            {
                harmony.Patch(ApparelWearGetter, transpiler: new HarmonyMethod(patch, nameof(TranWearerGetter)));
            }
            if (LoadedModManager.RunningModsListForReading.FindIndex(mod => mod.PackageIdPlayerFacing == "AB.HATweaker") == -1)
            {
                MethodInfo adjustParms = AccessTools.Method(renderTree, "AdjustParms");
                if (adjustParms != null)
                {
                    harmony.Patch(adjustParms, transpiler: new HarmonyMethod(patch, nameof(TranAdjustParms)));
                }
            }
        }

        public static IEnumerable<CodeInstruction> TranWearerGetter(IEnumerable<CodeInstruction> codes)
        {
            MethodInfo method = AccessTools.Method(typeof(FWModHarmonyPatch), nameof(GetWearer));
            MethodInfo method0 = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.ParentHolder));
            List<CodeInstruction> list = codes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction code = list[i];
                if (code.opcode == OpCodes.Ldnull)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, method0);
                    yield return new CodeInstruction(OpCodes.Call, method);
                }
                else
                {
                    yield return code;
                }
            }
        }

        public static Pawn GetWearer(IThingHolder holder)
        {
            //Log.Warning((holder is FashionOverrideComp).ToStringSafe());
            return holder is FashionOverrideComp.FWApparelHolder ? (holder as FashionOverrideComp.FWApparelHolder).comp.parent as Pawn : null;
        }

        public static IEnumerable<CodeInstruction> TranGetDynamicNodes(IEnumerable<CodeInstruction> codes)
        {
            MethodInfo wornApparelCount = AccessTools.PropertyGetter(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.WornApparelCount));
            MethodInfo wornApparel = AccessTools.PropertyGetter(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.WornApparel));
            List<CodeInstruction> list = codes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction code = list[i];
                if (code.opcode == OpCodes.Callvirt && code.OperandIs(wornApparelCount))
                {
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(patch, nameof(GetDisplayApparelCount)));
                }
                else
                if (code.opcode == OpCodes.Callvirt && code.OperandIs(wornApparel))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, nodeSetupPwan);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(patch, nameof(GetFWApparel)));
                }
                else
                {
                    yield return code;
                }
            }
        }

        public static int GetDisplayApparelCount(Pawn_ApparelTracker pawn_Apparel)
        {
            if (FWork(pawn_Apparel.pawn))
            {
                FashionOverrideComp comp = pawn_Apparel.pawn.GetComp<FashionOverrideComp>();
                return comp.GetApparel().Count;
            }
            return pawn_Apparel.WornApparelCount;
        }

        public static IEnumerable<CodeInstruction> TranAdjustParms(IEnumerable<CodeInstruction> codes)
        {
            MethodInfo method = AccessTools.PropertyGetter(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.WornApparel));
            List<CodeInstruction> list = codes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction code = list[i];
                if (code.Is(OpCodes.Callvirt, method))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, typeof(PawnRenderTree).GetField("pawn"));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(patch, nameof(GetFWApparel)));
                }
                else
                {
                    yield return code;
                }
            }
        }

        public static bool FWork(Pawn pawn)
        {
            return pawn != null && FWUtility.FWork(pawn);
        }

        private static List<Apparel> GetFWApparel(List<Apparel> origin, Pawn pawn)
        {
            if (FWork(pawn))
            {
                FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                return comp.GetApparel();
            }
            return origin;
        }
    }
}
