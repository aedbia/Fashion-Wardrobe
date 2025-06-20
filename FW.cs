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
using static Verse.KeyPrefs;

namespace Fashion_Wardrobe
{
    public class FWMod : Mod
    {
        public static int HATWeakerLoadrIndex = -1;
        internal static FWSetting setting;
        private static KeyPrefsData keyPrefsData;
        private static Dialog_DefineBinding Dialog_DefineBinding;
        public FWMod(ModContentPack content) : base(content)
        {
            setting = GetSettings<FWSetting>();
            HATWeakerLoadrIndex = LoadedModManager.RunningMods.FirstIndexOf(x => x.PackageIdPlayerFacing == "AB.HATweaker");
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Default_EnableFashion".Translate(), ref FWSetting.DefaultEnableFashion);
            ls.CheckboxLabeled("Only_Colonist".Translate(), ref FWSetting.OnlyForColonist);
            ls.CheckboxLabeled("Show_InDoorFight".Translate(), ref FWSetting.ShowInDoorFight);
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
        internal static bool EnableMainButton = true;
        internal static List<PresetData> PresetDatas = new List<PresetData>();
        internal static bool EnablePreview = true;
        internal static bool Direct_Open_FW = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref OnlyForColonist, "OnlyForColonist", true, true);
            Scribe_Values.Look(ref ShowInDoorFight, "ShowInDoorFight", false, true);
            Scribe_Values.Look(ref DefaultEnableFashion, "DefaultEnableFashion", false, true);
            Scribe_Values.Look(ref EnableMainButton, "EnableMainButton", true, true);
            Scribe_Values.Look(ref Direct_Open_FW, "Direct_Open_FW", false, true);
            Scribe_Values.Look(ref EnablePreview, "EnablePreview", true, true);
            Scribe_Collections.Look(ref PresetDatas, "PresetDatas", LookMode.Deep);
            if (PresetDatas == null)
            {
                PresetDatas = new List<PresetData>();
            }
            PresetDatas.Remove(null);
        }
        public class PresetData : IExposable
        {
            private string ID = "";
            public Dictionary<string, Color> apparels = new Dictionary<string, Color>();
            public string Id
            {
                get { return ID; }
            }
            public PresetData()
            {
                ID = "NORMAL";
            }
            public PresetData(string presetName)
            {
                ID = presetName;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref ID, "Id");
                Scribe_Collections.Look(ref apparels, "apparels", LookMode.Value, LookMode.Value);
            }
        }
    }

    public class FashionOverrideComp : ThingComp
    {
        private Pawn Pawn => (parent as Pawn);

        internal bool FashionClothesEnable;
        internal ThingOwner<Apparel> Clothes;
        internal List<Apparel> FashionApparel = new List<Apparel>();
        internal Dictionary<string, FWCompData> DrawRule = new Dictionary<string, FWCompData>();
        private List<Apparel> postAdds = new List<Apparel>();
        private bool draft;
        private bool underRoof;
        internal bool Draft
        {
            get { return draft; }
            set
            {
                if (draft != value)
                {
                    draft = value;
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
            if (Clothes.InnerListForReading.Count > 1)
            {
                List<Apparel> apparels = Clothes.InnerListForReading;
                SortCloths(ref apparels);
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
                if (Pawn.Map != null)
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
        public void AddApparel(Apparel apparel, bool removeHolder = false)
        {
            if (Clothes == null)
            {
                return;
            }
            if (apparel != null && !Clothes.Contains(apparel))
            {
                Clothes.RemoveAll(a => !ApparelUtility.CanWearTogether(a.def, apparel.def, Pawn.RaceProps.body));
                if (removeHolder)
                {
                    Apparel apparel1 = FWUtility.NewApparel(apparel.def);
                    apparel1.SetColor(apparel.DrawColor);
                    apparel1.StyleDef = apparel.StyleDef;
                    Clothes.TryAdd(apparel1, 1);
                }
                else
                {
                    Clothes.TryAdd(apparel, 1);
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
            bool flag = !list0.NullOrEmpty();
            List<Apparel> list1;
            if (!Pawn.apparel.WornApparel.NullOrEmpty())
            {
                list1 = new List<Apparel>(Pawn.apparel.WornApparel);
                if (flag)
                {
                    list1.RemoveAll(a => a.def.apparel.layers.Any(b => list0.Any(c => c.def.apparel.layers.Contains(b))));
                    list1.AddRange(list0);
                }
            }
            else
            {
                list1 = new List<Apparel>();
                if (flag)
                {
                    list1.AddRange(list0);
                }
            }
            RemoveNoDisplayGraphic(ref list1);
            PostGetApparel(ref list1);
            if (flag)
            {
                SortCloths(ref list1);
            }
            return list1;
        }

        public void SortCloths(ref List<Apparel> apparels)
        {
            apparels.Sort((Apparel a, Apparel b) => a.def.apparel.LastLayer.drawOrder.CompareTo(b.def.apparel.LastLayer.drawOrder));
        }
        private void PostGetApparel(ref List<Apparel> list)
        {
            if (!postAdds.NullOrEmpty())
            {
                foreach (Apparel ap in postAdds)
                {
                    if (!list.Contains(ap))
                    {
                        list.Add(ap);
                    }
                }
            }

        }
        public void AddOrRemovePostList(Apparel apparel, bool add)
        {
            if (postAdds == null)
            {
                postAdds = new List<Apparel>();
            }
            if (postAdds.Count == 0)
            {
                if (add)
                {
                    postAdds.Add(apparel);
                }

            }
            else
            {
                if (add && !postAdds.Contains(apparel))
                {
                    postAdds.Add(apparel);
                }
                else if (!add && postAdds.Contains(apparel))
                {
                    postAdds.Remove(apparel);
                }
            }
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

                        if (data.HideInDoor && Pawn.Map != null && !Pawn.Position.UsesOutdoorTemperature(Pawn.Map))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        internal void ApplyPreset(FWSetting.PresetData preset)
        {
            if (preset.apparels.NullOrEmpty())
            {
                return;
            }
            foreach (string defName in preset.apparels.Keys)
            {
                Apparel apparel = FWUtility.NewApparel(ThingDef.Named(defName));
                if (apparel != null)
                {
                    if (apparel.GetComp<CompColorable>() != null && preset.apparels.TryGetValue(defName, out Color color))
                    {
                        apparel.SetColor(color);
                    }
                    AddApparel(apparel, false);
                }
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

    public static class FW_Windows
    {
        private static readonly Color WindowBGBorderColor = new ColorInt(97, 108, 122).ToColor;
        private static readonly Color WindowBGFillColor = new ColorInt(43, 44, 45).ToColor;
        private static readonly List<Color> colors;
        internal static readonly PawnApparelSettingWindow apparel_window = new PawnApparelSettingWindow();
        private static readonly List<ThingDef> AllapparelDef = DefDatabase<ThingDef>.AllDefs.Where(a => a.IsApparel).ToList();
        private static readonly GUIStyle MidLStyle = new GUIStyle(Text.fontStyles[1])

        {
            alignment = TextAnchor.MiddleLeft
        };
        private static readonly GUIStyle MidCStyle = new GUIStyle(Text.fontStyles[1])
        {
            alignment = TextAnchor.MiddleCenter
        };

        static FW_Windows()
        {
            List<Color> colors0 = (from c in DefDatabase<ColorDef>.AllDefsListForReading
                                   select c.color).ToList<Color>();
            colors0.Add(Color.white);
            colors0.AddRange(from f in Find.FactionManager.AllFactionsVisible
                             select f.Color);
            colors0 = colors0.Distinct().ToList();
            colors0.SortByColor((Color c) => c);
            colors = colors0;
            CreateColorFloatSel();
        }
        private static Action<Color> setColor;
        private static FloatMenuGrid colorSelect;

        private static FWSetting.PresetData selPreset;
        private static FloatMenu floatMenu;
        private static readonly PresetManagerWindow presetManager = new PresetManagerWindow();

        private static void CreateFloatMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (!FWSetting.PresetDatas.NullOrEmpty())
            {
                for (int i = 0; i < FWSetting.PresetDatas.Count; i++)
                {
                    FWSetting.PresetData data = FWSetting.PresetDatas[i];
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

        private static void CreateColorFloatSel()
        {
            if (!colors.NullOrEmpty())
            {
                List<FloatMenuGridOption> colorSel = new List<FloatMenuGridOption>();
                for (int i = 0; i < colors.Count; i++)
                {
                    Color col = colors[i];
                    Texture2D ct = SolidColorMaterials.NewSolidColorTexture(col);
                    colorSel.Add(new FloatMenuGridOption(ct, () =>
                    {
                        setColor?.Invoke(col);
                    }));
                }
                colorSelect = new FloatMenuGrid(colorSel);
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

        private static readonly string tooltipGrid = "PawnDrawTooltips";
        private static Vector2 fwTipSize = new Vector2(100, 100);

        private static void DrawTooltipGrid(Texture texture)
        {
            Text.Font = GameFont.Small;

            Vector2 pos = UI.MousePositionOnUIInverted;
            if (pos.x + fwTipSize.x > UI.screenWidth)
            {
                float a = pos.x - fwTipSize.x;
                pos.x = a > 0 ? a : 0;
            }
            if (pos.y - fwTipSize.y > 0)
            {
                pos.y -= fwTipSize.y;
            }
            Rect bgRect = new Rect(0, 0, fwTipSize.x, fwTipSize.y)
            {
                position = pos
            };
            if (!LongEventHandler.AnyEventWhichDoesntUseStandardWindowNowOrWaiting)
            {
                Find.WindowStack.ImmediateWindow(153 * tooltipGrid.GetHashCode() + 62346, bgRect, WindowLayer.Super, delegate
                {
                    DrawInner(bgRect.AtZero(), texture);
                }, doBackground: false);
                //Log.Warning("000");
            }
            else
            {
                Widgets.DrawShadowAround(bgRect);
                Widgets.DrawWindowBackground(bgRect);
                DrawInner(bgRect, texture);
            }
        }

        private static void DrawInner(Rect rect, Texture texture)
        {
            Rect rect2 = rect;
            rect2.yMin += 4f;
            rect2.yMax -= 4f;
            Widgets.DrawHighlight(rect2);
            Widgets.DrawBox(rect2);
            GUI.DrawTexture(rect2, texture);
        }

        public class PawnApparelSettingWindow : Window
        {

            private int tabInt = 0;
            private Vector2 scrollPosition = Vector2.zero;
            internal Pawn pawn = null;
            private SelApparelWindow SelApparelWindow = null;

            private static List<Apparel> copys;

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
                Rect rect0 = new Rect(contectRect.x + contectRect.height * a + 5f, contectRect.y + contectRect.height * a, contectRect.width - contectRect.height * a - 5f, contectRect.height * a);
                Rect copyLoc = new Rect(contectRect.x, contectRect.y, contectRect.height * a, contectRect.height * a);
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
                Rect rect1 = new Rect(contectRect.x, contectRect.y + contectRect.height * a, contectRect.width - 1f, contectRect.height * 0.89f);
                TabDrawer.DrawTabs(rect0, tabs);
                GUI.color = WindowBGFillColor;
                GUI.DrawTexture(rect1, BaseContent.WhiteTex);
                GUI.color = WindowBGBorderColor;
                Widgets.DrawLineHorizontal(rect1.x, rect1.y, tabInt == 0 ? (copyLoc.width + 6f) : (copyLoc.width + rect0.width / 2 + 3f));
                if (tabInt == 0)
                {
                    Widgets.DrawLineHorizontal(rect1.x + (copyLoc.width + rect0.width / 2 + 9f), rect1.y, rect0.width / 2 - 4f);
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
                            if (Widgets.ButtonImage(copyLoc, TexButton.Copy))
                            {
                                copys = new List<Apparel>(apparels);
                            }

                            if (DrawScroll(rect2, comp, apparels))
                            {
                                pawn.apparel.Notify_ApparelChanged();
                            }
                        }
                    }
                    else
                    {
                        if (!copys.NullOrEmpty() && Widgets.ButtonImage(copyLoc, TexButton.Paste))
                        {
                            comp.SortCloths(ref copys);
                            //Log.Warning(copys.Count.ToString());
                            foreach (Apparel item in copys)
                            {
                                if (comp.Clothes != null)
                                {
                                    comp.AddApparel(item, true);
                                }
                            }
                        }
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
                        Rect rect3 = rect2.BottomPart(0.05f).RightHalf();
                        if (Widgets.ButtonText(rect3.LeftHalf(), "Preset".Translate()))
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
                        if (Widgets.ButtonText(rect3.RightHalf(), "Manage".Translate()))
                        {
                            if (!presetManager.IsOpen)
                            {
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
                }
                comp.SortCloths(ref apparels);
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
                            if (Widgets.ButtonInvisible(iconLoc) && colorSelect != null)
                            {
                                setColor = (Color color) =>
                                {
                                    apparel.SetColor(color);
                                    setColor = null;
                                };
                                Find.WindowStack.Add(colorSelect);
                            }
                        }
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
                            active = true;
                        }
                    }
                }
                if (drawRemove)
                {
                    Rect rect0 = new Rect(iconLoc.x + 4f, iconLoc.y + 4f, view.width - 8f, 52f);
                    Rect addIcon = new Rect(rect0.x, rect0.y, 52f, 52f);
                    Rect addLoc = new Rect(rect0.x + 56f, rect0.y, rect0.width - 56f, 52f);
                    Widgets.DrawBoxSolidWithOutline(rect0, Color.grey, Color.white);
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
                preventCameraMotion = false;
            }

            public override void DoWindowContents(Rect inRect)
            {
                Rect rect = new Rect(inRect.x, inRect.y, inRect.width - 1f, inRect.height * 0.94f);
                search = Widgets.TextEntryLabeled(rect.TopPart(0.03f), "Search".Translate(), search);
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

                Rect outRect = new Rect(rect0.x + 2f, rect0.y + 35f, rect0.width * 0.21f, rect0.height - 35f);
                Rect viewRect = new Rect(0, 0, outRect.width - 20f, (outRect.width + 20f) * showCount);
                Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
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
                Widgets.DrawLineVertical(rect0.x + (rect0.width * 0.21f + 10f), rect0.y, rect0.height);
                Rect rect1 = new Rect(rect0.x + (rect0.width * 0.21f + 26f), rect0.y, rect0.width * 0.79f - 26f, rect0.height);
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
                    comp.AddApparel(apparel, false);
                    pawn.apparel.Notify_ApparelChanged();
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

        public class PresetManagerWindow : Window
        {
            private int unitCount = 20;
            private FWSetting.PresetData selPreset;
            public MainTabWindow_Fashion FashionWindow;
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
                FWMod.setting.Write();
            }

            private Vector2 RigScr = Vector2.zero;

            private void DrawRight(Rect inRect)
            {
                Widgets.DrawBoxSolidWithOutline(inRect.ContractedBy(3f), WindowBGFillColor, WindowBGBorderColor);
                Rect rect = inRect.ContractedBy(8f);
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
                            string defName = selPreset.apparels.ElementAt(a).Key;
                            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                            if (def != null)
                            {
                                if (unit.y + unit.height > RigScr.y && unit.y < RigScr.y + outRect.height)
                                {
                                    Widgets.DrawBoxSolid(unit, Color.gray);
                                    Texture2D tex = def.uiIcon ?? Texture2D.blackTexture;
                                    if (selPreset.apparels.TryGetValue(defName, out Color color))
                                    {
                                        GUI.DrawTexture(iconLoc, tex, ScaleMode.StretchToFill, true, 0, color, 0, 0);
                                    }
                                    else
                                    {
                                        GUI.DrawTexture(iconLoc, tex);
                                    }
                                    GUI.Label(laLoc, def.label, MidLStyle);
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

            private string serch = "";
            private Vector2 MidScr = Vector2.zero;
            private void DrawMiddle(Rect inRect)
            {
                Widgets.DrawBoxSolidWithOutline(inRect.ContractedBy(3f), WindowBGFillColor, WindowBGBorderColor);
                Rect rect = inRect.ContractedBy(8f);
                Widgets.BeginGroup(rect);
                float uH = rect.height / unitCount;
                Rect rect0 = new Rect(0, 0, rect.width, uH);
                serch = Widgets.TextField(rect0, serch);
                if (selPreset != null && !AllapparelDef.NullOrEmpty())
                {
                    Rect outRect = new Rect(0, uH + 5f, rect.width, rect.height - uH - 5f);
                    Widgets.DrawWindowBackground(outRect);
                    List<ThingDef> apparels = AllapparelDef.Where(a => serch.Count() == 0 || a.label.IndexOf(serch) != -1).ToList();
                    if (!apparels.NullOrEmpty())
                    {
                        float viewHeight = apparels.Count * (uH + 5f) - 5f;
                        bool scr = viewHeight > outRect.height;
                        Rect viewRect = new Rect(0, 0, outRect.width - (scr ? 20f : 0), viewHeight);
                        Widgets.BeginScrollView(outRect, ref MidScr, viewRect);
                        Rect unit = new Rect(0, 0, viewRect.width, uH);
                        Rect iconLoc = new Rect(0, 0, uH, uH);
                        Rect butLoc = new Rect(viewRect.width - uH + 5f, 5f, uH - 10f, uH - 10f);
                        Rect laLoc = new Rect(uH + 5f, 0, viewRect.width - uH - 10f, uH);
                        for (int i = 0; i < apparels.Count; i++)
                        {
                            if (unit.y + unit.height > MidScr.y && unit.y < MidScr.y + outRect.height)
                            {
                                ThingDef def = apparels[i];
                                GUI.DrawTexture(iconLoc, def.uiIcon ?? Texture2D.blackTexture);
                                GUI.Label(laLoc, def.label, MidLStyle);
                                GUI.DrawTexture(butLoc, TexUI.ArrowTexRight);
                                if (Mouse.IsOver(unit))
                                {
                                    Widgets.DrawHighlight(unit);
                                }
                                if (Widgets.ButtonInvisible(unit) && colorSelect != null)
                                {
                                    setColor = (Color color) =>
                                    {
                                        if (!selPreset.apparels.NullOrEmpty())
                                        {
                                            selPreset.apparels.RemoveAll(d =>
                                            {
                                                ThingDef thing = ThingDef.Named(d.Key);
                                                return thing == null || !ApparelUtility.CanWearTogether(thing, def, BodyDefOf.Human);
                                            });
                                        }
                                        if (selPreset.apparels == null)
                                        {
                                            selPreset.apparels = new Dictionary<string, Color>();
                                        }
                                        selPreset.apparels.SetOrAdd(def.defName, color);
                                        setColor = null;
                                    };
                                    Find.WindowStack.Add(colorSelect);
                                }
                            }
                            iconLoc.y += uH + 5f;
                            butLoc.y += uH + 5f;
                            laLoc.y += uH + 5f;
                            unit.y += uH + 5f;
                        }

                        Widgets.EndScrollView();
                    }

                }
                Widgets.EndGroup();
            }

            private Vector2 leftScr = Vector2.zero;
            private bool addPreset = false;
            private string addName = "";
            private readonly Texture2D highLightSel = SolidColorMaterials.NewSolidColorTexture(new Color(1f, 244f, 1f, 0.1f));

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
                        Widgets.DrawBoxSolid(rect1, Color.gray);
                        FWSetting.PresetData data = FWSetting.PresetDatas[i];
                        if (data != null)
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
                            bool over = false;
                            if (Mouse.IsOver(texLoc0))
                            {
                                over = true;
                            }
                            Color color = Color.red;
                            GUI.DrawTexture(texLoc0, TexButton.Delete, ScaleMode.StretchToFill, true, 0, over ? color : Color.white, 0, 0);
                            if (Widgets.ButtonInvisible(texLoc0) && FWSetting.PresetDatas.Contains(data))
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
                    Widgets.DrawBoxSolid(butLoc, Color.gray);
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
                                FWSetting.PresetDatas = new List<FWSetting.PresetData>();
                            }
                            FWSetting.PresetDatas.Add(new FWSetting.PresetData(addName));
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
    }

    [StaticConstructorOnStartup]
    public static class FWModHarmonyPatch
    {
        internal static MethodInfo getPawn = null;
        internal static Type RPGInvType = null;
        private static readonly Type patch = typeof(FWModHarmonyPatch);
        private static readonly Type renderTree = typeof(PawnRenderTree);
        private static readonly Type renderSetup = typeof(DynamicPawnRenderNodeSetup_Apparel);
        static FieldInfo nodeSetupPwan = null;
        static FWModHarmonyPatch()
        {
            Harmony harmony = new Harmony("aedbia.fashionwardrobe");
            var flag = BindingFlags.Instance | BindingFlags.NonPublic;
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
            return holder is FashionOverrideComp.ApparelHolder ? (holder as FashionOverrideComp.ApparelHolder).comp.parent as Pawn : null;
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

    [DefOf]
    public static class FWMainTabDefOf
    {
        public static MainButtonDef FW_FashionTab;
    }
}