# 🩺 HybridMedRender — Hybrid Medical Image Visualization (Surface + Volume Rendering)

**HybridMedRender** is a GPU-based rendering solution that reproduces and extends the method described in  
**_“Hybrid visualisation of medical image: Surface and volume rendering”_**  
(*Palma, B., Casanova-Salas, P., Gimeno, J., & Casas-Yrurzum, S., Computers & Graphics, 133, 104453, 2025*).  
👉 [DOI: 10.1016/j.cag.2025.104453](https://doi.org/10.1016/j.cag.2025.104453)

It also supports the **[VRDF SDK (Volume Rendering Data Format)](https://github.com/guillaume-schneider/vrdf-sdk)** —  
a lightweight, open-spec binary format designed for volumetric datasets (CT, MRI, segmentation) with metadata and transfer-function definitions.  
The VRDF SDK provides a **cross-platform toolkit** (Python encoder, Unity/C# runtime) that simplifies loading, sharing, and visualizing medical volumetric data efficiently.

This project implements the paper’s **hybrid visualization technique**, combining **Order-Independent Transparency (OIT)** with **Direct Volume Rendering (DVR)** to achieve physically correct blending of segmented 3D anatomical meshes and volumetric medical data.

This hybrid approach preserves the structural clarity of surface models while maintaining the depth and density information from the original medical scans. It supports **interactive exploration**, **region of interest (ROI) tools**, and **VR visualization** for immersive surgical planning and anatomical understanding.

---

## 🧠 Key Features

- **Hybrid Rendering (OIT + DVR)**  
  Real-time fusion of surface-based 3D models and volumetric medical data.

- **Per-Pixel Linked Lists (Order-Independent Transparency)**  
  Ensures precise transparency sorting and accurate depth compositing.

- **Direct Volume Rendering (DVR)**  
  Raymarching through 3D textures with density windowing and transfer functions.

- **VRDF Integration**  
  Native support for the [VRDF SDK](https://github.com/guillaume-schneider/vrdf-sdk) — efficient I/O, metadata parsing, and transfer-function loading.

- **Interactive Visualization Tools**  
  ROI selection, brightness/contrast/hue adjustments, and color mapping.

- **VR Support**  
  Full immersive inspection, scale control, and real-time interaction via XR controllers.

---

## 🧩 Repository Structure

```text
HybridMedRender/
├─ README.md
├─ LICENSE
├─ docs/
│  ├─ overview.md
│  ├─ pipeline.png
│  ├─ algo_OIT_DVR.md
│  └─ vr_interaction.md
├─ unity-project/
│  ├─ Assets/
│  │  ├─ Scripts/
│  │  │  ├─ Rendering/
│  │  │  │  ├─ OIT/
│  │  │  │  ├─ DVR/
│  │  │  │  └─ Hybrid/
│  │  │  ├─ Utils/
│  │  ├─ Shaders/
│  │  ├─ Resources/
│  │  ├─ Scenes/
│  │  └─ XR/
├─ data/
│  ├─ README_DATA.md
├─ scripts/
│  ├─ convert_dicom_to_3dtex.py
│  ├─ segment_to_mesh_slicer.md
│  └─ make_transfer_function.ipynb
└─ .gitignore
```

---

## ⚙️ Technical Overview

1. **OIT Geometry Pass**  
   Each anatomical mesh writes its fragments into GPU buffers instead of the framebuffer:  
   - `HeadPointerBuffer` → stores the start of a per-pixel linked list  
   - `NodeBuffer` → stores color, depth, world position, and next pointer  

2. **Per-Pixel Sorting**  
   Linked lists are sorted by fragment depth (back-to-front), allowing order-independent transparency.

3. **Hybrid Compositing (OIT + DVR)**  
   A global volume raymarch samples voxel densities while comparing each sample’s depth with the sorted mesh fragments.  
   Both are blended using the same alpha accumulation formula in a shared clip-space coordinate system.

4. **VRDF Support**  
   VRDF (.vrdf) files are directly parsed to load voxel grids, segmentation masks, metadata, and transfer-functions for rendering.  
   This reduces preprocessing overhead and ensures reproducibility across platforms.

---

## 🧰 Requirements

- **Unity 2022.3 LTS** or newer  
- **Direct3D 11 / URP or Built-in Render Pipeline**  
- **VRDF SDK** (included as dependency or local import)  

---

## 🚀 Quick Start

1. Clone this repository:
   ```bash
   git clone https://github.com/<your-username>/HybridMedRender.git
   ```
2. Install the [VRDF SDK](https://github.com/guillaume-schneider/vrdf-sdk)  
   or add it as a Unity package dependency.

3. Open `unity-project/` in Unity.
4. Load the `DemoDesktop` scene to run the hybrid renderer.
5. (Optional) Enable VR mode and open `DemoVR` for immersive exploration.
6. Import your `.vrdf` files or DICOM/NiFTi/RAW data into `Assets/AssetsStream/`.

---

## 📚 Reference

> Palma, B., Casanova-Salas, P., Gimeno, J., & Casas-Yrurzum, S. (2025).  
> **Hybrid visualisation of medical image: Surface and volume rendering.**  
> *Computers & Graphics, 133*, 104453.  
> [https://doi.org/10.1016/j.cag.2025.104453](https://doi.org/10.1016/j.cag.2025.104453)

---

## ⚖️ License

This project is released under the **MIT License**.  
You are free to use, modify, and distribute the code for research and educational purposes.  
**No medical or clinical use is authorized.**

---

## ⚠️ Disclaimer

HybridMedRender is a **research prototype** intended for educational and experimental visualization only.  
It is **not a certified medical device** and should **not be used for diagnostic or surgical purposes**.
