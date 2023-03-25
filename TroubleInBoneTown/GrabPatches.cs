using HarmonyLib;
using MelonLoader;
using SLZ;
using SLZ.Interaction;
using SLZ.Rig;

namespace TroubleInBoneTown
{
    public class GrabPatches
    {
        public class GrabVariables
        {
            public static TroubleInBoneTownGamemode.ChestInspection rChestInspection;
            public static TroubleInBoneTownGamemode.ChestInspection lChestInspection;
        }
        
        public class GrabPatch
        {
            public static void Prefix(Grip __instance, Hand hand)
            {
                if (TroubleInBoneTownGamemode.Instance == null)
                {
                    return;
                }

                if (TroubleInBoneTownGamemode.Instance.IsActive())
                {
                    if (hand.manager == BoneLib.Player.rigManager)
                    {
                        if (!__instance.HasHost)
                        {
                            return;
                        }

                        if (__instance.Host.Rb != null)
                        {
                            if (__instance.Host.Rb.GetComponentInParent<RigManager>())
                            {
                                RigManager rigManager = __instance.Host.Rb.GetComponentInParent<RigManager>();
                                if (rigManager.name.ToLower().Contains("ragdoll"))
                                {
                                    TroubleInBoneTownGamemode.ChestInspection foundInspection = null;
                                    foreach (var keypair in TroubleInBoneTownGamemode._ChestInspections)
                                    {
                                        if (keypair.Key.name == rigManager.name)
                                        {
                                            foundInspection = keypair.Value;
                                            break;
                                        }
                                    }
                                    if (foundInspection != null)
                                    {
                                        if (hand.handedness == Handedness.RIGHT)
                                        {
                                            GrabVariables.rChestInspection = foundInspection;
                                            GrabVariables.rChestInspection.Toggle(true);
                                        }

                                        if (hand.handedness == Handedness.LEFT)
                                        {
                                            GrabVariables.lChestInspection = foundInspection;
                                            GrabVariables.lChestInspection.Toggle(true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public class GrabDetachPatch
        {
            public static void Prefix(Hand __instance)
            {
                if (TroubleInBoneTownGamemode.Instance == null)
                {
                    return;
                }
                
                if (TroubleInBoneTownGamemode.Instance.IsActive())
                {
                    if (__instance.manager == BoneLib.Player.rigManager)
                    {
                        if (__instance.handedness == Handedness.RIGHT)
                        {
                            if (GrabVariables.rChestInspection != null)
                            {
                                if (GrabVariables.lChestInspection == null)
                                {
                                    GrabVariables.rChestInspection.Toggle(false);
                                }
                                else
                                {
                                    if (GrabVariables.lChestInspection != GrabVariables.rChestInspection)
                                    {
                                        GrabVariables.rChestInspection.Toggle(false);
                                    }
                                }
                                GrabVariables.rChestInspection = null;
                            }
                        }
                        if (__instance.handedness == Handedness.LEFT)
                        {
                            if (GrabVariables.lChestInspection != null)
                            {
                                if (GrabVariables.rChestInspection == null)
                                {
                                    GrabVariables.lChestInspection.Toggle(false);
                                }
                                else
                                {
                                    if (GrabVariables.rChestInspection != GrabVariables.lChestInspection)
                                    {
                                        GrabVariables.lChestInspection.Toggle(false);
                                    }
                                }
                                GrabVariables.lChestInspection = null;
                            }
                        }
                    }
                }
            }
        }
    }
}