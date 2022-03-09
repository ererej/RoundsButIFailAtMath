using System;
using System.Collections;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using UnboundLib;
using UnboundLib.GameModes;
using UnboundLib.Utils.UI;
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
        private const string ModVersion = "1.0.0";

        private static float rangeMin, rangeMax;
        private const float sliderRange = 5f;
        private static ConfigEntry<float> RangeMinConfig, RangeMaxConfig;
        private static UnityEngine.UI.Slider RangeMinSlider, RangeMaxSlider;

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
            Unbound.RegisterMenu(ModName, () => { }, NewGUI, null, false);
            GameModeManager.AddHook(GameModeHooks.HookGameStart, this.RandomiseCardStats);
        }

        // Methods to be called on slider update
        void RangeMinSliderAction(float val)
        {
            // Stretch slider as user approches the edge
            if (RangeMinSlider != null)
            {
                if (RangeMinSlider.minValue - val > -0.1f) { RangeMinSlider.minValue -= 0.1f; }
                else if (RangeMinSlider.minValue < -sliderRange) { RangeMinSlider.minValue += 0.1f; }
                if (RangeMinSlider.maxValue - val < 0.1f) { RangeMinSlider.maxValue += 0.1f; }
                else if (RangeMinSlider.maxValue > sliderRange) { RangeMinSlider.maxValue -= 0.1f; }
            }
            RangeMinConfig.Value = val;
            rangeMin = val;
        }
        void RangeMaxSliderAction(float val)
        {
            // Stretch slider as user approches the edge
            if (RangeMaxSlider != null)
            {
                if (RangeMaxSlider.minValue - val > -0.1f) { RangeMaxSlider.minValue -= 0.1f; }
                else if (RangeMaxSlider.minValue < -sliderRange) { RangeMaxSlider.minValue += 0.1f; }
                if (RangeMaxSlider.maxValue - val < 0.1f) { RangeMaxSlider.maxValue += 0.1f; }
                else if (RangeMaxSlider.maxValue > sliderRange) { RangeMaxSlider.maxValue -= 0.1f; }
            }
            RangeMaxConfig.Value = val;
            rangeMax = val;
        }

        // Options menu
        private void NewGUI(GameObject menu)
        {
            MenuHandler.CreateText("Set Multiplier Range", menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateSlider("Min", menu, 50, -sliderRange, sliderRange, RangeMinConfig.Value, RangeMinSliderAction, out RangeMinSlider);
            MenuHandler.CreateSlider("Max", menu, 50, -sliderRange, sliderRange, RangeMaxConfig.Value, RangeMaxSliderAction, out RangeMaxSlider);
        }

        private IEnumerator RandomiseCardStats(IGameModeHandler gm)
        {
            System.Random rng = new System.Random();

            // Loop through all cards
            foreach (CardInfo card in CardChoice.instance.cards)
            {
                card.cardStats = CardChoice.instance.GetSourceCard(card).cardStats;
                // Loop through each stat on the card
                foreach (CardInfoStat stat in card.cardStats)
                {
                    // Parseable version of the label
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
                                block.cdMultiplier = amount/100f;
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
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + (int)amount;
                            break;

                        case "Bullet bounce":
                        case "Bullet bounces":
                            gun.reflects = (int)amount;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + (int)amount;
                            break;

                        case "Bullet slow":
                            gun.slow = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Bullet speed":
                        case "Bullet speed ":
                            gun.projectileSpeed = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Bullets drill through walls":
                            // TODO: Figure out how to get drill component
                            break;
                            
                        case "DMG":
                            gun.damage = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Health":
                        case "HP":
                            stat.positive = (amount >= 0);
                            charstat.health = Math.Max(1f + amount/100f, 0.5f); // Prevent neg. HP (blackhole)
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Life steal":
                            charstat.lifeSteal = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Projectile speed":
                            gun.projectielSimulatonSpeed = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Reload speed":
                            gun.reloadTime = amount/100f;
                            stat.positive = (amount >= 0);
                            stat.amount = (stat.positive? "+":"") + amount + "%";
                            break;

                        case "Reload time":
                            gun.reloadTimeAdd = amount;
                            stat.positive = (amount < 0);
                            stat.amount = (!stat.positive? "+":"") + amount + "s";
                            break;

                        case "Splash DMG":
                            // TODO: Figure out how splash dmg is applied
                            break;

                        default:
                            Logger.LogWarning("Unknown stat '" + stat.stat + ": " + stat.amount + "' encountered");
                            break;
                    }
                }
            }

            yield break;
        }
    }
}
