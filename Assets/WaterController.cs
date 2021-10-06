using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;

public class WaterController : MonoBehaviour
{

    struct WaterParticle
    {
        public Vector3 pos;
    }

    struct PressureGrid
    {
        public float[] a;
        public int[] colA;
        public float[] x;
        public float[] b;

        public int level;

        public int sizeX;
        public int sizeY;
        public int sizeZ;
        public int numCells;
    }

    struct Cell
    {
        int layer;
        public int isWater;
        public float pressure;
        public Vector3 vel;
        public Vector3 tempVel;
    }

    const int gridSizeX = 32;
    const int gridSizeY = 16;
    const int gridSizeZ = 32;
    const float cellWidth = 1.0f;
    const int gaussIntervals = 3;
    const int extrapIntervals = 3;
    const float visVal = 2.7f;
    const float airDensity = 1.0f;
    const float fluidDensity = 100.0f;
    float gravity = 9.8f;
    const bool rayCasting = false;
    const float atmoPressure = rayCasting ? -2000.0f : -1000.0f;
    bool runSim = false;
    int numCells;

    public TextMeshProUGUI airDensityText;
    public TextMeshProUGUI fluidDensityText;
    public TextMeshProUGUI viscosityText;
    public TextMeshProUGUI gravityText;
    public TextMeshProUGUI markerParticleText;

    public Material material;
    public int particleAmount = 1000;
    public ComputeShader cellOperationShader;
    public ComputeShader physicsComputeShader;
    public ComputeShader particleComputeShader;
    public GameObject particleObject;
    int randKernel; //Created (ParticleCompute), Set
    int airKernel; //Created (CellCompute), Set
    int markFluidKernel; //Created (CellCompute), Set
    int convectionKernel; //Created (PhysicsCompute), Set
    int swapVelKernel; //Created (CellCompute), Set
    int externalForcesKernel; //Created (PhysicsCompute), Set
    int viscosityKernel; //Created (PhysicsCompute), Set
    int pressureSetupKernel; //Created (CellCompute), Set
    int pressureCopyKernel; //Created (CellCompute), Set
    int pressureApplyKernel; //Created (PhysicsCompute), Set
    int extrapolateFluidKernel; //Created (PhysicsCompute), Set
    int solidVelKernel; //Created (CellCompute), Set
    int moveParticleKernel; //Created (ParticleCompute), Set
    int gaussKernel; //Created (CellCompute), Set
    int xVecZeroKernel; //Created (CellCompute), Set

    PressureGrid[] grid;
    Cell[] cells;
    WaterParticle[] particles;

    ComputeBuffer waterParticlebuffer;
    ComputeBuffer cellBuffer;
    ComputeBuffer quad;

    ComputeBuffer gridaBuffer;
    ComputeBuffer gridcolABuffer;
    ComputeBuffer gridxBuffer;
    ComputeBuffer gridbBuffer;

    float particleSizeMult = 10;

    private void Start()
    {
        //Set up cell, particle and pressure grid array
        numCells = gridSizeX * gridSizeY * gridSizeZ;
        cells = new Cell[numCells];
        initPressureGrid();
        particles = new WaterParticle[particleAmount];

        particleSizeMult = cellWidth*10;
        //Initialize buffers
        waterParticlebuffer = new ComputeBuffer(particles.Length, Marshal.SizeOf(typeof(WaterParticle)));
        cellBuffer = new ComputeBuffer(numCells, Marshal.SizeOf(typeof(Cell)));
        quad = new ComputeBuffer(4, Marshal.SizeOf(typeof(Vector3)));
        
        gridaBuffer     = new ComputeBuffer(grid[0].a.Length, sizeof(float));
        gridcolABuffer  = new ComputeBuffer(grid[0].colA.Length, sizeof(int));
        gridxBuffer     = new ComputeBuffer(grid[0].x.Length, sizeof(float));
        gridbBuffer     = new ComputeBuffer(grid[0].b.Length, sizeof(float));

        for (int i = 1; i < numCells/2; i+=gridSizeX/4)
        {
            cells[getCellIndex(i-1, 0, 0)].tempVel = new Vector3(5, 50, 0);
            cells[getCellIndex(i, 0, 0)].tempVel = new Vector3(5, 70, 0);
            cells[getCellIndex(i + 1, 0, 0)].tempVel = new Vector3(5, 50, 0);
        }

        //Give the buffers their data
        waterParticlebuffer.SetData(particles);
        cellBuffer.SetData(cells);

        quad.SetData(new[]
        {
            new Vector3(-0.5f*particleSizeMult,0.5f*particleSizeMult),
            new Vector3(0.5f*particleSizeMult,0.5f*particleSizeMult),
            new Vector3(0.5f*particleSizeMult,-0.5f*particleSizeMult),
            new Vector3(-0.5f*particleSizeMult,-0.5f*particleSizeMult)
        });

        gridaBuffer.SetData(grid[0].a);
        gridcolABuffer.SetData(grid[0].colA);
        gridxBuffer.SetData(grid[0].x);
        gridbBuffer.SetData(grid[0].b);

        //Set all kernels
        /*ORDER OF OPERATIONS:
        randKernel
        airKernel
        markFluidKernel
        convectionKernel
        swapVelKernel
        externalForcesKernel
        viscosityKernel
        pressureSetupKernel
        pressureCopyKernel
        pressureApplyKernel
        extrapolateFluidKernel
        solidVelKernel
        moveParticleKernel
        */

        randKernel = particleComputeShader.FindKernel("RandomPosition");
        moveParticleKernel = particleComputeShader.FindKernel("MoveParticle");

        airKernel = cellOperationShader.FindKernel("SetAllAir");
        markFluidKernel = cellOperationShader.FindKernel("SetParticleCellToWater");
        swapVelKernel = cellOperationShader.FindKernel("SwapVel");
        pressureSetupKernel = cellOperationShader.FindKernel("PressureSetup");
        pressureCopyKernel = cellOperationShader.FindKernel("PressureCopy");
        solidVelKernel = cellOperationShader.FindKernel("SetSolidVel");
        gaussKernel = cellOperationShader.FindKernel("GaussSeidelPressure");
        xVecZeroKernel = cellOperationShader.FindKernel("SetXVecToZero");

        convectionKernel = physicsComputeShader.FindKernel("ConvectionCalc");
        externalForcesKernel = physicsComputeShader.FindKernel("ExternalForceCalc");
        viscosityKernel = physicsComputeShader.FindKernel("ViscosityCalc");
        pressureApplyKernel = physicsComputeShader.FindKernel("PressureApply");
        extrapolateFluidKernel = physicsComputeShader.FindKernel("ExtrapolateVelocity");

        //Give all marker particles random positions
        particleComputeShader.SetBuffer(randKernel, "particles", waterParticlebuffer);
        particleComputeShader.SetFloat("cellWidth", cellWidth);
        particleComputeShader.SetInt("gridSizeX", gridSizeX);
        particleComputeShader.SetInt("gridSizeY", gridSizeY);
        particleComputeShader.SetInt("gridSizeZ", gridSizeZ);
        particleComputeShader.SetInt("rngStart", Random.Range(0, 1000));
        particleComputeShader.Dispatch(randKernel, (particles.Length + 64) / 64, 1, 1);

        SetUIValues();
    }

    void SetUIValues()
    {
        viscosityText.text = "" + visVal;
        airDensityText.text = "" + airDensity;
        fluidDensityText.text = "" + fluidDensity;
        gravityText.text = "" + gravity;
        markerParticleText.text = "" + particleAmount;
    }

    void initPressureGrid()
    {
        int maxGridLevel = 1;
        grid = new PressureGrid[maxGridLevel];
        grid[0].level = 0;
        grid[0].sizeX = gridSizeX;
        grid[0].sizeY = gridSizeY;
        grid[0].sizeZ = gridSizeZ;
        grid[0].numCells = numCells;

        grid[0].a = new float[numCells * 7];
        grid[0].colA = new int[numCells * 6];
        grid[0].x = new float[numCells];
        grid[0].b = new float[numCells];

        for(int i = 1; i < maxGridLevel; i++)
        {
            grid[i].level = i;
            grid[i].sizeX = grid[i - 1].sizeX / 2;
            grid[i].sizeY = grid[i - 1].sizeY / 2;
            grid[i].sizeZ = grid[i - 1].sizeZ / 2;
            grid[i].numCells = grid[i].sizeX * grid[i].sizeY * grid[i].sizeZ;

            grid[i].a = new float[grid[i].numCells * 7];
            grid[i].colA = new int[grid[i].numCells * 6];
            grid[i].x = new float[grid[i].numCells];
            grid[i].b = new float[grid[i].numCells];
        }
    }

    int getCellIndex(int x, int y, int z)
    {
        return (z * gridSizeX * gridSizeY) + (y * gridSizeX) + x;
    }

    Vector3 getCellCoordinates(int index)
    {
        Vector3 pos;
        pos.z = index / (gridSizeX * gridSizeY);
        index -= ((int)pos.z * gridSizeX * gridSizeY);
        pos.y = index / gridSizeX;
        pos.x = index % gridSizeX;
        return pos;
    }

    private void FixedUpdate()
    {
        if (runSim)
        {
            RunSimulator();
        }else if (Input.GetKeyDown(KeyCode.K))
        {
            //RunSimulator();
            runSim = true;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            addGridLevel();
        }

        for (int i = 0; i < numCells; i++)
        {
            Vector3 pos = getCellCoordinates(i);
            Vector3 direction = cells[i].vel.normalized / 2;
            Debug.DrawRay(pos, direction);
        }
    }

    int gridLevel;

    void addGridLevel()
    {
        Debug.Log("Grid level added");
        gridLevel++;
    }

    void RunSimulator()
    {
        //SET DELTA TIME VARIABLE IN PHYSICS SHADER
        physicsComputeShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        particleComputeShader.SetFloat("deltaTime", Time.fixedDeltaTime);

        //SET ALL CELL TYPES TO AIR
        cellOperationShader.SetBuffer(airKernel, "cells", cellBuffer);
        cellOperationShader.Dispatch(airKernel, (cells.Length + 64) / 64, 1, 1);

        //SET ALL CELL TYPES WITH PARTICLE MARKERS IN THEM TO WATER
        cellOperationShader.SetBuffer(markFluidKernel, "cells", cellBuffer);
        cellOperationShader.SetBuffer(markFluidKernel, "particles", waterParticlebuffer);
        cellOperationShader.SetInt("gridSizeX", gridSizeX);
        cellOperationShader.SetInt("gridSizeY", gridSizeY);
        cellOperationShader.SetFloat("cellWidth", cellWidth);
        cellOperationShader.Dispatch(markFluidKernel, (particles.Length + 64) / 64, 1, 1);

        //DO CONVECTION CALCULATION ON CELLS
        physicsComputeShader.SetBuffer(convectionKernel, "cells", cellBuffer);
        physicsComputeShader.SetInt("gridSizeX", gridSizeX);
        physicsComputeShader.SetInt("gridSizeY", gridSizeY);
        physicsComputeShader.SetFloat("cellWidth", cellWidth);
        physicsComputeShader.Dispatch(convectionKernel, (cells.Length+64)/64, 1, 1);

        //SET THE CURRENT VELOCITY TO THE TEMP VELOCITY
        cellOperationShader.SetBuffer(swapVelKernel, "cells", cellBuffer);
        cellOperationShader.Dispatch(swapVelKernel, (cells.Length + 64) / 64, 1, 1);

        //CALCULATE ALL EXTERNAL FORCES
        physicsComputeShader.SetFloat("gravity", gravity);
        physicsComputeShader.SetBuffer(externalForcesKernel, "cells", cellBuffer);
        physicsComputeShader.Dispatch(externalForcesKernel, (cells.Length + 64) / 64, 1, 1);

        //SET THE CURRENT VELOCITY TO THE TEMP VELOCITY
        cellOperationShader.SetBuffer(swapVelKernel, "cells", cellBuffer);
        cellOperationShader.Dispatch(swapVelKernel, (cells.Length + 64) / 64, 1, 1);

        //CALCULATE THE VISCOSITY OF THE CELLS
        physicsComputeShader.SetBuffer(viscosityKernel, "cells", cellBuffer);
        physicsComputeShader.SetFloat("visVal", visVal);
        physicsComputeShader.Dispatch(viscosityKernel, (cells.Length + 64) / 64, 1, 1);

        //SET THE CURRENT VELOCITY TO THE TEMP VELOCITY
        cellOperationShader.SetBuffer(swapVelKernel, "cells", cellBuffer);
        cellOperationShader.Dispatch(swapVelKernel, (cells.Length + 64) / 64, 1, 1);

        //SETUP THE PRESSURE OF THE CELLS
        cellOperationShader.SetBuffer(pressureSetupKernel, "cells", cellBuffer);
        cellOperationShader.SetBuffer(pressureSetupKernel, "gridaBuffer", gridaBuffer);
        cellOperationShader.SetBuffer(pressureSetupKernel, "gridcolABuffer", gridcolABuffer);
        cellOperationShader.SetBuffer(pressureSetupKernel, "gridbBuffer", gridbBuffer);
        cellOperationShader.SetInt("level", grid[0].level);
        cellOperationShader.SetInt("numCells", grid[0].numCells);
        cellOperationShader.SetInt("sizeX", grid[0].sizeX);
        cellOperationShader.SetInt("sizeY", grid[0].sizeY);
        cellOperationShader.SetInt("sizeZ", grid[0].sizeZ);
        cellOperationShader.SetFloat("cellWidth", cellWidth);
        cellOperationShader.SetFloat("deltaTime", Time.fixedDeltaTime);
        cellOperationShader.SetFloat("fluidDensity", fluidDensity);
        cellOperationShader.SetFloat("airDensity", airDensity);
        cellOperationShader.SetFloat("atmoPressure", atmoPressure);
        cellOperationShader.Dispatch(pressureSetupKernel, (cells.Length + 64) / 64, 1, 1);

        //SET THE X VECTOR BUFFER TO ZERO
        cellOperationShader.SetBuffer(xVecZeroKernel, "gridxBuffer", gridxBuffer);
        cellOperationShader.Dispatch(xVecZeroKernel, (grid[0].x.Length + 64) / 64, 1, 1);

        //DO THE GAUSS SEIDEL CALCULATIONS ON THE CELLS
        for (int i = 0; i < gaussIntervals; i++)
        {
            cellOperationShader.SetBuffer(gaussKernel, "gridaBuffer", gridaBuffer);
            cellOperationShader.SetBuffer(gaussKernel, "gridcolABuffer", gridcolABuffer);
            cellOperationShader.SetBuffer(gaussKernel, "gridxBuffer", gridxBuffer);
            cellOperationShader.SetBuffer(gaussKernel, "gridbBuffer", gridbBuffer);
            cellOperationShader.Dispatch(gaussKernel, (cells.Length + 64) / 64, 1, 1);
        }

        //IF CELL IS WATER, KEEP LAST CALCULATION PRESSURE, ELSE SET TO AIR PRESSURE
        cellOperationShader.SetBuffer(pressureCopyKernel, "cells", cellBuffer);
        cellOperationShader.SetBuffer(pressureCopyKernel, "gridxBuffer", gridxBuffer);
        cellOperationShader.Dispatch(pressureCopyKernel, (cells.Length + 64) / 64, 1, 1);

        //APPLY THE PRESSURE CALCULATIONS TO EACH CELL
        physicsComputeShader.SetBuffer(pressureApplyKernel, "cells", cellBuffer);
        physicsComputeShader.SetFloat("fluidDensity", fluidDensity);
        physicsComputeShader.SetFloat("airDensity", airDensity);
        physicsComputeShader.Dispatch(pressureApplyKernel, (cells.Length + 64) / 64, 1, 1);

        cellBuffer.GetData(cells);
        Debug.Log(cells[0].pressure);

        //SET THE CURRENT VELOCITY TO THE TEMP VELOCITY
        cellOperationShader.SetBuffer(swapVelKernel, "cells", cellBuffer);
        cellOperationShader.Dispatch(swapVelKernel, (cells.Length + 64) / 64, 1, 1);

        //EXTRAPOLATE CELL VELOCITIES TO SURROUNDING CELLS
        for (int i = 1; i< extrapIntervals; i++)
        {
            physicsComputeShader.SetBuffer(extrapolateFluidKernel, "cells", cellBuffer);
            physicsComputeShader.SetInt("layer", i);
            physicsComputeShader.Dispatch(extrapolateFluidKernel, (cells.Length + 64) / 64, 1, 1);

            cellOperationShader.SetBuffer(swapVelKernel, "cells", cellBuffer);
            cellOperationShader.Dispatch(swapVelKernel, (cells.Length + 64) / 64, 1, 1);
        }

        //SET CELL VELOCITY TO ZERO IF A SOLID
        cellOperationShader.SetBuffer(solidVelKernel, "cells", cellBuffer);
        cellOperationShader.SetInt("gridSizeZ", gridSizeZ);
        cellOperationShader.Dispatch(solidVelKernel, (cells.Length + 64) / 64, 1, 1);

        //MOVE MARKER PARTICLES IN BUFFER BY CELL VALUES
        particleComputeShader.SetBuffer(moveParticleKernel, "particles", waterParticlebuffer);
        particleComputeShader.SetBuffer(moveParticleKernel, "cells", cellBuffer);
        particleComputeShader.SetFloat("cellWidth", cellWidth);
        particleComputeShader.Dispatch(moveParticleKernel, (particles.Length + 64) / 64, 1, 1);

        cellBuffer.GetData(cells);
    }

    void OnPostRender()
    {
        material.SetPass(0);
        material.SetBuffer("particles", waterParticlebuffer);
        material.SetBuffer("quad", quad);
        Graphics.DrawProceduralNow(MeshTopology.Quads, 4, particles.Length);
    }

    private void OnDestroy()
    {
        waterParticlebuffer.Dispose();
        cellBuffer.Dispose();
        quad.Dispose();
        gridaBuffer.Dispose();
        gridcolABuffer.Dispose();
        gridxBuffer.Dispose();
        gridbBuffer.Dispose();
    }
}
