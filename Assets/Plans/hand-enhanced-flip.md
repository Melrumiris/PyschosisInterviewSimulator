# Project Overview
- Game Title: Homework2 (XR Project)
- High-Level Concept: Hand models with VR and Keyboard control.
- Target Platform: StandaloneWindows64 (XR)

# Game Mechanics
## Core Gameplay Loop
- Hand interactions.
## Controls and Input Methods
- XR and Keyboard.
- Enhanced Palm Flip: Smooth transition, hide controller, disable interaction.

# Key Asset & Context
- `Assets/HandmadeHand/HandController.cs`: Main script to be updated.
- `Assets/HandmadeHand/Curler.controller`: Animator controller to fix.
- `Assets/HandmadeHand/ProceduralIndexCurl.anim`: The animation clip to ensure is correctly generated and assigned.

# Implementation Steps

## 1. Fix Animator Controller
- **Description**: Assign `ProceduralIndexCurl.anim` to the `Index Layer` Blend Tree at threshold 1. Currently, it is set to `None`.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## 2. Upgrade HandController.cs
- **Description**: Implement enhanced Palm Flip:
  - Add `targetFlipPosition` and `targetFlipRotation` (Euler) variables.
  - Add `controllerVisual` and `interactors` list.
  - Implement smooth interpolation for position and rotation using a timer/coroutine or `Update`.
  - Disable `controllerVisual` and `interactors` during the flipped state.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: No

## 3. Scene Configuration
- **Description**: Set the new variables on the `AdvancedHandController` components in `Assets/Scenes/BasicScene.unity`.
  - Link the `Left Controller Visual` and `Right Controller Visual`.
  - Link the interactors (Poke, Near-Far, Teleport).
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

# Verification & Testing
- **Index Finger**: Verify it curls fully (all joints) when pressing '2' or '7'.
- **Palm Flip**: Verify smooth transition to the target position/rotation when pressing 'P'.
- **Interaction/Visuals**: Verify controller mesh disappears and interaction stops working when flipped.
