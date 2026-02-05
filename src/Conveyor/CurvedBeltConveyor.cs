using Godot;
using System;
using System.Threading.Tasks;

public partial class CurvedBeltConveyor : Node3D, IBeltConveyor
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
			Atualizar_Cor_Correia();
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
			Atualizar_Textura_Correia();
		}
	}

	[Export]
	public float Speed { get; set; }

	// Referências
	public Root Main {get; set; }

	// Visual
	private Color beltColor = new Color(1, 1, 1, 1);
	private IBeltConveyor.ConvTexture beltTexture = IBeltConveyor.ConvTexture.Standard;
	
	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	public double beltPosition = 0.0;
	
	// TagOpc
	private string tagEsteira = "";

	// Componentes
	private RigidBody3D rb;
	private MeshInstance3D mesh;
	private Material beltMaterial;
	private Material metalMaterial;
	private ConveyorEnd conveyorEnd1;
	private ConveyorEnd conveyorEnd2;
	
	// Física
	private Vector3 origin;
	
	// Referências
	private int currentScene;

	public override void _Ready()
	{
		Inicializacao_Componentes3D();
		Inicializacao_Materials();
		Conectar_Root();
	}

    public override void _ExitTree()
    {
        if (Main == null) return;

        Main.SimulationStarted -= OnSimulationStarted;
        Main.SimulationEnded -= OnSimulationEnded;
		Main.OpcDataChanged -= OnOpcDataReceived;
    }

	private void Inicializacao_Componentes3D()
	{
		rb = GetNode<RigidBody3D>("RigidBody3D");
		mesh = GetNode<MeshInstance3D>("RigidBody3D/MeshInstance3D");
		
		// Duplica mesh e materiais
		mesh.Mesh = mesh.Mesh.Duplicate() as Mesh;
		metalMaterial = mesh.Mesh.SurfaceGetMaterial(0).Duplicate() as Material;
		beltMaterial = mesh.Mesh.SurfaceGetMaterial(1).Duplicate() as Material;
		
		mesh.Mesh.SurfaceSetMaterial(0, metalMaterial);
		mesh.Mesh.SurfaceSetMaterial(1, beltMaterial);
		
		conveyorEnd1 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd");
		conveyorEnd2 = GetNode<ConveyorEnd>("RigidBody3D/Ends/ConveyorEnd2");
		
		origin = rb.Position;
	}

	private void Inicializacao_Materials()
	{
		Atualizar_Textura_Correia();
		Atualizar_Cor_Correia();
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;
		
		if (Main == null)
		{
			GD.PrintErr($"[CurvedBeltConveyor] {Name}: Root não encontrado!");
			return;
		}
		
		currentScene = Main.currentScene;
		
		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
		Main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		// Carrega tag configurada
		tagEsteira = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";
		
		if (string.IsNullOrEmpty(tagEsteira))
		{
			GD.PrintErr($"[CurvedBeltConveyor] {Name}: Tag OPC não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[CurvedBeltConveyor] {Name}: OPC conectado → {tagEsteira}");
		}
		else
		{
			GD.PrintErr($"[CurvedBeltConveyor] {Name}: OPC não conectado - esteira ficará parada!");
		}
		
		// Reset de estado
		running = true;
		Speed = 0f;
		beltPosition = 0.0;
	}

	private void OnSimulationEnded()
	{
		running = false;
		Speed = 0f;
		beltPosition = 0;
		
		// Reseta visual
		((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition);
		
		// Reseta física
		rb.Position = Vector3.Zero;
		rb.Rotation = Vector3.Zero;
		rb.AngularVelocity = Vector3.Zero;
		
		// Reseta filhos
		foreach (Node3D child in rb.GetChildren())
		{
			child.Position = Vector3.Zero;
			child.Rotation = Vector3.Zero;
		}
	}

	private void OnOpcDataReceived(string tagName, object value)
	{
		// Recebe velocidade do OPC via subscription
		if (tagName != tagEsteira || value == null) return;
		
		try
		{
			float newSpeed = Convert.ToSingle(value);
			
			// Atualiza velocidade apenas se mudou significativamente
			if (Mathf.Abs(newSpeed - Speed) > 0.01f)
			{
				Speed = newSpeed;
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[CurvedBeltConveyor] {Name}: Speed = {Speed:F2}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[CurvedBeltConveyor] {Name}: Erro ao converter velocidade:");
			GD.PrintErr(e.Message);
		}
	}

    public override void _PhysicsProcess(double delta)
	{
		if (Main == null || !running) return;
		
		// Atualiza movimento da esteira curva
		Atualizar_Movimento_Correia(delta);
		
		// Atualiza shader scale
		Atualizar_Escala_Shader();
	}

	private void Atualizar_Movimento_Correia(double delta)
	{
		// Aplica velocidade angular (rotação em vez de linear)
		var localUp = rb.GlobalTransform.Basis.Y.Normalized();
		var velocity = -localUp * Speed * MathF.PI * 0.25f * (1 / Scale.X);
		rb.AngularVelocity = velocity;
		
		// Atualiza animação da textura
		beltPosition += Speed * delta;
		if (beltPosition >= 1.0)
			beltPosition = 0.0;
		
		if (Speed != 0)
		{
			((ShaderMaterial)beltMaterial).SetShaderParameter("BeltPosition", beltPosition * Mathf.Sign(Speed));
		}
		
		// Mantém posição, rotação e escala
		rb.Position = Vector3.Zero;
		rb.Rotation = Vector3.Zero;
		rb.Scale = new Vector3(1, 1, 1);
	}

	private void Atualizar_Escala_Shader()
	{
		Scale = new Vector3(Scale.X, 1, Scale.X);
		
		if (Scale.X > 0.5f && Speed != 0)
		{
			if (beltMaterial != null)
				((ShaderMaterial)beltMaterial).SetShaderParameter("Scale", Scale.X * Mathf.Sign(Speed));
			
			if (metalMaterial != null)
				((ShaderMaterial)metalMaterial).SetShaderParameter("Scale", Scale.X);
		}
	}

	private void Atualizar_Cor_Correia()
	{
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("ColorMix", beltColor);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("ColorMix", beltColor);
	}

	private void Atualizar_Textura_Correia()
	{
		bool isStandard = beltTexture == IBeltConveyor.ConvTexture.Standard;
		
		if (beltMaterial != null)
			((ShaderMaterial)beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd1 != null)
			((ShaderMaterial)conveyorEnd1.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
		
		if (conveyorEnd2 != null)
			((ShaderMaterial)conveyorEnd2.beltMaterial).SetShaderParameter("BlackTextureOn", isStandard);
	}
}
