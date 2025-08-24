using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Fashion_Wardrobe
{
    public class FWSetting : ModSettings
    {
        internal static bool OnlyForColonist = true;
        internal static bool ShowInDoorFight = false;
        internal static bool DefaultEnableFashion = false;
        internal static bool EnableMainButton = true;
        internal static bool OnlyShowFashion = false;
        internal static List<FWPresetData> PresetDatas = new List<FWPresetData>();
        internal static Dictionary<string, FWCompData> DefaultCompDatas = new Dictionary<string, FWCompData>();
        internal static bool EnablePreview = true;
        internal static bool Direct_Open_FW = false;
        internal static bool CanAddConflict = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref OnlyForColonist, "OnlyForColonist", true, true);
            Scribe_Values.Look(ref ShowInDoorFight, "ShowInDoorFight", false, true);
            Scribe_Values.Look(ref DefaultEnableFashion, "DefaultEnableFashion", false, true);
            Scribe_Values.Look(ref EnableMainButton, "EnableMainButton", true, true);
            Scribe_Values.Look(ref OnlyShowFashion, "OnlyShowFashion", false, true);
            Scribe_Values.Look(ref CanAddConflict, "CanAddConflict", false, true);
            Scribe_Values.Look(ref Direct_Open_FW, "Direct_Open_FW", false, true);
            Scribe_Values.Look(ref EnablePreview, "EnablePreview", true, true);
            Scribe_Collections.Look(ref PresetDatas, "PresetDatas", LookMode.Deep);
            Scribe_Collections.Look(ref DefaultCompDatas, "DefaultCompDatas", LookMode.Value, LookMode.Deep);
            if (PresetDatas == null)
            {
                PresetDatas = new List<FWPresetData>();
            }
            PresetDatas.RemoveAll(a => a == null);
            if (DefaultCompDatas == null)
            {
                DefaultCompDatas = new Dictionary<string, FWCompData>();
            }
            DefaultCompDatas.RemoveAll(a => a.Key == null || a.Value == null);
        }
    }
    public class FWPresetData : IExposable
    {
        private string ID = "";
        public Dictionary<string, FWPresetApparelData> apparels = new Dictionary<string, FWPresetApparelData>();

        public string Id
        {
            get { return ID; }
        }

        public FWPresetData()
        {
            ID = "NORMAL";
        }

        public FWPresetData(string presetName)
        {
            ID = presetName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ID, "Id");
            Scribe_Collections.Look(ref apparels, "PresetData", LookMode.Value, LookMode.Deep);
            if (apparels == null)
            {
                apparels = new Dictionary<string, FWPresetApparelData>();
            }
            apparels.RemoveAll(a => a.Key == null || a.Value == null);
        }

        internal void ApplyID(string renameStr)
        {
            ID = renameStr;
        }

    }
    public class FWPresetApparelData : IExposable
    {
        public Color color = Color.white;
        public string styleDef;
        public int pathIndex;
        public FWPresetApparelData()
        {
        }
        public FWPresetApparelData(Color color, string styleDef, int pathIndex)
        {
            this.color = color;
            this.styleDef = styleDef;
            this.pathIndex = pathIndex;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref color, "color");
            Scribe_Values.Look(ref styleDef, "StyleDef");
            Scribe_Values.Look(ref pathIndex, "pathIndex",-1);
        }
    }
}
