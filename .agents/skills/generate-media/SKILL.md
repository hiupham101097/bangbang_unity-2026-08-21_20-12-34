---
name: generate-media
description: "Generates high-quality images, graphics, audio placeholders, and GIFs using built-in agent capabilities and scripts."
argument-hint: "[type] [prompt]"
user-invocable: true
allowed-tools: GenerateImage, RunCommand, WriteToFile
---

# Media Generation Pipeline

This skill orchestrates the creation of media assets including static graphics, animations (GIFs), and audio.

## Phase 1: Static Graphics & Concept Art
When requested to create an image, graphic, or UI mockup:
1. Always use the `generate_image` tool. 
2. Before generating, refine the prompt to enforce premium quality:
   - Include keywords like "high resolution, digital art, vibrant colors, clean design, masterpiece".
   - Specify the aspect ratio if needed (e.g. 16:9 for landscapes, 1:1 for icons).
3. Save the image with a descriptive snake_case name (e.g., `magic_sword_icon`).
4. Display the generated image to the user in an artifact using the `![caption](/absolute/path)` syntax.

## Phase 2: Animations (GIFs)
Since the agent cannot directly output GIFs via a single tool, you must use a scripted approach if the user requests one:
1. Write a Python script using a library like `Pillow` (PIL) or `matplotlib.animation` to generate frames programmatically based on logic (e.g., a rotating cube, a pulsing wave).
2. Use `run_command` to execute the script and save the output as a `.gif` in the `artifacts/scratch/` directory.
3. Alternatively, generate a series of images with `generate_image` and combine them into a GIF using ImageMagick/FFmpeg via `run_command`.

## Phase 3: Audio Generation
For audio generation (sound effects, music):
1. The agent currently lacks a direct `generate_audio` tool.
2. Therefore, write a Python script using libraries like `numpy` and `scipy.io.wavfile` to synthesize procedural sound effects (e.g., sine sweeps for lasers, white noise for explosions).
3. Execute the script to output a `.wav` file into the workspace.
4. Notify the user of the generated audio file path.

---
**Protocol**: When running this skill, verify what the user wants (Image, GIF, Audio), then execute the corresponding phase and return the absolute path of the generated asset.
