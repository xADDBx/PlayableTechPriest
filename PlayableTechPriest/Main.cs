using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM.VM.CharGen;
using Kingmaker.UI.MVVM.VM.CharGen.Phases.Appearance;
using Kingmaker.UI.MVVM.VM.CharGen.Phases.Appearance.Pages;
using Kingmaker.UnitLogic.Progression.Paths;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using System.IO;
using Kingmaker.UnitLogic.Levelup.Selections;
using Kingmaker.UnitLogic.Progression.Features;
using Kingmaker.UnitLogic.Levelup.Obsolete;
using Kingmaker.UnitLogic.Progression.Prerequisites;
using Kingmaker.UnitLogic.Levelup;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.EntitySystem.Entities.Base;
using System.Collections.Generic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.Levelup.Selections.Doll;
using Kingmaker.Visual.CharacterSystem;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem.Core;
using Kingmaker.Controllers.StarSystem;
using Owlcat.Runtime.Core;
using Kingmaker.View;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker;

namespace PlayableTechPriest;

#if DEBUG
[EnableReloading]
#endif
public static class Main {
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger log;
    public static bool Load(UnityModManager.ModEntry modEntry) {
        log = modEntry.Logger;
#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        modEntry.OnGUI = OnGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    internal static bool createTechPriest = false;
    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        GUILayout.Label("Should the next character creation be a Tech Priest?");
        createTechPriest = GUILayout.Toggle(createTechPriest, "Create Tech Priest");
    }

#if DEBUG
    public static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif    
    [HarmonyPatch(typeof(CharGenConfig), nameof(CharGenConfig.Create))]
    internal static class CharGenConfig_Create_Patch {
        [HarmonyPrefix]
        private static void Create(CharGenConfig.CharGenMode mode) {
            if (createTechPriest) {
                CharGenContext_GetOriginPath_Patch.isMercenary = mode == CharGenConfig.CharGenMode.NewCompanion;
            }
        }
    }
    [HarmonyPatch(typeof(CharGenContext), nameof(CharGenContext.GetOriginPath))]
    internal static class CharGenContext_GetOriginPath_Patch {
        internal static bool isMercenary = false;
        [HarmonyPostfix]
        private static void GetOriginPath(ref BlueprintOriginPath __result) {
            if (createTechPriest) {
                var copy = CopyBlueprint(__result);
                try {
                    var c = copy.Components.OfType<AddFeaturesToLevelUp>().Where(c => c.Group == FeatureGroup.ChargenOccupation).First();
                    var techPriestOccupation = ResourcesLibrary.BlueprintsCache.Load("777d9f9c570443b59120e78f2d9dd515") as BlueprintFeature;
                    c.m_Features = new[] { techPriestOccupation.ToReference<BlueprintFeatureReference>() }.AddRangeToArray(c.m_Features);
                    copy.Components[1] = c;
                    __result = copy;
                } catch (Exception e) {
                    log.Log(e.ToString());
                }
            }
        }
        private static T CopyBlueprint<T>(T bp) where T : SimpleBlueprint {
            var writer = new StringWriter();
            var serializer = JsonSerializer.Create(Json.Settings);
            serializer.Serialize(writer, new BlueprintJsonWrapper(bp));
            return serializer.Deserialize<BlueprintJsonWrapper>(new JsonTextReader(new StringReader(writer.ToString()))).Data as T;
        }
    }
#pragma warning disable CS0612 // Type or member is obsolete
    [HarmonyPatch(typeof(Prerequisite), nameof(Prerequisite.Meet))]
#pragma warning restore CS0612 // Type or member is obsolete
    internal static class Prerequisite_Meet_Patch {
        [HarmonyPostfix]
        private static void Meet(ref bool __result, Prerequisite __instance, IBaseUnitEntity unit) {
            var techPriestOccupation = ResourcesLibrary.BlueprintsCache.Load("777d9f9c570443b59120e78f2d9dd515") as BlueprintFeature;
            Feature feature = unit.ToBaseUnitEntity().Facts.Get(techPriestOccupation) as Feature;
            if (createTechPriest && feature != null) {
                var key = __instance.Owner.name;
                if ((key?.Contains("DarkestHour") ?? false) || (key?.Contains("MomentOfTriumph") ?? false)) {
                    __result = true;
                }
            }
        }
    }
    private static List<string> femaleEEIds = new() { "95b61f949fe46bc43a4107a77fa10a97", "c256cd40ee105e44b8b67edd6f71784f", "d317d7fb22ff9824ab497a029b8e1c3b", "9991c5802950b6241991e51d61c45ed1" };
    private static List<string> maleEEIds = new() { "26055e6f510e74442a881f0707b45f98", "f38850c1ac1b5cb4ebcb462198ebfed7", "9f77631259fb68344851d55c62536071", "fb7dbf6f0935fc1458c1a86488ce5de7" };
    [HarmonyPatch(typeof(CharGenContextVM), nameof(CharGenContextVM.CompleteCharGen))]
    internal static class CharGenContextVM_ComplteCharGen_Patch {
        [HarmonyPrefix]
        private static void CompleteCharGen(BaseUnitEntity resultUnit) {
            var techPriestOccupation = ResourcesLibrary.BlueprintsCache.Load("777d9f9c570443b59120e78f2d9dd515") as BlueprintFeature;
            Feature feature = resultUnit.Facts.Get(techPriestOccupation) as Feature;
            if (createTechPriest && feature != null) {
                EntityPartStorage.perSave.AddClothes[resultUnit.UniqueId] = resultUnit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? maleEEIds : femaleEEIds; 
                EntityPartStorage.SavePerSaveSettings();
            }
        }
    }
    [HarmonyPatch(typeof(PartUnitProgression))]
    internal static class PartUnitProgression_Patch {
        [HarmonyPatch(nameof(PartUnitProgression.AddFeatureSelection))]
        [HarmonyPrefix]
        private static void AddFeatureSelection(ref BlueprintPath path) {
            if (createTechPriest && path is BlueprintOriginPath) {
                path = CharGenContext_GetOriginPath_Patch.isMercenary ? BlueprintCharGenRoot.Instance.NewCompanionCustomChargenPath : BlueprintCharGenRoot.Instance.NewGameCustomChargenPath;
            }
        }
        [HarmonyPatch(nameof(PartUnitProgression.AddPathRank))]
        [HarmonyPrefix]
        private static void AddPathRank(ref BlueprintPath path) {
            if (createTechPriest && path is BlueprintOriginPath) {
                path = CharGenContext_GetOriginPath_Patch.isMercenary ? BlueprintCharGenRoot.Instance.NewCompanionCustomChargenPath : BlueprintCharGenRoot.Instance.NewGameCustomChargenPath;
            }
        }
    }
    [HarmonyPatch(typeof(PartUnitViewSettings), nameof(PartUnitViewSettings.Instantiate))]
    internal static class PartUnitViewSettings_Instantiate_Patch {
        [HarmonyPrefix]
        private static void Instant_Pre(PartUnitViewSettings __instance) {
            DollData_CreateUnitView_Patch.context = __instance.Owner;
        }
        [HarmonyPostfix]
        private static void Instant_Post() {
            DollData_CreateUnitView_Patch.context = null;
        }
    }
    [HarmonyPatch(typeof(DollState), nameof(DollState.CollectMechanicEntities))]
    internal static class DollState_CollectMechanicEntities_Patch {
        [HarmonyPostfix]
        private static void CollectMechanicEntitities(DollState __instance, ref IEnumerable<EquipmentEntityLink> __result, BaseUnitEntity unit) {
            var techPriestOccupation = ResourcesLibrary.BlueprintsCache.Load("777d9f9c570443b59120e78f2d9dd515") as BlueprintFeature;
            Feature feature = unit.Facts.Get(techPriestOccupation) as Feature;
            if (createTechPriest && feature != null) {
                var ids = unit.Gender == Kingmaker.Blueprints.Base.Gender.Male ? maleEEIds : femaleEEIds;
                var eels = ids.Select(id => new EquipmentEntityLink() { AssetId = id });
                var res = __result.ToList();
                res.AddRange(eels);
                __result = res.AsEnumerable();
            }
        }
    }
    [HarmonyPatch(typeof(DollData), nameof(DollData.CreateUnitView))]
    internal static class DollData_CreateUnitView_Patch {
        internal static AbstractUnitEntity context = null;
        [HarmonyPrefix]
        private static bool CreateUnitView(DollData __instance, ref UnitEntityView __result, bool savedEquipment) {
            if (EntityPartStorage.perSave.AddClothes.TryGetValue(context.UniqueId, out var ees)) {
                BlueprintCharGenRoot charGenRoot = BlueprintRoot.Instance.CharGenRoot;
                Character character = ((__instance.Gender == Kingmaker.Blueprints.Base.Gender.Male) ? charGenRoot.MaleDoll : charGenRoot.FemaleDoll);
                UnitEntityView component = character.GetComponent<UnitEntityView>();
                if (component == null) {
                    throw new Exception(string.Format("Could not create unit view by doll data: invalid prefab {0}", character));
                }
                UnitEntityView unitEntityView = UnityEngine.Object.Instantiate<UnitEntityView>(component);
                Character component2 = unitEntityView.GetComponent<Character>();
                if (component2 == null) {
                    return unitEntityView;
                }
                component2.RemoveAllEquipmentEntities(savedEquipment);
                if (__instance.RacePreset != null) {
                    component2.Skeleton = ((__instance.Gender == Kingmaker.Blueprints.Base.Gender.Male) ? __instance.RacePreset.MaleSkeleton : __instance.RacePreset.FemaleSkeleton);
                    component2.AddEquipmentEntities(__instance.RacePreset.Skin.Load(__instance.Gender, __instance.RacePreset.RaceId), savedEquipment);
                }
                foreach (string text in __instance.EquipmentEntityIds) {
                    EquipmentEntity equipmentEntity = ResourcesLibrary.TryGetResource<EquipmentEntity>(text, false, false);
                    component2.AddEquipmentEntity(equipmentEntity, savedEquipment);
                }
                foreach (var eeId in ees) {
                    var eel = new EquipmentEntityLink() { AssetId = eeId };
                    var ee = eel.Load();
                    if (!component2.EquipmentEntities.Where(e => e.name == ee.name).Any()) {
                        component2.AddEquipmentEntity(ee, savedEquipment);
                    }
                }
                __instance.ApplyRampIndices(component2, savedEquipment);
                __result = unitEntityView;
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Game))]
    internal static class Game_Patch {
        [HarmonyPatch(nameof(Game.LoadArea), new Type[] { typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action) })]
        [HarmonyPrefix]
        private static void LoadArea() {
            EntityPartStorage.ClearCachedPerSave();
        }
    }
}