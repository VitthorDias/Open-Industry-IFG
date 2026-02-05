using Godot;
using System;
using System.Threading.Tasks;

public partial class CurvedRollerConveyor : Node3D
{
	[Export]
	public float Speed { get; set; } = -1.0f;

	private enum Scales { Low, Mid, High }

	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	
	// Scale management
	private Scales currentScale = Scales.Mid;
	private Scales CurrentScale
	{
		get => currentScale;
		set
		{
			if (value != currentScale)
			{
				currentScale = value;
				Atualizar_Visibilidade_Rollers();
			}
		}
	}
	
	// TagOpc
	private string tagEsteira = "";
	
	// Componentes 3D
	private MeshInstance3D meshInstance;
	private Material metalMaterial;
	private Node3D rollersLow;
	private Node3D rollersMid;
	private Node3D rollersHigh;
	private Node3D ends;
	
	// Referências
	private Root main;
	private int currentScene;
	
	public override void _Ready()
	{
		Inicializacao_Componentes3D();
		Inicializacao_Rollers();
		Conectar_Root();
	}

    public override void _ExitTree()
    {
        if (main == null) return;
		
		main.SimulationStarted -= OnSimulationStarted;
		main.SimulationEnded -= OnSimulationEnded;
		main.OpcDataChanged -= OnOpcDataReceived;
    }

	private void Inicializacao_Componentes3D()
	{
		meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
		meshInstance.Mesh = meshInstance.Mesh.Duplicate() as Mesh;
		metalMaterial = meshInstance.Mesh.SurfaceGetMaterial(0).Duplicate() as Material;
		meshInstance.Mesh.SurfaceSetMaterial(0, metalMaterial);
		
		rollersLow = GetNode<Node3D>("RollersLow");
		rollersMid = GetNode<Node3D>("RollersMid");
		rollersHigh = GetNode<Node3D>("RollersHigh");
		ends = GetNode<Node3D>("Ends");
	}

	private void Inicializacao_Rollers()
	{
		Atualizar_Escala_Atual();
		Atualizar_Speed_AllRollers();
	}

	private void Conectar_Root()
	{
		main = GetTree().CurrentScene as Root;
		
		if (main == null)
		{
			GD.PrintErr($"[CurvedRollerConveyor] {Name}: Root não encontrado!");
			return;
		}
		
		currentScene = main.currentScene;
		
		main.SimulationStarted += OnSimulationStarted;
		main.SimulationEnded += OnSimulationEnded;
		main.OpcDataChanged += OnOpcDataReceived;
	}

	private void OnSimulationStarted()
	{
		// Carrega a tag configurada
		tagEsteira = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";
		
		if (string.IsNullOrEmpty(tagEsteira))
		{
			GD.PrintErr($"[CurvedRollerConveyor] {Name}: Tag OPC não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[CurvedRollerConveyor] {Name}: OPC conectado → {tagEsteira}");
		}
		else
		{
			GD.PrintErr($"[CurvedRollerConveyor] {Name}: OPC não conectado - rollers ficarão parados!");
		}
		
		// Reset de estado
		running = true;
		Speed = 0f;
	}

	private void OnSimulationEnded()
	{
		running = false;
		Speed = 0f;

		Atualizar_Speed_AllRollers();
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
				
				if (main.DebugOpcEvents)
				{
					GD.Print($"[CurvedRollerConveyor] {Name}: Speed = {Speed:F2}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[CurvedRollerConveyor] {Name}: Erro ao converter velocidade:");
			GD.PrintErr(e.Message);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// Atualiza scale - sempre X = Z para esteira curva
		Atualizar_Escala();
		
		// Atualiza shader
		Atualizar_Escala_Shader();
		
		// Atualiza scale das extremidades
		Atualizar_Escala_Ends();
		
		// Atualiza scale category baseado no tamanho
		Atualizar_Escala_Atual();
		
		// Se estiver rodando, atualiza velocidade dos rollers
		if (running)
		{
			Atualizar_Speed_AllRollers();
		}
	}

	private void Atualizar_Escala()
	{
		Scale = new Vector3(Scale.X, 1, Scale.X);
	}

	private void Atualizar_Escala_Shader()
	{
		if (Scale.X > 0.5f && Speed != 0 && metalMaterial != null)
		{
			((ShaderMaterial)metalMaterial).SetShaderParameter("Scale", Scale.X);
		}
	}

	private void Atualizar_Escala_Ends()
	{
		if (ends == null) return;
		
		foreach (MeshInstance3D end in ends.GetChildren())
		{
			end.Scale = new Vector3(1 / Scale.X, 1, 1);
		}
	}

	private void Atualizar_Escala_Atual()
	{
		if (Scale.X < 0.8f)
		{
			CurrentScale = Scales.Low;
		}
		else if (Scale.X >= 0.8f && Scale.X < 1.6f)
		{
			CurrentScale = Scales.Mid;
		}
		else
		{
			CurrentScale = Scales.High;
		}
	}

	private void Atualizar_Visibilidade_Rollers()
	{
		// Apenas rollers apropriados para o scale atual
		switch (currentScale)
		{
			case Scales.Low:
				rollersLow.Visible = true;
				rollersMid.Visible = false;
				rollersHigh.Visible = false;
				break;
			
			case Scales.Mid:
				rollersLow.Visible = false;
				rollersMid.Visible = true;
				rollersHigh.Visible = false;
				break;
			
			case Scales.High:
				rollersLow.Visible = false;
				rollersMid.Visible = true;
				rollersHigh.Visible = true;
				break;
		}
	}

	private void Atualizar_Speed_AllRollers()
	{
		Atualizar_Speed_Rollers(rollersLow, Speed);
		Atualizar_Speed_Rollers(rollersMid, Speed);
		Atualizar_Speed_Rollers(rollersHigh, Speed);
	}

	private void Atualizar_Speed_Rollers(Node3D rollers, float speed)
	{
		if (rollers == null) return;
		
		foreach (RollerCorner roller in rollers.GetChildren())
		{
			roller.SetSpeed(speed);
		}
	}
}