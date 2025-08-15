using ABEasyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Fashion_Wardrobe
{
    public class FashionOverrideComp : ThingComp
    {
        private Pawn pawn => (parent as Pawn);

        internal bool FashionClothesEnable;
        internal bool OnWorkNoDraft = false;
        internal ThingOwner<Apparel> Clothes;
        internal Dictionary<string, FWCompData> DrawRule = new Dictionary<string, FWCompData>();
        private bool draft;
        private bool underRoof;
        private PawnTextureCache textureCache;
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
            Clothes = new ThingOwner<Apparel>(new FWApparelHolder(this));
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref FashionClothesEnable, "FashionClothesEnable", true);
            Scribe_Values.Look(ref OnWorkNoDraft, "OnWorkNoDraft", false);
            Scribe_Deep.Look(ref Clothes, "clothes", new object[]
            {
                new FWApparelHolder(this)
            });
            if (Clothes == null)
            {
                Clothes = new ThingOwner<Apparel>(new FWApparelHolder(this));
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
            Draft = pawn.Drafted;
            if (!FWSetting.OnlyForColonist || pawn.IsColonist)
            {
                if (pawn.Map != null)
                {
                    UnderRoof = !pawn.Position.UsesOutdoorTemperature(pawn.Map);
                }
                if (draftValueChange || UnderRoofChange)
                {
                    if (FWMod.HATWeakerLoadrIndex == -1 && pawn.apparel != null)
                    {
                        pawn.apparel.Notify_ApparelChanged();
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
                if (!FWSetting.CanAddConflict)
                {
                    Clothes.RemoveAll(a => !ApparelUtility.CanWearTogether(a.def, apparel.def, pawn.RaceProps.body));
                }
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
                AddDefaultDrawRule(apparel.def.defName);
            }

        }
        public void AddDefaultDrawRule(string defName)
        {
            if (DrawRule == null)
            {
                DrawRule = new Dictionary<string, FWCompData>();
            }
            if ((DrawRule.Count == 0 || !DrawRule.ContainsKey(defName))
                && !FWSetting.DefaultCompDatas.NullOrEmpty() && FWSetting.DefaultCompDatas.TryGetValue(defName, out FWCompData data))
            {
                DrawRule.Add(defName, data);
            }
        }
        public List<Apparel> GetApparel()
        {
            if (pawn == null)
            {
                return new List<Apparel>();
            }
            List<Apparel> list0;
            try
            {
                if (textureCache == null)
                {
                    if (PawnTextureCache.GetPawnTextureCache(pawn, out PawnTextureCache a))
                    {
                        textureCache = a;
                    }
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce(e.ToString(), GetHashCode());
            }
            bool hasCache = textureCache != null;
            if (FashionClothesEnable && Clothes.Count != 0 && (!pawn.Drafted || !OnWorkNoDraft))
            {
                list0 = new List<Apparel>(Clothes);
            }
            else
            {
                list0 = new List<Apparel>();
            }
            bool flag = !list0.NullOrEmpty();
            List<Apparel> list1 = new List<Apparel>();
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            if (!FWSetting.OnlyShowFashion && !wornApparel.NullOrEmpty())
            {
                for (int i = 0; i < wornApparel.Count; i++)
                {
                    Apparel apparel = wornApparel[i];
                    if (!list0.Any(b => ApparelUtility.CanWearTogether(apparel.def, b.def, pawn.RaceProps?.body ?? BodyDefOf.Human)))
                    {
                        list1.Add(apparel);
                    }
                    AddDefaultDrawRule(apparel.def.defName);
                }
                if (flag)
                {
                    list1.AddRange(list0);
                }
            }
            else if (flag)
            {
                list1.AddRange(list0);
            }
            if (list1.Count > 0)
            {
                RemoveNoDisplayGraphic(ref list1);
                SortCloths(ref list1);
                if (hasCache)
                {
                    if (!textureCache.postApparels.NullOrEmpty())
                    {
                        foreach (var apparel in textureCache.postApparels)
                        {
                            list1.RemoveAll(a => ApparelUtility.CanWearTogether(a.def, apparel.def, pawn.RaceProps?.body ?? BodyDefOf.Human));
                            list1.Add(apparel);
                        }
                    }
                    textureCache.OverrideApparels = list1;
                }
            }
            return list1;
        }

        public void SortCloths(ref List<Apparel> apparels)
        {
            apparels.Sort((Apparel a, Apparel b) =>
            {
                int ai = a.def.apparel.LastLayer.drawOrder;
                int bi = b.def.apparel.LastLayer.drawOrder;
                var aTag = a.def.apparel.tags;
                if (!aTag.NullOrEmpty() && aTag.Contains("ABVisiblePants"))
                {
                    ai -= 1;
                }
                var bTag = b.def.apparel.tags;
                if (!bTag.NullOrEmpty() && bTag.Contains("ABVisiblePants"))
                {
                    bi -= 1;
                }
                return ai.CompareTo(bi);
            });
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
                        if (!pawn.Drafted)
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

                        if (data.HideInDoor && pawn.Map != null && !pawn.Position.UsesOutdoorTemperature(pawn.Map))
                        {
                            return true;
                        }
                    }
                }
                return false;
            });
        }

        internal void ApplyPreset(FWPresetData preset)
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
                    if (apparel.GetComp<CompColorable>() != null && preset.apparels.TryGetValue(defName, out FWPresetApparelData aData))
                    {
                        apparel.SetColor(aData.color);
                        if (!aData.styleDef.NullOrEmpty())
                        {
                            ThingStyleDef styleDef = DefDatabase<ThingStyleDef>.GetNamedSilentFail(aData.styleDef);
                            if (styleDef != null)
                            {
                                apparel.SetStyleDef(styleDef);
                            }
                        }
                    }
                    AddApparel(apparel, false);
                }
            }
        }
        public class FWApparelHolder : IThingHolder
        {
            public FashionOverrideComp comp;
            public IThingHolder ParentHolder => comp.ParentHolder;

            public FWApparelHolder(FashionOverrideComp comp)
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

}
