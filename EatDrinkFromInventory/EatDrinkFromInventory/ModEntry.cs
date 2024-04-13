﻿using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Menus;

namespace EatDrinkFromInventory
{
    public class ModEntry : Mod
    {
        // Variables
        bool usedHotkeyToEat = false;
        bool isEating = false;
        int facingDirectionBeforeEating = 0;
        bool shouldMountHorse = false;
        GameLocation shouldMountHorseLocation = Game1.currentLocation;

        private ModConfig config = new();

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
            {
                return;
            }

            configMenu.Register(
                mod: this.ModManifest,
                reset: () => config = new ModConfig(),
                save: () => this.Helper.WriteConfig(config)
            );

            configMenu.AddKeybindList(
                this.ModManifest,
                () => config.Hotkey,
                (KeybindList val) => config.Hotkey = val,
                () => "Consume Hotkey",
                () => "The hotkey to consume the food/drink that the cursor is over."
            );
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            CheckEating();
            CheckHorseAppeared();
        }

        private void CheckEating()
        {
            var oldIsEating = isEating;
            var newIsEating = Game1.player.isEating;
            isEating = newIsEating;

            if (oldIsEating && !newIsEating)
            {
                Game1.player.FacingDirection = facingDirectionBeforeEating;

                if (usedHotkeyToEat)
                {
                    usedHotkeyToEat = false;
                    EmulatePause();
                }
            }
        }

        private void CheckHorseAppeared()
        {
            if (!shouldMountHorse)
            {
                return;
            }

            Log("GH0");

            var oldLocation = shouldMountHorseLocation;
            var newLocation = Game1.currentLocation;
            shouldMountHorseLocation = newLocation;

            if (oldLocation != newLocation)
            {
                shouldMountHorse = false;
                return;
            }

            Log("GH1");

            Horse? maybeHorse = GetHorse();

            if (
                Game1.player.canMove
                && maybeHorse is Horse horse
                && horse.Tile.X == Game1.player.Tile.X
                && horse.Tile.Y == Game1.player.Tile.Y
            )
            {
                Log("GH2");
                shouldMountHorse = false;
                horse.checkAction(Game1.player, Game1.currentLocation);
            }
        }

        private static Horse? GetHorse()
        {
            foreach (NPC character in Game1.currentLocation.characters)
            {
                if (character is Horse horse)
                {
                    return horse;
                }
            }

            return null;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            if (config.Hotkey.IsDown())
            {
                ConsumeItemThatCursorIsOver();
            }
        }

        private void ConsumeItemThatCursorIsOver()
        {
            if (Game1.activeClickableMenu is not GameMenu gameMenu) {
                return;
            }

            if (gameMenu.pages.Count == 0)
            {
                return;
            }

            var firstPage = gameMenu.pages[0];
            if (firstPage is not InventoryPage inventoryPage)
            {
                return;
            }

            if (inventoryPage.hoveredItem is not StardewValley.Object obj)
            {
                return;
            }

            if (obj.Edibility > 0)
            {
                EatObject(obj);
            }
            else if (obj.Name == "Staircase" && Game1.currentLocation is MineShaft mineShaft)
            {
                DecrementStack(obj);

                // From: MineShaft.cs
                Game1.enterMine(mineShaft.mineLevel + 1);
                Game1.playSound("stairsdown");
                Game1.activeClickableMenu = null;
            }
            else if (obj.Name == "Warp Totem: Farm")
            {
                DecrementStack(obj);

                // From: Object::totemWarpForReal
                if (!Game1.getFarm().TryGetMapPropertyAs("WarpTotemEntry", out Point warp_location, false))
                {
                    switch (Game1.whichFarm)
                    {
                        case 6:
                            warp_location = new Point(82, 29);
                            break;
                        case 5:
                            warp_location = new Point(48, 39);
                            break;
                        default:
                            warp_location = new Point(48, 7);
                            break;
                    }
                }
                Game1.warpFarmer("Farm", warp_location.X, warp_location.Y, false);
            }
            else if (obj.Name == "Warp Totem: Mountains")
            {
                DecrementStack(obj);

                // From: Object::totemWarpForReal
                Game1.warpFarmer("Mountain", 31, 20, false);
            }
            else if (obj.Name == "Warp Totem: Beach")
            {
                DecrementStack(obj);

                // From: Object::totemWarpForReal
                Game1.warpFarmer("Beach", 20, 4, false);
            }
            else if (obj.Name == "Warp Totem: Desert")
            {
                DecrementStack(obj);

                // From: Object::totemWarpForReal
                Game1.warpFarmer("Desert", 35, 43, false);
            }
            else if (obj.Name == "Warp Totem: Island")
            {
                DecrementStack(obj);

                // From: Object::totemWarpForReal
                Game1.warpFarmer("IslandSouth", 11, 11, false);
            }
            else if (obj.Name == "Horse Flute")
            {
                Game1.activeClickableMenu = null;
                obj.performUseAction(Game1.currentLocation);
                shouldMountHorse = true;
                shouldMountHorseLocation = Game1.currentLocation;
            }
        }

        private void EatObject(StardewValley.Object obj)
        {
            usedHotkeyToEat = true;
            facingDirectionBeforeEating = Game1.player.FacingDirection;

            DecrementStack(obj);
            Game1.player.eatObject(obj);
            Game1.activeClickableMenu = null;
        }

        private void DecrementStack(StardewValley.Object obj)
        {
            if (obj.Stack > 1)
            {
                obj.Stack--;
            }
            else
            {
                Game1.player.Items.Remove(obj);
            }
        }

        private void EmulatePause()
        {
            if (Game1.activeClickableMenu is null)
            {
                Game1.activeClickableMenu = new GameMenu();
            }
        }

        private void Log(string msg)
        {
            this.Monitor.Log(msg, LogLevel.Debug);
        }
    }
}
