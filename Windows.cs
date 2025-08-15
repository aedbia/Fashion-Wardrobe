using ABEasyLib.ABExtensions;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Fashion_Wardrobe
{
    public static class FW_Windows
    {
        public static readonly Color WindowBGBorderColor = new ColorInt(97, 108, 122).ToColor;
        public static readonly Color WindowBGFillColor = new ColorInt(43, 44, 45).ToColor;
        internal static readonly PawnApparelSettingWindow apparel_window = new PawnApparelSettingWindow();
        private static readonly List<ThingDef> AllapparelDef = DefDatabase<ThingDef>.AllDefs.Where(a => a.IsApparel).ToList();
        private static readonly QuickSearchWidget quickSearch = new QuickSearchWidget();
        private static bool reFilter = true;
        private static Texture2D settingTex;
        public static readonly Color WidgetFillColor = new ColorInt(64, 64, 64).ToColor;
        public static Texture2D SettingTex
        {
            get
            {
                if (settingTex == null)
                {
                    try
                    {
                        settingTex = ContentFinder<Texture2D>.Get(OptionCategoryDefOf.General.texPath, false);
                    }
                    catch
                    {
                        Log.Warning("[Fashion Wardrobe] Failed to load Tex");
                    }
                }
                return settingTex;
            }
        }

        private static readonly GUIStyle MidLStyle = new GUIStyle(Text.fontStyles[1])

        {
            alignment = TextAnchor.MiddleLeft
        };

        private static readonly GUIStyle MidCStyle = new GUIStyle(Text.fontStyles[1])
        {
            alignment = TextAnchor.MiddleCenter
        };

        private static FWPresetData selPreset;
        private static FloatMenu floatMenu;
        private static readonly PresetManagerWindow presetManager = new PresetManagerWindow();
        private static void NotifyReFliter()
        {
            reFilter = true;
        }
        private static void CreateFloatMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (!FWSetting.PresetDatas.NullOrEmpty())
            {
                for (int i = 0; i < FWSetting.PresetDatas.Count; i++)
                {
                    FWPresetData data = FWSetting.PresetDatas[i];
                    if (data != null)
                    {
                        FloatMenuOption option = new FloatMenuOption("Apply".Translate() + " " + data.Id, () => selPreset = data);
                        options.Add(option);
                    }
                }
                if (options.Count > 0)
                {
                    floatMenu = new FloatMenu(options);
                }
            }
            else
            {
                floatMenu = null;
            }
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

        private static Vector2 TipSize = new Vector2(100, 100);

        private static void DrawTooltipGrid(Texture texture)
        {
            if (texture == null)
            {
                return;
            }
            Texture[] a = { texture };
            ABWidgetsExtensions.DrawTooltipsGrid(a, TipSize);
        }

        public class PawnApparelSettingWindow : Window
        {
            private int tabInt = 0;
            private Vector2 scrollPosition = Vector2.zero;
            internal Pawn pawn = null;
            private SelApparelWindow SelApparelWindow = null;
            private FWColorAndStyleWidget colorAndStyle = null;

            private static List<Apparel> copys;
            private bool coStWork = false;
            public override Vector2 InitialSize => new Vector2(400f, 800f);

            public PawnApparelSettingWindow()
            {
                doCloseX = true;
                doCloseButton = true;
                draggable = true;
                forcePause = false;
                preventCameraMotion = false;
                closeOnClickedOutside = true;
            }

            public void CheckPawn()
            {
                Pawn selPawn = Find.Selector?.SelectedPawns.FirstOrDefault();
                if (FWUtility.FWork(selPawn) && pawn != selPawn)
                {
                    pawn = selPawn;
                }
            }

            public override void PreOpen()
            {
                base.PreOpen();
                CreateFloatMenu();
            }

            public override void DoWindowContents(Rect inRect)
            {
                CheckPawn();
                float a = 0.05f;
                Rect label0 = inRect.TopPart(a);
                Rect contectRect = inRect.BottomPart(1 - a);
                Widgets.Label(label0, "Fashion_Wardrobe".Translate());
                Rect rect0 = new Rect(contectRect.x + contectRect.height * 2 * a + 4f, contectRect.y + contectRect.height * a +1f, contectRect.width - contectRect.height * 2 * a - 6f, contectRect.height * a+2f);
                Rect copyLoc = new Rect(contectRect.x, contectRect.y, contectRect.height * 2 * a, contectRect.height * a);
                List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("Wear_Apparel".Translate(), () =>
                {
                    tabInt = 0;
                    scrollPosition = Vector2.zero;
                    coStWork = false;
                }, tabInt == 0),
                new TabRecord("Fashion_Apparel".Translate(), () =>
                {
                    tabInt = 1;
                    scrollPosition = Vector2.zero;
                    coStWork = false;
                }, tabInt == 1)
            };
                Rect rect1 = new Rect(contectRect.x, contectRect.y + contectRect.height * a, contectRect.width - 1f, contectRect.height * 0.89f);
                
                Widgets.DrawBoxSolidWithOutline(rect1, WindowBGFillColor,Widgets.SeparatorLineColor);
                TabDrawer.DrawTabs(rect0, tabs);
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
                            List<Apparel> apparels = pawn.apparel.WornApparel;
                            if (!apparels.NullOrEmpty())
                            {
                                if (Widgets.ButtonImage(copyLoc.LeftHalf(), TexButton.Copy))
                                {
                                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                                {
                                new FloatMenuOption("Copy".Translate(), () =>
                                {
                                    copys = new List<Apparel>(apparels);
                                }),
                                new FloatMenuOption("Create_Preset".Translate(), () =>
                                {
                                    if (FWSetting.PresetDatas == null)
                                    {
                                        FWSetting.PresetDatas = new List<FWPresetData>();
                                    }
                                    FWSetting.PresetDatas.Add(new FWPresetData(pawn.Name.ToStringSafe())
                                    {
                                        apparels = apparels.ToDictionary(k => k.def.defName, d => new FWPresetApparelData(d.DrawColor,d.StyleDef?.defName??""))
                                    });
                                    CreateFloatMenu();
                                    FWMod.setting.Write();
                                })
                            }));
                                }
                            }
                            if (DrawScroll(rect2, comp, apparels))
                            {
                                pawn.apparel.Notify_ApparelChanged();
                            }
                        }
                    }
                    else
                    {
                        bool ccn = comp.Clothes != null;
                        bool cos = !coStWork;
                        if (ccn && comp.Clothes.Count != 0 && Widgets.ButtonImage(copyLoc.LeftHalf(), TexButton.Copy))
                        {
                            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                            {
                                new FloatMenuOption("Copy".Translate(), () =>
                                {
                                    copys = new List<Apparel>(comp.Clothes);
                                }),
                                new FloatMenuOption("Create_Preset".Translate(), () =>
                                {
                                    if (FWSetting.PresetDatas == null)
                                    {
                                        FWSetting.PresetDatas = new List<FWPresetData>();
                                    }
                                    FWSetting.PresetDatas.Add(new FWPresetData(pawn.Name.ToStringSafe())
                                    {
                                        apparels = comp.Clothes.InnerListForReading.ToDictionary(k => k.def.defName, d => new FWPresetApparelData(d.DrawColor,d.StyleDef?.defName??""))
                                    });
                                    CreateFloatMenu();
                                    FWMod.setting.Write();
                                })
                            }));
                        }
                        if (!copys.NullOrEmpty() && Widgets.ButtonImage(copyLoc.RightHalf(), TexButton.Paste, tooltip: "Paste".Translate()))
                        {
                            comp.SortCloths(ref copys);
                            //Log.Warning(copys.Count.ToString());
                            foreach (Apparel item in copys)
                            {
                                if (ccn)
                                {
                                    comp.AddApparel(item, true);
                                }
                            }
                        }
                        if (!cos)
                        {
                            if (colorAndStyle != null)
                            {
                                bool aa = colorAndStyle.def?.CanBeStyled() ?? false;
                                bool ab = colorAndStyle.def?.comps?.Any(t => typeof(CompColorable).IsAssignableFrom(t.compClass)) ?? false;
                                colorAndStyle.DoContentsWithCloseAction(rect2, true, aa, ab, () =>
                                {
                                    pawn.apparel.Notify_ApparelChanged();
                                });
                            }
                        }else if (DrawScroll(rect2.TopPart(0.95f), comp))
                        {
                            pawn.apparel.Notify_ApparelChanged();
                        }
                        float height = rect2.height * 0.05f;
                        Rect sr = new Rect(rect2.x, rect2.y + rect2.height - height, height, height);
                        
                        if (cos&&Widgets.ButtonImage(sr, SettingTex))
                        {
                            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                            {
                                new FloatMenuOption("FashionClothes_Enable".Translate(), () =>
                                {
                                    comp.FashionClothesEnable = !comp.FashionClothesEnable;
                                    if (pawn.apparel != null)
                                    {
                                        pawn.apparel.Notify_ApparelChanged();
                                    }
                                },extraPartWidth:20f,extraPartOnGUI: r =>
                                {
                                    Widgets.CheckboxDraw(r.x, r.y,comp.FashionClothesEnable,false);
                                    return false;
                                }){
                                    extraPartRightJustified = true
                                },
                                new FloatMenuOption("OnlyNoDraft".Translate(), () =>
                                {
                                    comp.OnWorkNoDraft = !comp.OnWorkNoDraft;
                                    if (pawn.apparel != null)
                                    {
                                        pawn.apparel.Notify_ApparelChanged();
                                    }
                                },extraPartWidth:20f,extraPartOnGUI: r =>
                                {
                                    Widgets.CheckboxDraw(r.x, r.y,comp.OnWorkNoDraft,false);
                                    return false;
                                }){
                                    extraPartRightJustified = true
                                }
                            }));
                        }
                        Rect rect3 = new Rect(sr.x + sr.width + 5f, sr.y, (rect2.width - sr.width - 5) / 2, sr.height);
                        
                        if (cos&&Widgets.ButtonText(rect3, "Preset".Translate()))
                        {
                            if (floatMenu != null)
                            {
                                Find.WindowStack.Add(floatMenu);
                            }
                        }
                        if (selPreset != null)
                        {
                            FashionOverrideComp comp1 = pawn.TryGetComp<FashionOverrideComp>();
                            if (comp1 != null && pawn.apparel != null)
                            {
                                comp1.ApplyPreset(selPreset);
                                pawn.apparel.Notify_ApparelChanged();
                            }
                            selPreset = null;
                        }
                        rect3.x += rect3.width;
                        if (cos&&Widgets.ButtonText(rect3, "Manage".Translate()))
                        {
                            if (!presetManager.IsOpen)
                            {
                                reFilter = true;
                                Find.WindowStack.Add(presetManager);
                            }
                        }
                    }
                }
            }

            private bool DrawScroll(Rect inRect, FashionOverrideComp comp, List<Apparel> apparels = null)
            {
                bool active = false;
                bool drawRemove = false;
                if (apparels == null)
                {
                    drawRemove = true;
                    apparels = new List<Apparel>(comp.Clothes);
                    comp.SortCloths(ref apparels);
                }
                float height = 120f * apparels.Count + (drawRemove ? 60f : 0);
                bool a0 = height > inRect.height;
                Rect view = new Rect(0, 0, inRect.width - (a0 ? 16f : 0), height);
                Widgets.BeginScrollView(inRect, ref scrollPosition, view);
                Rect iconLoc = new Rect(0, 0, 60f, 60f);
                if (!apparels.NullOrEmpty())
                {
                    Rect butLoc = new Rect(4f, 80f, 52f, 20f);
                    Rect LabelLoc = new Rect(60f, 0, view.width - 60f, 20f);
                    Rect checkLoc = new Rect(60f, 20f, LabelLoc.width, 30f);
                    for (int x = 0; x < apparels.Count; x++)
                    {
                        bool d = false;
                        Apparel apparel = apparels[x];
                        Widgets.ThingIcon(iconLoc, apparel);
                        if (drawRemove && apparel.GetComp<CompColorable>() != null)
                        {
                            if (Mouse.IsOver(iconLoc))
                            {
                                Widgets.DrawHighlight(iconLoc);
                                TooltipHandler.TipRegion(iconLoc, "Choose".Translate() + " " + "Color".Translate());
                            }
                            if (Widgets.ButtonInvisible(iconLoc))
                            {
                                if (colorAndStyle == null)
                                {
                                    colorAndStyle = new FWColorAndStyleWidget();
                                }
                                coStWork = true;
                                colorAndStyle.def = apparel.def;
                                colorAndStyle.Open = false;
                                colorAndStyle.closeAction = (color, style) =>
                                {
                                    if (apparel.GetComp<CompColorable>() != null)
                                    {
                                        apparel.SetColor(color);
                                    }
                                    if (apparel.def.CanBeStyled())
                                    {
                                        apparel.SetStyleDef(style);
                                    }
                                    coStWork = false;
                                };
                            }
                        }
                        if (drawRemove && Widgets.ButtonText(butLoc, "Remove".Translate()))
                        {
                            comp.Clothes.Remove(apparel);
                            d = true;
                        }
                        string label = drawRemove ? apparel.LabelNoParenthesisCap : apparel.LabelCap;
                        Widgets.DrawBoxSolid(LabelLoc, WidgetFillColor);
                        GUI.Label(LabelLoc, label, MidCStyle);
                        if (!comp.DrawRule.ContainsKey(apparel.def.defName))
                        {
                            comp.DrawRule.Add(apparel.def.defName, new FWCompData());
                        }
                        FWCompData data = comp.DrawRule[apparel.def.defName];
                        CheckboxLabeled(checkLoc, "Hide".Translate(), ref data.Hide, out bool a);
                        checkLoc.y += 30f;
                        CheckboxLabeled(checkLoc, "Hide_InDoor".Translate(), ref data.HideInDoor, out bool b, data.Hide);
                        bool c = false;
                        checkLoc.y += 30f;
                        if (!drawRemove || !comp.OnWorkNoDraft)
                        {
                            CheckboxLabeled(checkLoc, "Hide_NoFight".Translate(), ref data.HideNoFight, out c, data.Hide);
                        }
                        checkLoc.y += 35f;
                        Widgets.DrawLineHorizontal(view.x, checkLoc.y, view.width);
                        iconLoc.y += 120f;
                        butLoc.y += 120f;
                        LabelLoc.y += 120f;
                        checkLoc.y += 25f;
                        if (a || b || c || d)
                        {
                            active = true;
                        }
                    }
                }
                if (drawRemove)
                {
                    Rect rect0 = new Rect(iconLoc.x + 4f, iconLoc.y + 4f, view.width - 8f, 52f);
                    Rect addIcon = new Rect(rect0.x, rect0.y, 52f, 52f);
                    Rect addLoc = new Rect(rect0.x + 56f, rect0.y, rect0.width - 56f, 52f);
                    Widgets.DrawBoxSolidWithOutline(rect0, WidgetFillColor, Color.gray);
                    GUI.DrawTexture(addIcon, TexButton.Add);
                    GUI.Label(addLoc, "Add".Translate(), MidCStyle);
                    Widgets.DrawHighlightIfMouseover(rect0);
                    if (Widgets.ButtonInvisible(rect0))
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
                Widgets.EndScrollView();
                return active;
            }

            public override void Close(bool doCloseSound = true)
            {
                base.Close(doCloseSound);
                tabInt = 0;
                scrollPosition = Vector2.zero;
                coStWork = false;
                colorAndStyle = null;
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
        public class FWColorAndStyleWidget
        {
            private bool open = false;
            private Color color = new ColorInt(255, 255, 255).ToColor;
            private ThingStyleDef selDef = null;
            private readonly string[] buffer = new string[4];
            private int mode = 0;
            private bool drag = false;
            private bool styleWork = false;
            private bool colorWork = false;
            private readonly int[] rgba = new int[4]
            {
                255,
                255,
                255,
                255
            };
            internal ThingDef def;
            internal Action<Color, ThingStyleDef> closeAction;

            private List<Color> colors;
            public bool Open
            {
                get
                {
                    return open;
                }
                set
                {
                    if (value && open == false)
                    {
                        mode = 0;
                        drag = false;
                        styleWork = false;
                        colorWork = false;
                        selDef = null;
                        var list = DefDatabase<ColorDef>.AllDefs.Select(a => a.color);
                        colors = list.Distinct().ToList();
                        color = colors.RandomElement();
                        RefreshBuffer();
                    }
                    open = value;
                }
            }
            public void DoContentsWithCloseAction(Rect inRect, bool DrawStyle = true, bool DrawColor = true, bool drawClose = true, Action extraAction = null)
            {
                Open = true;
                if (!Open)
                {
                    return;
                }
                Rect rect = new Rect(inRect.x, inRect.y, inRect.width, 70f);
                bool st = false;
                if (DoStyleContents(rect))
                {
                    rect.y += rect.height;
                    st = true;
                }
                rect.height = inRect.height - 35f - (st ? rect.height : 0);
                DoColorContents(rect);
                if (drawClose)
                {
                    Rect cr = new Rect(inRect.x + inRect.width / 2 - 50f, inRect.yMax - 30f, 100f, 30f);
                    if (Widgets.ButtonText(cr, "Apply".Translate()))
                    {
                        open = false;
                        closeAction?.Invoke(color, selDef);
                        extraAction?.Invoke();
                        def = null;
                    }
                }
                else if (styleWork || colorWork)
                {
                    styleWork = false;
                    colorWork = false;
                    closeAction?.Invoke(color, selDef);
                    extraAction?.Invoke();
                }
            }
            public bool DoStyleContents(Rect inRect)
            {
                if (def == null)
                {
                    return false;
                }
                if (!def.CanBeStyled())
                {
                    return false;
                }
                List<ThingStyleDef> styles = DefDatabase<StyleCategoryDef>.AllDefs
                                    .SelectMany(sc => sc.thingDefStyles?
                                        .Where(ts => ts.ThingDef == def)
                                        .Select(ts => ts.StyleDef) ?? Enumerable.Empty<ThingStyleDef>())
                                    .ToList();
                if (styles.NullOrEmpty())
                {
                    return false;
                }
                Rect rect0 = new Rect(inRect.x, inRect.y, inRect.width, 30f);
                Widgets.DrawBoxSolid(rect0, WidgetFillColor);
                GUI.Label(rect0, "Style".Translate(), MidCStyle);
                rect0.y += 35f;
                string defa = "Default".Translate();
                string label = selDef == null ? defa : selDef.defName;
                Texture2D tex = selDef?.UIIcon ?? def.uiIcon;
                Rect rect = rect0.LeftPart(0.7f);
                Rect icr = new Rect(rect.x, rect.y, rect.height, rect.height);
                Rect lar = new Rect(rect.x + rect.height + 5f, rect.y + 3, rect.width - rect.height - 5f, rect.height - 6);
                if (tex != null)
                {
                    GUI.DrawTexture(icr, tex, ScaleMode.ScaleToFit, true, 0, color, 0, 0);
                }
                Widgets.Label(lar, label);
                if (Widgets.ButtonText(rect0.RightPart(0.3f), "Change".Translate()))
                {
                    var ls = new List<FloatMenuOption>();
                    foreach (var st in styles)
                    {
                        ls.Add(new FloatMenuOption(st.defName, () =>
                        {
                            selDef = st;
                            styleWork = true;
                        }));
                    }
                    ls.Add(new FloatMenuOption(defa, () =>
                    {
                        selDef = null;
                        styleWork = true;
                    }));
                    Find.WindowStack.Add(new FloatMenu(ls));
                }

                return true;
            }
            public void DoColorContents(Rect inRect)
            {
                int id = GetHashCode();
                Rect rect0 = new Rect(inRect.x, inRect.y, inRect.width, 30f);
                Widgets.DrawBoxSolid(rect0, WidgetFillColor);
                GUI.Label(rect0, "Color".Translate(), MidCStyle);
                rect0.y += 35f;
                Rect mr = new Rect(rect0.x + 5f, rect0.y, (rect0.width - 20f) / 3, rect0.height);
                string[] modes = new string[] {
                        "Box".Translate(),
                        "RGBA".Translate(),
                        "HSV".Translate()
                    };

                for (int i = 0; i < modes.Length; i++)
                {
                    string label = modes[i];
                    Widgets.DrawBoxSolid(mr, WidgetFillColor);
                    if (mode == i)
                    {
                        Widgets.DrawHighlightSelected(mr);
                    }
                    Widgets.DrawHighlightIfMouseover(mr);
                    GUI.Label(mr, label, MidCStyle);
                    if (Widgets.ButtonInvisible(mr))
                    {
                        mode = i;
                    }
                    mr.x += mr.width + 5f;
                }
                rect0.y += 35f;
                float maxHeight = inRect.y + inRect.height - rect0.y;
                switch (mode)
                {
                    case 0:
                        rect0.height = maxHeight;
                        DrawModeOne(rect0);
                        break;
                    case 1:
                        rect0.height = Mathf.Min(maxHeight / 4, 30);
                        DrawModeTwo(rect0);
                        break;
                    case 2:
                        rect0.height = maxHeight;
                        DrawModeThree(rect0);
                        break;
                }
            }

            private void RefreshBuffer()
            {
                rgba[0] = (int)(color.r * 255);
                rgba[1] = (int)(color.g * 255);
                rgba[2] = (int)(color.b * 255);
                rgba[3] = (int)(color.a * 255);
                buffer[0] = rgba[0].ToString();
                buffer[1] = rgba[1].ToString();
                buffer[2] = rgba[2].ToString();
                buffer[3] = rgba[3].ToString();
            }
            private void DrawModeOne(Rect rect0)
            {
                float mar = (rect0.width + 2f) % 28f;
                rect0.x += mar / 2;
                rect0.width -= mar;
                if (Widgets.ColorSelector(rect0, ref color, colors, out var height))
                {
                    colorWork = true;
                    RefreshBuffer();
                }
            }

            private void DrawModeTwo(Rect rect0)
            {
                float enWidth = Mathf.Min(40, (int)rect0.width / 5) * 4f + Text.CalcSize("255").x + 5f;
                string r = "Red".Translate();
                string b = "Blue".Translate();
                string g = "Green".Translate();
                string a = "Alpha".Translate();
                var rf = Text.CalcSize(r);
                float bf = Text.CalcSize(b).x;
                float gf = Text.CalcSize(g).x;
                float af = Text.CalcSize(a).x;
                float rgbaWidth = Mathf.Max(rf.x, bf, gf, af);
                Rect lt = new Rect(rect0.x, rect0.y + 15 - rf.y / 2, rgbaWidth, rf.y);
                Rect et = new Rect(lt.x + lt.width + 5f, rect0.y, enWidth, rect0.height);
                Rect cr = new Rect(rect0.x + rect0.width - 120f, et.y, 4 * et.height, 4 * et.height + 15f);
                bool right = false;
                if ((lt.width + et.width + 5f) * 4 <= rect0.width - cr.width - 5f)
                {
                    right = true;
                }
                if (lt.width + et.width + cr.width + 5f > rect0.width)
                {
                    cr.x = rect0.x;
                    cr.y = rect0.y;
                    cr.width = rect0.width;
                    float h = cr.height + 5f;
                    lt.y += h;
                    et.y += h;
                    et.width = rect0.width - lt.width - 10f;
                }
                DrawRGBATextEntryLabel(ref lt, ref et, r, ref rgba[0], ref buffer[0], right);
                DrawRGBATextEntryLabel(ref lt, ref et, g, ref rgba[1], ref buffer[1], right);
                DrawRGBATextEntryLabel(ref lt, ref et, b, ref rgba[2], ref buffer[2], right);
                DrawRGBATextEntryLabel(ref lt, ref et, a, ref rgba[3], ref buffer[3], right);
                color = new ColorInt(rgba[0], rgba[1], rgba[2], rgba[3]).ToColor;
                Widgets.DrawBoxSolidWithOutline(cr, color, Color.white);
            }

            private void DrawModeThree(Rect rect0)
            {
                bool down = false;
                float height = rect0.height;
                if (rect0.height > rect0.width)
                {
                    height = rect0.width;
                    down = true;
                }
                Rect other = down ? new Rect(rect0.x, rect0.y + height, height, rect0.height - height) : new Rect(rect0.x + height, rect0.y, rect0.width - height, height);
                Rect wr = new Rect(rect0.x, rect0.y, height, height);
                Widgets.HSVColorWheel(wr, ref color, ref drag);
                RefreshBuffer();
                colorWork = drag;
                Widgets.DrawBoxSolidWithOutline(other, color, Color.white);
                string text = string.Format("R{0}\nG{1}\nB{2}", new object[]
                {
                    color.r.ToString("F2"),
                    color.g.ToString("F2"),
                    color.b.ToString("F2")
                }).Colorize(new Color(1 - color.r, 1 - color.g, 1 - color.b));
                float textHeight = Text.CalcSize(text).y;
                if (textHeight > other.height - 2f)
                {
                    text.Replace("\n", "");
                }
                GUI.Label(other, text, MidCStyle);
            }

            private void DrawRGBATextEntryLabel(ref Rect rect, ref Rect enRect, string label, ref int rgba, ref string buffer, bool right = false)
            {
                Widgets.Label(rect, label);
                int a = rgba;
                Widgets.IntEntry(enRect.RightPart(0.8f), ref rgba, ref buffer);
                if (a != rgba)
                {
                    colorWork = true;
                }
                if (rgba < 0)
                {
                    rgba = 0;
                    buffer = "0";
                }
                if (rgba > 255)
                {
                    rgba = 255;
                    buffer = "255";
                }
                if (right)
                {
                    float add = rect.width + enRect.width + 10f;
                    rect.x += add;
                    enRect.x += add;
                }
                else
                {
                    rect.y += enRect.height + 5f;
                    enRect.y += enRect.height + 5f;
                }
            }
        }

        public class SelApparelWindow : Window
        {
            internal Pawn pawn = null;
            public override string CloseButtonText => "Add".Translate();
            public override Vector2 InitialSize => new Vector2(600f, 800f);
            private Vector2 scrollPosition = Vector2.zero;
            private ThingDef choose;
            private Apparel apparel = null;
            private bool fliterByLayer = true;
            private ApparelLayerDef layerDef;
            private List<FloatMenuOption> Options = new List<FloatMenuOption>();
            private FWColorAndStyleWidget colorAndStyle;
            public SelApparelWindow()
            {
                doCloseButton = true;
                draggable = true;
                forcePause = false;
                closeOnClickedOutside = true;
                preventCameraMotion = false;
            }
            private static List<ThingDef> filteredApparels = new List<ThingDef>();
            public override void DoWindowContents(Rect inRect)
            {
                Rect rect = new Rect(inRect.x, inRect.y, inRect.width - 1f, inRect.height * 0.94f);
                quickSearch.OnGUI(rect.TopPart(0.03f), NotifyReFliter, NotifyReFliter);
                if (reFilter || fliterByLayer)
                {
                    reFilter = false;
                    filteredApparels = AllapparelDef.Where(a => quickSearch.filter.Matches(a.label) && a.IsApparel && (layerDef == null || a.apparel.layers.Contains(layerDef))).ToList();
                }
                Rect rect0 = rect.BottomPart(0.96f);
                string allTitle = "All".Translate();
                string fliterStr = layerDef?.label ?? allTitle;
                if (Widgets.ButtonText(new Rect(rect0.x, rect0.y, rect0.width * 0.2f + 1f, 30f), fliterStr))
                {
                    List<ApparelLayerDef> layers = DefDatabase<ApparelLayerDef>.AllDefsListForReading;
                    if (Options.NullOrEmpty())
                    {
                        for (int i = 0; i < layers.Count; i++)
                        {
                            ApparelLayerDef layerDef0 = layers[i];
                            if (AllapparelDef.Any(a => a.apparel.layers.Contains(layerDef0)))
                            {
                                Options.Add(new FloatMenuOption(layerDef0.label, () => { this.layerDef = layerDef0; fliterByLayer = true; }));
                            }
                        }
                        Options.Add(new FloatMenuOption(allTitle, () => { layerDef = null; fliterByLayer = true; }));
                    }
                    Find.WindowStack.Add(new FloatMenu(Options));
                }
                if (filteredApparels.NullOrEmpty())
                {
                    quickSearch.noResultsMatched = true;
                    return;
                }
                else
                {
                    quickSearch.noResultsMatched = false;
                    Rect outRect = new Rect(rect0.x + 2f, rect0.y + 35f, rect0.width * 0.21f, rect0.height - 35f);
                    float itemHeight = outRect.width + 30f;
                    int visibleItemCount = Mathf.CeilToInt(outRect.height / itemHeight) + 1;
                    Widgets.BeginScrollView(outRect, ref scrollPosition, new Rect(0, 0, outRect.width - 20f, filteredApparels.Count * itemHeight));
                    int startIndex = Mathf.FloorToInt(scrollPosition.y / itemHeight);
                    int endIndex = Mathf.Min(startIndex + visibleItemCount, filteredApparels.Count);
                    float v1 = outRect.width - 20f;
                    Rect viewOne = new Rect(0, startIndex * itemHeight, v1, v1);
                    Rect textLoc = new Rect(0, viewOne.y + viewOne.height, outRect.width - 20f, 50f);
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        ThingDef def = filteredApparels[i];
                        Widgets.DrawBox(viewOne);
                        if (RadioTexture(viewOne, false, def.uiIcon) ||
                            Widgets.RadioButtonLabeled(textLoc, def.label, choose == def))
                        {
                            choose = def;
                            apparel = null;
                            if (colorAndStyle != null)
                            {
                                colorAndStyle.Open = false;
                            }
                        }
                        textLoc.y += itemHeight;
                        viewOne.y += itemHeight;
                    }
                    Widgets.EndScrollView();
                }
                Widgets.DrawLineVertical(rect0.x + (rect0.width * 0.21f + 10f), rect0.y, rect0.height);
                Rect rect1 = new Rect(rect0.x + (rect0.width * 0.21f + 26f), rect0.y, rect0.width * 0.79f - 26f, rect0.height);
                if (choose != null)
                {
                    ThingDef def = choose;
                    if (apparel == null || (apparel.def != def))
                    {
                        apparel = FWUtility.NewApparel(def);
                    }
                    if (colorAndStyle == null)
                    {
                        colorAndStyle = new FWColorAndStyleWidget();
                    }
                    colorAndStyle.def = def;
                    bool aa = apparel.def?.CanBeStyled() ?? false;
                    bool ab = apparel.GetComp<CompColorable>() != null;
                    colorAndStyle.closeAction = (color, style) =>
                    {
                        if (aa)
                        {
                            apparel.SetStyleDef(style);
                        }
                        if (ab)
                        {
                            apparel.SetColor(color);
                        }
                    };

                    Rect bot = rect1.BottomPart(0.68f);
                    Rect top = rect1.TopPart(0.3f);
                    colorAndStyle.DoContentsWithCloseAction(bot, aa, ab, false);
                    Widgets.ThingIcon(top, apparel);
                }
            }

            public override void Close(bool doCloseSound = true)
            {
                base.Close(doCloseSound);
                quickSearch.Reset();
                if (pawn != null && pawn.GetComp<FashionOverrideComp>() != null)
                {
                    FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                    comp.AddApparel(apparel, false);
                    pawn.apparel.Notify_ApparelChanged();
                }
                choose = null;
                pawn = null;
                apparel = null;
                scrollPosition = Vector2.zero;
                filteredApparels = new List<ThingDef>();
                reFilter = true;
                quickSearch.filter.Text = "";
                Options = new List<FloatMenuOption>();
                colorAndStyle = null;
            }
        }

        public class PresetManagerWindow : Window
        {
            private readonly int unitCount = 20;
            private FWPresetData selPreset;
            public MainTabWindow_Fashion FashionWindow;
            private FWColorAndStyleWidget colorAndStyle;
            private bool coStWork = false;

            public override Vector2 InitialSize
            {
                get
                {
                    return new Vector2(900f, 800f);
                }
            }

            public PresetManagerWindow()
            {
                closeOnClickedOutside = true;
                doCloseX = true;
                draggable = true;
                resizeable = true;
                forcePause = false;
                doCloseButton = false;
                preventCameraMotion = false;
                optionalTitle = "FW_PresetManager".Translate();
            }

            public override void DoWindowContents(Rect inRect)
            {
                DrawLeft(inRect.LeftPart(0.26f));
                Rect rect = inRect.RightPart(0.74f);
                DrawMiddle(rect.LeftHalf());
                DrawRight(rect.RightHalf());
            }

            public override void Close(bool doCloseSound = true)
            {
                base.Close(doCloseSound);
                quickSearch.Reset();
                FWMod.setting.Write();
                reFilter = true;
                coStWork = false;
            }

            private Vector2 RigScr = Vector2.zero;

            private void DrawRight(Rect inRect)
            {
                Widgets.DrawBoxSolidWithOutline(inRect.ContractedBy(3f), WindowBGFillColor, WindowBGBorderColor);
                Rect rect = inRect.ContractedBy(8f);
                if (coStWork)
                {
                    if (colorAndStyle != null)
                    {
                        bool aa = colorAndStyle.def?.CanBeStyled() ?? false;
                        bool ab = colorAndStyle.def?.comps?.Any(t => typeof(CompColorable).IsAssignableFrom(t.compClass)) ?? false;
                        colorAndStyle.DoContentsWithCloseAction(rect, aa, ab);
                    }
                    return;
                }
                Widgets.BeginGroup(rect);
                if (selPreset != null)
                {
                    if (!selPreset.apparels.NullOrEmpty())
                    {
                        Rect outRect = new Rect(0, 0, rect.width, rect.height);
                        float uH = outRect.height / unitCount;
                        float viewHeight = selPreset.apparels.Count * (uH + 5f) - 5f;
                        bool scr = viewHeight > outRect.height;
                        Rect viewRect = new Rect(0, 0, outRect.width - (scr ? 20f : 0), viewHeight);
                        Widgets.BeginScrollView(outRect, ref RigScr, viewRect);
                        Rect unit = new Rect(0, 0, viewRect.width, uH);
                        Rect iconLoc = new Rect(viewRect.width - uH, 0, uH, uH);
                        Rect butLoc = new Rect(5, 5, uH - 10, uH - 10);
                        Rect laLoc = new Rect(uH + 5f, 0, viewRect.width - uH - 10f, uH);
                        List<string> remove = new List<string>();
                        for (int a = 0; a < selPreset.apparels.Count; a++)
                        {
                            var obj = selPreset.apparels.ElementAt(a);
                            var sName = obj.Value?.styleDef;
                            bool hasStyle = !sName.NullOrEmpty();
                            string defName = obj.Key;
                            string styleName = sName;
                            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                            Texture2D icon = def.uiIcon;
                            string label = def.label;
                            if (def != null)
                            {
                                if (hasStyle)
                                {
                                    var sDef = DefDatabase<ThingStyleDef>.GetNamedSilentFail(styleName);
                                    if (sDef != null)
                                    {
                                        icon = sDef.UIIcon;
                                        label += "(" + sDef.defName + ")";
                                    }
                                }
                                if (unit.y + unit.height > RigScr.y && unit.y < RigScr.y + outRect.height)
                                {
                                    Widgets.DrawBoxSolid(unit, WidgetFillColor);
                                    Texture2D tex = icon ?? Texture2D.blackTexture;
                                    if (selPreset.apparels.TryGetValue(defName, out FWPresetApparelData aData))
                                    {
                                        GUI.DrawTexture(iconLoc, tex, ScaleMode.StretchToFill, true, 0, aData.color, 0, 0);
                                    }
                                    else
                                    {
                                        GUI.DrawTexture(iconLoc, tex);
                                    }
                                    GUI.Label(laLoc, label, MidLStyle);
                                    GUI.DrawTexture(butLoc, TexUI.ArrowTexLeft);
                                    if (Mouse.IsOver(unit))
                                    {
                                        Widgets.DrawHighlight(unit);
                                    }
                                    if (Widgets.ButtonInvisible(unit))
                                    {
                                        remove.Add(defName);
                                    }
                                }
                                iconLoc.y += uH + 5f;
                                butLoc.y += uH + 5f;
                                laLoc.y += uH + 5f;
                                unit.y += uH + 5f;
                            }
                        }
                        Widgets.EndScrollView();
                        if (!remove.NullOrEmpty())
                        {
                            for (int i = 0; i < remove.Count; i++)
                            {
                                selPreset.apparels.Remove(remove[i]);
                            }
                        }
                    }
                }
                Widgets.EndGroup();
            }
            private Vector2 MidScr = Vector2.zero;
            private List<ThingDef> listCache = new List<ThingDef>();
            private void DrawMiddle(Rect inRect)
            {
                Widgets.DrawBoxSolidWithOutline(inRect.ContractedBy(3f), WindowBGFillColor, WindowBGBorderColor);
                Rect rect = inRect.ContractedBy(8f);
                Widgets.BeginGroup(rect);
                float uH = rect.height / unitCount;
                Rect rect0 = new Rect(0, 0, rect.width, uH);
                quickSearch.OnGUI(rect0, NotifyReFliter, NotifyReFliter);
                if (selPreset != null && !AllapparelDef.NullOrEmpty())
                {
                    Rect outRect = new Rect(0, uH + 5f, rect.width, rect.height - uH - 5f);
                    Widgets.DrawWindowBackground(outRect);
                    if (reFilter)
                    {
                        listCache = AllapparelDef.Where(ap => quickSearch.filter.Matches(ap.label)).ToList();
                    }
                    if (!listCache.NullOrEmpty())
                    {
                        quickSearch.noResultsMatched = false;
                        float viewHeight = listCache.Count * (uH + 5f) - 5f;
                        bool scr = viewHeight > outRect.height;
                        Rect viewRect = new Rect(0, 0, outRect.width - (scr ? 20f : 0), viewHeight);
                        Widgets.BeginScrollView(outRect, ref MidScr, viewRect);
                        Rect unit = new Rect(0, 0, viewRect.width, uH);
                        Rect iconLoc = new Rect(0, 0, uH, uH);
                        Rect butLoc = new Rect(viewRect.width - uH + 5f, 5f, uH - 10f, uH - 10f);
                        Rect laLoc = new Rect(uH + 5f, 0, viewRect.width - uH - 10f, uH);
                        float maxY = MidScr.y + outRect.height;
                        for (int i = 0; i < listCache.Count; i++)
                        {
                            if (unit.y + unit.height > MidScr.y && unit.y < maxY)
                            {
                                ThingDef def = listCache[i];
                                GUI.DrawTexture(iconLoc, def.uiIcon ?? Texture2D.blackTexture);
                                GUI.Label(laLoc, def.label, MidLStyle);
                                GUI.DrawTexture(butLoc, TexUI.ArrowTexRight);
                                if (Mouse.IsOver(unit))
                                {
                                    Widgets.DrawHighlight(unit);
                                }
                                if (Widgets.ButtonInvisible(unit))
                                {
                                    if (colorAndStyle == null)
                                    {
                                        colorAndStyle = new FWColorAndStyleWidget();
                                    }
                                    colorAndStyle.def = def;
                                    colorAndStyle.Open = false;
                                    colorAndStyle.closeAction = (color, style) =>
                                    {
                                        if (!selPreset.apparels.NullOrEmpty())
                                        {
                                            selPreset.apparels.RemoveAll(d =>
                                            {
                                                ThingDef thing = ThingDef.Named(d.Key);
                                                return thing == null || (!FWSetting.CanAddConflict && !ApparelUtility.CanWearTogether(thing, def, BodyDefOf.Human));
                                            });
                                        }
                                        if (selPreset.apparels == null)
                                        {
                                            selPreset.apparels = new Dictionary<string, FWPresetApparelData>();
                                        }
                                        selPreset.apparels.SetOrAdd(def.defName, new FWPresetApparelData(color, style?.defName ?? ""));
                                        coStWork = false;
                                    };
                                    coStWork = true;
                                }
                            }
                            iconLoc.y += uH + 5f;
                            butLoc.y += uH + 5f;
                            laLoc.y += uH + 5f;
                            unit.y += uH + 5f;
                        }
                        Widgets.EndScrollView();
                    }
                    else
                    {
                        quickSearch.noResultsMatched = true;
                    }
                }
                Widgets.EndGroup();
            }

            private Vector2 leftScr = Vector2.zero;
            private bool addPreset = false;
            private string addName = "";
            private readonly Texture2D highLightSel = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 244f, 1f, 0.1f));
            private FWPresetData reNameData;
            private string renameStr = "";

            private void DrawLeft(Rect inRect)
            {
                string add = "Add".Translate();
                Widgets.DrawBoxSolidWithOutline(inRect.ContractedBy(3f), WindowBGFillColor, WindowBGBorderColor);
                Rect rect = inRect.ContractedBy(8f);
                Widgets.BeginGroup(rect);
                float uH = rect.height / unitCount;
                if (!FWSetting.PresetDatas.NullOrEmpty())
                {
                    float vH = (uH + 5f) * (FWSetting.PresetDatas.Count + 1) - 5f;
                    bool scr = vH > rect.height;
                    Widgets.BeginScrollView(new Rect(0, 0, rect.width, rect.height), ref leftScr, new Rect(0, 0, rect.width - (scr ? 20f : 0), vH));
                    Rect rect1 = new Rect(0, 0, rect.width - (scr ? 20f : 0), uH);
                    AddButton(rect1);
                    rect1.y += uH + 5f;
                    Rect laLoc0 = new Rect(rect1.x, rect1.y, rect1.width - rect1.height, rect1.height);
                    Rect texLoc0 = new Rect(rect1.x + laLoc0.width + 3f, rect1.y + 3f, rect1.height - 6f, rect1.height - 6f);
                    for (int i = 0; i < FWSetting.PresetDatas.Count; i++)
                    {
                        Widgets.DrawBoxSolid(rect1, WidgetFillColor);
                        FWPresetData data = FWSetting.PresetDatas[i];
                        if (data != null)
                        {
                            bool rename = data == reNameData;
                            if (rename)
                            {
                                renameStr = Widgets.TextField(laLoc0, renameStr);
                            }
                            else
                            {
                                GUI.Label(laLoc0, data.Id, MidLStyle);
                                if (selPreset == data)
                                {
                                    GUI.DrawTexture(laLoc0, TexUI.HighlightSelectedTex);
                                }
                                if (Mouse.IsOver(laLoc0))
                                {
                                    Widgets.DrawHighlight(laLoc0);
                                }
                                if (Widgets.ButtonInvisible(laLoc0))
                                {
                                    if (selPreset == data)
                                    {
                                        selPreset = null;
                                    }
                                    else
                                    {
                                        selPreset = data;
                                    }

                                    GUI.DrawTexture(laLoc0, highLightSel);
                                }
                            }
                            bool over = false;
                            if (Mouse.IsOver(texLoc0))
                            {
                                over = true;
                            }
                            Color color = Color.red;
                            Texture tex0 = rename ? Widgets.CheckboxOnTex : (SettingTex ?? TexButton.Delete);
                            GUI.DrawTexture(texLoc0, tex0, ScaleMode.StretchToFill, true, 0, over ? color : Color.white, 0, 0);
                            if (Widgets.ButtonInvisible(texLoc0))
                            {
                                if (rename)
                                {
                                    data.ApplyID(renameStr);
                                    renameStr = "";
                                    reNameData = null;
                                    CreateFloatMenu();
                                }
                                else
                                {
                                    Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                                    {
                                        new FloatMenuOption("Delete".Translate(), () =>
                                        {
                                            if (!FWSetting.PresetDatas.NullOrEmpty() && FWSetting.PresetDatas.Contains(data))
                                            {
                                                if (selPreset == data)
                                                {
                                                    selPreset = null;
                                                }
                                                FWSetting.PresetDatas.Remove(data);
                                                if (FashionWindow != null)
                                                {
                                                CreateFloatMenu();
                                                }
                                            }
                                        }),
                                        new FloatMenuOption("Rename".Translate(), () =>
                                        {
                                            reNameData = data;
                                        })
                                    }));
                                }
                            }
                            laLoc0.y += uH + 5f;
                            texLoc0.y += uH + 5f;
                            rect1.y += uH + 5f;
                        }
                    }
                    Widgets.EndScrollView();
                }
                else
                {
                    Rect rect1 = new Rect(0, 0, rect.width, uH);
                    AddButton(rect1);
                }
                Widgets.EndGroup();
                void AddButton(Rect butLoc)
                {
                    Widgets.DrawBoxSolid(butLoc, WidgetFillColor);
                    if (!addPreset)
                    {
                        Rect laLoc1 = new Rect(butLoc.x, butLoc.y, butLoc.width - butLoc.height, butLoc.height);
                        Rect texLoc1 = new Rect(butLoc.x + laLoc1.width + 3f, butLoc.y + 3f, butLoc.height - 6f, butLoc.height - 6f);
                        GUI.DrawTexture(texLoc1, TexButton.Add);
                        GUI.Label(laLoc1, add, MidCStyle);
                        if (Mouse.IsOver(butLoc))
                        {
                            Widgets.DrawHighlight(butLoc);
                        }
                        if (Widgets.ButtonInvisible(butLoc))
                        {
                            addPreset = true;
                        }
                    }
                    else
                    {
                        Rect texLoc1 = new Rect(butLoc.x, butLoc.y, butLoc.width - butLoc.height, butLoc.height);
                        addName = Widgets.TextField(texLoc1, addName);
                        Rect butLoc1 = new Rect(butLoc.x + texLoc1.width + 3f, butLoc.y + 3f, butLoc.height - 6f, butLoc.height - 6f);
                        bool over = false;
                        if (Mouse.IsOver(butLoc1))
                        {
                            over = true;
                        }
                        Color color = Color.yellow;
                        GUI.DrawTexture(butLoc1, Widgets.CheckboxOnTex, ScaleMode.StretchToFill, true, 0, over ? color : Color.white, 0, 0);
                        if (Widgets.ButtonInvisible(butLoc1))
                        {
                            if (FWSetting.PresetDatas == null)
                            {
                                FWSetting.PresetDatas = new List<FWPresetData>();
                            }
                            FWSetting.PresetDatas.Add(new FWPresetData(addName));
                            if (FashionWindow != null)
                            {
                                CreateFloatMenu();
                            }
                            addPreset = false;
                            addName = "";
                        }
                    }
                }
            }
        }

        public class MainTabWindow_Fashion : MainTabWindow
        {
            private float unitHight = 30f;
            private List<Pawn> pawns = new List<Pawn>();
            private Pawn selPawn;

            public override Vector2 RequestedTabSize
            {
                get
                {
                    int a;
                    if (Current.Game != null && Current.Game.CurrentMap != null)
                    {
                        pawns = Current.Game.CurrentMap.mapPawns.AllPawnsSpawned.Where(o => FWUtility.FWork(o)).ToList();
                    }
                    CreateFloatMenu();
                    if (pawns.NullOrEmpty())
                    {
                        a = 1;
                    }
                    else
                    {
                        a = pawns.Count > 6 ? 7 : pawns.Count + 1;
                    }
                    return new Vector2(1010f, a * (unitHight + 5f) + 40f);
                }
            }

            public MainTabWindow_Fashion()
            {
                presetManager.FashionWindow = this;
            }

            private Vector2 scr = Vector2.zero;
            private KeyValuePair<Pawn, RenderTexture> pawnTexture = new KeyValuePair<Pawn, RenderTexture>();
            private Vector2 pawnTextureSize = new Vector2(100, 100);

            public override void DoWindowContents(Rect inRect)
            {
                GameFont font = Text.Font;
                Text.Font = GameFont.Medium;
                string edit = "Edit".Translate();
                string preset = "Preset".Translate();
                Widgets.BeginGroup(inRect);
                Rect rect0 = new Rect(0, 0, inRect.width, unitHight);
                Widgets.Label(rect0.LeftPart(0.85f), "Fashion_Wardrobe".Translate());
                Text.Font = GameFont.Small;
                if (Widgets.ButtonText(rect0.RightPart(0.15f), "Manage".Translate() + " " + preset))
                {
                    if (!presetManager.IsOpen)
                    {
                        reFilter = true;
                        Find.WindowStack.Add(presetManager);
                    }
                }
                rect0.y += unitHight + 5f;
                if (pawns.NullOrEmpty())
                {
                    Text.Font = font;
                    Widgets.EndGroup();
                    return;
                }
                string checkeStr = "FashionClothes_Enable".Translate();
                Vector2 checkSize = Text.CalcSize(checkeStr);
                bool isScr = false;
                float outRectHeight = inRect.height - rect0.y;
                if (pawns.Count > 6)
                {
                    Rect outRect = new Rect(0f, rect0.y, rect0.width, outRectHeight);
                    Rect viewRect = new Rect(0, 0, outRect.width - 20f, pawns.Count * (unitHight + 5f) - 5f);
                    Widgets.BeginScrollView(outRect, ref scr, viewRect);
                    rect0 = new Rect(0, 0, viewRect.width, unitHight);
                    isScr = true;
                }
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if ((!isScr || (rect0.y + rect0.height > scr.y && rect0.y < scr.y + outRectHeight)) && pawn != null)
                    {
                        GUI.Label(rect0, pawn.Name.ToStringFull, MidLStyle);
                        Vector2 size = Text.CalcSize(pawn.Name.ToStringFull);
                        float w = 0.85f * rect0.width - checkSize.x - 40f;
                        Widgets.DrawLineHorizontal(rect0.x + size.x + 20f, rect0.y + rect0.height / 2, w - size.x - 30f);
                        Rect tooltipLoc = new Rect(rect0.x, rect0.y, w - 10f, rect0.height);
                        if (Mouse.IsOver(tooltipLoc))
                        {
                            Widgets.DrawHighlight(tooltipLoc);
                            if (FWSetting.EnablePreview)
                            {
                                if (pawnTexture.Key != pawn)
                                {
                                    RenderTexture rt = PortraitsCache.Get(pawn, pawnTextureSize, Rot4.South);
                                    pawnTexture = new KeyValuePair<Pawn, RenderTexture>(pawn, rt);
                                }
                                else if (windowRect != null)
                                {
                                    DrawTooltipGrid(pawnTexture.Value);
                                }
                            }
                        }
                        Rect rect1 = rect0.RightPart(0.15f);
                        FashionOverrideComp comp = pawn.GetComp<FashionOverrideComp>();
                        if (comp != null)
                        {
                            CheckboxLabeled(new Rect(rect1.x - checkSize.x - 45f, rect1.y, checkSize.x + 40f, rect1.height), checkeStr, ref comp.FashionClothesEnable, out bool click);
                            if (click)
                            {
                                if (pawn.apparel != null)
                                {
                                    pawn.apparel.Notify_ApparelChanged();
                                }
                            }
                            if (Widgets.ButtonText(rect1.LeftPart(0.49f), preset))
                            {
                                if (floatMenu != null)
                                {
                                    selPawn = pawn;
                                    Find.WindowStack.Add(floatMenu);
                                }
                            }
                            if (pawn == selPawn)
                            {
                                if (selPreset != null)
                                {
                                    FashionOverrideComp comp1 = pawn.TryGetComp<FashionOverrideComp>();
                                    if (comp1 != null && pawn.apparel != null)
                                    {
                                        comp1.ApplyPreset(selPreset);
                                        pawn.apparel.Notify_ApparelChanged();
                                    }
                                    selPawn = null;
                                    selPreset = null;
                                }
                            }
                            if (Widgets.ButtonText(rect1.RightPart(0.49f), edit))
                            {
                                if (!apparel_window.IsOpen)
                                {
                                    apparel_window.pawn = pawn;
                                    Find.WindowStack.Add(apparel_window);
                                }
                            }
                        }
                    }
                    rect0.y += unitHight + 5f;
                }
                Text.Font = font;
                if (isScr)
                {
                    Widgets.EndScrollView();
                }
                Widgets.EndGroup();
            }
        }
    }
}
