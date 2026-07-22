using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SFS.Parts;
using UnityEngine;

namespace PartId
{
    internal static class PartIdIdentityPatch
    {
        internal static bool Installed { get; private set; }
        internal static string Failure { get; private set; } = "";

        internal static void Install()
        {
            try
            {
                var harmony = new Harmony("01.PartId.Identity");
                MethodInfo createSaves = typeof(PartSave).GetMethod(
                    "CreateSaves",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(Part[]) },
                    null);
                MethodInfo createParts = typeof(PartsLoader)
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(method =>
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        return method.Name == "CreateParts" && method.ReturnType == typeof(Part[]) &&
                            parameters.Length > 0 && parameters[0].ParameterType == typeof(PartSave[]);
                    });

                if (createSaves == null)
                    throw new MissingMethodException("PartSave.CreateSaves(Part[])");
                if (createParts == null)
                    throw new MissingMethodException("PartsLoader.CreateParts(PartSave[], ...)");

                harmony.Patch(createSaves, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(PartIdIdentityPatch), nameof(CreateSavesPostfix))));
                harmony.Patch(createParts, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(PartIdIdentityPatch), nameof(CreatePartsPostfix))));
                Installed = true;
                Debug.Log("[PartId] Live identity save/load patches installed.");
            }
            catch (Exception ex)
            {
                Installed = false;
                Failure = ex.GetType().Name + ": " + ex.Message;
                Debug.Log("[PartId] Live identity persistence patches unavailable: " + Failure);
            }
        }

        public static void CreateSavesPostfix(Part[] __0, ref PartSave[] __result)
        {
            if (__0 == null || __result == null)
                return;

            int count = Math.Min(__0.Length, __result.Length);
            for (int i = 0; i < count; i++)
            {
                Part part = __0[i];
                PartSave save = __result[i];
                if (part == null || save == null || !PartIdRuntime.GetOrAssignPid(part, out string pid))
                    continue;

                if (save.TEXT_VARIABLES == null)
                    save.TEXT_VARIABLES = new Dictionary<string, string>();
                save.TEXT_VARIABLES[PartIdRuntime.SavedPidKey] = pid;
            }
        }

        public static void CreatePartsPostfix(PartSave[] __0, ref Part[] __result)
        {
            if (__0 == null || __result == null)
                return;

            int count = Math.Min(__0.Length, __result.Length);
            for (int i = 0; i < count; i++)
            {
                PartSave save = __0[i];
                Part part = __result[i];
                if (save == null || part == null || save.TEXT_VARIABLES == null ||
                    !save.TEXT_VARIABLES.TryGetValue(PartIdRuntime.SavedPidKey, out string pid))
                    continue;

                PartIdRuntime.BindSavedPid(part, pid);
            }
        }
    }
}
