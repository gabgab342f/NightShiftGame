# Night Shift - Demo Script (5-7 minutes)

## 1. Title screen (Member 1)
- Start from Title.unity. Point out START, SETTINGS (volume slider), and EXIT.
- Press START: scene transition to School.unity.

## 2. Core loop (Member 2)
- Grid stepping with Horizontal / Vertical (arrows or WASD); E / Space / Return interacts.
- Collision is baked from physics: closed doors block, open doors do not. Press E at a locker to hide.
- HUD: clock, objective, interaction prompt, keys, HP. Press Esc for pause; show Resume / Start over / Main Menu.
- Main Menu from pause returns to the title scene.

## 3. Objectives and audio (Member 3)
- Objective chain: desk note + whiteboard code 0417 -> faculty keypad -> lab key -> flash drive -> guard log -> spare fuse + electrical key -> power -> main lobby doors.
- Talk to the janitor in the hallway for the curse story.
- Audio is synthesized in code: title music, ambience, chase loop, and SFX for steps, pickups, doors, and the jumpscare.

## 4. Scare, win, and the harder level (Member 4)
- Open the electrical room and approach the generator by the fuse box to trigger the cursed-student jumpscare.
- Escape through the main doors: Level Complete panel -> Next level -> School2.unity.
- School2 is harder: 7-minute clock, faster and more frequent monster spawns, fewer lockers, stronger cursed student.
- Show a loss on purpose: get caught or run out of time. The end panel offers Checkpoint / Start over / Main Menu.
- Finish by winning School2: "You got out." panel -> Main Menu.

## Presentation notes
- Every member speaks in their assigned segment and hands off by naming the next topic.
- Keep Play mode running while explaining; avoid dead air.
- If scenes need rebuilding, quit Unity first, then run the headless build: NS_AUTOBUILD=1, NS_STAGE=build (no -quit).
