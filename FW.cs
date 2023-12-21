using ApparelPaperPattern;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fashion_Wardrobe
{
    public class FWMod : Mod
    {
        public static int HATWeakerLoadrIndex = -1;
        public static bool APPIsLoadered = false;
        public FWMod(ModContentPack content) : base(content)
        {
            HATWeakerLoadrIndex = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "AB.HATweaker");
            int a = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "nalsnoir.ApparelPaperPattern");
            int b = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == content.PackageIdPlayerFacing);
            if (a != -1 && a < b)
            {
                APPIsLoadered = true;
                FashionOverrideComp.patchForAPP = new FashionOverrideComp.PatchForAPP();
            }
            Harmony harmony = new Harmony(Content.PackageId);
            new HarmonyPatchA8(harmony);
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Only_Colonist".Translate(), ref FWSetting.OnlyForColonist);
            ls.CheckboxLabeled("Show_InDoorFight".Translate(), ref FWSetting.ShowInDoorFight);
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
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref OnlyForColonist, "OnlyForColonist", true, true);
            Scribe_Values.Look(ref ShowInDoorFight, "ShowInDoorFight", false, true);
        }
    }

    public class FashionOverrideComp : ThingComp
    {
        private Pawn Pawn => (parent as Pawn);

        internal bool FashionClothesEnable = true;

        internal ThingOwner<Apparel> Clothes = new ThingOwner<Apparel>();
        internal List<ApparelGraphicRecord> FashionApparel = new List<ApparelGraphicRecord>();
        internal Dictionary<string, FWCompData> DrawRule = new Dictionary<string, FWCompData>();

        private bool draft;
        private bool underRoof;

        internal static PatchForAPP patchForAPP = null;
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

        internal bool ReCollect = true;
        public bool listion = false;


        public override void PostExposeData()
        {
            Scribe_Values.Look(ref FashionClothesEnable, "FashionClothesEnable", true);
            Scribe_Deep.Look(ref Clothes, "clothes");
            if (Clothes == null)
            {
                Clothes = new ThingOwner<Apparel>();
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
            if (Pawn.Map != null && Pawn.Position != null)
            {
                UnderRoof = !Pawn.Position.UsesOutdoorTemperature(Pawn.Map);
            }
            if (FWMod.HATWeakerLoadrIndex == -1 && (draftValueChange || UnderRoofChange))
            {
                if (Pawn.apparel != null)
                {
                    Pawn.apparel.Notify_ApparelChanged();
                }
                draftValueChange = false;
                UnderRoofChange = false;
            }
        }

        public void ApplyApparel()
        {
            if (FashionClothesEnable)
            {
                if (Pawn.story != null && ReCollect)
                {
                    MakeFashionApparel();
                    ReCollect = false;
                    //Log.Warning("b");
                }
                if (!Clothes.InnerListForReading.NullOrEmpty())
                {
                    //Log.Warning("a");
                    Pawn.Drawer.renderer.graphics.apparelGraphics = new List<ApparelGraphicRecord>(FashionApparel);
                }
            }
            Pawn.Drawer.renderer.graphics.apparelGraphics.RemoveAll(a =>
            {
                if (!DrawRule.NullOrEmpty() && DrawRule.ContainsKey(a.sourceApparel.def.defName))
                {
                    {
                        FWCompData data = DrawRule[a.sourceApparel.def.defName];
                        if (data.Hide)
                        {
                            return true;
                        }
                        else if (data.HideNoFight)
                        {
                            if (!Pawn.Drafted)
                            {
                                return true;
                            }
                            else if (FWSetting.ShowInDoorFight)
                            {
                                return false;
                            }
                        }
                        else if (data.HideInDoor && Pawn.Map != null && Pawn.Position != null && !Pawn.Position.UsesOutdoorTemperature(Pawn.Map))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        private void MakeFashionApparel()
        {
            List<ApparelGraphicRecord> list0 = new List<ApparelGraphicRecord>();
            if (Clothes.Count != 0)
            {

                foreach (Apparel item in Clothes)
                {
                    ApparelGraphicRecordGetter.TryGetGraphicApparel(item, Pawn.story.bodyType, out ApparelGraphicRecord a);
                    if (FWMod.APPIsLoadered)
                    {
                        //Log.Warning("1");
                        if (patchForAPP != null)
                        {
                            patchForAPP.GetGraphic(Pawn, item, ref a);
                        }
                    }
                    list0.Add(a);

                }
            }
            FashionApparel = new List<ApparelGraphicRecord>();
            List<ApparelGraphicRecord> list1 = Pawn.Drawer.renderer.graphics.apparelGraphics;
            if (!list1.NullOrEmpty())
            {
                FashionApparel.AddRange(list1);
            }
            //Log.Warning(FashionApparel.Count.ToString());
            if (!list0.NullOrEmpty())
            {
                FashionApparel.RemoveAll(a => a.sourceApparel.def.apparel.layers.Any(b => list0.Any(c => c.sourceApparel.def.apparel.layers.Contains(b))));
                FashionApparel.AddRange(list0);
            }
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
        internal class PatchForAPP
        {
            public void GetGraphic(Pawn pawn, Apparel apparel, ref ApparelGraphicRecord apparelGraphicRecord)
            {
                MyApparelGraphicRecordGetter.TryGetGraphicApparelR1(MyApparelGraphicRecordGetter.GetDef(pawn.def.defName, pawn.story.bodyType, apparel), apparel, ref apparelGraphicRecord);
            }
        }
    }
    internal static class FW_Windows
    {
        private static Color WindowBGBorderColor = new ColorInt(97, 108, 122).ToColor;
        private static Color WindowBGFillColor = new ColorInt(43, 44, 45).ToColor;

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
            }, tabInt == 0),
                new TabRecord("Fashion_Apparel".Translate(), () =>
                {
                    tabInt = 1;
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
                        bool a = false;
                        bool b = false;
                        bool c = false;
                        bool d = false;
                        Apparel apparel = apparels[x];
                        Texture2D texture = apparel.def.uiIcon;
                        GUI.DrawTexture(iconLoc, texture, ScaleMode.StretchToFill, true, 0f, apparel.DrawColor, 0f, 0f);
                        if (drawRemove && Widgets.ButtonText(butLoc, "Remove".Translate()))
                        {
                            comp.Clothes.Remove(apparel);
                            comp.ReCollect = true;
                            d = true;
                        }
                        GUI.Label(LabelLoc, apparel.Label, GUI.skin.button);
                        if (!comp.DrawRule.ContainsKey(apparel.def.defName))
                        {
                            comp.DrawRule.Add(apparel.def.defName, new FashionOverrideComp.FWCompData());
                        }
                        FashionOverrideComp.FWCompData data = comp.DrawRule[apparel.def.defName];
                        CheckboxLabeled(checkLoc, "Hide".Translate(), ref data.Hide, out a);
                        checkLoc.y += 30f;
                        CheckboxLabeled(checkLoc, "Hide_InDoor".Translate(), ref data.HideInDoor, out b, data.Hide);
                        checkLoc.y += 30f;
                        CheckboxLabeled(checkLoc, "Hide_NoFight".Translate(), ref data.HideNoFight, out c, data.Hide);
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
            private int choose = -1;
            private Color RGB = new ColorInt(0, 0, 0).ToColor;
            private List<Color> colors = new List<Color>();
            private Apparel apparel = null;
            private string search = "";
            public SelApparelWindow()
            {
                doCloseButton = true;
                draggable = true;
                forcePause = false;
                closeOnClickedOutside = true;
                AllapparelDef = DefDatabase<ThingDef>.AllDefs.Where(a => a.IsApparel && !a.apparel.wornGraphicPath.NullOrEmpty()).ToList();
                colors = (from c in DefDatabase<ColorDef>.AllDefsListForReading
                          select c.color).ToList<Color>();
                colors.AddRange(from f in Find.FactionManager.AllFactionsVisible
                                select f.Color);
                colors.SortByColor((Color c) => c);
                RGB = colors.Last();
            }

            public override void DoWindowContents(Rect inRect)
            {
                Rect rect = new Rect(inRect.x, inRect.y, inRect.width - 1f, inRect.height * 0.94f);
                search = Widgets.TextEntryLabeled(rect.TopPart(0.03f), "Search", search);
                Rect rect0 = rect.BottomPart(0.97f);
                Rect outRect = new Rect(rect0.x + 2f, rect0.y + 8f, rect0.width * 0.2f - 8f, rect0.height - 18f);
                Rect viewRect = new Rect(0, 0, outRect.width, (outRect.width + 40f) * AllapparelDef.Count);
                Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, false);
                Rect viewOne = new Rect(0, 0, viewRect.width, viewRect.width);
                Rect textLoc = new Rect(0, viewRect.width, viewRect.width, 30f);
                for (int i = 0; i < AllapparelDef.Count; i++)
                {
                    ThingDef def = AllapparelDef[i];
                    if (def.label.IndexOf(search) != -1)
                    {
                        GUI.DrawTexture(viewOne, def.uiIcon);
                        Widgets.DrawBox(viewOne);
                        if (Widgets.RadioButtonLabeled(textLoc, def.label, choose == i))
                        {
                            choose = i;
                        }
                        textLoc.y += 30f;
                        Widgets.DrawLineHorizontal(textLoc.x, textLoc.y, textLoc.width);
                        viewOne.y += viewRect.width + 40f;
                        textLoc.y += viewRect.width + 10f;
                    }
                }
                Widgets.EndScrollView();
                Widgets.DrawLineVertical(rect0.x + (rect0.width * 0.2f + 10f), rect0.y, rect0.height);
                Rect rect1 = new Rect(rect0.x + (rect0.width * 0.2f + 26f), rect0.y, rect0.width * 0.8f - 26f, rect0.height);
                if (choose != -1)
                {
                    float noUse;
                    Widgets.ColorSelector(rect1.BottomPart(0.4f), ref RGB, colors, out noUse);
                    ThingDef def = AllapparelDef[choose];
                    apparel = FWUtility.NewApparel(def, RGB);
                    if (apparel != null)
                    {
                        GUI.DrawTexture(rect1.TopPart(0.58f), apparel.def.uiIcon, ScaleMode.ScaleToFit, true, 0f, apparel.DrawColor, 0f, 0f);
                    }
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
                        comp.Clothes.RemoveAll(a => a.def.apparel.layers.Any(b => apparel.def.apparel.layers.Contains(b)));
                        comp.Clothes.TryAdd(apparel, 1);
                        comp.ReCollect = true;
                    }
                    if (pawn.apparel != null)
                    {
                        pawn.apparel.Notify_ApparelChanged();
                    }
                }
                choose = -1;
                RGB = colors.Last();
                pawn = null;
                search = "";
            }
        }
    }

    public static class FWUtility
    {
        public static Apparel NewApparel(ThingDef def, Color color)
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
            CompColorable comp = thing.GetComp<CompColorable>();
            if (comp != null)
            {
                thing.SetColor(color);
            }
            return thing;
        }
    }


    public class HarmonyPatchA8
    {
        private static readonly FW_Windows.PawnApparelSettingWindow apparel_window = new FW_Windows.PawnApparelSettingWindow();
        private static readonly MethodInfo getPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), "SelPawnForGear");


        public HarmonyPatchA8(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ITab_Pawn_Gear), "FillTab"), prefix: new HarmonyMethod(typeof(HarmonyPatchA8), nameof(HarmonyPatchA8.preFillTab)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderer), "RenderPawnInternal"), prefix: new HarmonyMethod(typeof(HarmonyPatchA8), nameof(HarmonyPatchA8.PreRenderPawnInternal)));
        }
        public static void preFillTab(ref ITab_Pawn_Gear __instance)
        {
            Pawn pawn = (Pawn)getPawn.Invoke(__instance, new object[0]);
            if (pawn.GetComp<FashionOverrideComp>() != null && (!FWSetting.OnlyForColonist) || (pawn != null && pawn.IsColonist))
            {
                Rect rect = new Rect(5f, 5f, 130f, Text.LineHeight);
                if (Widgets.ButtonText(rect, "Fashion_Wardrobe".Translate()))
                {
                    if (!apparel_window.IsOpen)
                    {
                        apparel_window.pawn = pawn;
                        Find.WindowStack.Add(apparel_window);
                    }
                }
            }
        }
        public static void PreRenderPawnInternal(Pawn ___pawn)
        {
            Pawn pawn = ___pawn;
            if (pawn.GetComp<FashionOverrideComp>() == null)
            {
                return;
            }
            if (FWSetting.OnlyForColonist && (!pawn.IsColonist))
            {
                return;
            }
            FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
            comp.ApplyApparel();
        }
    }
}
