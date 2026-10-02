# Card Detail Tooltip - Card Survival Fantasy Forest

This mod shows card status and progress related details in tooltip.

[Preview](pic/screenshot1.png)

# Usage

## Install BepInEx

This mod works as a plug-in of BepInEx 5 framework.

See <https://docs.bepinex.dev/v5.4.21/articles/user_guide/installation/index.html>

## Mod Setup

- Download latest release from <https://github.com/computerfan/CSFFCardDetailTooltip/releases>. 

- Extract `CSFFCardDetailTooltip.dll` to `BepInEx/plugins folder`.

## Compatibility

Version 1.0.11 targets the Windows open beta `EA_beta_0.68b`.
Compatibility with older stable versions and Android has not been verified.

## Tooltip notes

- Cooking rates are averages; completion estimates use the current rate and can
  change when processing pauses or conditions change.
- Drop percentages describe possible outcome groups. Displayed quantities are
  examples, and trap timers do not predict when an animal will arrive.
- Wound-fatality estimates cover immediate wound blood loss after a hit, excluding
  later poison and other secondary effects.
- NPC work tooltips show the selected work step and its remaining ticks, not a
  whole expedition's completion time. Blocked-duty reasons reflect the game's
  last duty check. Outcome quantities are samples and may change before completion.
- NPC stat changes include the target's scaling, before repetition penalties and
  stat limits. Ordinary player-action buttons continue to use player calculations.

## Credits

Thanks to [Shosetsu](https://github.com/Shosetsu/CSFFCardDetailTooltip) for fork
updates, and [SamaraFleurety](https://github.com/SamaraFleurety/CSFFCardDetailTooltip)
for card-aware durability visibility, counter-modifier access and null-check fixes.

## Settings (Optional)

The configuration file can be found at `/BepInEx/config/CSFFCardDetailTooltip.cfg`

| Name                          | Default      | Description                                                                      |
| ----------------------------- | ------------ | -------------------------------------------------------------------------------- |
| Enabled                       | true         | If set to true, will show the details tooltips                                   |
| HotKey                        | F2           | The key to enable and disable the tool tips                                      |
| RecipesShowTargetDuration     | false        | If true, cookers like traps will show exact cooking duration instead of a range. |
| HideImpossibleDropSet         | true         | If true, impossible drop sets will be hidden.                                    |
| TooltipNextPageHotKey         | RightBracket | The key to show next page of the tool tip.                                       |
| TooltipPreviousPageHotKey     | LeftBracket  | The key to show previous page of the tool tip.                                   |
| AdditionalEncounterLogMessage | false        | If true, shows additional tips in the message log of combat encounter.           |
| ForceInspectStatInfos         | false        | If true, stats like Bacteria Fever are forced to be inspectable.                 |

__Toggle Note__: When using the hotkey to enable/disable the detailed tooltips, the tooltips will not be updated until the user moves the mouse off of a card.

# Change Log

## Unreleased

- Show NPC workers, selected duties, step countdowns, destinations and recorded
  duty blockers on NPC and working-card tooltips.
- Preview selected NPC work using the worker's action modifiers and drop weights.
- Show duty activation changes and target-scaled NPC relationship/stat changes.

- Expand encounter previews with conditional stat changes, vulnerability,
  wrestling and temporary effects, including duration refreshes.
- Use the hovered action for encounter distance and incoming escape bonuses;
  label native success estimates, respect guaranteed success and remove
  misleading harmless-attack hints.
- Group per-action-tick stat effects under one heading, preserving individual
  stat names and available modifier sources.
- Hide empty tooltip lines and empty stat-effect sections.
- Ignore disabled or missing local-counter modifiers in durability breakdowns.
- Handle missing localization dictionaries and stat-action models safely.

## 1.0.11

- Update compatibility with open beta `EA_beta_0.68b`, including recipe lookup,
  blueprint weight, drop probabilities and durability visibility.
- Show individual weapon moves with condition and skill modifiers. Mark feints as
  non-damaging and clarify which bonuses require ammunition or an encounter.
- Include armour quality, hardness and liquid coatings in combat previews, and
  account for separate damage rolls and target-specific bonuses.
- Show interpolated action effects, temporary stat changes and NPC stat effects.
  Include both cards' time modifiers and respect minimum and zero-cost actions.
- Restore detailed tooltips for alternate actions and actions with no card drops.
- Correct recipe averages, paused processing and nested liquid containers so
  ingredient effects are not applied to the container itself.
- Use the correct ingredient and cooker for recipe drops, and show liquid output
  from the corresponding outcome in actions with multiple results.
- Include durability scaling in passive-effect breakdowns and correctly compound
  stacked multipliers, including progress-rate multipliers.
- Display modified stat limits and effective durability maxima; prepare status
  tooltips before they appear.
- Preserve game randomness, action/drop caches and travel state during previews.
- Prevent stale local game DLLs from overriding the configured build references.

## 1.0.0
- Update for CSFF

## 1.0.1
- Fishing should now provide the correct chance report.

## 1.0.2

- Added support for CSFF's overhauled trap system.

## 1.0.3 ~ 1.0.5

- Improved Stat handling logic, significantly boosting performance (especially in late game).

## 1.0.6

- Fixed incorrect type definition in Encounter.cs.

## 1.0.7

- Fixed missing SpoilageRateModifier handling in liquid container details.

## 1.0.8 ~ 1.0.8.1

- Removed handling for unsupported `WeatherCardInspectable`.
- Disabled Weight display in `EquipmentButton` tooltip when [WikiMod](https://csff-db.uuppi.com/_WikiMod) v1.0.6 or higher is loaded; full encumbrance info is available there.

## 1.0.9

- Refined the display logic for the Time Cost Modifiers to improve clarity and readability.
- Removed the novelty countdown timer from permanently decaying stats, as these modifier do not actually expire—eliminating potential user confusion.

## 1.0.10

- Added support for displaying Multiply Modifiers.

# Change Log (CSTI)

## 1.0.2
- Now it shows detailed weight of items and stats of character.

## 1.0.3 
- Adds enable/disable hotkey.
- Shows action staleness on character stats.
- Adds up rate modification from cooking recipe.

## 1.0.4
- Shows probabilities for results of actions in events.
- Adds tooltips to placed containers like chests (issue #2).

## 1.0.5
- Shows trap duration and drop probability.
- Adds up stacking passive effects of multiple cards.
- Add Simplified Chinese localization

## 1.0.6
- Shows liquid spoilage.
- No longer shows stat modifiers from unknown sources.

## 1.1.0
- Shows StatModifier and CardStateChange for DismantleCardActions (e.g. changes in satiation when eating food).
- Shows card drop, transform or destroy by DismantleCardActions.
- Minor bug fix.

## 1.1.1
- Long tootip texts can now be shown in multiple pages, use [ and ] keys to turn pages.
- Shows changes made by CardOnCardAction (e.g. results of fishing with a fishing rod).
- Shows possible results of scene exploration.
- New config entry HideImpossibleDropSet. Set to false to show all card drops even with a chance of %0.

## 1.1.2
- Fix hold text and bar not displaying when have a card interaction.
- Shows state changes when build or deconstruct blueprints.
- Should work safely with game version 1.04.

## 1.1.3
- Resolved tooltip title overlap issue.
- New config entry "WeatherCardInspectable". Enabled by default to inspect weather card.
- Shows OnFull/Zero action details when card stats change.

## 1.1.4
- Show tooltip in the combat encounter (by @DreamPrism).
- New config entry ForceInspectStatInfos (by @DreamPrism).
- New config entry AdditionalEncounterLogMessage.
- Fix ignored possible same weather.
- Shows actions of liquid containers.
- Minor bug fix.
