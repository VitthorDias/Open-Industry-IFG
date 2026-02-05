using Godot;
using System;
using System.Threading.Tasks;

public partial class BladeStop : Node3D
{
	[Export]
	public float AirPressureHeight
	{
		get
		{
			return airPressureHeight;
		}
		set
		{
			airPressureHeight = value;
			Atualizar_Posicao_AirPressure();
		}
	}

	// Configuração
	private float airPressureHeight = 0.0f;
	private const float ACTIVE_POSITION_OFFSET = 0.24f; // Altura quando ativo
	private const float ANIMATION_DURATION = 0.15f;     // Duração da animação
	
	// Estado
	private bool running = false;
	private bool isCommsConnected = false;
	private bool active = false; // Controlado pelo OPC
	
	// TagOpc
	private string tagBladeStop = "";
	
	// Componentes 3D
	private StaticBody3D blade;
	private MeshInstance3D airPressureR;
	private MeshInstance3D airPressureL;
	private MeshInstance3D bladeCornerR;
	private MeshInstance3D bladeCornerL;
	private Node3D corners;

	// Referencias
	public Root Main { get; set; }
	private int currentScene;

	public override void _Ready()
	{
		Inicializacao_Componentes3D();
		Inicializacao_Posicoes();
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
		blade = GetNode<StaticBody3D>("Blade");
		corners = GetNode<Node3D>("Corners");
		airPressureR = GetNode<MeshInstance3D>("Corners/AirPressureR");
		airPressureL = GetNode<MeshInstance3D>("Corners/AirPressureL");
		bladeCornerR = GetNode<MeshInstance3D>("Corners/AirPressureR/BladeCornerR");
		bladeCornerL = GetNode<MeshInstance3D>("Corners/AirPressureL/BladeCornerL");
	}

	private void Inicializacao_Posicoes()
	{
		Atualizar_Posicao_AirPressure();
	}

	private void Conectar_Root()
	{
		Main = GetTree().CurrentScene as Root;
		
		if (Main == null)
		{
			GD.PrintErr($"[BladeStop] {Name}: Root não encontrado!");
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
		tagBladeStop = SceneComponents.GetComponentByKey(Name, currentScene)?.Tag ?? "";
		
		if (string.IsNullOrEmpty(tagBladeStop))
		{
			GD.PrintErr($"[BladeStop] {Name}: Tag OPC não configurada!");
		}
		
		// Verifica conexão OPC
		var globalVariables = GetNodeOrNull("/root/GlobalVariables");
		isCommsConnected = globalVariables != null && (bool)globalVariables.Get("opc_da_connected");
		
		if (isCommsConnected)
		{
			GD.Print($"[BladeStop] {Name}: OPC conectado → {tagBladeStop}");
		}
		else
		{
			GD.PrintErr($"[BladeStop] {Name}: OPC não conectado - blade stop não funcionará!");
		}
		
		// Reset de estado
		running = true;
		active = false;
	}

	private void OnSimulationEnded()
	{
		running = false;
		active = false;

		Animacao_Down();
	}

	private void OnOpcDataReceived(string tagName, object value)
	{
		// Recebe comando ativar/desativar do OPC
		if (tagName != tagBladeStop || value == null) 
			return;
		
		try
		{
			bool newActive = Convert.ToBoolean(value);
			
			// Atualiza estado apenas se mudou
			if (newActive != active)
			{
				active = newActive;
				
				if (Main.DebugOpcEvents)
				{
					GD.Print($"[BladeStop] {Name}: Active = {active}");
				}
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"[BladeStop] {Name}: Erro ao converter valor:");
			GD.PrintErr(e.Message);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!running)
		{
			active = false;
			return;
		}
		
		// Atualiza posição da blade baseado no estado
		Atualizar_Posicao_Blade();
		
		// Mantém escala dos corners
		Atualizar_Escala_Corners();
	}

	private void Atualizar_Posicao_Blade()
	{
		if (blade == null || bladeCornerR == null || bladeCornerL == null)
			return;
		
		if (active)
		{
			Animacao_Up();
		}
		else
		{
			Animacao_Down();
		}
	}

	private void Atualizar_Escala_Corners()
	{
		// Mantém scale Y=1, ajusta Z baseado no scale do pai
		Scale = new Vector3(1, 1, Scale.Z);
		
		if (corners != null)
		{
			foreach (Node3D child in corners.GetChildren())
			{
				child.Scale = new Vector3(1, 1, 1 / Scale.Z);
			}
		}
	}

	private void Animacao_Up()
	{
		Tween tween = GetTree().CreateTween().SetEase(Tween.EaseType.InOut).SetParallel();
		
		tween.TweenProperty(blade, "position", 
			new Vector3(blade.Position.X, airPressureHeight + ACTIVE_POSITION_OFFSET, blade.Position.Z), 
			ANIMATION_DURATION);
		
		tween.TweenProperty(bladeCornerR, "position", 
			new Vector3(bladeCornerR.Position.X, ACTIVE_POSITION_OFFSET, bladeCornerR.Position.Z), 
			ANIMATION_DURATION);
		
		tween.TweenProperty(bladeCornerL, "position", 
			new Vector3(bladeCornerL.Position.X, ACTIVE_POSITION_OFFSET, bladeCornerL.Position.Z), 
			ANIMATION_DURATION);
	}

	private void Animacao_Down()
	{
		Tween tween = GetTree().CreateTween().SetEase(Tween.EaseType.InOut).SetParallel();
		
		tween.TweenProperty(blade, "position", 
			new Vector3(blade.Position.X, airPressureHeight, blade.Position.Z), 
			ANIMATION_DURATION);
		
		tween.TweenProperty(bladeCornerR, "position", 
			new Vector3(bladeCornerR.Position.X, 0, bladeCornerR.Position.Z), 
			ANIMATION_DURATION);
		
		tween.TweenProperty(bladeCornerL, "position", 
			new Vector3(bladeCornerL.Position.X, 0, bladeCornerL.Position.Z), 
			ANIMATION_DURATION);
	}

	private void Atualizar_Posicao_AirPressure()
	{
		if (blade != null && airPressureR != null && airPressureL != null)
		{
			float bladeY = active ? airPressureHeight + ACTIVE_POSITION_OFFSET : airPressureHeight;
			
			blade.Position = new Vector3(blade.Position.X, bladeY, blade.Position.Z);
			airPressureR.Position = new Vector3(airPressureR.Position.X, airPressureHeight, airPressureR.Position.Z);
			airPressureL.Position = new Vector3(airPressureL.Position.X, airPressureHeight, airPressureL.Position.Z);
		}
	}
}
