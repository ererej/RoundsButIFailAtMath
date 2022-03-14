using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using Photon.Pun;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Utils.UI;
using UnboundLib.Networking;
using UnityEngine;
using TMPro;

namespace RoundsButIFailAtMath
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(ModID, ModNameShort, ModVersion)]
    [BepInProcess("Rounds.exe")]
    public class RoundsButIFailAtMath : BaseUnityPlugin
    {
        // Config
        private const string ModID = "com.senyksia.rounds.roundsbutifailatmath";
        private const string ModName = "Rounds but I Fail at Math";
        private const string ModNameShort = "RBMath";
        private const string ModVersion = "1.0.2";

        private static int seed;
        private static float rangeMin, rangeMax;
        private const float SLIDER_RANGE = 5f;
        private static ConfigEntry<float> RangeMinConfig, RangeMaxConfig;
        private static UnityEngine.UI.Slider RangeMinSlider, RangeMaxSlider;
        private static Dictionary<string, Dictionary<string, string>> defaultCards = new Dictionary<string, Dictionary<string, string>>(); // gross

        private void Awake()
        {
            RangeMinConfig = Config.Bind(ModNameShort, "Minimum multiplier for randomising stats", -1f);
            RangeMaxConfig = Config.Bind(ModNameShort, "Maximum multiplier for randomising stats", 1f);
        }

        private void Start()
        {
            // Prevent orphaning
            rangeMin = RangeMinConfig.Value;
            rangeMax = RangeMaxConfig.Value;

            // Registers
            Unbound.RegisterHandshake(ModID, OnHandShakeCompleted);
            Unbound.RegisterMenu(ModName, () => { }, NewGUI, null, false);
            GameModeManager.AddHook(GameModeHooks.HookGameStart, OnGameStart);
        }

        private void OnHandShakeCompleted()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            seed = Environment.TickCount; // Explicit seed for syncing purposes
            NetworkingManager.RPC_Others(typeof(RoundsButIFailAtMath), nameof(SyncClients), rangeMin, rangeMax, seed);
            RandomiseCardStats(); // Randomise for host
        }

        [UnboundRPC]
        private static void SyncClients(float _rangeMin, float _rangeMax, int _seed)
        {
            rangeMin = _rangeMin;
            rangeMax = _rangeMax;
            seed = _seed;
            RandomiseCardStats(); // Randomise for clients, to prevent race condition
        }

        // Methods to be called on slider update
        void RangeMinSliderAction(float val)
        {
            // Stretch slider as user approches the edge
            if (RangeMinSlider != null)
            {
                if (RangeMinSlider.minValue - val > -0.1f) { RangeMinSlider.minValue = val - 0.1f; } // L -> L
                else if (RangeMinSlider.minValue < -SLIDER_RANGE) { RangeMinSlider.minValue = val + 0.1f; } // L -> R
                if (RangeMinSlider.maxValue - val < 0.1f) { RangeMinSlider.maxValue = val + 0.1f; } // R -> R
                else if (RangeMinSlider.maxValue > SLIDER_RANGE) { RangeMinSlider.maxValue = val - 0.1f; } // R -> L
            }
            RangeMinConfig.Value = val;
            rangeMin = val;
        }
        void RangeMaxSliderAction(float val)
        {
            // Stretch slider as user approches the edge
            if (RangeMaxSlider != null)
            {
                if (RangeMaxSlider.minValue - val > -0.1f) { RangeMaxSlider.minValue = val - 0.1f; } // L -> L
                else if (RangeMaxSlider.minValue < -SLIDER_RANGE) { RangeMaxSlider.minValue = val + 0.1f; } // L -> R
                if (RangeMaxSlider.maxValue - val < 0.1f) { RangeMaxSlider.maxValue = val + 0.1f; } // R -> R
                else if (RangeMaxSlider.maxValue > SLIDER_RANGE) { RangeMaxSlider.maxValue = val - 0.1f; } // R -> L
            }
            RangeMaxConfig.Value = val;
            rangeMax = val;
        }

        // Options menu
        private void NewGUI(GameObject menu)
        {
            MenuHandler.CreateText("Set Multiplier Range", menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateSlider("Min", menu, 50, -SLIDER_RANGE, SLIDER_RANGE, RangeMinConfig.Value, RangeMinSliderAction, out RangeMinSlider);
            MenuHandler.CreateSlider("Max", menu, 50, -SLIDER_RANGE, SLIDER_RANGE, RangeMaxConfig.Value, RangeMaxSliderAction, out RangeMaxSlider);
        }

        private IEnumerator OnGameStart(IGameModeHandler _)
        {
            OnHandShakeCompleted();
            yield break;
        }

        private static void RandomiseCardStats()
        {
            System.Random rng = new System.Random(seed);

            // Loop through all cards
            foreach (CardInfo card in CardChoice.instance.cards)
            {
                // Intercept and save default card
                if (!defaultCards.ContainsKey(card.cardName))
                {
                    defaultCards.Add(card.cardName, new Dictionary<string, string>());
                    foreach (CardInfoStat stat in card.cardStats)
                    {
                        defaultCards[card.cardName].Add(stat.stat, stat.amount);
                    }
                }

                // Loop through each stat on the card
                foreach (CardInfoStat stat in card.cardStats)
                {
                    // Parseable version of the label
                    stat.amount = defaultCards[card.cardName][stat.stat];
                    string label = Regex.Replace(stat.amount, "[^0-9.+-]", "");

                    // Randomise multiplier
                    float MULTIPLIER = (float)(rng.NextDouble() * (rangeMax - rangeMin) + rangeMin);
                    float amount = float.Parse(label) * MULTIPLIER; // Calculate new modifier

                    // Components
                    Gun gun = card.GetComponent<Gun>();
                    Block block = card.GetComponent<Block>();
                    CharacterStatModifiers charstat = card.GetComponent<CharacterStatModifiers>();

                    // Update recognised card stats
                    switch (stat.stat)
                    {
                        case "Ammo":
                        case "AMMO":
                            gun.ammo = (int)amount; // Set new modifier
                            stat.positive = ((int)amount >= 0); // Colour stat green/red
                            stat.amount = (stat.positive? "+":"") + (int)amount; // Recompile display number
                            break;

                        case "ATKSPD":
                        case "Attack Speed":
                            stat.positive = (amount >= 0);
                            gun.attackSpeed = stat.positive // Atkspd functions differently to its label
                                ? 100f/(amount + 100f) // (1 + x/100)^-1
                                : 1f - amount/100f; //     1 + x/100
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Block cooldown":
                            stat.positive = (amount < 0);
                            if (stat.amount.EndsWith("%"))
                            {
                                block.cdMultiplier = 1f + amount/100f;
                                stat.amount = (!stat.positive? "+":"") + amount + "%";
                            }
                            else
                            {
                                block.cdAdd = amount;
                                stat.amount = (!stat.positive? "+":"") + amount + "s";
                            }
                            break;

                        case "Bullet":
                        case "Bullets":
                            gun.numberOfProjectiles = (int)amount;
                            stat.positive = ((int)amount >= 0);
                            stat.amount = (stat.positive? "+":"") + (int)amount;
                            break;

                        case "Bullet bounce":
                        case "Bullet bounces":
                            gun.reflects = (int)amount;
                            stat.positive = ((int)amount >= 0);
                            stat.amount = (stat.positive? "+":"") + (int)amount;
                            break;

                        case "Bullet slow":
                            gun.slow = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Bullet speed":
                        case "Bullet speed ":
                            gun.projectileSpeed = 1f + amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Bullets drill through walls":
                            // TODO: Figure out how to get drill component
                            break;
                            
                        case "DMG":
                            gun.damage = Math.Max(1f + amount/100f, 0f);
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + Math.Max(amount, -100) + "%";
                            break;

                        case "Health":
                        case "HP":
                            charstat.health = Math.Max(1f + amount/100f, 0.5f); // Prevent neg. HP (-50%/x0.5 min)
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + Math.Max(100 + amount, 50) + "%";
                            break;

                        case "Life steal":
                            charstat.lifeSteal = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Projectile speed":
                            gun.projectielSimulatonSpeed = 1f + amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Reload speed":
                        case "Reload time":
                            if (stat.amount.EndsWith("%"))
                            {
                                stat.positive = (amount >= 0);
                                gun.reloadTime = 1f + amount/100f;
                                stat.amount = (!stat.positive ? "+" : "") + amount + "%";
                            }
                            else
                            {
                                stat.positive = (amount < 0);
                                gun.reloadTimeAdd = amount;
                                stat.amount = (!stat.positive ? "+" : "") + amount + "s";
                            }
                            break;

                        case "Splash DMG":
                            // TODO: Figure out how splash dmg is applied
                            break;
                    }
                }
            }
        }
    }
}
