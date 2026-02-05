using Godot;
using System;
using System.Threading.Tasks;

public partial class BeltConveyor : Node3D, IBeltConveyor
{
	[Export]
	public Color BeltColor
	{
		get
		{
			return beltColor;
		}
		set
		{
			beltColor = value;

			if (beltMaterial != null)
				((ShaderMaterial)beltMaterial).SetShaderParameter("ColorMix", beltColor);
			if (conveyorEnd1 != null)
				((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("ColorMix", beltColor);
			if (conveyorEnd2 != null)
				((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("ColorMix", beltColor);
		}
	}

	[Export]
	public IBeltConveyor.ConvTexture BeltTexture
	{
		get
		{
			return beltTexture;
		}
		set
		{
			beltTexture = value;

			if (beltMaterial != null)
				((ShaderMaterial)beltMaterial).SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
			if (conveyorEnd1 != null)
				((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
			if (conveyorEnd2 != null)
				((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("BlackTextureOn", beltTexture == IBeltConveyor.ConvTexture.Standard);
		}
	}

	[Export]
	public float Speed { get; set; }

	// Visual
	private Color beltColor = new Color(1, 1, 1, 1);

	private IBeltConveyor.ConvTexture beltTexture = IBeltConveyor.ConvTexture.Standard;

	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	private double beltPosition = 0.0;

	// TagOpc
	private string tagEsteira = "";

	// Componentes
	private RigidBody3D rb;
	private MeshInstance3D mesh;
	private Material beltMaterial;
	private Material metalMaterial;
	private ConveyorEnd conveyorEnd1;
	private ConveyorEnd conveyorEnd2;

	// Fisica
	private Vector3 origin;

	// Referências
	public Root Main { get; set; }
	private int currentScene;
	

	public override void _Ready()
	{
		GD.Print("\n> [BeltConveyor.cs] [_Ready()]");

		Inicializacao_Componentes3D();
		Inicializacao_Materials();
		Conectar_Root();
	}

	public override void _ExitTree()
	{
		GD.Print("\n> [BeltConveyor.cs] [_ExitTree()]");
		if (Main == null) return;

		Main.SimulationStarted -= OnSimulationStarted;
		Main.SimulationEnded -= OnSimulationEnded;
		Main.OpcDataChanged -= OnOpcDataReceived;
	}

	private void Inicializacao_Componentes3D()
	{
		rb = GetNode<RigidBody3D>("RigidBody3D");
		mesh = GetNode<MeshInstance3D>("RigidBody3D/MeshInstance3D");

		mesh.Mesh = mesh.Mesh.Duplicate() as Mesh;
		beltMaterial = mesh.Mesh.SurfaceGetMaterial(0).Duplicate() as Material;
		metalMaterial = mesh.Mesh.SurfaceGetMaterial(1).Duplicate() as Material;
		
		mesh.Mesh.SurfaceSetMaterial(0, beltMaterial);
		mesh.Mesh.SurfaceSetMaterial(1, metalMaterial);
		mesh.Mesh.SurfaceSetMaterial(2, metalMaterial);

		conveyorEnd1 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd");
		conveyorEnd2 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd2");

		origin = rb.Position;
	}
	
	private void Inicializacao_Materials()
	{
		// Textura
		bool isStandard = beltTexture == IBeltConveyor.ConvTexture.Standard;
		
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);

		// Cores
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("ColorMix", beltColor);
	
	}
	
	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;

		if (Main == null)
		{
			GD.PrintErr($"[BeltConveyor] {Name}: Root não encontrado!");
			return;
		}

		currentScene = Main.currentScene;

		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
		Main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		GD.Print("\n> [BeltConveyor.cs] [OnSimulationStarted()]");

		// Carrega a Tag Configurada	
		tagEsteira = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";

		if (string.IsNullOrEmpty(tagEsteira))
		{
			GD.PrintErr($"[BeltConveyor] {Name}: Tag OPC não configurada!");
		}

		// Verificar a conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");

		if (isCommsConnected)
		{
			GD.Print($"[BeltConveyor] {Name}: OPC conectado → {tagEsteira}");
		}
		else
		{
			GD.PrintErr($"[BeltConveyor] {Name}: OPC não conectado - esteira parada!");
		}
		
		// Reset do estado
		running = true;
		Speed = 0f;
		beltPosition = 0.0;
	}

	private void OnSimulationEnded()
	{
		running = false;
		Speed = 0f;
		beltPosition = 0;
		
		// Reset visual
		((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition);
		
		// Reset física
		rb.Position = Vector3.Zero;
		rb.Rotation = Vector3.Zero;
		rb.LinearVelocity = Vector3.Zero;
		
		// Reset filhos
		foreach (Node3D child in rb.GetChildren())
		{
			child.Position = Vector3.Zero;
			child.Rotation = Vector3.Zero;
		}
	}

	private void OnOpcDataReceived(string tagName, object value)
    {
        // Recebe sinal de disparo do OPC via subscription
        if (tagName != tagEsteira || value == null)
			return;
		
		try
		{
			float newSpeed = Convert.ToSingle(value);
			
			// Atualiza velocidade apenas se mudou significativamente
			if (Mathf.Abs(newSpeed - Speed) > 0.01f)
			{
				Speed = newSpeed;
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[BeltConveyor] {Name}: Speed = {Speed:F2}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[BeltConveyor] {Name}: Erro ao converter velocidade de {tagName}: {e.Message}");
		}
    }

	public override void _PhysicsProcess(double delta)
	{
		// GD.Print("\n> [BeltConveyor.cs] [_PhysicsProcess()]");
		if (Main == null || !running)
			return;
		
		// Atualiza Movimento
		var localLeft = rb.GlobalTransform.Basis.X.Normalized();
		var velocity = localLeft * Speed;
		rb.LinearVelocity = velocity;
		rb.Position = origin;

		beltPosition += Speed * delta;
		if (beltPosition >= 1.0)
			beltPosition = 0.0;

		if (Speed != 0)
			((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition * Mathf.Sign(Speed));

		rb.Rotation = Vector3.Zero;
		rb.Scale = new Vector3(1, 1, 1);

		// Atualiza Shader
		Scale = new Vector3(Scale.X, 1, Scale.Z);

		if (Speed != 0)
		{
			if (beltMaterial != null)
				((ShaderMaterial)beltMaterial).SetShaderParameter("Scale", Scale.X * Mathf.Sign(Speed));
			
			if (metalMaterial != null)
				((ShaderMaterial)metalMaterial).SetShaderParameter("Scale", Scale.X);
		}
	}
}
