using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static Verse.KeyPrefs;

namespace Fashion_Wardrobe
{
    public class FWMod : Mod
    {
        public static int HATWeakerLoadrIndex = -1;
        internal static FWSetting setting;
        private static KeyPrefsData keyPrefsData;
        private static Dialog_DefineBinding Dialog_DefineBinding;
        private bool normal = true;
        private Vector2 scrollPosition = Vector2.zero;
        private QuickSearchWidget quickSearch = new QuickSearchWidget();
        private bool refilter = true;
        private List<ThingDef> filteredList = new List<ThingDef>();
        private ThingDef chooseDef = null;
        public FWMod(ModContentPack content) : base(content)
        {
            setting = GetSettings<FWSetting>();
            HATWeakerLoadrIndex = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "AB.HATweaker");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect0 = inRect.TopPart(0.05f);
            string title = normal ? "Default".Translate() : "Back".Translate();
            if (Widgets.ButtonText(normal ? rect0 : rect0.RightPart(0.3f), title))
            {
                normal = !normal;
            }
            Rect rect1 = inRect.BottomPart(0.945f);
            Widgets.DrawWindowBackground(rect1);
            if (normal)
            {
                DoNormalSettingsContents(rect1.ContractedBy(5f));
            }
            else
            {
                quickSearch.OnGUI(rect0.LeftPart(0.69f), () => refilter = true, () => refilter = true);
                DoDefaultDataContents(rect1.ContractedBy(5f));
            }
        }
        private void DoDefaultDataContents(Rect inRect)
        {
            if (refilter)
            {
                filteredList = DefDatabase<ThingDef>.AllDefs.Where(def => def.IsApparel && quickSearch.filter.Matches(def.label)).ToList();
            }
            Rect lRect = inRect.LeftPart(0.3f);
            Widgets.DrawBoxSolid(lRect, FW_Windows.WindowBGFillColor);
            if (!filteredList.NullOrEmpty())
            {
                quickSearch.noResultsMatched = false;
                Rect viewRect = new Rect(0, 0, lRect.width, 35f * filteredList.Count);
                Rect uRect = new Rect(5f, 0, lRect.width - 10f, 30f);
                Rect iconRect = new Rect(7, 2, 26f, 26f);
                Rect labelRect = new Rect(35, 3, uRect.width - 30f, 24f);
                Widgets.BeginScrollView(lRect, ref scrollPosition, viewRect, false);
                for (int i = 0; i < filteredList.Count; i++)
                {
                    if (uRect.y + uRect.height > scrollPosition.y && uRect.y < scrollPosition.y + inRect.height)
                    {
                        ThingDef def = filteredList[i];
                        Widgets.DrawBoxSolid(uRect, FW_Windows.WidgetFillColor);
                        if (chooseDef == def)
                        {
                            Widgets.DrawHighlightSelected(uRect);
                        }
                        Widgets.DrawHighlightIfMouseover(uRect);
                        if (def.uiIcon != null)
                        {
                            GUI.DrawTexture(iconRect, def.uiIcon);
                        }
                        Widgets.Label(labelRect, def.label);
                        if (Widgets.ButtonInvisible(uRect))
                        {
                            if (chooseDef == def)
                            {
                                chooseDef = null;
                            }
                            else
                            {
                                chooseDef = def;
                                if (FWSetting.DefaultCompDatas == null)
                                {
                                    FWSetting.DefaultCompDatas = new Dictionary<string, FWCompData>();
                                }
                                if (FWSetting.DefaultCompDatas.Count == 0 || !FWSetting.DefaultCompDatas.ContainsKey(def.defName))
                                {
                                    FWSetting.DefaultCompDatas.Add(def.defName, new FWCompData());
                                }
                            }
                        }
                    }
                    uRect.y += 35f;
                    iconRect.y += 35f;
                    labelRect.y += 35f;
                }
                Widgets.EndScrollView();
            }
            else
            {
                quickSearch.noResultsMatched = true;
            }
            if (chooseDef != null && !FWSetting.DefaultCompDatas.NullOrEmpty() && FWSetting.DefaultCompDatas.TryGetValue(chooseDef.defName, out FWCompData data))
            {
                Rect rect0 = new Rect(lRect.x + lRect.width + 5f, lRect.y, inRect.width - lRect.width - 10f, 30f);
                Widgets.DrawHighlightIfMouseover(rect0);
                Widgets.CheckboxLabeled(rect0, "Hide".Translate(), ref data.Hide);
                rect0.y += 35f;
                Widgets.DrawHighlightIfMouseover(rect0);
                Widgets.CheckboxLabeled(rect0, "Hide_InDoor".Translate(), ref data.HideInDoor);
                rect0.y += 35f;
                Widgets.DrawHighlightIfMouseover(rect0);
                Widgets.CheckboxLabeled(rect0, "ShowInVacuum".Translate(), ref data.HideNonVacuum);
                rect0.y += 35f;
                Widgets.DrawHighlightIfMouseover(rect0);
                Widgets.CheckboxLabeled(rect0, "Hide_NoFight".Translate(), ref data.HideNoFight);
            }
        }
        private void DoNormalSettingsContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Default_EnableFashion".Translate(), ref FWSetting.DefaultEnableFashion);
            ls.CheckboxLabeled("Only_Colonist".Translate(), ref FWSetting.OnlyForColonist);
            ls.CheckboxLabeled("Show_InDoorFight".Translate(), ref FWSetting.ShowInDoorFight);
            ls.CheckboxLabeled("CanAddConflicting".Translate(), ref FWSetting.CanAddConflict);
            ls.CheckboxLabeled("OnlyShowFashion".Translate(), ref FWSetting.OnlyShowFashion);
            ls.CheckboxLabeled("Enable_MainButton".Translate(), ref FWSetting.EnableMainButton);
            ls.CheckboxLabeled("Direct_Open_FW".Translate(), ref FWSetting.Direct_Open_FW);
            if (!FWSetting.Direct_Open_FW)
            {
                ls.CheckboxLabeled("Enable_Preview".Translate(), ref FWSetting.EnablePreview);
            }
            if (FWMainTabDefOf.FW_FashionTab != null && FWMainTabDefOf.FW_FashionTab.hotKey != null)
            {
                if (keyPrefsData == null)
                {
                    keyPrefsData = KeyPrefs.KeyPrefsData.Clone();
                }
                KeyBindingDef keyDef = FWMainTabDefOf.FW_FashionTab.hotKey;
                BindingSlot slot = BindingSlot.A;
                if (ls.ButtonTextLabeled("Set_key".Translate(), keyPrefsData.GetBoundKeyCode(keyDef, slot).ToStringReadable()))
                {
                    if (Event.current.button == 0)
                    {
                        if (Dialog_DefineBinding == null)
                        {
                            Dialog_DefineBinding = new Dialog_DefineBinding(keyPrefsData, keyDef, BindingSlot.A);
                        }
                        Find.WindowStack.Add(Dialog_DefineBinding);
                        Event.current.Use();
                    }
                    else
                    if (Event.current.button == 1)
                    {
                        List<FloatMenuOption> list = new List<FloatMenuOption>();
                        list.Add(new FloatMenuOption("ResetBinding".Translate(), delegate ()
                        {
                            KeyCode keyCode = keyDef.defaultKeyCodeA;
                            keyPrefsData.SetBinding(keyDef, slot, keyCode);
                            ApplyKey();
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        list.Add(new FloatMenuOption("ClearBinding".Translate(), delegate ()
                        {
                            keyPrefsData.SetBinding(keyDef, slot, KeyCode.None);
                            ApplyKey();
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        Find.WindowStack.Add(new FloatMenu(list));
                    }
                }
                if (Dialog_DefineBinding != null && !Dialog_DefineBinding.IsOpen)
                {
                    ApplyKey();
                }
            }
            ls.End();
        }
        private static void ApplyKey()
        {
            if (keyPrefsData != null)
            {
                KeyPrefs.KeyPrefsData = keyPrefsData;
                KeyPrefs.Save();
                Event.current.Use();
                keyPrefsData = null;
            }
            Dialog_DefineBinding = null;
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            filteredList = new List<ThingDef>();
            refilter = true;
            Map map = Find.CurrentMap;
            if (map != null)
            {
                List<Pawn> list = map.mapPawns.FreeColonists;
                if (!list.NullOrEmpty())
                {
                    foreach (Pawn pawn in list)
                    {
                        FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                        if (comp != null && pawn.apparel != null)
                        {
                            pawn.apparel.Notify_ApparelChanged();
                        }
                    }
                }
            }

        }

        public override string SettingsCategory()
        {
            return Content.Name;
        }
    }

    public static class FWorkers
    {
        public class FW_MainButtonWorker : MainButtonWorker_ToggleTab
        {
            public override bool Visible
            {
                get
                {
                    return FWSetting.EnableMainButton;
                }
            }

            public override void Activate()
            {
                if (FWSetting.Direct_Open_FW)
                {
                    Pawn pawn = Find.Selector?.SelectedPawns.FirstOrDefault();
                    if (pawn != null && FWUtility.FWork(pawn) && !FW_Windows.apparel_window.IsOpen)
                    {
                        FW_Windows.apparel_window.pawn = pawn;
                        Find.WindowStack.Add(FW_Windows.apparel_window);
                    }
                }
                else
                {
                    base.Activate();
                }
                ;
            }
        }
    }

    public static class FWUtility
    {
        public static bool FWork(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.GetComp<FashionOverrideComp>() == null || pawn.apparel == null)
            {
                return false;
            }
            if (FWSetting.OnlyForColonist && (!pawn.IsColonist))
            {
                return false;
            }
            return true;
        }

        public static Apparel NewApparel(ThingDef def)
        {
            if (def == null)
            {
                return null;
            }
            ThingDef stuff = null;
            if (def.MadeFromStuff)
            {
                stuff = GenStuff.DefaultStuffFor(def);
            }
            Apparel thing = (Apparel)ThingMaker.MakeThing(def, stuff);
            return thing;
        }
        public static void SetApparelIDNumberWithPathIndex(this Apparel apparel, int number)
        {
            if (apparel.def.HasThingIDNumber)
            {
                var paths = apparel.def.apparel?.wornGraphicPaths;
                if (!paths.NullOrEmpty() && number < paths.Count && paths[number] != apparel.WornGraphicPath)
                {
                    apparel.thingIDNumber += number - apparel.thingIDNumber % paths.Count;
                }
            }
        }
        public static List<ThingStyleDef> GetThingStyleDefs(ThingDef def)
        {
            if (def == null||!def.CanBeStyled())
            {
                return new List<ThingStyleDef>();
            }
            List<ThingStyleDef> styles = DefDatabase<StyleCategoryDef>.AllDefs
                                .SelectMany(sc => sc.thingDefStyles?
                                    .Where(ts => ts.ThingDef == def)
                                    .Select(ts => ts.StyleDef) ?? Enumerable.Empty<ThingStyleDef>())
                                .ToList();
            var random = def.randomStyle?.Select(a => a.StyleDef);
            if (random != null && random.Any())
            {
                styles.AddRangeUnique(random);
            }
            return styles;
        }
    }

    [DefOf]
    public static class FWMainTabDefOf
    {
        public static MainButtonDef FW_FashionTab;
    }
}