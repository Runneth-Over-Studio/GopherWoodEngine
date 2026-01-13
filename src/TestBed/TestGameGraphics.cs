using GopherWoodEngine.Runtime;
using GopherWoodEngine.Runtime.Modules.Rendering;
using System;
using System.IO;
using System.Numerics;

namespace TestBed;

internal class TestGameGraphics(string selectedTest, EngineConfig engineConfig) : GameBase(engineConfig)
{
    private readonly string _selectedTest = selectedTest;
    private readonly string _assetsPath = Path.Combine(AppContext.BaseDirectory, "Base", "Assets");

    private Mesh? _activeMesh;
    private float _rotation = 0f;

    public override void Initialize()
    {
        switch (_selectedTest)
        {
            case "Hello Triangle":
                InitializeTriangle();
                break;
            case "Texture Mapping":
                InitializeTexturedSquare();
                break;
            case "3D Model":
                Initialize3DModel();
                break;
            default:
                InitializeTriangle();
                break;
        }
    }

    public override void Update(double deltaTime)
    {
        // Rotate the mesh
        _rotation += (float)deltaTime * 90.0f; // 90 degrees per second

        if (_activeMesh != null)
        {
            var currentTransform = _activeMesh.Transform;
            _activeMesh.Transform = currentTransform with
            {
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, _rotation * MathF.PI / 180f)
            };
        }
    }

    public override void Render(double deltaTime)
    {
        if (_activeMesh != null)
        {
            RenderContext.Submit(_activeMesh);
        }
    }

    private void InitializeTriangle()
    {
        // Create colored triangle vertices (2D positions + colors)
        var vertices = new Vertex[]
        {
            new() { Position = new Vector3(-0.5f, -0.5f, 0.0f), Color = new Vector3(1.0f, 0.0f, 0.0f), TexCoord = Vector2.Zero },
            new() { Position = new Vector3(0.5f, -0.5f, 0.0f), Color = new Vector3(0.0f, 1.0f, 0.0f), TexCoord = Vector2.Zero },
            new() { Position = new Vector3(0.5f, 0.5f, 0.0f), Color = new Vector3(0.0f, 0.0f, 1.0f), TexCoord = Vector2.Zero },
            new() { Position = new Vector3(-0.5f, 0.5f, 0.0f), Color = new Vector3(1.0f, 1.0f, 1.0f), TexCoord = Vector2.Zero }
        };

        var indices = new uint[] { 0, 1, 2, 2, 3, 0 };

        _activeMesh = new Mesh
        {
            Vertices = vertices,
            Indices = indices,
            Transform = new Transform { Position = Vector3.Zero, Scale = Vector3.One },
            Material = new Material
            {
                Name = "ColoredTriangle",
                Shader = new Shader
                {
                    Name = "triangle",
                    VertexShaderPath = "triangle.vert.spv",
                    FragmentShaderPath = "triangle.frag.spv"
                }
            }
        };
    }

    private void InitializeTexturedSquare()
    {
        // Create textured square vertices (2D positions + colors + texture coords)
        var vertices = new Vertex[]
        {
            new() { Position = new Vector3(-0.5f, -0.5f, 0.0f), Color = new Vector3(1.0f, 0.0f, 0.0f), TexCoord = new Vector2(1.0f, 0.0f) },
            new() { Position = new Vector3(0.5f, -0.5f, 0.0f), Color = new Vector3(0.0f, 1.0f, 0.0f), TexCoord = new Vector2(0.0f, 0.0f) },
            new() { Position = new Vector3(0.5f, 0.5f, 0.0f), Color = new Vector3(0.0f, 0.0f, 1.0f), TexCoord = new Vector2(0.0f, 1.0f) },
            new() { Position = new Vector3(-0.5f, 0.5f, 0.0f), Color = new Vector3(1.0f, 1.0f, 1.0f), TexCoord = new Vector2(1.0f, 1.0f) }
        };

        var indices = new uint[] { 0, 1, 2, 2, 3, 0 };

        // Load the statue texture
        string texturePath = Path.Combine(_assetsPath, "statue-sculpture-figure.jpg");
        var texture = new Texture
        {
            FilePath = texturePath,
            Width = 0, // Will be loaded by backend
            Height = 0
        };

        _activeMesh = new Mesh
        {
            Vertices = vertices,
            Indices = indices,
            Transform = new Transform
            {
                Position = new Vector3(0, 0, -1), // Tilt back
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -30 * MathF.PI / 180f),
                Scale = Vector3.One
            },
            Material = new Material
            {
                Name = "TexturedSquare",
                Shader = new Shader
                {
                    Name = "texture",
                    VertexShaderPath = "texture.vert.spv",
                    FragmentShaderPath = "texture.frag.spv"
                },
                AlbedoTexture = texture
            }
        };
    }

    private void Initialize3DModel()
    {
        // TODO: Load viking_room.obj model
        // For now, create a simple cube as a placeholder
        string texturePath = Path.Combine(_assetsPath, "viking_room.png");
        var texture = new Texture
        {
            FilePath = texturePath,
            Width = 0,
            Height = 0
        };

        _activeMesh = new Mesh
        {
            Vertices = CreateCubeVertices(),
            Indices = CreateCubeIndices(),
            Transform = new Transform
            {
                Position = new Vector3(0, 0, -2),
                Scale = Vector3.One
            },
            Material = new Material
            {
                Name = "VikingRoom",
                Shader = new Shader
                {
                    Name = "model",
                    VertexShaderPath = "model.vert.spv",
                    FragmentShaderPath = "model.frag.spv"
                },
                AlbedoTexture = texture
            }
        };
    }

    private static Vertex[] CreateCubeVertices()
    {
        return new Vertex[]
        {
            // Front face
            new() { Position = new Vector3(-0.5f, -0.5f, 0.5f), Normal = new Vector3(0, 0, 1), Color = Vector3.One, TexCoord = new Vector2(0, 0) },
            new() { Position = new Vector3(0.5f, -0.5f, 0.5f), Normal = new Vector3(0, 0, 1), Color = Vector3.One, TexCoord = new Vector2(1, 0) },
            new() { Position = new Vector3(0.5f, 0.5f, 0.5f), Normal = new Vector3(0, 0, 1), Color = Vector3.One, TexCoord = new Vector2(1, 1) },
            new() { Position = new Vector3(-0.5f, 0.5f, 0.5f), Normal = new Vector3(0, 0, 1), Color = Vector3.One, TexCoord = new Vector2(0, 1) },
            // Add other cube faces as needed...
        };
    }

    private static uint[] CreateCubeIndices()
    {
        return new uint[]
        {
            // Front face
            0, 1, 2, 2, 3, 0,
            // Add other cube face indices as needed...
        };
    }
}
