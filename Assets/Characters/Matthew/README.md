# Matthew – Unity character package

Seated NPC for a VR scene: rigged, weight painted, two looping animations, and a jaw for talking.

```
Matthew/
├── Matthew.fbx            ← import this (mesh + skeleton + 2 animation clips)
├── Textures/
│   ├── T_Matthew_Albedo.png   (2048², sRGB, opaque)
│   └── T_Matthew_Normal.png   (2048², tangent-space normal map)
└── Source~/Matthew.blend  ← editable Blender source (Unity ignores folders ending in "~")
```

## Contents

| | |
|---|---|
| Mesh | 9,110 verts / 15,835 tris, 1 material (`M_Matthew`), 1 UV set |
| Skeleton | 55 bones, Unity Humanoid naming, root bone `Hips` |
| Skin weights | max 4 bones per vertex, normalized, no unweighted vertices |
| Scale / units | metres, scale 1, height 1.86 m standing, faces +Z in Unity |
| Clips | `Sit_Idle`, `Sit_Talk` – 30 fps, 241 frames (8 s), seamless loops |

**Sit_Idle**: seated, hands resting on the thighs, breathing, small head movements, mouth closed.
**Sit_Talk**: same seat, with head nods and turns, torso engagement, right-hand (and smaller left-hand) gestures, and speech-like jaw motion in three phrases. Both clips start and end on the same pose, so they cross-fade cleanly.

## Import steps

1. Copy the whole `Matthew` folder into `Assets/`.
2. Select `Matthew.fbx` → **Model** tab: Scale Factor 1, *Convert Units* on, Normals *Import*, Tangents *Calculate Mikktspace*.
3. **Rig** tab → Animation Type **Humanoid**, Avatar Definition *Create From This Model* → **Apply**.
   - Click **Configure…** → *Pose → Enforce T-Pose* (the model was built in an A-pose) → Apply/Done.
   - Check that **Jaw**, **Left/Right Eye** and **UpperChest** are filled in (Head → optional slots). If auto-mapping missed anything, use the table below.
4. **Animation** tab, for **both** clips:
   - **Loop Time** ✔ (and *Loop Pose* ✔)
   - Root Transform Rotation: **Bake Into Pose** ✔, Based Upon *Original*
   - Root Transform Position (Y): **Bake Into Pose** ✔, Based Upon *Original*
   - Root Transform Position (XZ): **Bake Into Pose** ✔
   → Apply. This keeps him exactly where you place the GameObject.
5. **Materials** tab → *Extract Materials…*, or create your own material:
   - Base Map / Albedo = `T_Matthew_Albedo`
   - Normal Map = `T_Matthew_Normal` (set the texture's *Texture Type* to **Normal map**)
   - Metallic 0, Smoothness ≈ 0.25 (Blender roughness was 0.75), Opaque.
6. Add an **Animator Controller** with `Sit_Idle` as default and `Sit_Talk` as a second state (e.g. a `Talking` bool, transition duration ~0.3 s both ways).

## Placing him on a chair

The GameObject origin is on the floor, directly below his upper thighs (between the back of the seat and his knees). In Unity (he faces +Z):

| | |
|---|---|
| Seat height (underside of thighs/butt) | **0.43 m** |
| Butt reaches back to | **Z = −0.20 m** |
| Thigh contact on the seat spans | Z ≈ −0.07 … +0.23 m |
| Lower back (for a backrest) | Z ≈ −0.23 m, height 0.56–0.93 m |
| Knees, front | Z ≈ +0.39 m |
| Feet on floor | Z ≈ +0.31 … +0.60 m, ±0.28 m wide |
| Hip width | ±0.21 m |

So a chair with a 0.43 m seat whose front edge sits about 0.25 m in front of his origin, and whose backrest is about 0.23 m behind it, fits him.

## Talking / lip sync

The clip already contains canned jaw motion. To sync to real dialogue audio, override the jaw in `LateUpdate`, for example:

```csharp
// Attach to Matthew. Rotates the jaw open with voice loudness.
public class SimpleJaw : MonoBehaviour {
    public AudioSource voice;
    public float maxOpenDegrees = 6.5f, gain = 40f;
    Transform jaw; float[] buf = new float[256]; float open;
    void Start() { jaw = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Jaw); }
    void LateUpdate() {
        if (!voice || !voice.isPlaying || !jaw) return;
        voice.GetOutputData(buf, 0);
        float rms = 0; foreach (var s in buf) rms += s * s; rms = Mathf.Sqrt(rms / buf.Length);
        open = Mathf.Lerp(open, Mathf.Clamp01(rms * gain), 0.4f);
        jaw.localRotation *= Quaternion.AngleAxis(open * maxOpenDegrees, Vector3.right);
    }
}
```
If the mouth closes instead of opening, flip the sign of `maxOpenDegrees`. Unity's Humanoid jaw range defaults to ±10°, so keep the opening below that. The mouth interior is a dark pocket; there are no teeth or tongue.

## Bone mapping (if Humanoid auto-mapping needs help)

| Unity slot | Bone | Unity slot | Bone |
|---|---|---|---|
| Hips | Hips | Left/Right Shoulder | Shoulder.L / .R |
| Spine | Spine | Left/Right Upper Arm | UpperArm.L / .R |
| Chest | Chest | Left/Right Lower Arm | LowerArm.L / .R |
| Upper Chest | UpperChest | Left/Right Hand | Hand.L / .R |
| Neck | Neck | Left/Right Upper Leg | UpperLeg.L / .R |
| Head | Head | Left/Right Lower Leg | LowerLeg.L / .R |
| Jaw | Jaw | Left/Right Foot | Foot.L / .R |
| Left/Right Eye | Eye.L / .R | Left/Right Toes | Toes.L / .R |

Fingers: `ThumbProximal/Intermediate/Distal`, `IndexProximal/…`, `MiddleProximal/…`, `RingProximal/…`, `LittleProximal/…`, each with a `.L` / `.R` suffix. `.L` is the character's left.

## Verification done before packaging

- The FBX was re-imported and compared against the Blender original. Bind pose matches to within 0.001 mm. Animated frames of both clips match to within 1.3 mm; the difference comes from FBX keyframe compression.
- Bone hierarchy, 55/55 bones and vertex groups, and both clips are present.
- Vertex colours are excluded on purpose: the source had a leftover mask attribute that some shaders would multiply in as black.
