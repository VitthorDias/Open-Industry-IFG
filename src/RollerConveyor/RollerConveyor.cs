using Godot;
using System;
using System.Threading.Tasks;

public partial class RollerConveyor : Node3D
{
	[Export]
	public float Speed { get; set; } = 1.0f;
	
	[Export]
	public float SkewAngle
	{
		get
		{
			return skewAngle;
		}
		set
		{
			skewAngle = value;
			Atualizar_Rotacao_Rollers();
		}
	}

	// Configuração
	private float skewAngle = 0.0f;

	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	
	// Scale tracking
	private float nodeScaleX = 1.0f;
	private float nodeScaleZ = 1.0f;
	private float lastScale = 0.0f;
	
	// Tag OPC
	private string tagEsteira = "";
	
	// Componentes 3D
	private MeshInstance3D meshInstance;
	private Material metalMaterial;
	private Rollers rollers;
	private Node3D ends;

	// Referencias
	public Root Main { get; set; }
	private int currentScene;

	public override void _Ready()
	{
		Inicializacao_Componentes3D();
		Inicializacao_Rollers();
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
		meshInstance = GetNode<MeshInstance3D>("ConvRoller");
		meshInstance.Mesh = meshInstance.Mesh.Duplicate() as Mesh;
		metalMaterial = meshInstance.Mesh.SurfaceGetMaterial(0).Duplicate() as Material;
		meshInstance.Mesh.SurfaceSetMaterial(0, metalMaterial);
		
		rollers = GetNodeOrNull<Rollers>("Rollers");
		ends = GetNodeOrNull<Node3D>("Ends");
	}

	private void Inicializacao_Rollers()
	{
		Atualizar_Speed_Rollers();
		Atualizar_Rotacao_Rollers();
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;
		
		if (Main == null)
		{
			GD.PrintErr($"[RollerConveyor] {Name}: Root não encontrado!");
			return;
		}
		
		currentScene = Main.currentScene;
		
		Main.SimulationStarted += OnSimulationStarted;
		Main.SimulationEnded += OnSimulationEnded;
		Main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		// Carrega a tag configurada
		tagEsteira = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";
		
		if (string.IsNullOrEmpty(tagEsteira))
		{
			GD.PrintErr($"[RollerConveyor] {Name}: Tag OPC não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[RollerConveyor] {Name}: OPC conectado → {tagEsteira}");
		}
		else
		{
			GD.PrintErr($"[RollerConveyor] {Name}: OPC não conectado - rollers ficarão parados!");
		}
		
		// Reset de estado
		running = true;
		Speed = 0f;
	}

	private void OnSimulationEnded()
	{
		running = false;
		Speed = 0f;

		Atualizar_Speed_Rollers();
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
					GD.Print($"[RollerConveyor] {Name}: Speed = {Speed:F2}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[RollerConveyor] {Name}: Erro ao converter velocidade:");
			GD.PrintErr(e.Message);
		}
	}

    public override void _PhysicsProcess(double delta)
	{
		// Atualiza scale
		Atualizar_Escala();
		
		// Atualiza shader
		Atualizar_Escala_Shader();
		
		// Atualiza rollers se scale mudou
		Atualizar_Escala_Rollers();
		
		// Se estiver rodando, atualiza velocidade dos rollers
		if (running)
		{
			Atualizar_Speed_Rollers();
		}
	}

	private void Atualizar_Escala()
	{
		if (Scale.X >= 1.0f)
			nodeScaleX = Scale.X;
		
		nodeScaleZ = Scale.Z;
		
		Scale = new Vector3(nodeScaleX, 1, nodeScaleZ);
	}

	private void Atualizar_Escala_Shader()
	{
		if (metalMaterial != null)
		{
			((ShaderMaterial)metalMaterial).SetShaderParameter("Scale", Scale.X);
		}
	}

	private void Atualizar_Escala_Rollers()
	{
		if (rollers != null && lastScale != Scale.X)
		{
			rollers.ChangeScale(Scale.X);
			lastScale = Scale.X;
		}
	}

	private void Atualizar_Speed_Rollers()
	{
		// Atualiza velocidade dos rollers principais
		if (rollers != null)
		{
			foreach (Roller roller in rollers.GetChildren())
			{
				roller.speed = Speed;
			}
		}
		
		// Atualiza velocidade dos rollers nas extremidades
		if (ends != null)
		{
			foreach (RollerConveyorEnd end in ends.GetChildren())
			{
				end.SetSpeed(Speed);
			}
		}
	}

	private void Atualizar_Rotacao_Rollers()
	{
		// Atualiza rotação dos rollers principais (skew angle)
		if (rollers != null)
		{
			foreach (Roller roller in rollers.GetChildren())
			{
				roller.RotationDegrees = new Vector3(0, SkewAngle, 0);
			}
		}
		
		// Atualiza rotação dos rollers nas extremidades
		if (ends != null)
		{
			foreach (RollerConveyorEnd end in ends.GetChildren())
			{
				end.RotateRoller(new Vector3(end.RotationDegrees.X, SkewAngle, end.RotationDegrees.Z));
			}
		}
	}
}