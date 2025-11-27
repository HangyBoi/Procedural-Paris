# Procedural Generation of a Reimagined Parisian Cityscape

> **A robust, two-layer procedural system for generating organic 18th-century city blocks in Unity.**

| **Role** | **Engine** | **Key Tech** |
| :--- | :--- | :--- |
| Tooling Engineer & Technical Artist | Unity 6 (C#) | Voronoi Tessellation, Sutherland-Hodgman Clipping, Procedural Mesh Generation, Shader Graph Triplanar Shader, Particle System, Optimization |

---

## Technical Documentation

This project is documented in detail in the **Wiki**. Please select a topic below for a deep dive:

| [**🏗️ Part 1: The City Planner**](https://github.com/HangyBoi/Procedural-Paris/wiki/High%E2%80%90Level-City-Generation-(The-City-Planner)) | [**🏠 Part 2: The Architect**](https://github.com/HangyBoi/Procedural-Paris/wiki/Procedural-Architecture-(The-Architect)) | [**🎨 Part 3: Tech Art & Optimization**](https://github.com/HangyBoi/Procedural-Paris/wiki/Tech-Art-&-Optimization) |
| :--- | :--- | :--- |
| Voronoi Layouts & Polygon Clipping | Procedural Mesh Generation & Roof Logic | Shaders, Batching & Performance |

---

## ☆ The Workflow ☆ 

My goal was to move away from "Manhattan-style" grid generation and capture the chaotic, organic nature of European urban planning. The system works in four stages:

### 1. The Building Blocks
The system relies on a modular kit of low-poly assets (Facade segments, Windows, Cornices). These are "smart" assets that the generator stretches and fits to arbitrary wall lengths.

<img width="2403" height="969" alt="image_2025-11-27_02-55-54" src="https://github.com/user-attachments/assets/7a8ed707-1400-4f66-a10b-eddaedcedde9" />

*(Modular Asset Kit created by Stefani Badzheva)*

### 2. One-Click City Generation
The `CitySectorGenerator` handles the macro-layout. It scatters seed points, generates a Voronoi diagram, clips the cells to the sector bounds, and creates the street network automatically.

![One-Click City Generation - Voronoi Debug to Full City](https://github.com/user-attachments/assets/aabd59ca-4843-47c3-9c8c-dc35e4cbc6ed)

### 3. Real-Time Manipulation
The generation is non-destructive. Using custom Editor Tools, I can grab any vertex of a building's footprint and drag it. The entire building—facades, roof, and windows—regenerates instantly to fit the new shape.

![Real-Time Manipulation - Reshaping Building Handle](https://github.com/user-attachments/assets/f5fae443-fae9-4a93-9068-5fe79b7828d5)

### 4. The Result
The final output is a performant, atmospheric city sector ready for gameplay.

![Final Result - Camera Flythrough](https://github.com/user-attachments/assets/68790b37-0bad-440e-b1e5-98d8d0f64d9b)

---

## Key Features

* **Voronoi Tessellation:** Generates organic, non-grid layouts.
* **Procedural Roofs:** Custom algorithm generates watertight Mansard roofs for any N-gon shape.
* **Triplanar Shaders:** Solves UV mapping issues on procedurally generated geometry.
* **Editor Tooling:** Fully interactive custom inspectors with scene-view handles.
* **Optimization:** Integrated Static Batching, GPU Instancing, and Shadow Caster optimization for real-time performance.

---

## Credits

* **3D Environment Art:** Stefani Badzheva
* **Engineering & Tech Art:** Nichita Cebotari
