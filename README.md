# shader.compute.upscaling

# Introduction

Compute shader for creating small perlin landscapes and upscaling them from a low power of 2, to max 4096.

Perlin landscapes are a (Perlin-)weighted admixture of a higher-frequency Perlin sample, and a lower-frequency Perlin sample. This weighted mixture is projected through a radius mask (to enclose landmass within a circle) and a Perlin mask (to introduce curvature).

But here we should also showcase a texture upsampling algorithm. It projects a sample from 
64 x 64, to 
128 x 128, to 
256 x 256... etc. to 
4096 x 4096. With an image created on the fly, almost instantly. This upsampling algorithm does require a GPU.

Open one of the links to image_1, image_2, or image_3. This will open a github webpage which has an image embedded in it. Right-click the image, select "Open image in new tab," zoom in, and see the image's full detail.

# Implementation details

Given an N x N image, creates a (2N, 2N) image. For the (2nth, 2mth) pixel, samples from 
{(n - 1, n + 0, n + 1)} and 
{(m - 1, m + 0, m + 1)} to determine how to fill in a pixel in the (2N, 2N) image.

# How to use

Option 1: Download shader.compute.upscaling.unitypackage and run the unitypackage with Unity.
Option 2: Create a new Unity project, overwrite the Assets and ProjectSettings files into your new Unity project, and run the Sample Scene.
Option 3: Download the Assets/CSPropagate.compute and Assets/ScriptComputeManager files into your Assets folder, create a Plane object in Unity, attach the ScriptComputeManager to your Plane, fill a few colors into map_colors, and point the "cs" variable to CSPropagate.compute. Run the scene.
