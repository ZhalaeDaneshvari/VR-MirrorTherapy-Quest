# Seated body mirroring test — current 0.7.1

This update adds in-headset testing controls to the accepted body-driven avatar and includes the pending 0.6.1 tabletop display correction. The core reflection and source-selection math already existed; this checkpoint makes it directly selectable for wearer verification. This is not yet a validated experiment or clinical application.

## Controls

Open **Quest Body Driven Avatar**, then open **Settings**. Look at a button and briefly pinch thumb and index, then release before the next selection. Inputs always come from physical XR hand tracking, never the remapped hand.

1. **REALIGN (3 sec)**: look straight ahead during the countdown. Start seated and comfortable; table settings remain saved.
2. **Normal**: physical left drives virtual left; physical right drives virtual right.
3. **Left -> Right**: physical left drives virtual right hand/fingers and right arm targets.
4. **Right -> Left**: physical right drives virtual left hand/fingers and left arm targets.
5. **Swap both sides**: physical right drives virtual left; physical left drives virtual right, independently and simultaneously. Both outputs are enabled while their source tracking is valid.
6. **Mirror view: both hands / opposite hand only**: toggles whether the source-side virtual hand/arm is also shown in a mirrored condition. Normal always shows both independently tracked hands. Swap both sides also keeps both outputs enabled; this visibility toggle only applies to the one-way modes.
7. **Close** hides the controls and status text, leaving only **Settings**. Hidden controls cannot be clicked. Opening/closing requires a fresh pinch; closing during a realignment countdown cancels that countdown. Settings starts closed on launch.

The current condition is printed above the buttons; its button remains highlighted. Condition and visibility changes persist to the existing settings file and produce monotonic-time diagnostic log messages. They are not trial events in a scientific dataset yet. No affected side is assumed.

## What to check on Quest

- In Normal, move each real hand separately and open/close its fingers.
- Select Left -> Right and opposite-hand-only. Move your real left hand gently: only the virtual right hand/arm should follow. Moving the real right hand must not drive that displayed right hand.
- Repeat in Right -> Left with the real right hand as the source.
- Enable both hands in each mirrored mode: one physical source should drive both displayed sides.
- Return to Normal: both sides should immediately return to independent tracking.
- Briefly occlude the source hand: unavailable output must hide rather than stay frozen. Do not expect runtime-estimated joints to disappear merely because a finger is partly occluded.
- Check contact correction separately: rest hands on the table, lift them, and move them genuinely below/around the table. Small surface overlaps should be corrected; fully below-table hands must remain below and occluded. Large discrepancies remain fit errors rather than being hidden with unlimited displacement.
- Recenter or remove/re-don the headset, then realign. Mirror output must not use an invalid body frame.

## Coordinate and data contract

Physical hand frames and body estimates remain independent of displayed targets. Position and orientation reflection use the calibrated participant frame, not world X. BodyArmTargets selects the physical side indicated by each displayed hand and reflects that side's shoulder/elbow estimates using the same frame. Avatar hand/wrist targets then receive the bounded display-only table correction; source and remapper arrays are unchanged. Per-side correction magnitudes are exposed and reported diagnostically; the final logger must record those along with the displayed result.

No clinician-hand recognition or touch detection is added. No automatic condition scheduling, trial state, participant ID collection or final scientific logging is implied by these testing controls.

## Build/checkpoint

Unity 6000.6.2f1; same pinned packages. Configure/build through **Quest Prototype > Body Avatar**. App id stays `org.mirrortherapy.questprototype.bodyavatar` so saved table settings survive the update. Current version 0.7.1/code 11. The diagnostic and previous mirror apps remain separate.

Configuration passed the existing reflection, source visibility, hand seam and invalidation checks, plus body-frame conversion and bounded contact guards. Release build, UI render inspection and hardware acceptance status are recorded below when completed.

Verification: release build succeeded on 2026-10-08; rendered control panel inspected for fit/readability; APK installed with `adb install -r` and launch command succeeded. SHA-256 `b877572caf227f67989d1e26bca9f3e2903dc13128bca98a80d460bc368a8355`. No settings file was overwritten during this update. Wearer acceptance of the two mirrored directions, source visibility and new contact correction remains pending. Previous startup-warning limitations are recorded in BODY_AVATAR.md; no new scientific validation is claimed.

## 0.7.1 bilateral swap and collapsible settings

The wearer reported that 0.7.0 looked good and requested independent two-way swapping plus a clear view with only a Settings button. SwapBoth is appended as enum value 3 so existing saved Normal/LeftToRight/RightToLeft values retain their meanings. The researcher CLI accepts `--condition swap-both`.

Added automated checks for all-joint bilateral positions/orientations, physical source identity, both visibility-flag values, independent invalid-joint propagation, and invalid alignment. The expanded/closed panel preview asserts that only the Settings text remains active when collapsed. These are software checks; new headset interaction and bilateral-motion acceptance are pending.

Headset check: open Settings, realign, select Swap both sides and close Settings. Raise only the real right hand, then only the real left; each should drive only its opposite virtual side. Move both differently at the same time. Reopen Settings and return to Normal. Check that the panel remains hidden when closed and that hidden mode buttons cannot react to pinches.

0.7.1 verification: Configure and release build succeeded; bilateral mapping regression checks passed. Expanded and collapsed previews inspected, including the assertion that only Settings remains visible. APK installed with settings-preserving replacement and launch command succeeded. SHA-256 `62b8aeba017da47d08d231135d51799cd7f120170b51d343d8a449d958963047`. Wearer confirmation remains pending; no new tracking accuracy or performance claim is made.
