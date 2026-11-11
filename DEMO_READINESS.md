# Demo readiness: version 0.13.0 (25)

## Instruction panel

After body alignment, every page uses the fixed calibrated body midline, seated head height and tabletop clearance. Selecting a button on the right must no longer shift the next page toward the current gaze direction. Realignment intentionally establishes a new reference. Before alignment the welcome/setup pages use the current head direction.

## Physical hand availability

After participant ID confirmation choose **Both hands**, **Left hand only**, or **Right hand only**. This is an explicit experimental configuration, never automatically inferred from lost tracking. Each new participant setup resets it to Both and asks again.

- Both: normal left/right practice followed by SwapBoth.
- Left only: normal left-hand practice, then left physical -> right virtual only.
- Right only: normal right-hand practice, then right physical -> left virtual only.

During one-hand mirroring the source-side virtual hand AND arm are hidden. Only the selected physical side is used for resting surface estimation, estimated segment-length fitting and readiness checks. Loss of the selected source pauses the activity; the other side cannot silently take over. The existing body estimator is still required for the selected arm. Actual one-hand/missing-limb feasibility needs headset validation; this is not a validated prosthetic or clinical calibration method.

The selected profile is recorded in `parameters.participantHands` in samples/events (0 Both, 1 LeftOnly, 2 RightOnly), with `hand_profile_selected` and the normal mapping/source metadata. Raw physical tracking remains separate and may still contain other provider-reported poses; they are not displayed when excluded by the profile.

## Headset acceptance checks

1. Both hands: confirm existing calibration, normal practice and SwapBoth still work.
2. Left only: keep the other hand out of view. Estimate table height, confirm fit, complete practice, then verify only the virtual RIGHT arm/hand is visible and driven by the real LEFT.
3. Right only: repeat with the sides reversed.
4. Hide the selected hand: verify a tracking pause, no indefinitely frozen visible hand and no switch to the other physical source. Restore tracking and deliberately Resume.
5. Confirm no activity prompt asks for the absent hand or two-handed actions.
6. Progress through right-side buttons while looking slightly right. Post-calibration panels should remain on the calibrated midline. Recalibrate facing a new direction and confirm intentional repositioning.
7. Finish and verify exported session records include the chosen profile, correct movement condition and session closure.

## Clinician tracker

The supplied product link corresponds to a VIVE Ultimate Tracker. HTC states that third-party Quest compatibility uses a Windows PC, VIVE Hub/SteamVR and a VIVE Wireless Dongle; it does not connect directly to standalone Quest. The current tracker-plus-Mac equipment is therefore insufficient for the supported setup. No tracker integration or automatic touch detection is enabled by this update.

A mounted tracker would provide a rigid device pose, not articulated fingers or skin-contact force. A future integration needs a stable tracker-to-hand mount, explicit clinician-device identity, coordinate calibration between tracker and participant, loss handling, and latency/contact validation. A PC-to-standalone-Quest pose bridge is an engineering option to investigate, not a verified feature or a reason to migrate the working pilot immediately.

Official references checked 11 November 2026:
- https://shop-us.vive.com/pages/vive-ultimate-tracker (requirements and FAQ)
- https://business.vive.com/uk/support/ultimate-tracker/category_howto/connecting-the-dongle-to-computer.html

## Validation completed

Unity 6000.6.2f1 Android build succeeded. Existing recording checks and UI render checks passed, plus new left-only/right-only opposite mapping, invalidated-source and cue tests. The hand-selection page was visually reviewed. APK: `Builds/QuestBodyDrivenAvatar.apk`. Version 0.13.0 / code 25. No Quest was connected for installation; all headset checks above remain pending.

### One-hand alignment fix (0.13.1 / code 26)

The Begin alignment action was resetting the selected hand profile to Both. Removed that redundant participant reset so alignment preserves LeftOnly/RightOnly. New-participant setup still resets the profile intentionally. Android build, recording checks, body mapping checks and guided UI checks passed. These automated checks do not establish on-headset calibration success; repeat the selected-hand acceptance checks above.

Version 0.13.1 / code 26 was installed over the existing app and launched on the connected Quest 3 on 11 November 2026; existing recordings were preserved. User confirmation of one-hand alignment remains pending.
