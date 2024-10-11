using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fashion_Wardrobe
{
    public class FWMod : Mod
    {
        public static int HATWeakerLoadrIndex = -1;
        //public static bool APPIsLoadered = false;
        internal static FWSetting setting;
        public FWMod(ModContentPack content) : base(content)
        {
            setting = GetSettings<FWSetting>();
            HATWeakerLoadrIndex = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "AB.HATweaker");
            //int a = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "nalsnoir.ApparelPaperPattern");
            //int b = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == content.PackageIdPlayerFacing);
            /*if (a != -1 && a < b)
            {
                APPIsLoadered = true;
                //FashionOverrideComp.patchForAPP = new FashionOverrideComp.PatchForAPP();
            }*/
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Default_EnableFashion".Translate(), ref FWSetting.DefaultEnableFashion);
            ls.CheckboxLabeled("Only_Colonist".Translate(), ref FWSetting.OnlyForColonist);
            ls.CheckboxLabeled("Show_InDoorFight".Translate(), ref FWSetting.ShowInDoorFight);
            ls.End();
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
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

    public class FWSetting : ModSettings
    {
        internal static bool OnlyForColonist = true;
        internal static bool ShowInDoorFight = false;
        internal static bool DefaultEnableFashion = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref OnlyForColonist, "OnlyForColonist", true, true);
            Scribe_Values.Look(ref ShowInDoorFight, "ShowInDoorFight", false, true);
            Scribe_Values.Look(ref DefaultEnableFashion, "DefaultEnableFashion", false, true);
        }
    }


    public class FashionOverrideComp : ThingComp
    {
        private Pawn Pawn => (parent as Pawn);

        internal bool FashionClothesEnable;
        internal ThingOwner<Apparel> Clothes;
        internal List<Apparel> FashionApparel = new List<Apparel>();
        internal Dictionary<string, FWCompData> DrawRule = new Dictionary<string, FWCompData>();

        private bool draft;
        private bool underRoof;

        //internal static PatchForAPP patchForAPP = null;
        internal bool Draft
        {
            get { return draft; }
            set
            {
                if (draft != value)
                {
                    draft = value;
                    //Log.Warning("2");
                    draftValueChange = true;
                }
            }
        }

        public bool draftValueChange = false;

        internal bool UnderRoof
        {
            get { return underRoof; }
            set
            {
                if (underRoof != value)
                {
                    underRoof = value;
                    //Log.Warning("1");
                    UnderRoofChange = true;

                }
            }
        }
        public bool UnderRoofChange = false;
        public bool listion = false;

        public FashionOverrideComp()
        {
            FashionClothesEnable = FWSetting.DefaultEnableFashion;
            Clothes = new ThingOwner<Apparel>(new ApparelHolder(this));
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref FashionClothesEnable, "FashionClothesEnable", true);
            Scribe_Deep.Look(ref Clothes, "clothes", new object[]
            {
                new ApparelHolder(this)
            });
            if (Clothes == null)
            {
                Clothes = new ThingOwner<Apparel>(new ApparelHolder(this));
            }
            Scribe_Collections.Look(ref DrawRule, "DrawRule", LookMode.Value, LookMode.Deep);
            if (DrawRule == null)
            {
                DrawRule = new Dictionary<string, FWCompData>();
            }
            else if (DrawRule.Count > 0)
            {
                List<string> list = DrawRule.Keys.ToList();
                for (int x = 0; x < list.Count; x++)
                {
                    string a = list[x];
                    if (DrawRule[a] == null)
                    {
                        DrawRule[a] = new FWCompData();
                    }

                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Draft = Pawn.Drafted;
            if (!FWSetting.OnlyForColonist || Pawn.IsColonist)
            {
                if (Pawn.Map != null && Pawn.Position != null)
                {
                    UnderRoof = !Pawn.Position.UsesOutdoorTemperature(Pawn.Map);
                }
                if (draftValueChange || UnderRoofChange)
                {
                    if (FWMod.HATWeakerLoadrIndex == -1 && Pawn.apparel != null)
                    {
                        Pawn.apparel.Notify_ApparelChanged();
                    }
                    draftValueChange = false;
                    UnderRoofChange = false;
                }
            }
        }

        public List<Apparel> GetApparel()
        {
            List<Apparel> list0;
            if (Clothes.Count != 0 && FashionClothesEnable)
            {
                list0 = new List<Apparel>(Clothes);
            }
            else
            {
                list0 = new List<Apparel>();
            }

            List<Apparel> list1;
            if (!Pawn.apparel.WornApparel.NullOrEmpty())
            {
                list1 = new List<Apparel>(Pawn.apparel.WornApparel);
                if (!list0.NullOrEmpty())
                {
                    list1.RemoveAll(a => a.def.apparel.layers.Any(b => list0.Any(c => c.def.apparel.layers.Contains(b))));
                    list1.AddRange(list0);
                }
            }
            else
            {
                list1 = new List<Apparel>();
                if (!list0.NullOrEmpty())
                {
                    list1.AddRange(list0);
                }
            }
            RemoveNoDisplayGraphic(ref list1);
            return list1;
        }
        public void RemoveNoDisplayGraphic(ref List<Apparel> apparels)
        {
            apparels.RemoveAll(a =>
            {
                if (!DrawRule.NullOrEmpty() && DrawRule.ContainsKey(a.def.defName))
                {
                    {
                        FWCompData data = DrawRule[a.def.defName];
                        if (data.Hide)
                        {
                            return true;
                        }
                        if (!Pawn.Drafted)
                        {
                            if (data.HideNoFight)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (FWSetting.ShowInDoorFight)
                            {
                                return false;
                            }
                        }

                        if (data.HideInDoor && Pawn.Map != null && Pawn.Position != null && !Pawn.Position.UsesOutdoorTemperature(Pawn.Map))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        public class FWCompData : IExposable
        {
            public bool Hide = false;
            public bool HideInDoor = false;
            public bool HideNoFight = false;
            public void ExposeData()
            {
                Scribe_Values.Look(ref Hide, "HideValue", false);
                Scribe_Values.Look(ref HideInDoor, "HideInDoor", false);
                Scribe_Values.Look(ref HideNoFight, "HideNoFight", false);
            }
        }
        public class ApparelHolder : IThingHolder
        {
            public FashionOverrideComp comp;
            public IThingHolder ParentHolder => comp.ParentHolder;

            public ApparelHolder(FashionOverrideComp comp)
            {
                this.comp = comp;
            }

            public void GetChildHolders(List<IThingHolder> outChildren)
            {
                ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
            }

            public ThingOwner GetDirectlyHeldThings()
            {
                return comp.Clothes ?? new ThingOwner<Apparel>();
            }
        }
        /*internal class PatchForAPP
        {
            public void GetGraphic(Pawn pawn, Apparel apparel, ref ApparelGraphicRecord apparelGraphicRecord)
            {
                MyApparelGraphicRecordGetter.TryGetGraphicApparelR1(MyApparelGraphicRecordGetter.GetDef(pawn.def.defName, pawn.story.bodyType, apparel), apparel, ref apparelGraphicRecord);
            }
        }*/
    }
    internal static class FW_Windows
    {
        private static Color WindowBGBorderColor = new ColorInt(97, 108, 122).ToColor;
        private static Color WindowBGFillColor = new ColorInt(43, 44, 45).ToColor;
        private static List<Color> colors = new List<Color>();
        static FW_Windows()
        {
            colors = (from c in DefDatabase<ColorDef>.AllDefsListForReading
                      select c.color).ToList<Color>();
            colors.Add(Color.white);
            colors.AddRange(from f in Find.FactionManager.AllFactionsVisible
                            select f.Color);
            colors = colors.Distinct().ToList();
            colors.SortByColor((Color c) => c);
        }
        private static void CheckboxLabeled(Rect rect, string label, ref bool checkOn, out bool click, bool disabled = false)
        {
            bool a = false;
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;

            Rect rect2 = rect;
            rect2.xMax -= 24f;
            Widgets.Label(rect2, label);
            if (!disabled && Widgets.ButtonInvisible(rect))
            {
                a = true;
                checkOn = !checkOn;
                if (checkOn)
                {
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                }
                else
                {
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                }
            }
            Widgets.CheckboxDraw(rect.x + rect.width - 24f, rect.y + (rect.height - 24f) / 2f, checkOn, disabled);
            Text.Anchor = anchor;
            click = a;
        }

        private static bool RadioTexture(Rect rect, bool select, Texture texture, string toolTip = "")
        {
            GUI.DrawTexture(rect, texture);
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, toolTip);
                Widgets.DrawHighlight(rect);
            }
            else
            if (select)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            return Widgets.ButtonInvisible(rect);
        }


        public class PawnApparelSettingWindow : Window
        {

            private int tabInt = 0;
            private Vector2 scrollPosition = Vector2.zero;
            internal Pawn pawn = null;
            private SelApparelWindow SelApparelWindow = null;

            public override Vector2 InitialSize => new Vector2(400f, 800f);
            public PawnApparelSettingWindow()
            {
                doCloseX = true;
                doCloseButton = true;
                draggable = true;
                forcePause = false;
                closeOnClickedOutside = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                float a = 0.05f;
                Widgets.Label(inRect.TopPart(a), "Fashion_Wardrobe".Translate());
                Rect rect0 = new Rect(inRect.x + inRect.width / 3, inRect.y + inRect.height * a, inRect.width * 2 / 3, inRect.height * 0.05f);
                List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Wear_Apparel".Translate(), () =>
                {
                    tabInt = 0;
                    scrollPosition = Vector2.zero;
                }, tabInt == 0),
                new TabRecord("Fashion_Apparel".Translate(), () =>
                {
                    tabInt = 1;
                    scrollPosition = Vector2.zero;
                }, tabInt == 1)
            };
                Rect rect1 = new Rect(inRect.x, inRect.y + inRect.height * a, inRect.width - 1f, inRect.height * 0.89f);
                TabDrawer.DrawTabs(rect0, tabs);
                GUI.color = WindowBGFillColor;
                GUI.DrawTexture(rect1, BaseContent.WhiteTex);
                GUI.color = WindowBGBorderColor;
                Widgets.DrawLineHorizontal(rect1.x, rect1.y, tabInt == 0 ? (rect1.width / 3 + 1f) : (rect1.width * 2 / 3 - 2f));
                if (tabInt == 0)
                {
                    Widgets.DrawLineHorizontal(rect1.x + (rect1.width * 2 / 3 + 4f), rect1.y, rect1.width / 3 - 4f);
                }
                Widgets.DrawLineHorizontal(rect1.x, rect1.y + rect1.height, rect1.width);
                Widgets.DrawLineVertical(rect1.x, rect1.y, rect1.height);
                Widgets.DrawLineVertical(rect1.x + rect1.width, rect1.y, rect1.height);
                GUI.color = Color.white;
                Rect rect2 = new Rect(rect1.x + 3f, rect1.y + 8f, rect1.width - 6f, rect1.height - 16f);
                FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                if (pawn != null && comp != null)
                {
                    if (tabInt == 0)
                    {
                        if (SelApparelWindow != null && SelApparelWindow.IsOpen)
                        {
                            SelApparelWindow.Close(false);
                        }
                        if (pawn.apparel != null)
                        {
                            List<Apparel> apparels = pawn.apparel.WornApparel.Where(b => !b.WornGraphicPath.NullOrEmpty()).ToList();
                            if (DrawScroll(rect2, comp, apparels))
                            {
                                pawn.apparel.Notify_ApparelChanged();
                            }
                        }
                    }
                    else
                    {
                        if (DrawScroll(rect2.TopPart(0.95f), comp))
                        {
                            pawn.apparel.Notify_ApparelChanged();
                        }
                        CheckboxLabeled(rect2.BottomPart(0.05f).LeftHalf(), "FashionClothes_Enable".Translate(), ref comp.FashionClothesEnable, out bool click);
                        if (click)
                        {
                            if (pawn.apparel != null)
                            {
                                pawn.apparel.Notify_ApparelChanged();
                            }
                        }

                        if (Widgets.ButtonText(rect2.BottomPart(0.05f).RightHalf(), "Add".Translate()))
                        {
                            if (SelApparelWindow == null)
                            {
                                SelApparelWindow = new SelApparelWindow();
                            }
                            if (!SelApparelWindow.IsOpen)
                            {
                                SelApparelWindow.pawn = pawn;
                                Find.WindowStack.Add(SelApparelWindow);
                            }
                        }
                    }
                }
            }

            private bool DrawScroll(Rect inRect, FashionOverrideComp comp, List<Apparel> apparels = null)
            {
                bool action = false;
                bool drawRemove = false;
                if (apparels == null)
                {
                    drawRemove = true;
                    apparels = comp.Clothes.InnerListForReading;
                }
                if (!apparels.NullOrEmpty())
                {
                    Rect view = new Rect(0, 0, inRect.width, 120f * apparels.Count);
                    Widgets.BeginScrollView(inRect, ref scrollPosition, view);
                    Rect iconLoc = new Rect(0, 0, 60f, 60f);
                    Rect butLoc = new Rect(4f, 80f, 52f, 20f);
                    Rect LabelLoc = new Rect(60f, 0, view.width - 60f, 20f);
                    Rect checkLoc = new Rect(60f, 20f, LabelLoc.width, 30f);
                    for (int x = 0; x < apparels.Count; x++)
                    {
                        bool d = false;
                        Apparel apparel = apparels[x];
                        Widgets.ThingIcon(iconLoc, apparel);
                        if (drawRemove && Widgets.ButtonText(butLoc, "Remove".Translate()))
                        {
                            comp.Clothes.Remove(apparel);
                            d = true;
                        }
                        GUI.Label(LabelLoc, apparel.Label, GUI.skin.button);
                        if (!comp.DrawRule.ContainsKey(apparel.def.defName))
                        {
                            comp.DrawRule.Add(apparel.def.defName, new FashionOverrideComp.FWCompData());
                        }
                        FashionOverrideComp.FWCompData data = comp.DrawRule[apparel.def.defName];
                        CheckboxLabeled(checkLoc, "Hide".Translate(), ref data.Hide, out bool a);
                        checkLoc.y += 30f;
                        CheckboxLabeled(checkLoc, "Hide_InDoor".Translate(), ref data.HideInDoor, out bool b, data.Hide);
                        checkLoc.y += 30f;
                        CheckboxLabeled(checkLoc, "Hide_NoFight".Translate(), ref data.HideNoFight, out bool c, data.Hide);
                        checkLoc.y += 35f;
                        Widgets.DrawLineHorizontal(view.x, checkLoc.y, view.width);
                        iconLoc.y += 120f;
                        butLoc.y += 120f;
                        LabelLoc.y += 120f;
                        checkLoc.y += 25f;
                        if (a || b || c || d)
                        {
                            action = true;
                        }
                    }
                    Widgets.EndScrollView();
                }
                return action;
            }

            public override void Close(bool doCloseSound = true)
            {
                base.Close(doCloseSound);
                tabInt = 0;
                scrollPosition = Vector2.zero;
                if (SelApparelWindow != null && SelApparelWindow.IsOpen)
                {
                    SelApparelWindow.Close(false);
                }
                if (pawn != null && pawn.apparel != null && pawn.GetComp<FashionOverrideComp>() != null)
                {
                    FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                    List<Apparel> list = new List<Apparel>();
                    list.AddRange(pawn.apparel.WornApparel);
                    list.AddRange(comp.Clothes);
                    List<string> name = list.Select(a => a.def.defName).ToList();
                    pawn.GetComp<FashionOverrideComp>().DrawRule.RemoveAll(a => !name.Contains(a.Key));
                    pawn.apparel.Notify_ApparelChanged();
                }
            }
        }
        public class SelApparelWindow : Window
        {
            private static List<ThingDef> AllapparelDef;
            internal Pawn pawn = null;
            public override string CloseButtonText => "Add".Translate();
            public override Vector2 InitialSize => new Vector2(600f, 800f);
            private Vector2 scrollPosition = Vector2.zero;
            private Vector2 scrollPosition_1 = Vector2.zero;
            private int choose = -1;
            private int showCount = 5;
            private Color RGB = Color.white;
            private Apparel apparel = null;
            private List<ThingStyleDef> styles = new List<ThingStyleDef>();
            private ThingStyleDef thingStyleDef = null;
            private string search = "";
            private ApparelLayerDef fliter = null;
            public SelApparelWindow()
            {
                doCloseButton = true;
                draggable = true;
                forcePause = false;
                closeOnClickedOutside = true;
                AllapparelDef = DefDatabase<ThingDef>.AllDefs.Where(a => a.IsApparel && !a.apparel.wornGraphicPath.NullOrEmpty()).ToList();
            }

            public override void DoWindowContents(Rect inRect)
            {
                Rect rect = new Rect(inRect.x, inRect.y, inRect.width - 1f, inRect.height * 0.94f);
                search = Widgets.TextEntryLabeled(rect.TopPart(0.03f), "Search", search);
                Rect rect0 = rect.BottomPart(0.96f);
                string fliterStr;
                if (fliter == null)
                {
                    fliterStr = "All".Translate();
                }
                else
                {
                    fliterStr = fliter.label;
                }
                if (Widgets.ButtonText(new Rect(rect0.x, rect0.y, rect0.width * 0.2f + 1f, 30f), fliterStr))
                {
                    List<FloatMenuOption> Options = new List<FloatMenuOption>();
                    for (int i = 0; i < DefDatabase<ApparelLayerDef>.AllDefs.Count(); i++)
                    {
                        ApparelLayerDef layerDef = DefDatabase<ApparelLayerDef>.AllDefsListForReading[i];
                        Options.Add(new FloatMenuOption(layerDef.label, () => fliter = layerDef));
                    }
                    Find.WindowStack.Add(new FloatMenu(Options));
                }
                Rect outRect = new Rect(rect0.x + 2f, rect0.y + 40f, rect0.width * 0.2f - 8f, rect0.height - 50f);
                Rect viewRect = new Rect(0, 0, outRect.width, (outRect.width + 40f) * showCount);
                Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, false);
                Rect viewOne = new Rect(0, 0, viewRect.width, viewRect.width);
                Rect textLoc = new Rect(0, viewRect.width, viewRect.width, 30f);
                int count = 0;
                for (int i = 0; i < AllapparelDef.Count; i++)
                {
                    ThingDef def = AllapparelDef[i];
                    if (def.label.IndexOf(search) != -1 && (fliter == null || def.apparel.layers.Contains(fliter)))
                    {
                        Widgets.DrawBox(viewOne);
                        if (RadioTexture(viewOne, false, def.uiIcon) || Widgets.RadioButtonLabeled(textLoc, def.label, choose == i))
                        {
                            choose = i;
                            if (def.CanBeStyled())
                            {
                                styles = DefDatabase<StyleCategoryDef>.AllDefs.SelectMany(a =>
                                {
                                    return a.thingDefStyles.Where(b => b.ThingDef == def).Select(c => c.StyleDef);
                                }).ToList();
                                thingStyleDef = null;
                            }
                            else
                            {
                                styles = new List<ThingStyleDef>();
                                thingStyleDef = null;
                            }
                            int index = UnityEngine.Random.Range(0, colors.Count - 1);
                            RGB = colors[index];
                            scrollPosition_1 = Vector2.zero;
                            apparel = null;
                        }
                        textLoc.y += 30f;
                        Widgets.DrawLineHorizontal(textLoc.x, textLoc.y, textLoc.width);
                        viewOne.y += viewRect.width + 40f;
                        textLoc.y += viewRect.width + 10f;
                        count++;
                    }
                }
                showCount = count;
                Widgets.EndScrollView();
                Widgets.DrawLineVertical(rect0.x + (rect0.width * 0.2f + 10f), rect0.y, rect0.height);
                Rect rect1 = new Rect(rect0.x + (rect0.width * 0.2f + 26f), rect0.y, rect0.width * 0.8f - 26f, rect0.height);
                if (choose != -1)
                {
                    ThingDef def = AllapparelDef[choose];
                    if (apparel == null || (apparel.def != def))
                    {
                        apparel = FWUtility.NewApparel(def);
                    }
                    if (def.CanBeStyled())
                    {
                        Rect rect2 = new Rect(rect1.x, rect1.y + rect1.height * 0.50f, rect1.width, rect.height * 0.09f);
                        if (!styles.NullOrEmpty())
                        {
                            float width = (rect2.height + 5f) * (styles.Count + 1);
                            Rect BG = new Rect(rect2.x, rect2.y - 5f, Math.Min(width, 5f * (rect2.height + 5f)) + 5f, rect2.height + 10f);
                            if (styles.Count > 5)
                            {
                                Rect rect3 = new Rect(BG.x, BG.y, rect2.width * 0.06f + 1f, BG.height);
                                if (Widgets.ButtonText(rect3, "<"))
                                {
                                    scrollPosition_1.x -= (rect2.height + 5f) * 5f;
                                }
                                rect3.x += BG.width + rect3.width + 10f;
                                if (Widgets.ButtonText(rect3, ">"))
                                {
                                    scrollPosition_1.x += (rect2.height + 5f) * 5f;
                                }
                                rect2.x += rect3.width + 5f;
                                BG.x += rect3.width + 5f;
                            }
                            Widgets.DrawBox(BG);
                            Widgets.DrawTitleBG(BG);
                            Widgets.BeginScrollView(new Rect(rect2.x + 5f, rect2.y, (rect2.height + 5f) * 5f, rect2.height), ref scrollPosition_1, new Rect(0, 0, width, rect2.height), false);
                            Rect GrapLoc = new Rect(0, 0, rect2.height, rect2.height);
                            if (RadioTexture(GrapLoc, thingStyleDef == null, def.uiIcon, def.label))
                            {
                                thingStyleDef = null;
                            }
                            GrapLoc.x += (rect2.height + 5f);
                            for (int x = 0; x < styles.Count; x++)
                            {
                                ThingStyleDef styleDef = styles[x];
                                string name;
                                if (!styleDef.label.NullOrEmpty())
                                {
                                    name = styleDef.label;
                                }
                                else if (!styleDef.overrideLabel.NullOrEmpty())
                                {
                                    name = styleDef.overrideLabel;
                                }
                                else
                                {
                                    name = styleDef.defName;
                                }
                                if (RadioTexture(GrapLoc, thingStyleDef == styleDef, styleDef.UIIcon, name))
                                {
                                    thingStyleDef = styleDef;
                                }
                                GrapLoc.x += (rect2.height + 5f);
                            }
                            Widgets.EndScrollView();
                        }
                        if (apparel.GetStyleDef() != thingStyleDef)
                        {
                            apparel.SetStyleDef(thingStyleDef);
                        }
                    }
                    CompColorable comp = apparel.GetComp<CompColorable>();
                    if (comp != null)
                    {
                        Widgets.ColorSelector(rect1.BottomPart(!styles.NullOrEmpty() ? 0.38f : 0.48f), ref RGB, colors, out float noUse);
                        if (apparel.DrawColor != RGB)
                        {
                            apparel.SetColor(RGB);
                        }
                    }
                    GUI.DrawTexture(rect1.TopPart(0.48f), thingStyleDef != null ? thingStyleDef.UIIcon : def.uiIcon, ScaleMode.ScaleToFit, true, 0f, apparel.DrawColor, 0f, 0f);

                }

            }
            public override void Close(bool doCloseSound = true)
            {
                base.Close(doCloseSound);
                if (pawn != null && pawn.GetComp<FashionOverrideComp>() != null)
                {
                    FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                    if (apparel != null && !comp.Clothes.Contains(apparel))
                    {
                        comp.Clothes.RemoveAll(a =>
                        {
                            if (a.def.apparel.layers.Any(b => apparel.def.apparel.layers.Contains(b)))
                            {
                                return a.def.apparel.bodyPartGroups.Any(b => apparel.def.apparel.bodyPartGroups.Contains(b));
                            }
                            return false;
                        });
                        comp.Clothes.TryAdd(apparel, 1);
                    }
                    if (pawn.apparel != null)
                    {
                        pawn.apparel.Notify_ApparelChanged();
                    }
                }
                choose = -1;
                RGB = Color.white;
                styles = new List<ThingStyleDef>();
                thingStyleDef = null;
                pawn = null;
                apparel = null;
                search = "";
                scrollPosition = Vector2.zero;
                scrollPosition_1 = Vector2.zero;
                fliter = null;
            }
        }
    }

    public static class FWUtility
    {
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
    }

    [StaticConstructorOnStartup]
    public static class HarmonyPatchA8
    {
        private static readonly FW_Windows.PawnApparelSettingWindow apparel_window = new FW_Windows.PawnApparelSettingWindow();
        internal static MethodInfo getPawn = null;
        internal static Type RPGInvType = null;
        private static readonly Type patch = typeof(HarmonyPatchA8);
        private static readonly Type renderTree = typeof(PawnRenderTree);
        static HarmonyPatchA8()
        {
            Harmony harmony = new Harmony("aedbia.fashionwardrobe");
            RPGInvType = AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab");
            if (RPGInvType == null)
            {
                getPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");
                harmony.Patch(AccessTools.Method(typeof(ITab_Pawn_Gear), "FillTab"), transpiler: new HarmonyMethod(patch, nameof(HarmonyPatchA8.TranFillTab)));
            }
            else
            {
                getPawn = AccessTools.PropertyGetter(RPGInvType, "SelPawnForGear");
                harmony.Patch(AccessTools.Method(RPGInvType, "FillTab"), transpiler: new HarmonyMethod(patch, nameof(HarmonyPatchA8.TranFillTab)));

            }
            MethodInfo setupApparel = AccessTools.Method(renderTree, "SetupApparelNodes");
            if (setupApparel != null)
            {
                harmony.Patch(setupApparel, transpiler: new HarmonyMethod(patch, nameof(TranSetupApparelNodes)));
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
            MethodInfo method = AccessTools.Method(typeof(HarmonyPatchA8), nameof(GetWearer));
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
            return holder is FashionOverrideComp.ApparelHolder ? (holder as FashionOverrideComp.ApparelHolder).comp.parent as Pawn : null;
        }

        public static IEnumerable<CodeInstruction> TranFillTab(IEnumerable<CodeInstruction> codes)
        {
            MethodInfo method = AccessTools.Method(typeof(HarmonyPatchA8), nameof(FillTab_1));
            MethodInfo method1 = AccessTools.Method(typeof(Widgets), nameof(Widgets.CheckboxLabeled));
            List<CodeInstruction> list = codes.ToList();
            bool a = false;
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction code = list[i];
                if (!a && code.opcode == OpCodes.Stloc_1)
                {
                    a = true;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, getPawn);
                    yield return new CodeInstruction(OpCodes.Call, method);
                }
                else if (!a && code.opcode == OpCodes.Call && code.operand == (object)method1)
                {
                    a = true;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 320f);
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0f);
                    yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(Rect), new Type[]
                    {
                        typeof(float),typeof(float),typeof(float), typeof(float)
                    }));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, getPawn);
                    yield return new CodeInstruction(OpCodes.Call, method);
                }
                else
                {
                    yield return code;
                }
            }
        }

        public static void FillTab_1(Rect rect, Pawn pawn)
        {
            if (pawn.GetComp<FashionOverrideComp>() != null && pawn.apparel != null && (!FWSetting.OnlyForColonist || (pawn != null && pawn.IsColonist)))
            {
                Rect rect1 = RPGInvType == null ? new Rect(5f, 5f, 130f, Text.LineHeight) : new Rect(rect.width - 150f, 5f, 130f, Text.LineHeight);
                if (Widgets.ButtonText(rect1, "Fashion_Wardrobe".Translate()))
                {
                    if (!apparel_window.IsOpen)
                    {
                        apparel_window.pawn = pawn;
                        Find.WindowStack.Add(apparel_window);
                    }
                }
            }
        }
        public static IEnumerable<CodeInstruction> TranSetupApparelNodes(IEnumerable<CodeInstruction> codes)
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
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(renderTree, nameof(PawnRenderTree.pawn)));
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
            if (pawn_Apparel.pawn != null && FWork(pawn_Apparel.pawn))
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
        private static List<Apparel> GetFWApparel(List<Apparel> origin, Pawn pawn)
        {
            if (pawn != null && FWork(pawn))
            {
                FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                return comp.GetApparel();
            }
            return origin;
        }
    }
}