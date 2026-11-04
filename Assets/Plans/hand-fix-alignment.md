# Project Overview
- Game Title: Homework2 (XR Project)
- High-Level Concept: Hand-modeled hands with animations controlled via VR input and keyboard shortcuts.
- Players: Single player (VR/AR)
- Inspiration / Reference Games: VR Hand Tracking demos
- Tone / Art Direction: Realistic/Stylized Hands
- Target Platform: StandaloneWindows64 (XR)
- Screen Orientation / Resolution: VR
- Render Pipeline: URP

# Game Mechanics
## Core Gameplay Loop
- Interacting with objects using hand models that reflect finger curling states.
- Hand movement tracked by controllers.
## Controls and Input Methods
- XR Interaction Toolkit (VR Input)
- Keyboard shortcuts (1-0 keys) for testing finger curls.
- Hand pose and palm orientation control.

# UI
- Minimal HUD (already implemented in simulator feedback).

# Key Asset & Context
- `Assets/HandmadeHand/HandController.cs`: Main script for hand animation.
- `Assets/HandmadeHand/Curler.controller`: Animator controller with finger layers.
- `Assets/HandmadeHand/ProceduralIndexCurl.anim`: Generated animation for the index finger.
- `Assets/HandmadeHand/MaskIndex.mask`: Avatar mask for the index layer.

# Implementation Steps

## 1. Fix Procedural Index Animation
- **Description**: The previous procedural animation had bone path errors (using "IndexIndex" instead of "IndexMiddle") and included unnecessary palm motion. I will recreate `ProceduralIndexCurl.anim` by copying curves from the working `MiddleCurl` and correctly mapping them to `IndexProximal`, `IndexMiddle`, and `IndexDistal`, while excluding `IndexPalm`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## 2. Correct Avatar Masks
- **Description**: The `MaskIndex` incorrectly has parts of other fingers (MiddleDistal, etc.) active, which causes conflicts. I will clean all finger masks to ensure they only contain their respective bones.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 3. Hand Positioning and Scale
- **Description**: Adjust the `Hand` GameObject transform relative to the controllers to better align with the "pressing buttons" pose.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

## 4. Palm Flip Scripting
- **Description**: Add a "Flip Palm" feature to `AdvancedHandController.cs` (e.g., using the 'P' key or a VR button) that rotates the hand model 180 degrees locally so the palm faces the viewer.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

# Verification & Testing
- **Manual Test**: Run the scene and use keys 1-5 and 6-0 to verify individual finger curls.
- **Visual Check**: Observe the index finger to ensure all joints (Proximal, Middle, Distal) bend without the palm moving inwards.
- **Interaction Check**: Verify the hand position looks correct when interacting with simulated controllers.
- **Feature Check**: Press the 'P' key to toggle palm orientation.
