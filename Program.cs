using System;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using MathHelper = Silk.NET.Maths.Scalar;

class Program
{
    private static IWindow window = null!;
    private static GL gl = null!;

	private static IInputContext input = null!;

	// Floor OpenGL handles
private static uint floorVao;
private static uint floorVbo;
private static uint floorEbo;
private static int floorIndexCount;

    // Camera State
    private static Vector3D<float> cameraPos = new(0.0f, 0.0f, 5.0f);
    private static Vector3D<float> cameraFront = new(0.0f, 0.0f, -1.0f);
    private static Vector3D<float> cameraUp = new(0.0f, 1.0f, 0.0f);
    private static float yaw = -90.0f;
    private static float pitch = 0.0f;
    private static float lastX = 400, lastY = 300;
    private static bool firstMouse = true;

    // OpenGL Handle IDs
    private static uint shaderProgram;
    private static uint vao;
    private static uint vbo;

    // Uniform Locations
    private static int modelLoc;
    private static int viewLoc;
    private static int projLoc;

    private static float cubeRotation = 0f;

    static void Main()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Silk.NET OpenGL FPS Camera";
        options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3));

		 // --- ADD THESE LINES TO REMOVE VSYNC AND UNCAP FRAMERATE ---
    options.VSync = false; 
    options.UpdatesPerSecond = 0; // Uncaps update loop thread execution throttling
    options.FramesPerSecond = 0;  // Uncaps render loop thread execution throttling
    // -----------------------------------------------------------

        window = Window.Create(options);
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.Closing += OnClosing;

        window.Run();
    }

    static void OnLoad()
    {
        gl = GL.GetApi(window);
        gl.Enable(EnableCap.DepthTest); // Ensure proper 3D depth rendering

        // Configure Mouse Capture
			input = window.CreateInput();
        foreach (var mouse in input.Mice)
        {
            mouse.Cursor.CursorMode = CursorMode.Raw;
            mouse.MouseMove += OnMouseMove;
        }

// --- GENERATE WIREMESH FLOOR GRID ---
int gridWidth = 20;
int gridDepth = 20;
float gridSpacing = 1.0f;

var floorVertices = new List<float>();
var floorIndices = new List<uint>();

// Generate positions (Centered around X=0, Z=0)
float startX = -(gridWidth * gridSpacing) / 2.0f;
float startZ = -(gridDepth * gridSpacing) / 2.0f;

for (int z = 0; z <= gridDepth; z++)
{
    for (int x = 0; x <= gridWidth; x++)
    {
        float posX = startX + (x * gridSpacing);
        float posZ = startZ + (z * gridSpacing);
        float posY = -1.5f; // Positioned 1.5 units below the origin camera space

        floorVertices.Add(posX);
        floorVertices.Add(posY);
        floorVertices.Add(posZ);

        // Simple green color for grid lines (R, G, B)
        floorVertices.Add(0.0f);
        floorVertices.Add(0.8f);
        floorVertices.Add(0.2f);
    }
}

// Generate topology index layout (Triangles)
int verticesPerRow = gridWidth + 1;
for (int z = 0; z < gridDepth; z++)
{
    for (int x = 0; x < gridWidth; x++)
    {
        uint row1 = (uint)(z * verticesPerRow + x);
        uint row2 = (uint)((z + 1) * verticesPerRow + x);

        // Triangle 1
        floorIndices.Add(row1);
        floorIndices.Add(row2);
        floorIndices.Add(row1 + 1);

        // Triangle 2
        floorIndices.Add(row1 + 1);
        floorIndices.Add(row2);
        floorIndices.Add(row2 + 1);
    }
}

floorIndexCount = floorIndices.Count;
float[] fVertices = floorVertices.ToArray();
uint[] fIndices = floorIndices.ToArray();

// Setup floor buffers
floorVao = gl.GenVertexArray();
floorVbo = gl.GenBuffer();
floorEbo = gl.GenBuffer();

gl.BindVertexArray(floorVao);

gl.BindBuffer(BufferTargetARB.ArrayBuffer, floorVbo);
unsafe
{
    fixed (float* v = fVertices)
    {
        gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(fVertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
    }

    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, floorEbo);
    fixed (uint* i = fIndices)
    {
        gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(fIndices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
    }

    // Position attribute (Location 0)
    gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
    gl.EnableVertexAttribArray(0);

    // Color attribute (Location 1)
    gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
    gl.EnableVertexAttribArray(1);
}


        // 1. Shaders Source
        string vertexShaderSource = @"
            #version 330 core
            layout (location = 0) in vec3 aPos;
            layout (location = 1) in vec3 aColor;
            out vec3 ourColor;
            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;
            void main() {
                gl_Position = projection * view * model * vec4(aPos, 1.0);
                ourColor = aColor;
            }";

        string fragmentShaderSource = @"
            #version 330 core
            in vec3 ourColor;
            out vec4 FragColor;
            void main() {
                FragColor = vec4(ourColor, 1.0);
            }";

        // Compile and Link Shaders
        uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vertexShader, vertexShaderSource);
        gl.CompileShader(vertexShader);

        uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fragmentShader, fragmentShaderSource);
        gl.CompileShader(fragmentShader);

        shaderProgram = gl.CreateProgram();
        gl.AttachShader(shaderProgram, vertexShader);
        gl.AttachShader(shaderProgram, fragmentShader);
        gl.LinkProgram(shaderProgram);

        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);

        // Get uniform locations from shader
        modelLoc = gl.GetUniformLocation(shaderProgram, "model");
        viewLoc = gl.GetUniformLocation(shaderProgram, "view");
        projLoc = gl.GetUniformLocation(shaderProgram, "projection");

        // 3. Setup VBO and VAO
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();

        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

        
    }

    static void OnUpdate(double deltaTime)
    {
        cubeRotation += 20f * (float)deltaTime; // Rotate target over time

        //var input = window.CreateInput();
        var keyboard = input.Keyboards[0];

        if (keyboard.IsKeyPressed(Key.Escape)) window.Close();

        // Keyboard Movement Vector Adjustments
        float cameraSpeed = 4.0f * (float)deltaTime;
        if (keyboard.IsKeyPressed(Key.W)) cameraPos += cameraFront * cameraSpeed;
        if (keyboard.IsKeyPressed(Key.S)) cameraPos -= cameraFront * cameraSpeed;
        
        var rightVector = Vector3D.Normalize(Vector3D.Cross(cameraFront, cameraUp));
        if (keyboard.IsKeyPressed(Key.A)) cameraPos -= rightVector * cameraSpeed;
        if (keyboard.IsKeyPressed(Key.D)) cameraPos += rightVector * cameraSpeed;
    }

    static unsafe void OnRender(double deltaTime)
    {
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        gl.ClearColor(0.1f, 0.15f, 0.2f, 1.0f);

        gl.UseProgram(shaderProgram);

        // --- MATH BINDINGS FOR CAMERA AND PROJECTION ---

        // Model Matrix (Object space -> World Space)
		 
		 // Define unit axis vectors for Y and X rotation
		var axisY = new Vector3D<float>(0.0f, 1.0f, 0.0f);
var axisX = new Vector3D<float>(1.0f, 0.0f, 0.0f);

// Create rotations using Quaternions
var rotY = Quaternion<float>.CreateFromAxisAngle(axisY, Scalar.DegreesToRadians(cubeRotation));
var rotX = Quaternion<float>.CreateFromAxisAngle(axisX, Scalar.DegreesToRadians(cubeRotation * 0.3f));

// Combine quaternions and convert into a Matrix4X4
var model = Matrix4X4.CreateFromQuaternion(rotY * rotX);
		 
        gl.UniformMatrix4(modelLoc, 1, false, (float*)&model);

        // 2. View Matrix (World Space -> Camera Space) via LookAt
        var view = Matrix4X4.CreateLookAt(cameraPos, cameraPos + cameraFront, cameraUp);
        gl.UniformMatrix4(viewLoc, 1, false, (float*)&view);

        // 3. Projection Matrix (Camera Space -> Clip Space) via Perspective Field of View
        float fovRadians = MathHelper.DegreesToRadians(45.0f);
        float aspectRatio = (float)window.Size.X / window.Size.Y;
        var projection = Matrix4X4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.1f, 100.0f);
        gl.UniformMatrix4(projLoc, 1, false, (float*)&projection);

        // Render Cube Geometry
        gl.BindVertexArray(vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
		
// --- RENDER WIREMESH FLOOR ---
// 1. Reset model matrix for the floor (Identity Matrix so it doesn't spin like the cube)
var floorModel = Matrix4X4<float>.Identity;
unsafe { gl.UniformMatrix4(modelLoc, 1, false, (float*)&floorModel); }

// 2. Force OpenGL to render geometry as wireframe lines
gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);

// 3. Draw the elements using EBO
gl.BindVertexArray(floorVao);
unsafe { gl.DrawElements(PrimitiveType.Triangles, (uint)floorIndexCount, DrawElementsType.UnsignedInt, (void*)0); }

// 4. Critical: Restore standard fill mode so the solid cube renders correctly next frame
gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
    }

    static void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        if (firstMouse)
        {
            lastX = position.X;
            lastY = position.Y;
            firstMouse = false;
        }

        float xOffset = position.X - lastX;
        float yOffset = lastY - position.Y; // Reversed since y-coordinates go from bottom to top
        lastX = position.X;
        lastY = position.Y;

        float sensitivity = 0.1f;
        xOffset *= sensitivity;
        yOffset *= sensitivity;

        yaw += xOffset;
        pitch += yOffset;

        // Prevent camera flipping upside down
        if (pitch > 89.0f) pitch = 89.0f;
        if (pitch < -89.0f) pitch = -89.0f;

        // Trigonometry direction evaluation
        Vector3D<float> direction;
        direction.X = MathF.Cos(MathHelper.DegreesToRadians(yaw)) * MathF.Cos(MathHelper.DegreesToRadians(pitch));
        direction.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
        direction.Z = MathF.Sin(MathHelper.DegreesToRadians(yaw)) * MathF.Cos(MathHelper.DegreesToRadians(pitch));
        
        cameraFront = Vector3D.Normalize(direction);
    }

    static void OnClosing()
    {
        gl.DeleteBuffer(vbo);
        gl.DeleteVertexArray(vao);
		
		 // Dispose Floor Assets
    gl.DeleteBuffer(floorVbo);
    gl.DeleteBuffer(floorEbo);
    gl.DeleteVertexArray(floorVao);
		
        gl.DeleteProgram(shaderProgram);
    }
}
