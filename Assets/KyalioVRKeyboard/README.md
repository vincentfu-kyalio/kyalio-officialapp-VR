# Kyalio VR Keyboard

An in-world keyboard for `TMP_InputField`s on a world-space canvas in VR.

- **No system keyboard:** typing stays inside the app, so it works with composition-layer canvases (e.g. `OVROverlayCanvas` underlay), and the Quest keyboard's out-of-sync preview box is gone.
- **The field keeps its caret:** it blinks, and pointing into the text moves it.
- **Layouts:**
  - **Letters:** QWERTY, with Shift; double-tap Shift for caps lock.
  - **Symbols pages:** numbers and common symbols (`123`), and the remaining ASCII symbols (`#+=`), so any password can be typed.
  - **Email:** adds `@` and `.com` keys to the letters page, and has no space key.
  - **Number pad:** for codes.
- **Built from one key template:** restyle the template and every key follows it.

Depends only on Unity UI and TextMeshPro (`Kyalio.VRKeyboard.asmdef`). To use it in another project, copy the `KyalioVRKeyboard` folder.

## Setup

1. Select the world-space Canvas (or an object under it that **stays active**) and choose **GameObject → Kyalio → VR Keyboard**. This creates the following:

   ```
   VR Keyboard        VirtualKeyboard — the controller, stays here
   └ Panel            the visible keyboard (background Image, its size = keyboard size)
     └ Keys           key area, inset for padding
       └ Key Template KeyboardKey: background Image + Label (TMP) + Icon (inactive)
   Keyboard Dock      where the keyboard appears (its centre) — move it in the Scene view
   ```

2. Style **Panel** and **Key Template** however you like: colours, sprites, the label's font. You can also set the Del / Shift / Done icons on `VirtualKeyboard`.
3. Register the fields from code:

   ```csharp
   using Kyalio.VRKeyboard;

   [SerializeField] VirtualKeyboard keyboard;

   void Awake()
   {
       keyboard.Register(emailInput,    KeyboardLayoutKind.Email);
       keyboard.Register(passwordInput, KeyboardLayoutKind.Text);
       keyboard.Register(codeInput,     KeyboardLayoutKind.Numeric, codeDock); // optional own dock
   }
   ```

`Register` returns `false` and leaves the field untouched (keeping its platform keyboard) when the keyboard isn't set up correctly: Panel, Key Area or Key Template missing, or not nested as above.

The keyboard shows while a registered field is focused, and hides when focus leaves it or when **Done** is pressed. Done only closes the keyboard; it never submits. Subscribe to `DonePressed` if you want it to submit, and to `KeyPressed` for click sounds or haptics.

## How it works (and what to keep in mind)

- **Focus is never lost.**
  - Input modules deselect the current selection when the pressed object has a different select handler.
  - Keys are not `Selectable`. While shown, the panel is parented under the focused field, so the event system finds the field (already selected) and keeps it.
  - Do not add a `Button` or other `Selectable` to the key template.
- **Its own Canvas.** The panel has a nested `Canvas` with **Override Sorting** (`sortingOrder`). That stops the page drawing over it and parent `RectMask2D`s clipping it, and lets it hide cheaply.
- **Typing goes through the field.** It uses `TMP_InputField.ProcessEvent`, so content type, character validation, character limit and replacing a selection all behave as with a hardware keyboard.
- **Hidden presses and drags.** Presses on the panel background, and drags across keys, are swallowed (`KeyboardSurface`, `KeyboardKey`). This stops them reaching the field and moving the caret or selecting text.
- **Docks:** a field's own dock wins, then the keyboard's default dock. With neither, the keyboard appears just below the field. A dock sets position, rotation and scale only; the keyboard's size is the **Panel**'s size (the dock's rectangle is just a guide in the Scene view).
- **Raycasting:** the panel's nested Canvas gets a plain `GraphicRaycaster`, which works with Meta Interaction SDK's `PointableCanvasModule`. With an input setup that needs a different raycaster (e.g. XRI's `TrackedDeviceGraphicRaycaster`), add that one to the Panel yourself; an existing raycaster is kept.
- **If a field is destroyed while focused**, the panel goes with it; the keyboard rebuilds the panel from a spare copy the next time it's needed.
- **The controller must stay active.** The `VirtualKeyboard` object polls focus every frame, and only the panel moves. Do not put the controller under a page that gets deactivated.
- **Soft keyboard off.** `Register` sets `shouldHideSoftKeyboard` and `shouldHideMobileInput` on the field.
