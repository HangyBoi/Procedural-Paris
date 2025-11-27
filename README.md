# Procedural Generation of a Reimagined Parisian Cityscape

> **A robust, two-layer procedural system for generating organic 18th-century city blocks in Unity.**

| **Role** | **Engine** | **Key Tech** |
| :--- | :--- | :--- |
| Tooling Engineer & Technical Artist | Unity 2022 (C#) | Voronoi Tessellation, Sutherland-Hodgman Clipping, Procedural Mesh Generation, HLSL Triplanar Shaders |

---

## 📖 Technical Documentation

This project is documented in detail in the **Wiki**. Please select a topic below for a deep dive:

| [**🏗️ Part 1: The City Planner**](#) | [**🏠 Part 2: The Architect**](#) | [**🎨 Part 3: Tech Art & Optimization**](#) |
| :--- | :--- | :--- |
| Voronoi Layouts & Polygon Clipping | Procedural Mesh Generation & Roof Logic | Shaders, Batching & Performance |

---

## 🚀 The Workflow

My goal was to move away from "Manhattan-style" grid generation and capture the chaotic, organic nature of European urban planning. The system works in four stages:

### 1. The Building Blocks
The system relies on a modular kit of low-poly assets (Facade segments, Windows, Cornices). These are "smart" assets that the generator stretches and fits to arbitrary wall lengths.
*(Modular Asset Kit created by Stefani Badzheva)*

### 2. One-Click City Generation
The `CitySectorGenerator` handles the macro-layout. It scatters seed points, generates a Voronoi diagram, clips the cells to the sector bounds, and creates the street network automatically.

![One-Click City Generation - Voronoi Debug to Full City](INSERT_GIF_LINK_HERE)

### 3. Real-Time Manipulation
The generation is non-destructive. Using custom Editor Tools, I can grab any vertex of a building's footprint and drag it. The entire building—facades, roof, and windows—regenerates instantly to fit the new shape.

![Real-Time Manipulation - Reshaping Building Handle](INSERT_GIF_LINK_HERE)

### 4. The Result
The final output is a performant, atmospheric city sector ready for gameplay. The system handles "Tech Art" tasks automatically, such as assigning Triplanar shaders to roofs to prevent texture stretching on irregular shapes.

![Final Result - Camera Flythrough](INSERT_GIF_LINK_HERE)

---

## 🔧 Key Features

* **Voronoi Tessellation:** Generates organic, non-grid layouts.
* **Procedural Roofs:** Custom algorithm generates watertight Mansard roofs for any N-gon shape.
* **Triplanar Shaders:** Solves UV mapping issues on procedurally generated geometry.
* **Editor Tooling:** Fully interactive custom inspectors with scene-view handles.
* **Optimization:** Integrated Static Batching, GPU Instancing, and Shadow Caster optimization for real-time performance.

---

## 🤝 Credits

* **3D Environment Art:** Stefani Badzheva
* **Engineering & Tech Art:** Nichita Cebotari
